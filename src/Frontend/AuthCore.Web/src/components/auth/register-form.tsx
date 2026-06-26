"use client";

import Link from "next/link";
import { FormEvent, useState } from "react";
import { toast } from "sonner";
import {
  BriefcaseBusiness,
  Eye,
  EyeOff,
  LockKeyhole,
  Mail,
  ShieldCheck,
  UserRound,
} from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { startGoogleAuthentication } from "@/features/auth/api/google-auth";
import { readApiError } from "@/lib/http-errors";

export function RegisterForm() {
  const [error, setError] = useState<string | null>(null);
  const [registeredEmail, setRegisteredEmail] = useState<string | null>(null);
  const [isPending, setIsPending] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const form = event.currentTarget;
    const formData = new FormData(form);
    const email = String(formData.get("email") ?? "");
    const fullName = String(formData.get("fullName") ?? "");
    const { firstName, lastName } = splitFullName(fullName);

    setError(null);
    setRegisteredEmail(null);
    setIsPending(true);

    try {
      const response = await fetch("/api/auth/register", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          FirstName: firstName,
          LastName: lastName,
          Email: email,
          Contact: String(formData.get("contact") ?? ""),
          Password: String(formData.get("password") ?? ""),
          ConfirmPassword: String(formData.get("confirmPassword") ?? ""),
        }),
      });

      if (!response.ok) {
        setError(await readApiError(response));
        return;
      }

      form.reset();
      setRegisteredEmail(email);
      toast.success("Conta criada.");
    } finally {
      setIsPending(false);
    }
  }

  function handleGoogleRegister() {
    startGoogleAuthentication();
  }

  return (
    <Card className="w-full max-w-[48rem] rounded-lg border-border/70 bg-card/55 px-6 py-7 shadow-2xl backdrop-blur-xl sm:px-10 lg:px-14 lg:py-9">
      <CardContent className="p-0">
        <div className="mx-auto max-w-[40rem]">
          <div className="mb-7 flex flex-col items-center gap-5 text-center">
            <div className="inline-flex items-center gap-2 rounded-lg border border-border/70 bg-background/45 px-4 py-3 text-base font-semibold">
              <ShieldCheck className="size-5 text-muted-foreground" aria-hidden="true" />
              AuthCore
            </div>
            <div className="space-y-3">
              <h1 className="text-3xl font-bold leading-tight text-foreground sm:text-4xl">
                Criar conta no AuthCore
              </h1>
              <p className="mx-auto max-w-md text-lg leading-7 text-muted-foreground">
                Comece com uma base segura e pronta para escalar sua aplicacao.
              </p>
            </div>
          </div>

          <form onSubmit={handleSubmit}>
            <FieldGroup className="gap-4">
              {error && (
                <Alert variant="destructive" className="border-destructive/40 bg-destructive/10">
                  <AlertTitle>Falha no registro</AlertTitle>
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}

              {registeredEmail && (
                <Alert className="border-primary/35 bg-primary/10">
                  <AlertTitle>Registro recebido</AlertTitle>
                  <AlertDescription>
                    Verificacao enviada para {registeredEmail}.
                  </AlertDescription>
                </Alert>
              )}

              <Field className="gap-2">
                <FieldLabel htmlFor="fullName" className="text-base font-semibold text-foreground">
                  Nome completo
                </FieldLabel>
                <div className="relative">
                  <UserRound
                    className="pointer-events-none absolute left-5 top-1/2 size-5 -translate-y-1/2 text-muted-foreground"
                    aria-hidden="true"
                  />
                  <Input
                    id="fullName"
                    name="fullName"
                    autoComplete="name"
                    placeholder="Seu nome completo"
                    required
                    className="h-12 rounded-lg border-border/80 bg-background/30 pl-14 text-lg text-foreground placeholder:text-muted-foreground/70 focus-visible:ring-primary/30"
                  />
                </div>
              </Field>

              <Field className="gap-2">
                <FieldLabel htmlFor="email" className="text-base font-semibold text-foreground">
                  E-mail profissional
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
                    className="h-12 rounded-lg border-border/80 bg-background/30 pl-14 text-lg text-foreground placeholder:text-muted-foreground/70 focus-visible:ring-primary/30"
                  />
                </div>
              </Field>

              <Field className="gap-2">
                <FieldLabel htmlFor="contact" className="text-base font-semibold text-foreground">
                  Contato
                </FieldLabel>
                <div className="relative">
                  <BriefcaseBusiness
                    className="pointer-events-none absolute left-5 top-1/2 size-5 -translate-y-1/2 text-muted-foreground"
                    aria-hidden="true"
                  />
                  <Input
                    id="contact"
                    name="contact"
                    autoComplete="tel"
                    placeholder="Telefone ou contato profissional"
                    required
                    className="h-12 rounded-lg border-border/80 bg-background/30 pl-14 text-lg text-foreground placeholder:text-muted-foreground/70 focus-visible:ring-primary/30"
                  />
                </div>
              </Field>

              <PasswordField
                id="password"
                label="Senha"
                autoComplete="new-password"
                isVisible={showPassword}
                onToggle={() => setShowPassword((current) => !current)}
              />

              <PasswordField
                id="confirmPassword"
                label="Confirmar senha"
                autoComplete="new-password"
                isVisible={showConfirmPassword}
                onToggle={() => setShowConfirmPassword((current) => !current)}
              />

              <label className="flex items-center gap-3 text-sm text-muted-foreground">
                <Checkbox name="terms" required className="size-5 rounded-[4px]" />
                <span>
                  Li e concordo com os{" "}
                  <Link href="/register" className="text-primary hover:text-primary/80">
                    Termos de uso
                  </Link>{" "}
                  e{" "}
                  <Link href="/register" className="text-primary hover:text-primary/80">
                    Politica de privacidade
                  </Link>
                </span>
              </label>

              <Button
                className="h-12 w-full rounded-lg bg-primary text-lg font-bold text-primary-foreground shadow-[0_0_24px_hsl(170_90%_48%/0.28)] hover:bg-primary/90"
                type="submit"
                disabled={isPending}
              >
                {isPending ? "Criando..." : "Criar conta"}
              </Button>
            </FieldGroup>
          </form>

          <div className="my-4 flex items-center gap-6 text-sm text-muted-foreground">
            <span className="h-px flex-1 bg-border" />
            <span>ou continue com</span>
            <span className="h-px flex-1 bg-border" />
          </div>

          <Button
            type="button"
            variant="outline"
            className="h-12 w-full rounded-lg border-border/80 bg-background/20 text-lg font-bold hover:bg-background/35"
            onClick={handleGoogleRegister}
          >
            <GoogleMark />
            Cadastrar com Google
          </Button>

          <p className="mt-5 text-center text-base text-muted-foreground">
            Ja tem conta?{" "}
            <Link className="font-semibold text-primary hover:text-primary/80" href="/sign-in">
              Entrar
            </Link>
          </p>
        </div>
      </CardContent>
    </Card>
  );
}

