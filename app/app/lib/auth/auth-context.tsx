import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import type { User } from "oidc-client-ts";

interface AuthContextType {
  user: User | null;
  isLoading: boolean;
  isAuthenticated: boolean;
  signIn: () => Promise<void>;
  signOut: () => Promise<void>;
  getAccessToken: () => Promise<string | null>;
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
    let mounted = true;

    const initAuth = async () => {
      if (typeof window === "undefined") {
        return;
      }

      const { getUserManager } = await import("./user-manager.client");
      const userManager = getUserManager();

      try {
        const currentUser = await userManager.getUser();
        if (mounted) {
          setUser(currentUser);
          setIsLoading(false);
        }
      } catch (error) {
        console.error("Failed to get user:", error);
        if (mounted) {
          setIsLoading(false);
        }
      }

      userManager.events.addUserLoaded((loadedUser) => {
        if (mounted) {
          setUser(loadedUser);
        }
      });

      userManager.events.addUserUnloaded(() => {
        if (mounted) {
          setUser(null);
        }
      });

      userManager.events.addSilentRenewError((error) => {
        console.error("Silent renew error:", error);
      });
    };

    initAuth();

    return () => {
      mounted = false;
    };
  }, []);

  const signIn = async () => {
    const { signIn: signInFn } = await import("./user-manager.client");
    await signInFn();
  };

  const signOut = async () => {
    const { signOut: signOutFn } = await import("./user-manager.client");
    await signOutFn();
  };

  const getAccessToken = async () => {
    const { getAccessToken: getTokenFn } = await import(
      "./user-manager.client"
    );
    return getTokenFn();
  };

  const value: AuthContextType = {
    user,
    isLoading,
    isAuthenticated: !!user && !user.expired,
    signIn,
    signOut,
    getAccessToken,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
