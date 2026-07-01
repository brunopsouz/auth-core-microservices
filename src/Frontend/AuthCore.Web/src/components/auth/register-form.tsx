"use client";

import Link from "next/link";
import { FormEvent, useState } from "react";
import { toast } from "sonner";
import {
  Eye,
  EyeOff,
  KeyRound,
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
import {
  InputOTP,
  InputOTPGroup,
  InputOTPSlot,
} from "@/components/ui/input-otp";
import { PhoneNumberField } from "@/components/auth/phone-number-field";
import { startGoogleAuthentication } from "@/features/auth/api/google-auth";
import { readApiError } from "@/lib/http-errors";
import { isValidPhoneNumber } from "react-phone-number-input";

type RegisterStep = "account" | "code" | "password" | "complete";

type PendingRegistration = {
  email: string;
  firstName: string;
  lastName: string;
  contact: string;
};

export function RegisterForm() {
  const [step, setStep] = useState<RegisterStep>("account");
  const [error, setError] = useState<string | null>(null);
  const [pendingRegistration, setPendingRegistration] =
    useState<PendingRegistration | null>(null);
  const [resendEmail, setResendEmail] = useState<string | null>(null);
  const [verificationCode, setVerificationCode] = useState("");
  const [isPending, setIsPending] = useState(false);
  const [isResending, setIsResending] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [contact, setContact] = useState("");

  async function handleAccountSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const form = event.currentTarget;
    const formData = new FormData(form);
    const email = String(formData.get("email") ?? "").trim();
    const fullName = String(formData.get("fullName") ?? "");
    const { firstName, lastName } = splitFullName(fullName);
    const registration = {
      email,
      firstName,
      lastName,
      contact,
    };

    setError(null);

    if (!isValidPhoneNumber(registration.contact)) {
      setError("Informe um telefone válido com DDD.");
      return;
    }

    setResendEmail(null);
    setIsPending(true);

    try {
      const response = await fetch("/api/auth/register", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          FirstName: registration.firstName,
          LastName: registration.lastName,
          Email: registration.email,
          Contact: registration.contact,
        }),
      });

      if (!response.ok) {
        setResendEmail(registration.email);
        setError(await readApiError(response));
        return;
      }

      setPendingRegistration(registration);
      setResendEmail(null);
      setVerificationCode("");
      setStep("code");
      toast.success("Código enviado por e-mail.");
    } finally {
      setIsPending(false);
    }
  }

  function handleCodeSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    if (verificationCode.length !== 6) {
      setError("Informe o código de 6 dígitos enviado por e-mail.");
      return;
    }

    setStep("password");
  }

  async function handlePasswordSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!pendingRegistration) {
      setStep("account");
      return;
    }

    const form = event.currentTarget;
    const formData = new FormData(form);

    setError(null);
    setIsPending(true);

    try {
      const response = await fetch("/api/auth/complete-registration", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          Email: pendingRegistration.email,
          Code: verificationCode,
          Password: String(formData.get("password") ?? ""),
          ConfirmPassword: String(formData.get("confirmPassword") ?? ""),
        }),
      });

      if (!response.ok) {
        setError(await readApiError(response));
        return;
      }

      form.reset();
      setStep("complete");
      toast.success("Conta verificada.");
    } finally {
      setIsPending(false);
    }
  }

  async function handleResendVerification(email?: string) {
    const targetEmail = email ?? pendingRegistration?.email;

    if (!targetEmail) {
      return;
    }

    setError(null);
    setIsResending(true);

    try {
      const response = await fetch("/api/auth/resend-verification", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          Email: targetEmail,
        }),
      });

      if (!response.ok) {
        setError(await readApiError(response));
        return;
      }

      setPendingRegistration({
        email: targetEmail,
        firstName: "",
        lastName: "",
        contact: "",
      });
      setResendEmail(null);
      setVerificationCode("");
      setStep("code");
      toast.success("Código reenviado.");
    } finally {
      setIsResending(false);
    }
  }

  function handleGoogleRegister() {
    startGoogleAuthentication();
  }

  return (
    <Card className="w-full max-w-[27rem] border-0 bg-transparent py-0 shadow-none ring-0">
      <CardContent className="p-0">
        <RegisterHeader step={step} email={pendingRegistration?.email} />

        {error && (
          <Alert variant="destructive" className="mb-3 rounded-lg">
            <AlertTitle>Falha no registro</AlertTitle>
            <AlertDescription className="space-y-3">
              <span className="block">{error}</span>
              {step === "account" && resendEmail && (
                <Button
                  type="button"
                  variant="outline"
                  className="h-9 rounded-lg border-red-200 bg-white px-3 text-xs font-semibold text-red-700 shadow-none hover:bg-red-50 hover:text-red-800"
                  disabled={isResending || isPending}
                  onClick={() => handleResendVerification(resendEmail)}
                >
                  {isResending ? "Reenviando..." : "Reenviar código OTP"}
                </Button>
              )}
            </AlertDescription>
          </Alert>
        )}

        {step === "account" && (
          <AccountStep
            contact={contact}
            isPending={isPending}
            onContactChange={setContact}
            onSubmit={handleAccountSubmit}
          />
        )}

        {step === "code" && pendingRegistration && (
          <CodeStep
            code={verificationCode}
            email={pendingRegistration.email}
            isPending={isPending}
            isResending={isResending}
            onBack={() => {
              setError(null);
              setStep("account");
            }}
            onChange={setVerificationCode}
            onResend={handleResendVerification}
            onSubmit={handleCodeSubmit}
          />
        )}

        {step === "password" && pendingRegistration && (
          <PasswordStep
            isPending={isPending}
            showConfirmPassword={showConfirmPassword}
            showPassword={showPassword}
            onBack={() => {
              setError(null);
              setStep("code");
            }}
            onSubmit={handlePasswordSubmit}
            onToggleConfirmPassword={() =>
              setShowConfirmPassword((current) => !current)
            }
            onTogglePassword={() => setShowPassword((current) => !current)}
          />
        )}

        {step === "complete" && <CompleteStep />}

        {step === "account" && (
          <>
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
          </>
        )}

        <p className="mt-4 text-center text-sm text-[#68707d]">
          Já tem conta?{" "}
          <Link className="font-semibold text-[#111318] hover:text-[#3b4350]" href="/sign-in">
            Entrar
          </Link>
        </p>
      </CardContent>
    </Card>
  );
}

