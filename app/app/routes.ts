import { type RouteConfig, index, route } from "@react-router/dev/routes";

export default [
  index("routes/home.tsx"),
  route("auth/login", "routes/auth/login.tsx"),
  route("auth/register", "routes/auth/register.tsx"),
  route("auth/logout", "routes/auth/logout.tsx"),
  route("dashboard", "routes/protected/dashboard.tsx"),
  route("chat", "routes/protected/chat/index.tsx"),
  route("chat/:chatId", "routes/protected/chat/$chatId.tsx"),
] satisfies RouteConfig;
