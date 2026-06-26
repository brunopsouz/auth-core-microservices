export const AUTH_SESSION_COOKIE_NAME =
  process.env.AUTHCORE_SESSION_COOKIE_NAME ?? "__Host-auth.sid";

export const REDIRECT_WHEN_NOT_AUTHENTICATED_ROUTE = "/sign-in";
export const REDIRECT_WHEN_AUTHENTICATED_ROUTE = "/";

export const PUBLIC_ROUTES = [
  {
    path: "/sign-in",
    whenAuthenticated: "redirect",
  },
  {
    path: "/register",
    whenAuthenticated: "redirect",
  },
  {
    path: "/onboarding",
    whenAuthenticated: "redirect",
  },
  {
    path: "/auth/error",
    whenAuthenticated: "next",
  },
] as const;

export function findPublicRoute(pathname: string) {
  return PUBLIC_ROUTES.find((route) => route.path === pathname);
}
