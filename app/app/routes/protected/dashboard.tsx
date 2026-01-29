import { useEffect, useState } from "react";
import { Link } from "react-router";
import { AuthGuard } from "~/components/auth/auth-guard";
import { LogoutButton } from "~/components/auth/logout-button";
import { useAuth } from "~/lib/auth/auth-context";
import { userApi } from "~/lib/api/api-client";
import type { User } from "~/lib/auth/types";

export function meta() {
  return [{ title: "Dashboard - Phlox" }];
}

function DashboardContent() {
  const { user } = useAuth();
  const [apiUser, setApiUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchUserInfo = async () => {
      try {
        const userInfo = await userApi.getCurrentUser();
        setApiUser(userInfo);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to fetch user");
      } finally {
        setLoading(false);
      }
    };

    fetchUserInfo();
  }, []);

  return (
    <div className="min-h-screen bg-gray-100">
      <header className="bg-white shadow">
        <div className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8 flex justify-between items-center">
          <h1 className="text-3xl font-bold tracking-tight text-gray-900">
            Dashboard
          </h1>
          <div className="flex items-center gap-4">
            <Link
              to="/chat"
              className="rounded-md bg-blue-600 px-4 py-2 text-white hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
            >
              Open Chat
            </Link>
            <LogoutButton />
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-7xl py-6 sm:px-6 lg:px-8">
        <div className="px-4 py-6 sm:px-0">
          <div className="grid gap-6 md:grid-cols-2">
            <div className="rounded-lg bg-white p-6 shadow">
              <h2 className="text-xl font-semibold text-gray-900 mb-4">
                Session Info
              </h2>
              {user ? (
                <dl className="space-y-2">
                  <div>
                    <dt className="text-sm font-medium text-gray-500">
                      User ID
                    </dt>
                    <dd className="text-sm text-gray-900">{user.id}</dd>
                  </div>
                  <div>
                    <dt className="text-sm font-medium text-gray-500">Email</dt>
                    <dd className="text-sm text-gray-900">{user.email}</dd>
                  </div>
                  <div>
                    <dt className="text-sm font-medium text-gray-500">
                      Username
                    </dt>
                    <dd className="text-sm text-gray-900">{user.username}</dd>
                  </div>
                  <div>
                    <dt className="text-sm font-medium text-gray-500">Name</dt>
                    <dd className="text-sm text-gray-900">
                      {user.name ?? "N/A"}
                    </dd>
                  </div>
                </dl>
              ) : (
                <p className="text-gray-500">No user info available</p>
              )}
            </div>

            <div className="rounded-lg bg-white p-6 shadow">
              <h2 className="text-xl font-semibold text-gray-900 mb-4">
                API User Info
              </h2>
              {loading ? (
                <div className="flex items-center">
                  <div className="h-5 w-5 animate-spin rounded-full border-2 border-blue-600 border-t-transparent"></div>
                  <span className="ml-2 text-gray-500">Loading...</span>
                </div>
              ) : error ? (
                <p className="text-red-500">{error}</p>
              ) : apiUser ? (
                <dl className="space-y-2">
                  <div>
                    <dt className="text-sm font-medium text-gray-500">
                      Database ID
                    </dt>
                    <dd className="text-sm text-gray-900">{apiUser.id}</dd>
                  </div>
                  <div>
                    <dt className="text-sm font-medium text-gray-500">Email</dt>
                    <dd className="text-sm text-gray-900">{apiUser.email}</dd>
                  </div>
                  <div>
                    <dt className="text-sm font-medium text-gray-500">
                      Username
                    </dt>
                    <dd className="text-sm text-gray-900">
                      {apiUser.username}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-sm font-medium text-gray-500">Name</dt>
                    <dd className="text-sm text-gray-900">
                      {apiUser.name ?? "N/A"}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-sm font-medium text-gray-500">
                      Created At
                    </dt>
                    <dd className="text-sm text-gray-900">
                      {new Date(apiUser.createdAt).toLocaleString()}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-sm font-medium text-gray-500">
                      Last Login
                    </dt>
                    <dd className="text-sm text-gray-900">
                      {apiUser.lastLoginAt
                        ? new Date(apiUser.lastLoginAt).toLocaleString()
                        : "N/A"}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-sm font-medium text-gray-500">Status</dt>
                    <dd className="text-sm text-gray-900">
                      {apiUser.isActive ? (
                        <span className="text-green-600">Active</span>
                      ) : (
                        <span className="text-red-600">Inactive</span>
                      )}
                    </dd>
                  </div>
                </dl>
              ) : (
                <p className="text-gray-500">No API user info available</p>
              )}
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}

export default function Dashboard() {
  return (
    <AuthGuard>
      <DashboardContent />
    </AuthGuard>
  );
}
