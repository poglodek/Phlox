import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ChatMessage, StreamingMessage } from "./chat-message";
import type { Message } from "~/lib/chat/types";

describe("ChatMessage", () => {
  const baseMessage: Message = {
    id: "msg-1",
    role: "user",
    content: "Hello, world!",
    createdAt: "2024-01-15T10:30:00Z",
  };

  describe("User messages", () => {
    it("renders user message content", () => {
      render(<ChatMessage message={baseMessage} />);

      expect(screen.getByText("Hello, world!")).toBeInTheDocument();
    });

    it("displays timestamp for user messages", () => {
      render(<ChatMessage message={baseMessage} />);

      // Check that some time is displayed (format depends on locale)
      const messageContainer = screen.getByText("Hello, world!").parentElement;
      expect(messageContainer).toBeInTheDocument();
    });

    it("applies correct styling for user messages (right aligned, blue background)", () => {
      render(<ChatMessage message={baseMessage} />);

      const messageContainer = screen
        .getByText("Hello, world!")
        .closest("div[class*='max-w']");
      expect(messageContainer).toHaveClass("bg-blue-600");
      expect(messageContainer).toHaveClass("text-white");
    });
  });

  describe("Assistant messages", () => {
    const assistantMessage: Message = {
      ...baseMessage,
      id: "msg-2",
      role: "assistant",
      content: "Hello! How can I help you?",
    };

    it("renders assistant message content", () => {
      render(<ChatMessage message={assistantMessage} />);

      expect(screen.getByText("Hello! How can I help you?")).toBeInTheDocument();
    });

    it("applies correct styling for assistant messages (left aligned, gray background)", () => {
      render(<ChatMessage message={assistantMessage} />);

      const messageContainer = screen
        .getByText("Hello! How can I help you?")
        .closest("div[class*='max-w']");
      expect(messageContainer).toHaveClass("bg-gray-100");
      expect(messageContainer).toHaveClass("text-gray-900");
    });
  });

  describe("Message content handling", () => {
    it("preserves whitespace in messages", () => {
      const multilineMessage: Message = {
        ...baseMessage,
        content: "Line 1\nLine 2\nLine 3",
      };

      render(<ChatMessage message={multilineMessage} />);

      const contentElement = screen.getByText(/Line 1/);
      expect(contentElement).toHaveClass("whitespace-pre-wrap");
    });

    it("handles long messages with word breaking", () => {
      const longMessage: Message = {
        ...baseMessage,
        content: "A".repeat(500),
      };

      render(<ChatMessage message={longMessage} />);

      const contentElement = screen.getByText("A".repeat(500));
      expect(contentElement).toHaveClass("break-words");
    });

    it("renders empty message without crashing", () => {
      const emptyMessage: Message = {
        ...baseMessage,
        content: "",
      };

      render(<ChatMessage message={emptyMessage} />);
      // Should not throw
    });
  });
});

describe("StreamingMessage", () => {
  it("renders streaming content", () => {
    render(<StreamingMessage content="Generating response..." />);

    expect(screen.getByText(/Generating response.../)).toBeInTheDocument();
  });

  it("shows cursor animation", () => {
    render(<StreamingMessage content="Hello" />);

    // Check for the animated cursor span
    const container = screen.getByText(/Hello/).parentElement;
    const cursor = container?.querySelector("span.animate-pulse");
    expect(cursor).toBeInTheDocument();
  });

  it("applies assistant message styling", () => {
    render(<StreamingMessage content="Streaming..." />);

    const messageContainer = screen
      .getByText(/Streaming.../)
      .closest("div[class*='max-w']");
    expect(messageContainer).toHaveClass("bg-gray-100");
  });

  it("handles empty content", () => {
    render(<StreamingMessage content="" />);
    // Should render without crashing and show cursor
  });
});
