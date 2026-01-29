import { useEffect, useState, useCallback } from "react";
import { useNavigate } from "react-router";
import { AuthGuard } from "~/components/auth/auth-guard";
import { ChatSidebar } from "~/components/chat/chat-sidebar";
import { chatApi, isApiError } from "~/lib/api/api-client";
import type { Chat } from "~/lib/chat/types";

export function meta() {
  return [{ title: "Chat - Phlox" }];
}

function ChatIndexContent() {
  const navigate = useNavigate();
  const [chats, setChats] = useState<Chat[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchChats = useCallback(async () => {
    try {
      const data = await chatApi.getChats();
      setChats(data);
    } catch (err) {
      if (isApiError(err)) {
        setError(err.message);
      } else {
        setError("Failed to load chats");
      }
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchChats();
  }, [fetchChats]);

  const handleNewChat = async () => {
    try {
      const chat = await chatApi.createChat();
      setChats((prev) => [chat, ...prev]);
      navigate(`/chat/${chat.id}`);
    } catch (err) {
      if (isApiError(err)) {
        setError(err.message);
      } else {
        setError("Failed to create chat");
      }
    }
  };

  const handleDeleteChat = async (id: string) => {
    try {
      await chatApi.deleteChat(id);
      setChats((prev) => prev.filter((c) => c.id !== id));
    } catch (err) {
      if (isApiError(err)) {
        setError(err.message);
      } else {
        setError("Failed to delete chat");
      }
    }
  };

  return (
    <div className="flex h-screen">
      <ChatSidebar
        chats={chats}
        onNewChat={handleNewChat}
        onDeleteChat={handleDeleteChat}
        isLoading={isLoading}
      />

      <div className="flex flex-1 flex-col items-center justify-center bg-gray-50">
        {error ? (
          <div className="text-center">
            <p className="text-red-500 mb-4">{error}</p>
            <button
              onClick={fetchChats}
              className="rounded-md bg-blue-600 px-4 py-2 text-white hover:bg-blue-700"
            >
              Retry
            </button>
          </div>
        ) : (
          <div className="text-center">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              viewBox="0 0 24 24"
              fill="currentColor"
              className="mx-auto h-16 w-16 text-gray-400"
            >
              <path
                fillRule="evenodd"
                d="M4.848 2.771A49.144 49.144 0 0 1 12 2.25c2.43 0 4.817.178 7.152.52 1.978.292 3.348 2.024 3.348 3.97v6.02c0 1.946-1.37 3.678-3.348 3.97a48.901 48.901 0 0 1-3.476.383.39.39 0 0 0-.297.17l-2.755 4.133a.75.75 0 0 1-1.248 0l-2.755-4.133a.39.39 0 0 0-.297-.17 48.9 48.9 0 0 1-3.476-.384c-1.978-.29-3.348-2.024-3.348-3.97V6.741c0-1.946 1.37-3.68 3.348-3.97ZM6.75 8.25a.75.75 0 0 1 .75-.75h9a.75.75 0 0 1 0 1.5h-9a.75.75 0 0 1-.75-.75Zm.75 2.25a.75.75 0 0 0 0 1.5H12a.75.75 0 0 0 0-1.5H7.5Z"
                clipRule="evenodd"
              />
            </svg>
            <h2 className="mt-4 text-xl font-semibold text-gray-900">
              Welcome to Phlox Chat
            </h2>
            <p className="mt-2 text-gray-600">
              Select a conversation or start a new one
            </p>
            <button
              onClick={handleNewChat}
              className="mt-6 rounded-md bg-blue-600 px-6 py-3 text-white hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
            >
              Start New Chat
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

export default function ChatIndex() {
  return (
    <AuthGuard>
      <ChatIndexContent />
    </AuthGuard>
  );
}
