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
    <Card className="w-full max-w-[27rem] border-0 bg-transparent py-0 shadow-none ring-0">
      <CardContent className="p-0">
        <div className="mb-5 space-y-2">
          <div className="mb-5 inline-flex size-11 items-center justify-center rounded-xl border border-[#d9dee7] bg-[#f7f8fa] text-[#151922]">
            <ShieldCheck className="size-5" aria-hidden="true" />
          </div>
          <h1 className="text-3xl font-bold leading-tight text-[#111318]">
            Criar conta no AuthCore
          </h1>
          <p className="text-sm leading-6 text-[#68707d]">
            Comece com uma base segura e pronta para escalar sua aplicacao.
          </p>
        </div>

        <form onSubmit={handleSubmit}>
          <FieldGroup className="gap-3">
            {error && (
              <Alert variant="destructive" className="rounded-lg">
                <AlertTitle>Falha no registro</AlertTitle>
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}

            {registeredEmail && (
              <Alert className="rounded-lg border-[#cfd6e2] bg-[#f6f8fb] text-[#111318]">
                <AlertTitle>Registro recebido</AlertTitle>
                <AlertDescription>
                  Verificacao enviada para {registeredEmail}.
                </AlertDescription>
              </Alert>
            )}

            <Field className="gap-1.5">
              <FieldLabel htmlFor="fullName" className="text-sm font-semibold text-[#171a20]">
                Nome completo
              </FieldLabel>
              <div className="relative">
                <UserRound
                  className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-[#8a93a0]"
                  aria-hidden="true"
                />
                <Input
                  id="fullName"
                  name="fullName"
                  autoComplete="name"
                  placeholder="Seu nome completo"
                  required
                  className="h-10 rounded-lg border-[#d8dee8] bg-white pl-11 text-sm text-[#111318] placeholder:text-[#9aa3af] focus-visible:ring-[#9aa3b2]/35"
                />
              </div>
            </Field>

            <Field className="gap-1.5">
              <FieldLabel htmlFor="email" className="text-sm font-semibold text-[#171a20]">
                E-mail profissional
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
                  className="h-10 rounded-lg border-[#d8dee8] bg-white pl-11 text-sm text-[#111318] placeholder:text-[#9aa3af] focus-visible:ring-[#9aa3b2]/35"
                />
              </div>
            </Field>

            <Field className="gap-1.5">
              <FieldLabel htmlFor="contact" className="text-sm font-semibold text-[#171a20]">
                Empresa ou projeto
              </FieldLabel>
              <div className="relative">
                <BriefcaseBusiness
                  className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-[#8a93a0]"
                  aria-hidden="true"
                />
                <Input
                  id="contact"
                  name="contact"
                  autoComplete="organization"
                  placeholder="Nome da sua empresa ou projeto"
                  required
                  className="h-10 rounded-lg border-[#d8dee8] bg-white pl-11 text-sm text-[#111318] placeholder:text-[#9aa3af] focus-visible:ring-[#9aa3b2]/35"
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

            <label className="flex items-start gap-2 text-xs leading-5 text-[#68707d]">
              <Checkbox name="terms" required className="mt-0.5 size-4 rounded-[4px] border-[#d8dee8]" />
              <span>
                Li e concordo com os{" "}
                <Link href="/register" className="font-semibold text-[#111318] hover:text-[#3b4350]">
                  Termos de uso
                </Link>{" "}
                e{" "}
                <Link href="/register" className="font-semibold text-[#111318] hover:text-[#3b4350]">
                  Politica de privacidade
                </Link>
              </span>
            </label>

            <Button
              className="h-10 w-full rounded-lg bg-[#111722] text-sm font-semibold text-white shadow-[0_10px_24px_rgba(17,23,34,0.16)] hover:bg-[#1a2230]"
              type="submit"
              disabled={isPending}
            >
              {isPending ? "Criando..." : "Criar conta"}
            </Button>
          </FieldGroup>
        </form>

        <div className="my-4 flex items-center gap-4 text-xs font-medium text-[#8a93a0]">
          <span className="h-px flex-1 bg-[#dfe4ec]" />
          <span>ou continue com</span>
          <span className="h-px flex-1 bg-[#dfe4ec]" />
        </div>

        <Button
          type="button"
          variant="outline"
          className="h-10 w-full rounded-lg border-[#d8dee8] bg-white text-sm font-semibold text-[#111318] shadow-none hover:bg-[#f6f7f9]"
          onClick={handleGoogleRegister}
        >
          <GoogleMark />
          Cadastrar com Google
        </Button>

        <p className="mt-4 text-center text-sm text-[#68707d]">
          Ja tem conta?{" "}
          <Link className="font-semibold text-[#111318] hover:text-[#3b4350]" href="/sign-in">
            Entrar
          </Link>
        </p>
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
    <Field className="gap-1.5">
      <FieldLabel htmlFor={id} className="text-sm font-semibold text-[#171a20]">
        {label}
      </FieldLabel>
      <div className="relative">
        <LockKeyhole
          className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-[#8a93a0]"
          aria-hidden="true"
        />
        <Input
          id={id}
          name={id}
          type={isVisible ? "text" : "password"}
          autoComplete={autoComplete}
          placeholder="**********"
          required
          className="h-10 rounded-lg border-[#d8dee8] bg-white pl-11 pr-11 text-sm text-[#111318] placeholder:text-[#9aa3af] focus-visible:ring-[#9aa3b2]/35"
        />
        <button
          type="button"
          className="absolute right-4 top-1/2 -translate-y-1/2 text-[#8a93a0] transition-colors hover:text-[#111318]"
          onClick={onToggle}
          aria-label={isVisible ? "Ocultar senha" : "Mostrar senha"}
        >
          {isVisible ? (
            <EyeOff className="size-4" aria-hidden="true" />
          ) : (
            <Eye className="size-4" aria-hidden="true" />
          )}
        </button>
      </div>
    </Field>
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

function splitFullName(fullName: string) {
  const segments = fullName.trim().split(/\s+/).filter(Boolean);
  const firstName = segments.shift() ?? "";
  const lastName = segments.join(" ");

  return {
    firstName,
    lastName: lastName || firstName,
  };
}
