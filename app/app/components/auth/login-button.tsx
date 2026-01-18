import { Link } from "react-router";

interface LoginButtonProps {
  className?: string;
  children?: React.ReactNode;
}

export function LoginButton({ className, children }: LoginButtonProps) {
  return (
    <Link
      to="/auth/login"
      className={
        className ??
        "rounded-md bg-blue-600 px-4 py-2 text-white hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
      }
    >
      {children ?? "Sign In"}
    </Link>
  );
}
