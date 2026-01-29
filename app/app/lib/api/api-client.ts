import { apiBaseUrl } from "~/config/auth.config";
import { getStoredToken } from "~/lib/auth/token-storage";
import type { AuthResponse, LoginRequest, RegisterRequest, User } from "~/lib/auth/types";
import type { Chat, CreateChatRequest, SendMessageRequest, StreamChunk } from "~/lib/chat/types";

interface RequestOptions extends RequestInit {
  skipAuth?: boolean;
}

interface ApiError {
  message: string;
  status: number;
}

async function request<T>(
  endpoint: string,
  options: RequestOptions = {}
): Promise<T> {
  const { skipAuth = false, headers: customHeaders, ...restOptions } = options;

  const headers: HeadersInit = {
    "Content-Type": "application/json",
    ...customHeaders,
  };

  if (!skipAuth) {
    const token = getStoredToken();
    if (token) {
      (headers as Record<string, string>)["Authorization"] = `Bearer ${token}`;
    }
  }

  const response = await fetch(`${apiBaseUrl}${endpoint}`, {
    headers,
    ...restOptions,
  });

  if (!response.ok) {
    let message = `API error: ${response.status} ${response.statusText}`;
    try {
      const errorBody = await response.json();
      if (errorBody.message) {
        message = errorBody.message;
      }
    } catch {
      // Ignore JSON parsing errors
    }

    const error: ApiError = {
      message,
      status: response.status,
    };
    throw error;
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json();
}

export const apiClient = {
  get: <T>(endpoint: string, options?: RequestOptions) =>
    request<T>(endpoint, { ...options, method: "GET" }),

  post: <T>(endpoint: string, data?: unknown, options?: RequestOptions) =>
    request<T>(endpoint, {
      ...options,
      method: "POST",
      body: data ? JSON.stringify(data) : undefined,
    }),

  put: <T>(endpoint: string, data?: unknown, options?: RequestOptions) =>
    request<T>(endpoint, {
      ...options,
      method: "PUT",
      body: data ? JSON.stringify(data) : undefined,
    }),

  delete: <T>(endpoint: string, options?: RequestOptions) =>
    request<T>(endpoint, { ...options, method: "DELETE" }),
};

export const authApi = {
  login: (data: LoginRequest) =>
    apiClient.post<AuthResponse>("/api/auth/login", data, { skipAuth: true }),

  register: (data: RegisterRequest) =>
    apiClient.post<AuthResponse>("/api/auth/register", data, { skipAuth: true }),
};

export const userApi = {
  getCurrentUser: () => apiClient.get<User>("/api/user/me"),
};

export const chatApi = {
  createChat: (data?: CreateChatRequest) =>
    apiClient.post<Chat>("/api/chat", data),

  getChat: (id: string) => apiClient.get<Chat>(`/api/chat/${id}`),

  getChats: (page = 1, pageSize = 20) =>
    apiClient.get<Chat[]>(`/api/chat?page=${page}&pageSize=${pageSize}`),

  deleteChat: (id: string) => apiClient.delete<void>(`/api/chat/${id}`),

  sendMessage: async function* (
    chatId: string,
    data: SendMessageRequest,
    signal?: AbortSignal
  ): AsyncGenerator<StreamChunk> {
    const token = getStoredToken();

    const response = await fetch(`${apiBaseUrl}/api/chat/${chatId}/messages`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: JSON.stringify(data),
      signal,
    });

    if (!response.ok) {
      throw {
        message: `API error: ${response.status} ${response.statusText}`,
        status: response.status,
      };
    }

    const reader = response.body?.getReader();
    if (!reader) {
      throw new Error("No response body");
    }

    const decoder = new TextDecoder();
    let buffer = "";

    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });

        const lines = buffer.split("\n");
        buffer = lines.pop() || "";

        for (const line of lines) {
          if (line.startsWith("data: ")) {
            const data = line.slice(6).trim();
            if (data === "[DONE]") {
              return;
            }
            try {
              const chunk: StreamChunk = JSON.parse(data);
              yield chunk;
            } catch {
              // Ignore invalid JSON
            }
          }
        }
      }
    } finally {
      reader.releaseLock();
    }
  },
};

export function isApiError(error: unknown): error is ApiError {
  return (
    typeof error === "object" &&
    error !== null &&
    "message" in error &&
    "status" in error
  );
}
