import { UserManager, WebStorageStateStore } from "oidc-client-ts";
import { authConfig } from "~/config/auth.config";

let userManagerInstance: UserManager | null = null;

export function getUserManager(): UserManager {
  if (typeof window === "undefined") {
    throw new Error("UserManager can only be used in the browser");
  }

  if (!userManagerInstance) {
    userManagerInstance = new UserManager({
      ...authConfig,
      userStore: new WebStorageStateStore({ store: window.sessionStorage }),
    });
  }

  return userManagerInstance;
}

export async function getAccessToken(): Promise<string | null> {
  if (typeof window === "undefined") {
    return null;
  }

  const userManager = getUserManager();
  const user = await userManager.getUser();
  return user?.access_token ?? null;
}

export async function signIn(): Promise<void> {
  const userManager = getUserManager();
  await userManager.signinRedirect();
}

export async function signOut(): Promise<void> {
  const userManager = getUserManager();
  await userManager.signoutRedirect();
}
