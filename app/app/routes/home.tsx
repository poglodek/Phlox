import { Link } from "react-router";
import type { Route } from "./+types/home";
import { LoginButton } from "~/components/auth/login-button";
import { LogoutButton } from "~/components/auth/logout-button";
import { useAuth } from "~/lib/auth/auth-context";

export function meta({}: Route.MetaArgs) {
  return [
    { title: "Phlox - AI Assistant" },
    { name: "description", content: "Phlox AI Assistant Application" },
  ];
}

export default function Home() {
  const { isAuthenticated, isLoading, user } = useAuth();

  return (
    <main className="flex min-h-screen flex-col items-center justify-center bg-gradient-to-b from-gray-50 to-gray-100">
      <div className="w-full max-w-md space-y-8 px-4">
        <div className="text-center">
          <h1 className="text-4xl font-bold tracking-tight text-gray-900">
            Phlox
          </h1>
          <p className="mt-2 text-lg text-gray-600">AI Assistant Application</p>
        </div>

        <div className="rounded-xl bg-white p-8 shadow-lg">
          {isLoading ? (
            <div className="flex justify-center">
              <div className="h-8 w-8 animate-spin rounded-full border-4 border-blue-600 border-t-transparent"></div>
            </div>
          ) : isAuthenticated ? (
            <div className="space-y-4">
              <div className="text-center">
                <p className="text-sm text-gray-500">Signed in as</p>
                <p className="font-medium text-gray-900">
                  {user?.name ?? user?.username ?? user?.email ?? "User"}
                </p>
              </div>
              <div className="flex flex-col gap-3">
                <Link
                  to="/dashboard"
                  className="rounded-md bg-blue-600 px-4 py-2 text-center text-white hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
                >
                  Go to Dashboard
                </Link>
                <LogoutButton className="rounded-md bg-gray-200 px-4 py-2 text-gray-700 hover:bg-gray-300 focus:outline-none focus:ring-2 focus:ring-gray-500 focus:ring-offset-2" />
              </div>
            </div>
          ) : (
            <div className="space-y-4">
              <p className="text-center text-gray-600">
                Sign in to access the AI Assistant
              </p>
              <LoginButton className="w-full rounded-md bg-blue-600 px-4 py-2 text-center text-white hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2" />
            </div>
          )}
        </div>
      </div>
    </main>
  );
}
