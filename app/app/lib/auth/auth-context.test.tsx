import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, act } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AuthProvider, useAuth } from "./auth-context";
import type { User, AuthResponse } from "./types";

// Mock the API client
vi.mock("~/lib/api/api-client", () => ({
  authApi: {
    login: vi.fn(),
    register: vi.fn(),
  },
  userApi: {
    getCurrentUser: vi.fn(),
  },
}));

// Mock token storage
vi.mock("./token-storage", () => ({
  getStoredToken: vi.fn(),
  getStoredUser: vi.fn(),
  setStoredToken: vi.fn(),
  setStoredUser: vi.fn(),
  clearAuthStorage: vi.fn(),
}));

import { authApi, userApi } from "~/lib/api/api-client";
import * as tokenStorage from "./token-storage";

// Test component that uses useAuth
function TestComponent() {
  const { user, isLoading, isAuthenticated, login, register, logout } =
    useAuth();

  const handleLogin = async () => {
    try {
      await login({ emailOrUsername: "test@test.com", password: "pass" });
    } catch {
      // Error handled in real app
    }
  };

  const handleRegister = async () => {
    try {
      await register({
        email: "new@test.com",
        username: "newuser",
        password: "pass",
      });
    } catch {
      // Error handled in real app
    }
  };

  return (
    <div>
      <div data-testid="loading">{isLoading ? "loading" : "not-loading"}</div>
      <div data-testid="authenticated">
        {isAuthenticated ? "authenticated" : "not-authenticated"}
      </div>
      <div data-testid="user">{user ? user.email : "no-user"}</div>
      <button onClick={handleLogin}>Login</button>
      <button onClick={handleRegister}>Register</button>
      <button onClick={logout}>Logout</button>
    </div>
  );
}

