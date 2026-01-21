import { authConfig } from "~/config/auth.config";
import type { User } from "./types";

export function getStoredToken(): string | null {
  if (typeof window === "undefined") {
    return null;
  }
  return localStorage.getItem(authConfig.tokenKey);
}

export function setStoredToken(token: string): void {
  if (typeof window === "undefined") {
    return;
  }
  localStorage.setItem(authConfig.tokenKey, token);
}

export function removeStoredToken(): void {
  if (typeof window === "undefined") {
    return;
  }
  localStorage.removeItem(authConfig.tokenKey);
}

export function getStoredUser(): User | null {
  if (typeof window === "undefined") {
    return null;
  }
  const userJson = localStorage.getItem(authConfig.userKey);
  if (!userJson) {
    return null;
  }
  try {
    return JSON.parse(userJson) as User;
  } catch {
    return null;
  }
}

export function setStoredUser(user: User): void {
  if (typeof window === "undefined") {
    return;
  }
  localStorage.setItem(authConfig.userKey, JSON.stringify(user));
}

export function removeStoredUser(): void {
  if (typeof window === "undefined") {
    return;
  }
  localStorage.removeItem(authConfig.userKey);
}

export function clearAuthStorage(): void {
  removeStoredToken();
  removeStoredUser();
}
