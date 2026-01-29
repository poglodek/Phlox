import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { ChatSidebar } from "./chat-sidebar";
import type { Chat } from "~/lib/chat/types";

const renderWithRouter = (ui: React.ReactElement) => {
  return render(<MemoryRouter>{ui}</MemoryRouter>);
};

describe("ChatSidebar", () => {
  const mockChats: Chat[] = [
    {
      id: "chat-1",
      title: "First Chat",
      createdAt: "2024-01-15T10:00:00Z",
      updatedAt: "2024-01-15T11:00:00Z",
      messages: [
        {
          id: "msg-1",
          role: "user",
          content: "Hello",
          createdAt: "2024-01-15T10:00:00Z",
        },
      ],
    },
    {
      id: "chat-2",
      title: null,
      createdAt: "2024-01-14T10:00:00Z",
      updatedAt: null,
      messages: [
        {
          id: "msg-2",
          role: "user",
          content: "This is a longer message that should be truncated",
          createdAt: "2024-01-14T10:00:00Z",
        },
      ],
    },
    {
      id: "chat-3",
      title: null,
      createdAt: "2024-01-13T10:00:00Z",
      updatedAt: null,
      messages: [],
    },
  ];

  const defaultProps = {
    chats: mockChats,
    onNewChat: vi.fn(),
    onDeleteChat: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    // Reset confirm mock
    vi.spyOn(window, "confirm").mockImplementation(() => true);
  });

  describe("Rendering", () => {
    it("renders new chat button", () => {
      renderWithRouter(<ChatSidebar {...defaultProps} />);

      expect(
        screen.getByRole("button", { name: /new chat/i })
      ).toBeInTheDocument();
    });

    it("renders chat list with titles", () => {
      renderWithRouter(<ChatSidebar {...defaultProps} />);

      expect(screen.getByText("First Chat")).toBeInTheDocument();
    });

    it("shows first message as title when title is null", () => {
      renderWithRouter(<ChatSidebar {...defaultProps} />);

      // Should show truncated first message (30 chars + "...")
      // "This is a longer message that should be truncated".slice(0, 30) = "This is a longer message that "
      expect(
        screen.getByText("This is a longer message that ...")
      ).toBeInTheDocument();
    });

    it("shows 'New Chat' when no title and no messages", () => {
      renderWithRouter(<ChatSidebar {...defaultProps} />);

      expect(screen.getByText("New Chat")).toBeInTheDocument();
    });

    it("renders dashboard link", () => {
      renderWithRouter(<ChatSidebar {...defaultProps} />);

      expect(
        screen.getByRole("link", { name: /back to dashboard/i })
      ).toBeInTheDocument();
    });
  });

  describe("Loading state", () => {
    it("shows loading spinner when isLoading is true", () => {
      renderWithRouter(<ChatSidebar {...defaultProps} chats={[]} isLoading />);

      const spinner = document.querySelector(".animate-spin");
      expect(spinner).toBeInTheDocument();
    });

    it("hides chat list when loading", () => {
      renderWithRouter(
        <ChatSidebar {...defaultProps} chats={mockChats} isLoading />
      );

      expect(screen.queryByText("First Chat")).not.toBeInTheDocument();
    });
  });

  describe("Empty state", () => {
    it("shows empty message when no chats", () => {
      renderWithRouter(<ChatSidebar {...defaultProps} chats={[]} />);

      expect(screen.getByText("No conversations yet")).toBeInTheDocument();
    });
  });

  describe("Active chat highlighting", () => {
    it("highlights current chat", () => {
      renderWithRouter(
        <ChatSidebar {...defaultProps} currentChatId="chat-1" />
      );

      const activeLink = screen.getByText("First Chat").closest("a");
      expect(activeLink).toHaveClass("bg-gray-700");
    });

    it("does not highlight other chats", () => {
      renderWithRouter(
        <ChatSidebar {...defaultProps} currentChatId="chat-1" />
      );

      const inactiveLink = screen
        .getByText("This is a longer message that ...")
        .closest("a");
      expect(inactiveLink).not.toHaveClass("bg-gray-700");
    });
  });

  describe("New chat action", () => {
    it("calls onNewChat when new chat button is clicked", async () => {
      const user = userEvent.setup();
      const onNewChat = vi.fn();
      renderWithRouter(<ChatSidebar {...defaultProps} onNewChat={onNewChat} />);

      await user.click(screen.getByRole("button", { name: /new chat/i }));

      expect(onNewChat).toHaveBeenCalledTimes(1);
    });
  });

  describe("Delete chat action", () => {
    it("calls onDeleteChat when delete button is clicked and confirmed", async () => {
      const user = userEvent.setup();
      const onDeleteChat = vi.fn();
      vi.spyOn(window, "confirm").mockReturnValue(true);

      renderWithRouter(
        <ChatSidebar {...defaultProps} onDeleteChat={onDeleteChat} />
      );

      // Hover over the first chat item to show delete button
      const chatItem = screen.getByText("First Chat").closest("a");
      await user.hover(chatItem!);

      // Find and click the delete button
      const deleteButtons = screen.getAllByTitle("Delete chat");
      await user.click(deleteButtons[0]);

      expect(onDeleteChat).toHaveBeenCalledWith("chat-1");
    });

    it("does not call onDeleteChat when delete is cancelled", async () => {
      const user = userEvent.setup();
      const onDeleteChat = vi.fn();
      vi.spyOn(window, "confirm").mockReturnValue(false);

      renderWithRouter(
        <ChatSidebar {...defaultProps} onDeleteChat={onDeleteChat} />
      );

      const chatItem = screen.getByText("First Chat").closest("a");
      await user.hover(chatItem!);

      const deleteButtons = screen.getAllByTitle("Delete chat");
      await user.click(deleteButtons[0]);

      expect(onDeleteChat).not.toHaveBeenCalled();
    });

    it("shows confirmation dialog before deleting", async () => {
      const user = userEvent.setup();
      const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);

      renderWithRouter(<ChatSidebar {...defaultProps} />);

      const chatItem = screen.getByText("First Chat").closest("a");
      await user.hover(chatItem!);

      const deleteButtons = screen.getAllByTitle("Delete chat");
      await user.click(deleteButtons[0]);

      expect(confirmSpy).toHaveBeenCalledWith(
        "Are you sure you want to delete this chat?"
      );
    });
  });

  describe("Navigation", () => {
    it("chat items are links to correct chat pages", () => {
      renderWithRouter(<ChatSidebar {...defaultProps} />);

      const firstChatLink = screen.getByText("First Chat").closest("a");
      expect(firstChatLink).toHaveAttribute("href", "/chat/chat-1");
    });

    it("dashboard link points to /dashboard", () => {
      renderWithRouter(<ChatSidebar {...defaultProps} />);

      const dashboardLink = screen.getByRole("link", {
        name: /back to dashboard/i,
      });
      expect(dashboardLink).toHaveAttribute("href", "/dashboard");
    });
  });
});
