import type { NextRequest } from "next/server";
import { NextResponse } from "next/server";

import {
  AUTH_SESSION_COOKIE_NAME,
  REDIRECT_WHEN_AUTHENTICATED_ROUTE,
  REDIRECT_WHEN_NOT_AUTHENTICATED_ROUTE,
  findPublicRoute,
} from "@/lib/auth-routes";

export function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const publicRoute = findPublicRoute(pathname);
  const hasSessionCookie = request.cookies.has(AUTH_SESSION_COOKIE_NAME);

  if (!hasSessionCookie && publicRoute) {
    return NextResponse.next();
  }

  if (!hasSessionCookie && !publicRoute) {
    const redirectUrl = request.nextUrl.clone();
    redirectUrl.pathname = REDIRECT_WHEN_NOT_AUTHENTICATED_ROUTE;
    redirectUrl.search = "";

    return NextResponse.redirect(redirectUrl);
  }

  if (hasSessionCookie && publicRoute?.whenAuthenticated === "redirect") {
    const redirectUrl = request.nextUrl.clone();
    redirectUrl.pathname = REDIRECT_WHEN_AUTHENTICATED_ROUTE;
    redirectUrl.search = "";

    return NextResponse.redirect(redirectUrl);
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    "/((?!api|_next/static|_next/image|favicon.ico|sitemap.xml|robots.txt|.*\\..*).*)",
  ],
};
