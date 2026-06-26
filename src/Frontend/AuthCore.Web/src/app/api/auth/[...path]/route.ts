import type { NextRequest } from "next/server";

import { proxyAuthCoreRequest } from "@/lib/authcore-api";

type AuthCoreRouteContext = {
  params: Promise<{
    path: string[];
  }>;
};

export async function GET(
  request: NextRequest,
  context: AuthCoreRouteContext,
) {
  return handle(request, context);
}

export async function POST(
  request: NextRequest,
  context: AuthCoreRouteContext,
) {
  return handle(request, context);
}

export async function DELETE(
  request: NextRequest,
  context: AuthCoreRouteContext,
) {
  return handle(request, context);
}

async function handle(request: NextRequest, context: AuthCoreRouteContext) {
  const { path } = await context.params;

  return proxyAuthCoreRequest(request, path);
}
