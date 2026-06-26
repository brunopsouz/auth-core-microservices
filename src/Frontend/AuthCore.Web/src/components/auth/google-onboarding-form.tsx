"use client";

import { FormEvent, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { BriefcaseBusiness, CheckCircle2, Mail, ShieldCheck, UserRound } from "lucide-react";
import { toast } from "sonner";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
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
    <Card className="w-full max-w-[48rem] rounded-lg border-border/70 bg-card/55 px-6 py-7 shadow-2xl backdrop-blur-xl sm:px-10 lg:px-14 lg:py-10">
      <CardContent className="p-0">
        <div className="mx-auto max-w-[40rem]">
          <div className="mb-7 flex flex-col items-center gap-5 text-center">
            <div className="inline-flex items-center gap-2 rounded-lg border border-border/70 bg-background/45 px-4 py-3 text-base font-semibold">
              <ShieldCheck className="size-5 text-primary" aria-hidden="true" />
              AuthCore
            </div>
            <div className="space-y-3">
              <h1 className="text-3xl font-bold leading-tight text-foreground sm:text-4xl">
                Complete seu cadastro
              </h1>
              <p className="mx-auto max-w-md text-lg leading-7 text-muted-foreground">
                Confirme os dados finais para criar sua conta com Google.
              </p>
            </div>
          </div>

          <div className="mb-6 flex items-center gap-4 rounded-lg border border-border/70 bg-background/25 p-4">
            <Avatar className="size-12" size="lg">
              {onboarding?.pictureUrl ? (
                <AvatarImage src={onboarding.pictureUrl} alt="" />
              ) : null}
              <AvatarFallback className="bg-primary/10 text-primary">
                G
              </AvatarFallback>
            </Avatar>
            <div className="min-w-0">
              <div className="flex items-center gap-2 text-base font-semibold text-foreground">
                <CheckCircle2 className="size-4 text-primary" aria-hidden="true" />
                Google verificado
              </div>
              <p className="truncate text-sm text-muted-foreground">
                {onboarding?.email ?? "Carregando conta Google..."}
              </p>
            </div>
          </div>

          <form onSubmit={handleSubmit}>
            <FieldGroup className="gap-4">
              {error && (
                <Alert variant="destructive" className="border-destructive/40 bg-destructive/10">
                  <AlertTitle>Onboarding indisponivel</AlertTitle>
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}

              <Field className="gap-2">
                <FieldLabel htmlFor="firstName" className="text-base font-semibold text-foreground">
                  Nome
                </FieldLabel>
                <div className="relative">
                  <UserRound
                    className="pointer-events-none absolute left-5 top-1/2 size-5 -translate-y-1/2 text-muted-foreground"
                    aria-hidden="true"
                  />
                  <Input
                    id="firstName"
                    name="firstName"
                    autoComplete="given-name"
                    placeholder="Seu nome"
                    required
                    value={firstName}
                    onChange={(event) => setFirstName(event.target.value)}
                    disabled={isLoading || !onboarding}
                    className="h-12 rounded-lg border-border/80 bg-background/30 pl-14 text-lg text-foreground placeholder:text-muted-foreground/70 focus-visible:ring-primary/30"
                  />
                </div>
              </Field>

              <Field className="gap-2">
                <FieldLabel htmlFor="lastName" className="text-base font-semibold text-foreground">
                  Sobrenome
                </FieldLabel>
                <div className="relative">
                  <UserRound
                    className="pointer-events-none absolute left-5 top-1/2 size-5 -translate-y-1/2 text-muted-foreground"
                    aria-hidden="true"
                  />
                  <Input
                    id="lastName"
                    name="lastName"
                    autoComplete="family-name"
                    placeholder="Seu sobrenome"
                    required
                    value={lastName}
                    onChange={(event) => setLastName(event.target.value)}
                    disabled={isLoading || !onboarding}
                    className="h-12 rounded-lg border-border/80 bg-background/30 pl-14 text-lg text-foreground placeholder:text-muted-foreground/70 focus-visible:ring-primary/30"
                  />
                </div>
              </Field>

              <Field className="gap-2">
                <FieldLabel htmlFor="email" className="text-base font-semibold text-foreground">
                  E-mail Google
                </FieldLabel>
                <div className="relative">
                  <Mail
                    className="pointer-events-none absolute left-5 top-1/2 size-5 -translate-y-1/2 text-muted-foreground"
                    aria-hidden="true"
                  />
                  <Input
                    id="email"
                    name="email"
                    value={onboarding?.email ?? ""}
                    disabled
                    className="h-12 rounded-lg border-border/80 bg-background/30 pl-14 text-lg text-foreground disabled:opacity-80"
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
                    value={contact}
                    onChange={(event) => setContact(event.target.value)}
                    disabled={isLoading || !onboarding}
                    className="h-12 rounded-lg border-border/80 bg-background/30 pl-14 text-lg text-foreground placeholder:text-muted-foreground/70 focus-visible:ring-primary/30"
                  />
                </div>
              </Field>

              <Button
                className="mt-2 h-12 w-full rounded-lg bg-primary text-lg font-bold text-primary-foreground shadow-[0_0_24px_hsl(170_90%_48%/0.28)] hover:bg-primary/90"
                type="submit"
                disabled={isLoading || isPending || !onboarding}
              >
                {isPending ? "Concluindo..." : "Concluir cadastro"}
              </Button>
            </FieldGroup>
          </form>

          <p className="mt-5 text-center text-base text-muted-foreground">
            Prefere outro metodo?{" "}
            <Link className="font-semibold text-primary hover:text-primary/80" href="/register">
              Voltar ao cadastro
            </Link>
          </p>
        </div>
      </CardContent>
    </Card>
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