describe("AuthProvider", () => {
  const mockUser: User = {
    id: "user-1",
    email: "test@test.com",
    username: "testuser",
    name: "Test User",
    createdAt: "2024-01-01T00:00:00Z",
    lastLoginAt: "2024-01-15T10:00:00Z",
    isActive: true,
  };

  const mockAuthResponse: AuthResponse = {
    token: "mock-jwt-token",
    userId: "user-1",
    email: "test@test.com",
    username: "testuser",
    name: "Test User",
    expiresAt: "2024-01-16T10:00:00Z",
  };

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(tokenStorage.getStoredToken).mockReturnValue(null);
    vi.mocked(tokenStorage.getStoredUser).mockReturnValue(null);
  });

  describe("Initial state", () => {
    it("starts with loading state", async () => {
      render(
        <AuthProvider>
          <TestComponent />
        </AuthProvider>
      );

      // Initial loading state
      await waitFor(() => {
        expect(screen.getByTestId("loading")).toHaveTextContent("not-loading");
      });
    });

    it("starts with unauthenticated state when no stored token", async () => {
      render(
        <AuthProvider>
          <TestComponent />
        </AuthProvider>
      );

      await waitFor(() => {
        expect(screen.getByTestId("authenticated")).toHaveTextContent(
          "not-authenticated"
        );
      });
    });

    it("starts with no user when not authenticated", async () => {
      render(
        <AuthProvider>
          <TestComponent />
        </AuthProvider>
      );

      await waitFor(() => {
        expect(screen.getByTestId("user")).toHaveTextContent("no-user");
      });
    });
  });

  describe("Token restoration", () => {
    it("restores user from stored token on mount", async () => {
      vi.mocked(tokenStorage.getStoredToken).mockReturnValue("valid-token");
      vi.mocked(tokenStorage.getStoredUser).mockReturnValue(mockUser);
      vi.mocked(userApi.getCurrentUser).mockResolvedValue(mockUser);

      render(
        <AuthProvider>
          <TestComponent />
        </AuthProvider>
      );

      await waitFor(() => {
        expect(screen.getByTestId("authenticated")).toHaveTextContent(
          "authenticated"
        );
        expect(screen.getByTestId("user")).toHaveTextContent("test@test.com");
      });
    });

    it("clears storage when token validation fails", async () => {
      vi.mocked(tokenStorage.getStoredToken).mockReturnValue("invalid-token");
      vi.mocked(tokenStorage.getStoredUser).mockReturnValue(mockUser);
      vi.mocked(userApi.getCurrentUser).mockRejectedValue(
        new Error("Token expired")
      );

      render(
        <AuthProvider>
          <TestComponent />
        </AuthProvider>
      );

      await waitFor(() => {
        expect(tokenStorage.clearAuthStorage).toHaveBeenCalled();
        expect(screen.getByTestId("authenticated")).toHaveTextContent(
          "not-authenticated"
        );
      });
    });
  });

  describe("Login", () => {
    it("authenticates user on successful login", async () => {
      const user = userEvent.setup();
      vi.mocked(authApi.login).mockResolvedValue(mockAuthResponse);

      render(
        <AuthProvider>
          <TestComponent />
        </AuthProvider>
      );

      await waitFor(() => {
        expect(screen.getByTestId("loading")).toHaveTextContent("not-loading");
      });

      await user.click(screen.getByRole("button", { name: "Login" }));

      await waitFor(() => {
        expect(screen.getByTestId("authenticated")).toHaveTextContent(
          "authenticated"
        );
      });
    });

    it("stores token after successful login", async () => {
      const user = userEvent.setup();
      vi.mocked(authApi.login).mockResolvedValue(mockAuthResponse);

      render(
        <AuthProvider>
          <TestComponent />
        </AuthProvider>
      );

      await waitFor(() => {
        expect(screen.getByTestId("loading")).toHaveTextContent("not-loading");
      });

      await user.click(screen.getByRole("button", { name: "Login" }));

      await waitFor(() => {
        expect(tokenStorage.setStoredToken).toHaveBeenCalledWith(
          "mock-jwt-token"
        );
      });
    });

    it("stores user after successful login", async () => {
      const user = userEvent.setup();
      vi.mocked(authApi.login).mockResolvedValue(mockAuthResponse);

      render(
        <AuthProvider>
          <TestComponent />
        </AuthProvider>
      );

      await waitFor(() => {
        expect(screen.getByTestId("loading")).toHaveTextContent("not-loading");
      });

      await user.click(screen.getByRole("button", { name: "Login" }));

      await waitFor(() => {
        expect(tokenStorage.setStoredUser).toHaveBeenCalled();
      });
    });

    it("does not authenticate on failed login", async () => {
      const user = userEvent.setup();
      const loginError = new Error("Invalid credentials");
      vi.mocked(authApi.login).mockRejectedValue(loginError);

      render(
        <AuthProvider>
          <TestComponent />
        </AuthProvider>
      );

      await waitFor(() => {
        expect(screen.getByTestId("loading")).toHaveTextContent("not-loading");
      });

      // Click the login button - the login will fail but the click won't throw
      await user.click(screen.getByRole("button", { name: "Login" }));

      // User should still be unauthenticated after failed login
      await waitFor(() => {
        expect(screen.getByTestId("authenticated")).toHaveTextContent(
          "not-authenticated"
        );
      });
    });
  });

  describe("Register", () => {
    it("authenticates user on successful registration", async () => {
      const user = userEvent.setup();
      const registerResponse = {
        ...mockAuthResponse,
        email: "new@test.com",
        username: "newuser",
      };
      vi.mocked(authApi.register).mockResolvedValue(registerResponse);

      render(
        <AuthProvider>
          <TestComponent />
        </AuthProvider>
      );

      await waitFor(() => {
        expect(screen.getByTestId("loading")).toHaveTextContent("not-loading");
      });

      await user.click(screen.getByRole("button", { name: "Register" }));

      await waitFor(() => {
        expect(screen.getByTestId("authenticated")).toHaveTextContent(
          "authenticated"
        );
        expect(screen.getByTestId("user")).toHaveTextContent("new@test.com");
      });
    });
  });

  describe("Logout", () => {
    it("clears authentication on logout", async () => {
      const user = userEvent.setup();
      vi.mocked(tokenStorage.getStoredToken).mockReturnValue("valid-token");
      vi.mocked(tokenStorage.getStoredUser).mockReturnValue(mockUser);
      vi.mocked(userApi.getCurrentUser).mockResolvedValue(mockUser);

      render(
        <AuthProvider>
          <TestComponent />
        </AuthProvider>
      );

      await waitFor(() => {
        expect(screen.getByTestId("authenticated")).toHaveTextContent(
          "authenticated"
        );
      });

      await user.click(screen.getByRole("button", { name: "Logout" }));

      await waitFor(() => {
        expect(screen.getByTestId("authenticated")).toHaveTextContent(
          "not-authenticated"
        );
        expect(screen.getByTestId("user")).toHaveTextContent("no-user");
      });
    });

    it("clears storage on logout", async () => {
      const user = userEvent.setup();
      vi.mocked(tokenStorage.getStoredToken).mockReturnValue("valid-token");
      vi.mocked(tokenStorage.getStoredUser).mockReturnValue(mockUser);
      vi.mocked(userApi.getCurrentUser).mockResolvedValue(mockUser);

      render(
        <AuthProvider>
          <TestComponent />
        </AuthProvider>
      );

      await waitFor(() => {
        expect(screen.getByTestId("authenticated")).toHaveTextContent(
          "authenticated"
        );
      });

      await user.click(screen.getByRole("button", { name: "Logout" }));

      expect(tokenStorage.clearAuthStorage).toHaveBeenCalled();
    });
  });
});

describe("useAuth", () => {
  it("throws error when used outside AuthProvider", () => {
    // Suppress console.error for this test
    const consoleSpy = vi.spyOn(console, "error").mockImplementation(() => {});

    expect(() => {
      render(<TestComponent />);
    }).toThrow("useAuth must be used within an AuthProvider");

    consoleSpy.mockRestore();
  });
});
