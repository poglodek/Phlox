import { type RouteConfig, index, route } from "@react-router/dev/routes";

export default [
  index("routes/home.tsx"),
  route("auth/callback", "routes/auth/callback.tsx"),
  route("auth/silent-renew", "routes/auth/silent-renew.tsx"),
  route("auth/logout", "routes/auth/logout.tsx"),
  route("dashboard", "routes/protected/dashboard.tsx"),
] satisfies RouteConfig;
