import type { UserManagerSettings } from "oidc-client-ts";

const getBaseUrl = () => {
  if (typeof window !== "undefined") {
    return window.location.origin;
  }
  return "http://localhost:5173";
};

export const authConfig: UserManagerSettings = {
  authority: "http://localhost:8080/realms/phlox",
  client_id: "phlox-frontend",
  redirect_uri: `${getBaseUrl()}/auth/callback`,
  post_logout_redirect_uri: `${getBaseUrl()}/`,
  silent_redirect_uri: `${getBaseUrl()}/auth/silent-renew`,
  scope: "openid profile email",
  response_type: "code",
  automaticSilentRenew: true,
  includeIdTokenInSilentRenew: true,
};

export const apiBaseUrl = "https://localhost:7086";
