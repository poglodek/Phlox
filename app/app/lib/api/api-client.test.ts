import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { apiClient, chatApi, authApi, isApiError } from "./api-client";

// Mock fetch globally
const mockFetch = vi.fn();
global.fetch = mockFetch;

// Mock token storage
vi.mock("~/lib/auth/token-storage", () => ({
  getStoredToken: vi.fn(() => "mock-token"),
}));

describe("apiClient", () => {
  beforeEach(() => {
    mockFetch.mockClear();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe("GET requests", () => {
    it("makes GET request with correct URL", async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve({ data: "test" }),
      });

      await apiClient.get("/api/test");

      expect(mockFetch).toHaveBeenCalledWith(
        "https://localhost:7086/api/test",
        expect.objectContaining({ method: "GET" })
      );
    });

    it("includes Authorization header with token", async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve({}),
      });

      await apiClient.get("/api/test");

      expect(mockFetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          headers: expect.objectContaining({
            Authorization: "Bearer mock-token",
          }),
        })
      );
    });

    it("returns parsed JSON response", async () => {
      const expectedData = { id: 1, name: "Test" };
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve(expectedData),
      });

      const result = await apiClient.get<typeof expectedData>("/api/test");

      expect(result).toEqual(expectedData);
    });

    it("returns undefined for 204 No Content", async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 204,
      });

      const result = await apiClient.get("/api/test");

      expect(result).toBeUndefined();
    });
  });

  describe("POST requests", () => {
    it("makes POST request with JSON body", async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve({}),
      });

      const data = { name: "Test" };
      await apiClient.post("/api/test", data);

      expect(mockFetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          method: "POST",
          body: JSON.stringify(data),
        })
      );
    });

    it("includes Content-Type header", async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve({}),
      });

      await apiClient.post("/api/test", {});

      expect(mockFetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          headers: expect.objectContaining({
            "Content-Type": "application/json",
          }),
        })
      );
    });
  });

  describe("DELETE requests", () => {
    it("makes DELETE request", async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 204,
      });

      await apiClient.delete("/api/test/1");

      expect(mockFetch).toHaveBeenCalledWith(
        "https://localhost:7086/api/test/1",
        expect.objectContaining({ method: "DELETE" })
      );
    });
  });

  describe("Error handling", () => {
    it("throws error with message for non-ok response", async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 404,
        statusText: "Not Found",
        json: () => Promise.resolve({ message: "Resource not found" }),
      });

      await expect(apiClient.get("/api/test")).rejects.toEqual({
        message: "Resource not found",
        status: 404,
      });
    });

    it("uses default message when JSON parsing fails", async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 500,
        statusText: "Internal Server Error",
        json: () => Promise.reject(new Error("Invalid JSON")),
      });

      await expect(apiClient.get("/api/test")).rejects.toEqual({
        message: "API error: 500 Internal Server Error",
        status: 500,
      });
    });
  });

  describe("skipAuth option", () => {
    it("skips Authorization header when skipAuth is true", async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve({}),
      });

      await apiClient.get("/api/test", { skipAuth: true });

      const callArgs = mockFetch.mock.calls[0][1];
      expect(callArgs.headers.Authorization).toBeUndefined();
    });
  });
});

