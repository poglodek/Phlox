import { useEffect, useState } from "react";
import { useNavigate } from "react-router";

export function meta() {
  return [{ title: "Logging out..." }];
}

export default function Logout() {
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const handleLogout = async () => {
      try {
        const { getUserManager } = await import(
          "~/lib/auth/user-manager.client"
        );
        const userManager = getUserManager();

        // Try to handle logout callback if redirected from Keycloak
        const urlParams = new URLSearchParams(window.location.search);
        if (urlParams.has("state")) {
          await userManager.signoutRedirectCallback();
        }

        navigate("/", { replace: true });
      } catch (err) {
        console.error("Logout error:", err);
        setError(err instanceof Error ? err.message : "Logout failed");
        // Still navigate home even on error
        setTimeout(() => navigate("/", { replace: true }), 2000);
      }
    };

    handleLogout();
  }, [navigate]);

  if (error) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <div className="text-center">
          <p className="text-gray-600">Logging out...</p>
          {error && <p className="mt-2 text-sm text-red-500">{error}</p>}
        </div>
      </div>
    );
  }

  return (
    <div className="flex min-h-screen items-center justify-center">
      <div className="text-center">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-blue-600 border-t-transparent mx-auto"></div>
        <p className="mt-4 text-gray-600">Logging out...</p>
      </div>
    </div>
  );
}
