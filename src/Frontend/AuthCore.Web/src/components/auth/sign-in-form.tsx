"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { FormEvent, useState } from "react";
import { toast } from "sonner";
import { Eye, EyeOff, LockKeyhole, Mail, ShieldCheck } from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { startGoogleAuthentication } from "@/features/auth/api/google-auth";
import { readApiError } from "@/lib/http-errors";

export function SignInForm() {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [isPending, setIsPending] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const formData = new FormData(event.currentTarget);
    setError(null);
    setIsPending(true);

    try {
      const response = await fetch("/api/auth/session/login", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          Email: String(formData.get("email") ?? ""),
          Password: String(formData.get("password") ?? ""),
        }),
      });

      if (!response.ok) {
        setError(await readApiError(response));
        return;
      }

      toast.success("Login realizado.");
      router.replace("/");
      router.refresh();
    } finally {
      setIsPending(false);
    }
  }

  function handleGoogleSignIn() {
    startGoogleAuthentication();
  }

  function handleForgotPassword() {
    toast.info("Recuperacao de senha sera especificada em fase futura.");
  }

  return (
    <Card className="w-full max-w-[27rem] border-0 bg-transparent py-0 shadow-none ring-0 lg:min-h-[40rem]">
      <CardContent className="p-0 lg:flex lg:flex-1 lg:flex-col lg:justify-center">
        <div className="mb-6">
          <div className="mb-4 flex items-center gap-3">
            <div className="inline-flex size-10 items-center justify-center rounded-xl border border-[#d9dee7] bg-[#f7f8fa] text-[#151922]">
              <ShieldCheck className="size-5" aria-hidden="true" />
            </div>
            <span className="text-lg font-bold text-[#111318]">AuthCore</span>
          </div>
          <h1 className="text-[1.7rem] font-bold leading-tight text-[#111318]">
            Entrar no AuthCore
          </h1>
          <p className="mt-2 text-sm leading-6 text-[#68707d]">
            Acesse sua conta para entrar na aplicacao de forma segura.
          </p>
        </div>

        <form onSubmit={handleSubmit}>
          <FieldGroup className="gap-3.5">
            {error && (
              <Alert variant="destructive" className="rounded-lg">
                <AlertTitle>Falha no login</AlertTitle>
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}

            <Field className="gap-2">
              <FieldLabel htmlFor="email" className="text-sm font-semibold text-[#171a20]">
                E-mail
              </FieldLabel>
              <div className="relative">
                <Mail
                  className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-[#8a93a0]"
                  aria-hidden="true"
                />
                <Input
                  id="email"
                  name="email"
                  type="email"
                  autoComplete="email"
                  placeholder="seu@email.com"
                  required
                  className="h-11 rounded-lg border-[#d8dee8] bg-white pl-11 text-sm text-[#111318] shadow-[inset_0_1px_0_rgba(255,255,255,0.7)] placeholder:text-[#9aa3af] focus-visible:ring-[#9aa3b2]/35"
                />
              </div>
            </Field>

            <Field className="gap-2">
              <div className="flex items-center justify-between gap-4">
                <FieldLabel htmlFor="password" className="text-sm font-semibold text-[#171a20]">
                  Senha
                </FieldLabel>
                <button
                  type="button"
                  className="text-xs font-medium text-[#68707d] transition-colors hover:text-[#111318]"
                  onClick={handleForgotPassword}
                >
                  Esqueceu a senha?
                </button>
              </div>
              <div className="relative">
                <LockKeyhole
                  className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-[#8a93a0]"
                  aria-hidden="true"
                />
                <Input
                  id="password"
                  name="password"
                  type={showPassword ? "text" : "password"}
                  autoComplete="current-password"
                  placeholder="**********"
                  required
                  className="h-11 rounded-lg border-[#d8dee8] bg-white pl-11 pr-11 text-sm text-[#111318] shadow-[inset_0_1px_0_rgba(255,255,255,0.7)] placeholder:text-[#9aa3af] focus-visible:ring-[#9aa3b2]/35"
                />
                <button
                  type="button"
                  className="absolute right-4 top-1/2 -translate-y-1/2 text-[#8a93a0] transition-colors hover:text-[#111318]"
                  onClick={() => setShowPassword((current) => !current)}
                  aria-label={showPassword ? "Ocultar senha" : "Mostrar senha"}
                >
                  {showPassword ? (
                    <EyeOff className="size-4" aria-hidden="true" />
                  ) : (
                    <Eye className="size-4" aria-hidden="true" />
                  )}
                </button>
              </div>
            </Field>

            <div className="flex items-center justify-between gap-4">
              <label className="flex items-center gap-2 text-xs font-medium text-[#68707d]">
                <Checkbox name="remember" className="size-4 rounded-[4px] border-[#d8dee8]" />
                Lembrar de mim
              </label>
            </div>

            <Button
              className="mt-1 h-11 w-full rounded-lg bg-[#111722] text-sm font-semibold text-white shadow-[0_10px_24px_rgba(17,23,34,0.16)] hover:bg-[#1a2230]"
              type="submit"
              disabled={isPending}
            >
              {isPending ? "Entrando..." : "Entrar"}
            </Button>
          </FieldGroup>
        </form>

        <div className="my-5 flex items-center gap-4 text-xs font-medium text-[#8a93a0]">
          <span className="h-px flex-1 bg-[#dfe4ec]" />
          <span>ou continue com e-mail</span>
          <span className="h-px flex-1 bg-[#dfe4ec]" />
        </div>

        <Button
          type="button"
          variant="outline"
          className="h-11 w-full rounded-lg border-[#d8dee8] bg-white text-sm font-semibold text-[#111318] shadow-none hover:bg-[#f6f7f9]"
          onClick={handleGoogleSignIn}
        >
          <GoogleMark />
          Entrar com Google
        </Button>

        <p className="mt-5 text-center text-sm text-[#68707d]">
          Ainda nao tem conta?{" "}
          <Link className="font-semibold text-[#111318] hover:text-[#3b4350]" href="/register">
            Criar conta
          </Link>
        </p>
      </CardContent>
    </Card>
  );
}

function GoogleMark() {
  return (
    <span className="mr-2 grid size-5 place-items-center rounded-full bg-white text-sm font-bold">
      <span className="bg-[conic-gradient(from_-45deg,#4285f4_0_25%,#34a853_0_50%,#fbbc05_0_75%,#ea4335_0_100%)] bg-clip-text text-transparent">
        G
      </span>
    </span>
  );
}
