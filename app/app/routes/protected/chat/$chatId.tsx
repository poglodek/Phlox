import { useEffect, useState, useCallback, useRef } from "react";
import { useParams, useNavigate } from "react-router";
import { AuthGuard } from "~/components/auth/auth-guard";
import { ChatSidebar } from "~/components/chat/chat-sidebar";
import { ChatMessage, StreamingMessage } from "~/components/chat/chat-message";
import { ChatInput } from "~/components/chat/chat-input";
import { chatApi, isApiError } from "~/lib/api/api-client";
import type { Chat, Message } from "~/lib/chat/types";

export function meta() {
  return [{ title: "Chat - Phlox" }];
}

function ChatContent() {
  const params = useParams();
  const navigate = useNavigate();
  const chatId = params.chatId as string;

  const [chats, setChats] = useState<Chat[]>([]);
  const [currentChat, setCurrentChat] = useState<Chat | null>(null);
  const [messages, setMessages] = useState<Message[]>([]);
  const [streamingContent, setStreamingContent] = useState("");
  const [isStreaming, setIsStreaming] = useState(false);
  const [isLoadingChats, setIsLoadingChats] = useState(true);
  const [isLoadingChat, setIsLoadingChat] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const messagesEndRef = useRef<HTMLDivElement>(null);
  const abortControllerRef = useRef<AbortController | null>(null);

  const scrollToBottom = useCallback(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, []);

  useEffect(() => {
    scrollToBottom();
  }, [messages, streamingContent, scrollToBottom]);

  const fetchChats = useCallback(async () => {
    try {
      const data = await chatApi.getChats();
      setChats(data);
    } catch (err) {
      if (isApiError(err)) {
        setError(err.message);
      }
    } finally {
      setIsLoadingChats(false);
    }
  }, []);

  const fetchChat = useCallback(async () => {
    if (!chatId) return;

    setIsLoadingChat(true);
    try {
      const chat = await chatApi.getChat(chatId);
      setCurrentChat(chat);
      setMessages(chat.messages);
    } catch (err) {
      if (isApiError(err)) {
        if (err.status === 404) {
          navigate("/chat");
          return;
        }
        setError(err.message);
      }
    } finally {
      setIsLoadingChat(false);
    }
  }, [chatId, navigate]);

  useEffect(() => {
    fetchChats();
  }, [fetchChats]);

  useEffect(() => {
    fetchChat();
  }, [fetchChat]);

  const handleNewChat = async () => {
    try {
      const chat = await chatApi.createChat();
      setChats((prev) => [chat, ...prev]);
      navigate(`/chat/${chat.id}`);
    } catch (err) {
      if (isApiError(err)) {
        setError(err.message);
      }
    }
  };

  const handleDeleteChat = async (id: string) => {
    try {
      await chatApi.deleteChat(id);
      setChats((prev) => prev.filter((c) => c.id !== id));
      if (id === chatId) {
        navigate("/chat");
      }
    } catch (err) {
      if (isApiError(err)) {
        setError(err.message);
      }
    }
  };

  const handleSendMessage = async (content: string) => {
    if (!chatId || isStreaming) return;

    // Add user message to UI immediately
    const userMessage: Message = {
      id: crypto.randomUUID(),
      role: "user",
      content,
      createdAt: new Date().toISOString(),
    };
    setMessages((prev) => [...prev, userMessage]);

    setIsStreaming(true);
    setStreamingContent("");
    setError(null);

    abortControllerRef.current = new AbortController();

    try {
      let fullContent = "";

      for await (const chunk of chatApi.sendMessage(
        chatId,
        { content },
        abortControllerRef.current.signal
      )) {
        if (chunk.error) {
          setError(chunk.error);
          break;
        }
        if (chunk.content) {
          fullContent += chunk.content;
          setStreamingContent(fullContent);
        }
      }

      // Add assistant message to messages
      if (fullContent) {
        const assistantMessage: Message = {
          id: crypto.randomUUID(),
          role: "assistant",
          content: fullContent,
          createdAt: new Date().toISOString(),
        };
        setMessages((prev) => [...prev, assistantMessage]);
      }
    } catch (err) {
      if (err instanceof Error && err.name === "AbortError") {
        // Ignore abort errors
      } else if (isApiError(err)) {
        setError(err.message);
      } else {
        setError("Failed to send message");
      }
    } finally {
      setIsStreaming(false);
      setStreamingContent("");
      abortControllerRef.current = null;
    }
  };

  return (
    <div className="flex h-screen">
      <ChatSidebar
        chats={chats}
        currentChatId={chatId}
        onNewChat={handleNewChat}
        onDeleteChat={handleDeleteChat}
        isLoading={isLoadingChats}
      />

      <div className="flex flex-1 flex-col bg-white">
        {isLoadingChat ? (
          <div className="flex flex-1 items-center justify-center">
            <div className="h-8 w-8 animate-spin rounded-full border-4 border-blue-600 border-t-transparent" />
          </div>
        ) : (
          <>
            <div className="flex-1 overflow-y-auto p-4">
              {messages.length === 0 && !isStreaming ? (
                <div className="flex h-full items-center justify-center">
                  <div className="text-center text-gray-500">
                    <p className="text-lg">Start a conversation</p>
                    <p className="mt-1 text-sm">
                      Type a message below to begin
                    </p>
                  </div>
                </div>
              ) : (
                <>
                  {messages.map((message) => (
                    <ChatMessage key={message.id} message={message} />
                  ))}
                  {isStreaming && streamingContent && (
                    <StreamingMessage content={streamingContent} />
                  )}
                  {isStreaming && !streamingContent && (
                    <div className="flex justify-start mb-4">
                      <div className="rounded-lg px-4 py-3 bg-gray-100">
                        <div className="flex items-center gap-2 text-gray-500">
                          <div className="h-4 w-4 animate-spin rounded-full border-2 border-gray-400 border-t-transparent" />
                          <span className="text-sm">Thinking...</span>
                        </div>
                      </div>
                    </div>
                  )}
                </>
              )}
              <div ref={messagesEndRef} />
            </div>

            {error && (
              <div className="mx-4 mb-2 rounded-lg bg-red-50 p-3 text-sm text-red-600">
                {error}
              </div>
            )}

            <ChatInput onSend={handleSendMessage} disabled={isStreaming} />
          </>
        )}
      </div>
    </div>
  );
}

export default function ChatPage() {
  return (
    <AuthGuard>
      <ChatContent />
    </AuthGuard>
  );
}