function RegisterHeader({
  step,
  email,
}: {
  step: RegisterStep;
  email?: string;
}) {
  const titleByStep: Record<RegisterStep, string> = {
    account: "Criar conta no AuthCore",
    code: "Confirme seu e-mail",
    password: "Defina sua senha",
    complete: "Conta verificada",
  };
  const descriptionByStep: Record<RegisterStep, string> = {
    account: "Informe seus dados para receber o código OTP por e-mail.",
    code: `Digite o código enviado para ${email ?? "seu e-mail"}.`,
    password: "Use uma senha forte para concluir seu cadastro.",
    complete: "Agora você já pode entrar com seu e-mail e senha.",
  };

  return (
    <div className="mb-5 space-y-2">
      <div className="mb-5 inline-flex size-11 items-center justify-center rounded-xl border border-[#d9dee7] bg-[#f7f8fa] text-[#151922]">
        <ShieldCheck className="size-5" aria-hidden="true" />
      </div>
      <h1 className="text-3xl font-bold leading-tight text-[#111318]">
        {titleByStep[step]}
      </h1>
      <p className="text-sm leading-6 text-[#68707d]">
        {descriptionByStep[step]}
      </p>
    </div>
  );
}

function AccountStep({
  contact,
  isPending,
  onContactChange,
  onSubmit,
}: {
  contact: string;
  isPending: boolean;
  onContactChange: (value: string) => void;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
}) {
  return (
    <form onSubmit={onSubmit}>
      <FieldGroup className="gap-3">
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

        <PhoneNumberField
          id="contact"
          label="Telefone"
          value={contact}
          onChange={onContactChange}
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
              Política de privacidade
            </Link>
          </span>
        </label>

        <Button
          className="h-10 w-full rounded-lg bg-[#111722] text-sm font-semibold text-white shadow-[0_10px_24px_rgba(17,23,34,0.16)] hover:bg-[#1a2230]"
          type="submit"
          disabled={isPending}
        >
          {isPending ? "Enviando..." : "Enviar código"}
        </Button>
      </FieldGroup>
    </form>
  );
}

