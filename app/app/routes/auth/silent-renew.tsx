import { useEffect } from "react";

export function meta() {
  return [{ title: "Silent Renew" }];
}

export default function SilentRenew() {
  useEffect(() => {
    const handleSilentRenew = async () => {
      try {
        const { getUserManager } = await import(
          "~/lib/auth/user-manager.client"
        );
        const userManager = getUserManager();
        await userManager.signinSilentCallback();
      } catch (err) {
        console.error("Silent renew callback error:", err);
      }
    };

    handleSilentRenew();
  }, []);

  return null;
}
