export interface User {
  id: string;
  email: string;
  username: string;
  name: string | null;
  createdAt: string;
  lastLoginAt: string | null;
  isActive: boolean;
}

export interface AuthResponse {
  token: string;
  userId: string;
  email: string;
  username: string;
  name: string | null;
  expiresAt: string;
}

export interface LoginRequest {
  emailOrUsername: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  username: string;
  password: string;
  name?: string;
}

export interface AuthState {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
}