function PasswordField({
  id,
  label,
  autoComplete,
  isVisible,
  onToggle,
}: {
  id: string;
  label: string;
  autoComplete: string;
  isVisible: boolean;
  onToggle: () => void;
}) {
  return (
    <Field className="gap-2">
      <FieldLabel htmlFor={id} className="text-base font-semibold text-foreground">
        {label}
      </FieldLabel>
      <div className="relative">
        <LockKeyhole
          className="pointer-events-none absolute left-5 top-1/2 size-5 -translate-y-1/2 text-muted-foreground"
          aria-hidden="true"
        />
        <Input
          id={id}
          name={id}
          type={isVisible ? "text" : "password"}
          autoComplete={autoComplete}
          placeholder="**********"
          required
          className="h-12 rounded-lg border-border/80 bg-background/30 pl-14 pr-14 text-lg text-foreground placeholder:text-muted-foreground/70 focus-visible:ring-primary/30"
        />
        <button
          type="button"
          className="absolute right-5 top-1/2 -translate-y-1/2 text-muted-foreground transition-colors hover:text-foreground"
          onClick={onToggle}
          aria-label={isVisible ? "Ocultar senha" : "Mostrar senha"}
        >
          {isVisible ? (
            <EyeOff className="size-5" aria-hidden="true" />
          ) : (
            <Eye className="size-5" aria-hidden="true" />
          )}
        </button>
      </div>
    </Field>
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

function splitFullName(fullName: string) {
  const segments = fullName.trim().split(/\s+/).filter(Boolean);
  const firstName = segments.shift() ?? "";
  const lastName = segments.join(" ");

  return {
    firstName,
    lastName: lastName || firstName,
  };
}
