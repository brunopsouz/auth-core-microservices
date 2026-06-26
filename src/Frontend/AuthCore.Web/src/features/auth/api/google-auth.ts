import { readApiError } from "@/lib/http-errors";

export type GoogleOnboarding = {
  email: string;
  fullName: string;
  pictureUrl: string;
};

export type CompleteGoogleOnboardingPayload = {
  firstName: string;
  lastName: string;
  contact: string;
};

export function startGoogleAuthentication(returnPath = "/") {
  const returnUrl = new URL(returnPath, window.location.origin);
  const searchParams = new URLSearchParams({
    returnUrl: returnUrl.href,
  });

  window.location.href = `/api/auth/external/google?${searchParams.toString()}`;
}

export async function getGoogleOnboarding() {
  const response = await fetch("/api/auth/external/google/onboarding", {
    method: "GET",
    headers: {
      Accept: "application/json",
    },
    cache: "no-store",
  });

  if (!response.ok) {
    throw new Error(await readApiError(response));
  }

  return (await response.json()) as GoogleOnboarding;
}

export async function completeGoogleOnboarding(
  payload: CompleteGoogleOnboardingPayload,
) {
  const response = await fetch("/api/auth/external/google/onboarding", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      FirstName: payload.firstName,
      LastName: payload.lastName,
      Contact: payload.contact,
    }),
  });

  if (!response.ok) {
    throw new Error(await readApiError(response));
  }
}
