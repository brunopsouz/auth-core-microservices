import { NextRequest, NextResponse } from "next/server";

const DEFAULT_AUTHCORE_API_BASE_URL = "http://localhost:5012";

const FORWARDED_REQUEST_HEADERS = [
  "accept",
  "content-type",
  "cookie",
  "origin",
  "referer",
  "user-agent",
  "x-csrf-token",
  "x-forwarded-for",
  "x-forwarded-host",
  "x-forwarded-proto",
] as const;

const FORWARDED_RESPONSE_HEADERS = [
  "cache-control",
  "content-type",
  "location",
  "retry-after",
] as const;

export async function proxyAuthCoreRequest(
  request: NextRequest,
  pathSegments: string[],
) {
  const targetUrl = buildTargetUrl(request, pathSegments);
  const response = await fetch(targetUrl, {
    method: request.method,
    headers: buildRequestHeaders(request),
    body: canForwardBody(request.method) ? await request.text() : undefined,
    cache: "no-store",
    redirect: "manual",
  });

  return buildProxyResponse(response);
}

function buildTargetUrl(request: NextRequest, pathSegments: string[]) {
  const baseUrl =
    process.env.AUTHCORE_API_BASE_URL ?? DEFAULT_AUTHCORE_API_BASE_URL;
  const normalizedBaseUrl = baseUrl.endsWith("/") ? baseUrl.slice(0, -1) : baseUrl;
  const path = pathSegments.map(encodeURIComponent).join("/");

  return `${normalizedBaseUrl}/api/auth/${path}${request.nextUrl.search}`;
}

function buildRequestHeaders(request: NextRequest) {
  const headers = new Headers();

  for (const headerName of FORWARDED_REQUEST_HEADERS) {
    const value = request.headers.get(headerName);

    if (value) {
      headers.set(headerName, value);
    }
  }

  return headers;
}

function buildProxyResponse(response: Response) {
  const proxyResponse = new NextResponse(response.body, {
    status: response.status,
    statusText: response.statusText,
  });

  for (const headerName of FORWARDED_RESPONSE_HEADERS) {
    const value = response.headers.get(headerName);

    if (value) {
      proxyResponse.headers.set(headerName, value);
    }
  }

  for (const cookie of getSetCookieHeaders(response.headers)) {
    proxyResponse.headers.append("set-cookie", cookie);
  }

  return proxyResponse;
}

function getSetCookieHeaders(headers: Headers) {
  const headersWithSetCookie = headers as Headers & {
    getSetCookie?: () => string[];
  };

  if (headersWithSetCookie.getSetCookie) {
    return headersWithSetCookie.getSetCookie();
  }

  const setCookie = headers.get("set-cookie");

  return setCookie ? [setCookie] : [];
}

function canForwardBody(method: string) {
  return method !== "GET" && method !== "HEAD";
}