describe("chatApi", () => {
  beforeEach(() => {
    mockFetch.mockClear();
  });

  describe("createChat", () => {
    it("creates chat with title", async () => {
      const mockChat = { id: "chat-1", title: "Test Chat" };
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve(mockChat),
      });

      const result = await chatApi.createChat({ title: "Test Chat" });

      expect(result).toEqual(mockChat);
      expect(mockFetch).toHaveBeenCalledWith(
        "https://localhost:7086/api/chat",
        expect.objectContaining({
          method: "POST",
          body: JSON.stringify({ title: "Test Chat" }),
        })
      );
    });

    it("creates chat without title", async () => {
      const mockChat = { id: "chat-1", title: null };
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve(mockChat),
      });

      const result = await chatApi.createChat();

      expect(result).toEqual(mockChat);
    });
  });

  describe("getChat", () => {
    it("fetches chat by id", async () => {
      const mockChat = { id: "chat-1", title: "Test", messages: [] };
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve(mockChat),
      });

      const result = await chatApi.getChat("chat-1");

      expect(result).toEqual(mockChat);
      expect(mockFetch).toHaveBeenCalledWith(
        "https://localhost:7086/api/chat/chat-1",
        expect.any(Object)
      );
    });
  });

  describe("getChats", () => {
    it("fetches chats with default pagination", async () => {
      const mockChats = [{ id: "chat-1" }, { id: "chat-2" }];
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve(mockChats),
      });

      const result = await chatApi.getChats();

      expect(result).toEqual(mockChats);
      expect(mockFetch).toHaveBeenCalledWith(
        "https://localhost:7086/api/chat?page=1&pageSize=20",
        expect.any(Object)
      );
    });

    it("fetches chats with custom pagination", async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve([]),
      });

      await chatApi.getChats(2, 10);

      expect(mockFetch).toHaveBeenCalledWith(
        "https://localhost:7086/api/chat?page=2&pageSize=10",
        expect.any(Object)
      );
    });
  });

  describe("deleteChat", () => {
    it("deletes chat by id", async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 204,
      });

      await chatApi.deleteChat("chat-1");

      expect(mockFetch).toHaveBeenCalledWith(
        "https://localhost:7086/api/chat/chat-1",
        expect.objectContaining({ method: "DELETE" })
      );
    });
  });

  describe("sendMessage", () => {
    it("sends message and streams response", async () => {
      const encoder = new TextEncoder();
      const chunks = [
        'data: {"content":"Hello"}\n\n',
        'data: {"content":" World"}\n\n',
        "data: [DONE]\n\n",
      ];

      let chunkIndex = 0;
      const mockReader = {
        read: vi.fn().mockImplementation(() => {
          if (chunkIndex < chunks.length) {
            const chunk = chunks[chunkIndex++];
            return Promise.resolve({
              done: false,
              value: encoder.encode(chunk),
            });
          }
          return Promise.resolve({ done: true, value: undefined });
        }),
        releaseLock: vi.fn(),
      };

      mockFetch.mockResolvedValueOnce({
        ok: true,
        body: {
          getReader: () => mockReader,
        },
      });

      const results: string[] = [];
      for await (const chunk of chatApi.sendMessage("chat-1", {
        content: "Hi",
      })) {
        if (chunk.content) {
          results.push(chunk.content);
        }
      }

      expect(results).toEqual(["Hello", " World"]);
    });

    it("throws error for non-ok response", async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 401,
        statusText: "Unauthorized",
      });

      const generator = chatApi.sendMessage("chat-1", { content: "Hi" });

      await expect(generator.next()).rejects.toEqual({
        message: "API error: 401 Unauthorized",
        status: 401,
      });
    });

    it("throws error when no response body", async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        body: null,
      });

      const generator = chatApi.sendMessage("chat-1", { content: "Hi" });

      await expect(generator.next()).rejects.toThrow("No response body");
    });

    it("supports abort signal", async () => {
      const abortController = new AbortController();

      mockFetch.mockResolvedValueOnce({
        ok: true,
        body: {
          getReader: () => ({
            read: () =>
              new Promise(() => {
                /* never resolves */
              }),
            releaseLock: vi.fn(),
          }),
        },
      });

      // Start the generator (don't await, just trigger the fetch)
      const generator = chatApi.sendMessage(
        "chat-1",
        { content: "Hi" },
        abortController.signal
      );

      // Trigger the first iteration to make the fetch call
      generator.next();

      // Wait for the fetch to be called
      await vi.waitFor(() => {
        expect(mockFetch).toHaveBeenCalled();
      });

      expect(mockFetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          signal: abortController.signal,
        })
      );
    });
  });
});

describe("authApi", () => {
  beforeEach(() => {
    mockFetch.mockClear();
  });

  describe("login", () => {
    it("sends login request without auth header", async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: vi.fn().mockResolvedValue({ token: "jwt-token", userId: "user-1" }),
      });

      await authApi.login({ emailOrUsername: "test@test.com", password: "pass" });

      const callArgs = mockFetch.mock.calls[0][1];
      expect(callArgs.headers.Authorization).toBeUndefined();
    });
  });

  describe("register", () => {
    it("sends register request without auth header", async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: vi.fn().mockResolvedValue({ token: "jwt-token", userId: "user-1" }),
      });

      await authApi.register({
        email: "test@test.com",
        username: "testuser",
        password: "pass",
      });

      const callArgs = mockFetch.mock.calls[0][1];
      expect(callArgs.headers.Authorization).toBeUndefined();
    });
  });
});

describe("isApiError", () => {
  it("returns true for valid API error", () => {
    const error = { message: "Not found", status: 404 };
    expect(isApiError(error)).toBe(true);
  });

  it("returns false for regular Error", () => {
    const error = new Error("Something went wrong");
    expect(isApiError(error)).toBe(false);
  });

  it("returns false for null", () => {
    expect(isApiError(null)).toBe(false);
  });

  it("returns false for undefined", () => {
    expect(isApiError(undefined)).toBe(false);
  });

  it("returns false for object without message", () => {
    expect(isApiError({ status: 404 })).toBe(false);
  });

  it("returns false for object without status", () => {
    expect(isApiError({ message: "Error" })).toBe(false);
  });
});
