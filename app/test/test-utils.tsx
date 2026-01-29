import { render, type RenderOptions } from "@testing-library/react";
import { type ReactElement, type ReactNode } from "react";
import { MemoryRouter } from "react-router";
import { AuthProvider } from "~/lib/auth/auth-context";

interface WrapperProps {
  children: ReactNode;
}

function AllProviders({ children }: WrapperProps) {
  return (
    <MemoryRouter>
      <AuthProvider>{children}</AuthProvider>
    </MemoryRouter>
  );
}

function RouterOnlyWrapper({ children }: WrapperProps) {
  return <MemoryRouter>{children}</MemoryRouter>;
}

const customRender = (
  ui: ReactElement,
  options?: Omit<RenderOptions, "wrapper">
) => render(ui, { wrapper: AllProviders, ...options });

const renderWithRouter = (
  ui: ReactElement,
  options?: Omit<RenderOptions, "wrapper">
) => render(ui, { wrapper: RouterOnlyWrapper, ...options });

export * from "@testing-library/react";
export { customRender as render, renderWithRouter };
