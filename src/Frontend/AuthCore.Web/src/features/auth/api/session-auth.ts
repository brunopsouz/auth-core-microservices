export type SignInPayload = {
  email: string;
  password: string;
};

export type RegisterPayload = {
  firstName: string;
  lastName: string;
  email: string;
  contact: string;
};

export type CompleteRegistrationPayload = {
  email: string;
  code: string;
  password: string;
  confirmPassword: string;
};

export async function signIn(payload: SignInPayload) {
  return fetch("/api/auth/session/login", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      Email: payload.email,
      Password: payload.password,
    }),
  });
}

export async function register(payload: RegisterPayload) {
  return fetch("/api/auth/register", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      FirstName: payload.firstName,
      LastName: payload.lastName,
      Email: payload.email,
      Contact: payload.contact,
    }),
  });
}

export async function completeRegistration(
  payload: CompleteRegistrationPayload,
) {
  return fetch("/api/auth/complete-registration", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      Email: payload.email,
      Code: payload.code,
      Password: payload.password,
      ConfirmPassword: payload.confirmPassword,
    }),
  });
}

export async function resendVerification(email: string) {
  return fetch("/api/auth/resend-verification", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      Email: email,
    }),
  });
}

export async function logoutSession() {
  return fetch("/api/auth/session/logout", {
    method: "POST",
    headers: buildCsrfHeaders(),
  });
}

function buildCsrfHeaders(): HeadersInit {
  const token = getCookieValue("XSRF-TOKEN");

  return token
    ? {
        "X-CSRF-TOKEN": token,
      }
    : {};
}

function getCookieValue(name: string) {
  const cookie = document.cookie
    .split("; ")
    .find((value) => value.startsWith(`${name}=`));

  return cookie ? decodeURIComponent(cookie.split("=")[1] ?? "") : null;
}
