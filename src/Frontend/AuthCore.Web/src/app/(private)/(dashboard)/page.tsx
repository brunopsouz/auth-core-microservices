import { KeyRound, ShieldCheck, UserRoundCheck } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";

const sessionItems = [
  {
    title: "Sessao por cookie",
    description: "Cookie HTTP-only emitido pelo AuthCore.",
    icon: ShieldCheck,
  },
  {
    title: "Acesso protegido",
    description: "Rotas privadas liberadas pelo proxy do Next.",
    icon: KeyRound,
  },
  {
    title: "Usuario ativo",
    description: "Dados reais devem vir de /api/auth/session/me.",
    icon: UserRoundCheck,
  },
];

export default function DashboardPage() {
  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-8 px-4 py-8 sm:px-6">
      <section className="flex flex-col gap-3">
        <Badge className="w-fit" variant="secondary">
          Dashboard
        </Badge>
        <div className="max-w-2xl space-y-2">
          <h1 className="text-3xl font-semibold tracking-normal text-foreground sm:text-4xl">
            Painel AuthCore
          </h1>
          <p className="text-base leading-7 text-muted-foreground">
            Esta area usa o cookie de sessao como sinal rapido de acesso.
          </p>
        </div>
      </section>

      <Separator />

      <section className="grid gap-4 md:grid-cols-3">
        {sessionItems.map((item) => (
          <Card key={item.title} className="rounded-lg">
            <CardHeader className="space-y-3">
              <div className="flex size-10 items-center justify-center rounded-md bg-accent text-accent-foreground">
                <item.icon className="size-5" aria-hidden="true" />
              </div>
              <CardTitle className="text-lg">{item.title}</CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-sm leading-6 text-muted-foreground">
                {item.description}
              </p>
            </CardContent>
          </Card>
        ))}
      </section>
    </div>
  );
}
