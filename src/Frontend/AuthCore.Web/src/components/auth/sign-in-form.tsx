"use client";

import { useRouter } from "next/navigation";
import { FormEvent, useState } from "react";
import { toast } from "sonner";
import { Eye, EyeOff, LockKeyhole, Mail, ShieldCheck } from "lucide-react";
import Link from "next/link";

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
    <Card className="w-full max-w-[52rem] rounded-lg border-border/70 bg-card/55 px-6 py-8 shadow-2xl backdrop-blur-xl sm:px-10 lg:px-16 lg:py-12">
      <CardContent className="p-0">
        <div className="mx-auto max-w-[43rem]">
          <div className="mb-10 flex flex-col items-center gap-5 text-center">
            <div className="inline-flex items-center gap-2 rounded-lg border border-border/70 bg-background/45 px-4 py-3 text-base font-semibold">
              <ShieldCheck className="size-5 text-primary" aria-hidden="true" />
              AuthCore
            </div>
            <div className="space-y-4">
              <h1 className="text-4xl font-bold leading-tight text-foreground">
                Entrar no AuthCore
              </h1>
              <p className="mx-auto max-w-md text-lg leading-8 text-muted-foreground">
                Acesse sua conta para entrar na aplicacao de forma segura.
              </p>
            </div>
          </div>

          <form onSubmit={handleSubmit}>
            <FieldGroup className="gap-5">
              {error && (
                <Alert variant="destructive" className="border-destructive/40 bg-destructive/10">
                  <AlertTitle>Falha no login</AlertTitle>
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}

              <Field className="gap-3">
                <FieldLabel htmlFor="email" className="text-base font-semibold text-foreground">
                  E-mail
                </FieldLabel>
                <div className="relative">
                  <Mail
                    className="pointer-events-none absolute left-5 top-1/2 size-5 -translate-y-1/2 text-muted-foreground"
                    aria-hidden="true"
                  />
                  <Input
                    id="email"
                    name="email"
                    type="email"
                    autoComplete="email"
                    placeholder="seu@email.com"
                    required
                    className="h-14 rounded-lg border-border/80 bg-background/30 pl-14 text-lg text-foreground placeholder:text-muted-foreground/70 focus-visible:ring-primary/30"
                  />
                </div>
              </Field>

              <Field className="gap-3">
                <FieldLabel htmlFor="password" className="text-base font-semibold text-foreground">
                  Senha
                </FieldLabel>
                <div className="relative">
                  <LockKeyhole
                    className="pointer-events-none absolute left-5 top-1/2 size-5 -translate-y-1/2 text-muted-foreground"
                    aria-hidden="true"
                  />
                  <Input
                    id="password"
                    name="password"
                    type={showPassword ? "text" : "password"}
                    autoComplete="current-password"
                    placeholder="**********"
                    required
                    className="h-14 rounded-lg border-border/80 bg-background/30 pl-14 pr-14 text-lg text-foreground placeholder:text-muted-foreground/70 focus-visible:ring-primary/30"
                  />
                  <button
                    type="button"
                    className="absolute right-5 top-1/2 -translate-y-1/2 text-muted-foreground transition-colors hover:text-foreground"
                    onClick={() => setShowPassword((current) => !current)}
                    aria-label={showPassword ? "Ocultar senha" : "Mostrar senha"}
                  >
                    {showPassword ? (
                      <EyeOff className="size-5" aria-hidden="true" />
                    ) : (
                      <Eye className="size-5" aria-hidden="true" />
                    )}
                  </button>
                </div>
              </Field>

              <div className="flex items-center justify-between gap-4 text-base">
                <label className="flex items-center gap-3 text-muted-foreground">
                  <Checkbox name="remember" className="size-5 rounded-[4px]" />
                  Lembrar de mim
                </label>
                <button
                  type="button"
                  className="font-medium text-primary hover:text-primary/80"
                  onClick={handleForgotPassword}
                >
                  Esqueceu a senha?
                </button>
              </div>

              <Button
                className="mt-5 h-14 w-full rounded-lg bg-primary text-lg font-bold text-primary-foreground shadow-[0_0_24px_hsl(170_90%_48%/0.28)] hover:bg-primary/90"
                type="submit"
                disabled={isPending}
              >
                {isPending ? "Entrando..." : "Entrar"}
              </Button>
            </FieldGroup>
          </form>

          <div className="my-9 flex items-center gap-6 text-sm text-muted-foreground">
            <span className="h-px flex-1 bg-border" />
            <span>ou continue com e-mail</span>
            <span className="h-px flex-1 bg-border" />
          </div>

          <Button
            type="button"
            variant="outline"
            className="h-14 w-full rounded-lg border-border/80 bg-background/20 text-lg font-bold hover:bg-background/35"
            onClick={handleGoogleSignIn}
          >
            <GoogleMark />
            Entrar com Google
          </Button>

          <p className="mt-8 text-center text-base text-muted-foreground">
            Ainda nao tem conta?{" "}
            <Link className="font-semibold text-primary hover:text-primary/80" href="/register">
              Criar conta
            </Link>
          </p>
        </div>
      </CardContent>
    </Card>
  );
}

function GoogleMark() {
  return (
    <span className="mr-2 grid size-6 place-items-center rounded-full bg-background text-base font-bold">
      <span className="bg-[conic-gradient(from_-45deg,#4285f4_0_25%,#34a853_0_50%,#fbbc05_0_75%,#ea4335_0_100%)] bg-clip-text text-transparent">
        G
      </span>
    </span>
  );
}
