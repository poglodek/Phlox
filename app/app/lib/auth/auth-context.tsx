import {
  createContext,
  useContext,
  useEffect,
  useState,
  useCallback,
  type ReactNode,
} from "react";
import { authApi, userApi } from "~/lib/api/api-client";
import {
  getStoredToken,
  getStoredUser,
  setStoredToken,
  setStoredUser,
  clearAuthStorage,
} from "./token-storage";
import type { User, LoginRequest, RegisterRequest, AuthResponse } from "./types";

interface AuthContextType {
  user: User | null;
  isLoading: boolean;
  isAuthenticated: boolean;
  login: (credentials: LoginRequest) => Promise<void>;
  register: (data: RegisterRequest) => Promise<void>;
  logout: () => void;
  getAccessToken: () => string | null;
}

const AuthContext = createContext<AuthContextType | null>(null);

export function useAuth(): AuthContextType {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}

interface AuthProviderProps {
  children: ReactNode;
}

export function AuthProvider({ children }: AuthProviderProps) {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const initAuth = async () => {
      if (typeof window === "undefined") {
        setIsLoading(false);
        return;
      }

      const token = getStoredToken();
      const storedUser = getStoredUser();

      if (token && storedUser) {
        // Verify token is still valid by fetching current user
        try {
          const currentUser = await userApi.getCurrentUser();
          setUser(currentUser);
          setStoredUser(currentUser);
        } catch {
          // Token is invalid, clear storage
          clearAuthStorage();
        }
      }

      setIsLoading(false);
    };

    initAuth();
  }, []);

  const handleAuthResponse = useCallback((response: AuthResponse) => {
    const newUser: User = {
      id: response.userId,
      email: response.email,
      username: response.username,
      name: response.name,
      createdAt: new Date().toISOString(),
      lastLoginAt: new Date().toISOString(),
      isActive: true,
    };

    setStoredToken(response.token);
    setStoredUser(newUser);
    setUser(newUser);
  }, []);

  const login = useCallback(async (credentials: LoginRequest) => {
    const response = await authApi.login(credentials);
    handleAuthResponse(response);
  }, [handleAuthResponse]);

  const register = useCallback(async (data: RegisterRequest) => {
    const response = await authApi.register(data);
    handleAuthResponse(response);
  }, [handleAuthResponse]);

  const logout = useCallback(() => {
    clearAuthStorage();
    setUser(null);
  }, []);

  const getAccessToken = useCallback(() => {
    return getStoredToken();
  }, []);

  const value: AuthContextType = {
    user,
    isLoading,
    isAuthenticated: !!user,
    login,
    register,
    logout,
    getAccessToken,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
