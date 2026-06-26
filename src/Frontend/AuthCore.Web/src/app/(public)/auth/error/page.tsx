import Link from "next/link";
import { AlertTriangle, ShieldCheck } from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

type AuthErrorPageProps = {
  searchParams: Promise<{
    reason?: string;
  }>;
};

const errorMessages: Record<string, string> = {
  external_callback_failed:
    "Nao foi possivel concluir o retorno do Google. Inicie o login novamente.",
};

export default async function AuthErrorPage({ searchParams }: AuthErrorPageProps) {
  const { reason } = await searchParams;
  const message = reason
    ? errorMessages[reason] ?? "Nao foi possivel concluir a autenticacao externa."
    : "Nao foi possivel concluir a autenticacao externa.";

  return (
    <Card className="w-full max-w-[42rem] rounded-lg border-border/70 bg-card/55 px-6 py-8 shadow-2xl backdrop-blur-xl sm:px-10 lg:px-14">
      <CardContent className="p-0">
        <div className="mb-7 flex flex-col items-center gap-5 text-center">
          <div className="inline-flex items-center gap-2 rounded-lg border border-border/70 bg-background/45 px-4 py-3 text-base font-semibold">
            <ShieldCheck className="size-5 text-primary" aria-hidden="true" />
            AuthCore
          </div>
          <div className="space-y-3">
            <h1 className="text-3xl font-bold leading-tight text-foreground">
              Falha na autenticacao
            </h1>
            <p className="mx-auto max-w-md text-lg leading-7 text-muted-foreground">
              O fluxo externo nao foi concluido.
            </p>
          </div>
        </div>

        <Alert variant="destructive" className="border-destructive/40 bg-destructive/10">
          <AlertTriangle className="size-4" aria-hidden="true" />
          <AlertTitle>Google indisponivel</AlertTitle>
          <AlertDescription>{message}</AlertDescription>
        </Alert>

        <Link
          href="/sign-in"
          className={buttonVariants({
            className:
              "mt-6 h-12 w-full rounded-lg bg-primary text-lg font-bold text-primary-foreground hover:bg-primary/90",
          })}
        >
          Voltar ao login
        </Link>
      </CardContent>
    </Card>
  );
}