function CodeStep({
  code,
  email,
  isPending,
  isResending,
  onBack,
  onChange,
  onResend,
  onSubmit,
}: {
  code: string;
  email: string;
  isPending: boolean;
  isResending: boolean;
  onBack: () => void;
  onChange: (code: string) => void;
  onResend: () => void;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
}) {
  return (
    <form onSubmit={onSubmit}>
      <FieldGroup className="gap-3">
        <Field className="gap-1.5">
          <FieldLabel htmlFor="verificationCode" className="text-sm font-semibold text-[#171a20]">
            Código OTP
          </FieldLabel>
          <InputOTP
            id="verificationCode"
            maxLength={6}
            value={code}
            onChange={onChange}
            containerClassName="w-full justify-between"
          >
            <InputOTPGroup className="w-full justify-between gap-2">
              {Array.from({ length: 6 }).map((_, index) => (
                <InputOTPSlot
                  key={index}
                  index={index}
                  className="size-11 rounded-lg border border-[#d8dee8] bg-white text-base font-semibold text-[#111318]"
                />
              ))}
            </InputOTPGroup>
          </InputOTP>
        </Field>

        <div className="flex items-center justify-between gap-3 text-xs text-[#68707d]">
          <span className="min-w-0 truncate">{email}</span>
          <button
            type="button"
            className="shrink-0 font-semibold text-[#111318] hover:text-[#3b4350]"
            aria-label="Reenviar código OTP"
            disabled={isResending || isPending}
            onClick={onResend}
          >
            {isResending ? "Reenviando..." : "Reenviar código"}
          </button>
        </div>

        <Button
          className="h-10 w-full rounded-lg bg-[#111722] text-sm font-semibold text-white shadow-[0_10px_24px_rgba(17,23,34,0.16)] hover:bg-[#1a2230]"
          type="submit"
          disabled={isPending}
        >
          Continuar
        </Button>
        <Button
          type="button"
          variant="outline"
          className="h-10 w-full rounded-lg border-[#d8dee8] bg-white text-sm font-semibold text-[#111318] shadow-none hover:bg-[#f6f7f9]"
          onClick={onBack}
        >
          Corrigir dados
        </Button>
      </FieldGroup>
    </form>
  );
}

function PasswordStep({
  isPending,
  showConfirmPassword,
  showPassword,
  onBack,
  onSubmit,
  onToggleConfirmPassword,
  onTogglePassword,
}: {
  isPending: boolean;
  showConfirmPassword: boolean;
  showPassword: boolean;
  onBack: () => void;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onToggleConfirmPassword: () => void;
  onTogglePassword: () => void;
}) {
  return (
    <form onSubmit={onSubmit}>
      <FieldGroup className="gap-3">
        <div className="flex items-center gap-2 rounded-lg border border-[#d8dee8] bg-[#f7f8fa] px-3 py-2 text-xs leading-5 text-[#68707d]">
          <KeyRound className="size-4 shrink-0 text-[#111318]" aria-hidden="true" />
          <span>O código será validado junto com a senha.</span>
        </div>

        <PasswordField
          id="password"
          label="Senha"
          autoComplete="new-password"
          isVisible={showPassword}
          onToggle={onTogglePassword}
        />

        <PasswordField
          id="confirmPassword"
          label="Confirmar senha"
          autoComplete="new-password"
          isVisible={showConfirmPassword}
          onToggle={onToggleConfirmPassword}
        />

        <Button
          className="h-10 w-full rounded-lg bg-[#111722] text-sm font-semibold text-white shadow-[0_10px_24px_rgba(17,23,34,0.16)] hover:bg-[#1a2230]"
          type="submit"
          disabled={isPending}
        >
          {isPending ? "Concluindo..." : "Concluir cadastro"}
        </Button>
        <Button
          type="button"
          variant="outline"
          className="h-10 w-full rounded-lg border-[#d8dee8] bg-white text-sm font-semibold text-[#111318] shadow-none hover:bg-[#f6f7f9]"
          onClick={onBack}
        >
          Voltar ao código
        </Button>
      </FieldGroup>
    </form>
  );
}

function CompleteStep() {
  return (
    <Link
      href="/sign-in"
      className="inline-flex h-10 w-full items-center justify-center rounded-lg bg-[#111722] text-sm font-semibold text-white shadow-[0_10px_24px_rgba(17,23,34,0.16)] hover:bg-[#1a2230]"
    >
      Entrar agora
    </Link>
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
