"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { FormEvent, useEffect, useState } from "react";
import type { LucideIcon } from "lucide-react";
import {
  CheckCircle2,
  Mail,
  ShieldCheck,
  UserRound,
} from "lucide-react";
import { toast } from "sonner";
import { isValidPhoneNumber } from "react-phone-number-input";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { PhoneNumberField } from "@/components/auth/phone-number-field";
import {
  completeGoogleOnboarding,
  getGoogleOnboarding,
  type GoogleOnboarding,
} from "@/features/auth/api/google-auth";

export function GoogleOnboardingForm() {
  const router = useRouter();
  const [onboarding, setOnboarding] = useState<GoogleOnboarding | null>(null);
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [contact, setContact] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isPending, setIsPending] = useState(false);

  useEffect(() => {
    let isMounted = true;

    async function loadOnboarding() {
      setError(null);

      try {
        const data = await getGoogleOnboarding();
        const parsedName = splitFullName(data.fullName);

        if (!isMounted) {
          return;
        }

        setOnboarding(data);
        setFirstName(parsedName.firstName);
        setLastName(parsedName.lastName);
      } catch (currentError) {
        if (!isMounted) {
          return;
        }

        setError(currentError instanceof Error ? currentError.message : "Onboarding indisponivel.");
      } finally {
        if (isMounted) {
          setIsLoading(false);
        }
      }
    }

    void loadOnboarding();

    return () => {
      isMounted = false;
    };
  }, []);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    setError(null);

    if (!isValidPhoneNumber(contact)) {
      setError("Informe um telefone válido com DDD.");
      return;
    }

    setIsPending(true);

    try {
      await completeGoogleOnboarding({
        firstName,
        lastName,
        contact,
      });

      toast.success("Conta criada com Google.");
      router.replace("/");
      router.refresh();
    } catch (currentError) {
      setError(currentError instanceof Error ? currentError.message : "Nao foi possivel concluir o cadastro.");
    } finally {
      setIsPending(false);
    }
  }

  return (
    <Card className="w-full max-w-[27rem] border-0 bg-transparent py-0 shadow-none ring-0">
      <CardContent className="p-0">
        <div className="mb-5 space-y-2">
          <div className="mb-5 inline-flex size-11 items-center justify-center rounded-xl border border-[#d9dee7] bg-[#f7f8fa] text-[#151922]">
            <ShieldCheck className="size-5" aria-hidden="true" />
          </div>
          <h1 className="text-3xl font-bold leading-tight text-[#111318]">
            Complete seu cadastro
          </h1>
          <p className="text-sm leading-6 text-[#68707d]">
            Confirme os dados finais para criar sua conta com Google.
          </p>
        </div>

        <div className="mb-4 flex items-center gap-3 rounded-lg border border-[#d8dee8] bg-[#f7f8fa] p-3">
          <Avatar className="size-10" size="lg">
            {onboarding?.pictureUrl ? (
              <AvatarImage src={onboarding.pictureUrl} alt="" />
            ) : null}
            <AvatarFallback className="bg-white text-sm font-semibold text-[#111318]">
              G
            </AvatarFallback>
          </Avatar>
          <div className="min-w-0">
            <div className="flex items-center gap-1.5 text-sm font-semibold text-[#111318]">
              <CheckCircle2 className="size-4 text-[#3b4350]" aria-hidden="true" />
              Google verificado
            </div>
            <p className="truncate text-xs text-[#68707d]">
              {onboarding?.email ?? "Carregando conta Google..."}
            </p>
          </div>
        </div>

        <form onSubmit={handleSubmit}>
          <FieldGroup className="gap-3">
            {error && (
              <Alert variant="destructive" className="rounded-lg">
                <AlertTitle>Onboarding indisponivel</AlertTitle>
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}

            <ControlledField
              id="firstName"
              name="firstName"
              label="Nome"
              autoComplete="given-name"
              placeholder="Seu nome"
              value={firstName}
              onChange={setFirstName}
              disabled={isLoading || !onboarding}
              icon={UserRound}
            />

            <ControlledField
              id="lastName"
              name="lastName"
              label="Sobrenome"
              autoComplete="family-name"
              placeholder="Seu sobrenome"
              value={lastName}
              onChange={setLastName}
              disabled={isLoading || !onboarding}
              icon={UserRound}
            />

            <ControlledField
              id="email"
              name="email"
              label="E-mail Google"
              autoComplete="email"
              placeholder="seu@email.com"
              value={onboarding?.email ?? ""}
              onChange={() => undefined}
              disabled
              icon={Mail}
            />

            <PhoneNumberField
              id="contact"
              label="Telefone"
              value={contact}
              onChange={setContact}
              disabled={isLoading || !onboarding}
            />

            <Button
              className="h-10 w-full rounded-lg bg-[#111722] text-sm font-semibold text-white shadow-[0_10px_24px_rgba(17,23,34,0.16)] hover:bg-[#1a2230]"
              type="submit"
              disabled={isLoading || isPending || !onboarding}
            >
              {isPending ? "Concluindo..." : "Concluir cadastro"}
            </Button>
          </FieldGroup>
        </form>

        <p className="mt-4 text-center text-sm text-[#68707d]">
          Prefere outro metodo?{" "}
          <Link className="font-semibold text-[#111318] hover:text-[#3b4350]" href="/register">
            Voltar ao cadastro
          </Link>
        </p>
      </CardContent>
    </Card>
  );
}

function ControlledField({
  id,
  name,
  label,
  autoComplete,
  placeholder,
  value,
  onChange,
  disabled,
  icon: Icon,
}: {
  id: string;
  name: string;
  label: string;
  autoComplete: string;
  placeholder: string;
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  icon: LucideIcon;
}) {
  return (
    <Field className="gap-1.5">
      <FieldLabel htmlFor={id} className="text-sm font-semibold text-[#171a20]">
        {label}
      </FieldLabel>
      <div className="relative">
        <Icon
          className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-[#8a93a0]"
          aria-hidden="true"
        />
        <Input
          id={id}
          name={name}
          autoComplete={autoComplete}
          placeholder={placeholder}
          required
          value={value}
          onChange={(event) => onChange(event.target.value)}
          disabled={disabled}
          className="h-10 rounded-lg border-[#d8dee8] bg-white pl-11 text-sm text-[#111318] placeholder:text-[#9aa3af] focus-visible:ring-[#9aa3b2]/35 disabled:bg-[#f6f7f9] disabled:text-[#68707d] disabled:opacity-100"
        />
      </div>
    </Field>
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
