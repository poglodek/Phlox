import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { AuthGuard } from "./auth-guard";

// Mock useAuth hook
const mockUseAuth = vi.fn();
vi.mock("~/lib/auth/auth-context", () => ({
  useAuth: () => mockUseAuth(),
}));

const renderWithRouter = (ui: React.ReactElement) => {
  return render(<MemoryRouter>{ui}</MemoryRouter>);
};

describe("AuthGuard", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe("Loading state", () => {
    it("shows loading spinner when auth is loading", () => {
      mockUseAuth.mockReturnValue({
        isLoading: true,
        isAuthenticated: false,
      });

      renderWithRouter(
        <AuthGuard>
          <div>Protected Content</div>
        </AuthGuard>
      );

      expect(screen.getByText("Loading...")).toBeInTheDocument();
      expect(screen.queryByText("Protected Content")).not.toBeInTheDocument();
    });

    it("shows spinning animation while loading", () => {
      mockUseAuth.mockReturnValue({
        isLoading: true,
        isAuthenticated: false,
      });

      renderWithRouter(
        <AuthGuard>
          <div>Protected Content</div>
        </AuthGuard>
      );

      const spinner = document.querySelector(".animate-spin");
      expect(spinner).toBeInTheDocument();
    });
  });

  describe("Authenticated state", () => {
    it("renders children when authenticated", () => {
      mockUseAuth.mockReturnValue({
        isLoading: false,
        isAuthenticated: true,
      });

      renderWithRouter(
        <AuthGuard>
          <div>Protected Content</div>
        </AuthGuard>
      );

      expect(screen.getByText("Protected Content")).toBeInTheDocument();
    });

    it("does not show login prompt when authenticated", () => {
      mockUseAuth.mockReturnValue({
        isLoading: false,
        isAuthenticated: true,
      });

      renderWithRouter(
        <AuthGuard>
          <div>Protected Content</div>
        </AuthGuard>
      );

      expect(
        screen.queryByText("Authentication Required")
      ).not.toBeInTheDocument();
    });
  });

  describe("Unauthenticated state", () => {
    it("shows authentication required message when not authenticated", () => {
      mockUseAuth.mockReturnValue({
        isLoading: false,
        isAuthenticated: false,
      });

      renderWithRouter(
        <AuthGuard>
          <div>Protected Content</div>
        </AuthGuard>
      );

      expect(screen.getByText("Authentication Required")).toBeInTheDocument();
    });

    it("shows sign in link when not authenticated", () => {
      mockUseAuth.mockReturnValue({
        isLoading: false,
        isAuthenticated: false,
      });

      renderWithRouter(
        <AuthGuard>
          <div>Protected Content</div>
        </AuthGuard>
      );

      const signInLink = screen.getByRole("link", { name: "Sign In" });
      expect(signInLink).toBeInTheDocument();
      expect(signInLink).toHaveAttribute("href", "/auth/login");
    });

    it("does not render children when not authenticated", () => {
      mockUseAuth.mockReturnValue({
        isLoading: false,
        isAuthenticated: false,
      });

      renderWithRouter(
        <AuthGuard>
          <div>Protected Content</div>
        </AuthGuard>
      );

      expect(screen.queryByText("Protected Content")).not.toBeInTheDocument();
    });

    it("shows helpful message to sign in", () => {
      mockUseAuth.mockReturnValue({
        isLoading: false,
        isAuthenticated: false,
      });

      renderWithRouter(
        <AuthGuard>
          <div>Protected Content</div>
        </AuthGuard>
      );

      expect(
        screen.getByText("Please sign in to access this page.")
      ).toBeInTheDocument();
    });
  });

  describe("Custom fallback", () => {
    it("renders custom fallback when provided and not authenticated", () => {
      mockUseAuth.mockReturnValue({
        isLoading: false,
        isAuthenticated: false,
      });

      renderWithRouter(
        <AuthGuard fallback={<div>Custom Login Prompt</div>}>
          <div>Protected Content</div>
        </AuthGuard>
      );

      expect(screen.getByText("Custom Login Prompt")).toBeInTheDocument();
      expect(
        screen.queryByText("Authentication Required")
      ).not.toBeInTheDocument();
    });

    it("does not show custom fallback when authenticated", () => {
      mockUseAuth.mockReturnValue({
        isLoading: false,
        isAuthenticated: true,
      });

      renderWithRouter(
        <AuthGuard fallback={<div>Custom Login Prompt</div>}>
          <div>Protected Content</div>
        </AuthGuard>
      );

      expect(screen.queryByText("Custom Login Prompt")).not.toBeInTheDocument();
      expect(screen.getByText("Protected Content")).toBeInTheDocument();
    });
  });

  describe("Multiple children", () => {
    it("renders all children when authenticated", () => {
      mockUseAuth.mockReturnValue({
        isLoading: false,
        isAuthenticated: true,
      });

      renderWithRouter(
        <AuthGuard>
          <div>Child 1</div>
          <div>Child 2</div>
          <div>Child 3</div>
        </AuthGuard>
      );

      expect(screen.getByText("Child 1")).toBeInTheDocument();
      expect(screen.getByText("Child 2")).toBeInTheDocument();
      expect(screen.getByText("Child 3")).toBeInTheDocument();
    });
  });
});
