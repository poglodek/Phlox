import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ChatInput } from "./chat-input";

describe("ChatInput", () => {
  describe("Rendering", () => {
    it("renders textarea with default placeholder", () => {
      render(<ChatInput onSend={vi.fn()} />);

      expect(
        screen.getByPlaceholderText("Type your message...")
      ).toBeInTheDocument();
    });

    it("renders textarea with custom placeholder", () => {
      render(<ChatInput onSend={vi.fn()} placeholder="Ask me anything..." />);

      expect(
        screen.getByPlaceholderText("Ask me anything...")
      ).toBeInTheDocument();
    });

    it("renders send button", () => {
      render(<ChatInput onSend={vi.fn()} />);

      expect(screen.getByRole("button", { name: "" })).toBeInTheDocument();
    });
  });

  describe("User input", () => {
    it("updates textarea value on input", async () => {
      const user = userEvent.setup();
      render(<ChatInput onSend={vi.fn()} />);

      const textarea = screen.getByPlaceholderText("Type your message...");
      await user.type(textarea, "Hello, world!");

      expect(textarea).toHaveValue("Hello, world!");
    });

    it("clears textarea after successful send", async () => {
      const user = userEvent.setup();
      const onSend = vi.fn();
      render(<ChatInput onSend={onSend} />);

      const textarea = screen.getByPlaceholderText("Type your message...");
      await user.type(textarea, "Hello");
      await user.click(screen.getByRole("button"));

      expect(textarea).toHaveValue("");
    });
  });

  describe("Send behavior", () => {
    it("calls onSend with trimmed message when button is clicked", async () => {
      const user = userEvent.setup();
      const onSend = vi.fn();
      render(<ChatInput onSend={onSend} />);

      const textarea = screen.getByPlaceholderText("Type your message...");
      await user.type(textarea, "  Hello, world!  ");
      await user.click(screen.getByRole("button"));

      expect(onSend).toHaveBeenCalledWith("Hello, world!");
    });

    it("calls onSend when Enter is pressed (without Shift)", async () => {
      const user = userEvent.setup();
      const onSend = vi.fn();
      render(<ChatInput onSend={onSend} />);

      const textarea = screen.getByPlaceholderText("Type your message...");
      await user.type(textarea, "Hello{Enter}");

      expect(onSend).toHaveBeenCalledWith("Hello");
    });

    it("does not call onSend when Shift+Enter is pressed", async () => {
      const user = userEvent.setup();
      const onSend = vi.fn();
      render(<ChatInput onSend={onSend} />);

      const textarea = screen.getByPlaceholderText("Type your message...");
      await user.type(textarea, "Hello{Shift>}{Enter}{/Shift}");

      expect(onSend).not.toHaveBeenCalled();
    });

    it("does not call onSend with empty message", async () => {
      const user = userEvent.setup();
      const onSend = vi.fn();
      render(<ChatInput onSend={onSend} />);

      await user.click(screen.getByRole("button"));

      expect(onSend).not.toHaveBeenCalled();
    });

    it("does not call onSend with whitespace-only message", async () => {
      const user = userEvent.setup();
      const onSend = vi.fn();
      render(<ChatInput onSend={onSend} />);

      const textarea = screen.getByPlaceholderText("Type your message...");
      await user.type(textarea, "   ");
      await user.click(screen.getByRole("button"));

      expect(onSend).not.toHaveBeenCalled();
    });
  });

  describe("Disabled state", () => {
    it("disables textarea when disabled prop is true", () => {
      render(<ChatInput onSend={vi.fn()} disabled />);

      expect(screen.getByPlaceholderText("Type your message...")).toBeDisabled();
    });

    it("disables send button when disabled prop is true", () => {
      render(<ChatInput onSend={vi.fn()} disabled />);

      expect(screen.getByRole("button")).toBeDisabled();
    });

    it("disables send button when textarea is empty", () => {
      render(<ChatInput onSend={vi.fn()} />);

      expect(screen.getByRole("button")).toBeDisabled();
    });

    it("enables send button when textarea has content", async () => {
      const user = userEvent.setup();
      render(<ChatInput onSend={vi.fn()} />);

      const textarea = screen.getByPlaceholderText("Type your message...");
      await user.type(textarea, "Hello");

      expect(screen.getByRole("button")).not.toBeDisabled();
    });

    it("does not call onSend when disabled even with content", async () => {
      const user = userEvent.setup();
      const onSend = vi.fn();
      render(<ChatInput onSend={onSend} disabled />);

      const textarea = screen.getByPlaceholderText("Type your message...");
      fireEvent.change(textarea, { target: { value: "Hello" } });

      // Try to submit via form
      const form = textarea.closest("form");
      if (form) {
        fireEvent.submit(form);
      }

      expect(onSend).not.toHaveBeenCalled();
    });
  });

  describe("Accessibility", () => {
    it("textarea is focusable", async () => {
      const user = userEvent.setup();
      render(<ChatInput onSend={vi.fn()} />);

      const textarea = screen.getByPlaceholderText("Type your message...");
      await user.click(textarea);

      expect(textarea).toHaveFocus();
    });

    it("form is submittable via button", async () => {
      const user = userEvent.setup();
      const onSend = vi.fn();
      render(<ChatInput onSend={onSend} />);

      const textarea = screen.getByPlaceholderText("Type your message...");
      await user.type(textarea, "Test message");

      const button = screen.getByRole("button");
      await user.click(button);

      expect(onSend).toHaveBeenCalledWith("Test message");
    });
  });
});
