"use client";

import type { LucideIcon } from "lucide-react";
import { usePathname } from "next/navigation";
import {
  Code2,
  LockKeyhole,
  ShieldCheck,
  ShieldPlus,
  UsersRound,
  Zap,
} from "lucide-react";
import type { ReactNode } from "react";

type AuthShellProps = {
  children: ReactNode;
};

const featureSets = {
  signIn: {
    eyebrow: "Autenticacao moderna e segura",
    title: (
      <>
        Autenticacao segura
        <br />
        para aplicacoes
        <br />
        modernas
      </>
    ),
    description:
      "Login, sessoes protegidas, controle de acesso e protecao de contas prontos para o seu projeto desde o primeiro dia.",
    features: [
      {
        icon: ShieldCheck,
        title: "Autenticacao segura",
        description:
          "Protecao avancada de contas com senhas fortes, hashing moderno e verificacao opcional em duas etapas.",
      },
      {
        icon: UsersRound,
        title: "Sessoes protegidas",
        description:
          "Gerenciamento de sessoes com expiracao inteligente, revogacao e protecao contra acessos indevidos.",
      },
      {
        icon: Code2,
        title: "Pronto para producao",
        description:
          "Boilerplate completo, escalavel e bem estruturado para acelerar seu desenvolvimento com seguranca.",
      },
    ],
    footer: "Seguro por padrao - Privacidade respeitada",
  },
  register: {
    eyebrow: "Autenticacao segura e escalavel",
    title: (
      <>
        Crie sua conta e
        <br />
        comece com
        <br />
        <span className="text-primary">autenticacao segura</span>
      </>
    ),
    description:
      "Gerencie usuarios, autenticacoes e permissoes com uma base solida, pronta para proteger seu produto desde o primeiro dia.",
    features: [
      {
        icon: Zap,
        title: "Setup rapido",
        description:
          "Integre autenticacao completa em minutos e foque no que importa.",
      },
      {
        icon: UsersRound,
        title: "Controle de acesso",
        description:
          "Permissoes granulares, papeis e politicas para proteger dados e recursos.",
      },
      {
        icon: ShieldPlus,
        title: "Base pronta para producao",
        description:
          "Boas praticas, seguranca e escalabilidade para aplicacoes modernas.",
      },
    ],
    footer: "Seguro por padrao - Privacidade em primeiro lugar - Escalavel",
  },
  onboarding: {
    eyebrow: "Cadastro Google com onboarding seguro",
    title: (
      <>
        Complete sua conta
        <br />
        com dados
        <br />
        <span className="text-primary">verificados pelo Google</span>
      </>
    ),
    description:
      "O backend valida sua conta Google, protege o ticket temporario e solicita apenas os dados finais do cadastro.",
    features: [
      {
        icon: ShieldCheck,
        title: "Google verificado",
        description:
          "O e-mail confirmado pelo Google e usado pelo AuthCore sem expor tokens ao navegador.",
      },
      {
        icon: UsersRound,
        title: "Cadastro completo",
        description:
          "Nome, sobrenome e contato completam o perfil necessario para criar a conta.",
      },
      {
        icon: Zap,
        title: "Sessao imediata",
        description:
          "Depois do onboarding, a sessao segura e emitida por cookies HttpOnly.",
      },
    ],
    footer: "Google validado - Ticket temporario - Sessao segura",
  },
} as const;

export function AuthShell({ children }: AuthShellProps) {
  const pathname = usePathname();
  const mode = pathname === "/register"
    ? "register"
    : pathname === "/onboarding"
      ? "onboarding"
      : "signIn";
  const content = featureSets[mode];

  return (
    <main className="dark min-h-screen overflow-hidden bg-background text-foreground">
      <div className="auth-grid relative isolate min-h-screen">
        <div className="absolute inset-0 -z-10 bg-[radial-gradient(circle_at_42%_50%,hsl(174_92%_38%/0.18),transparent_28%),radial-gradient(circle_at_86%_14%,hsl(196_80%_46%/0.1),transparent_22%),linear-gradient(115deg,hsl(180_60%_4%),hsl(210_42%_5%)_48%,hsl(190_60%_4%))]" />
        <div className="absolute inset-y-0 left-[28%] -z-10 hidden w-[38rem] rounded-full border border-primary/10 opacity-70 lg:block" />
        <div className="absolute left-[32%] top-[16%] -z-10 hidden size-[32rem] rounded-full border border-primary/10 opacity-50 lg:block" />
        <div className="absolute left-[36%] top-[24%] -z-10 hidden size-[20rem] rounded-full border border-primary/10 opacity-40 lg:block" />

        <div className="mx-auto grid min-h-screen w-full max-w-[112rem] gap-10 px-6 py-8 sm:px-10 lg:grid-cols-[minmax(28rem,38rem)_minmax(34rem,52rem)] lg:items-center lg:justify-between lg:px-16">
          <section className="flex min-h-[calc(100vh-4rem)] flex-col justify-between gap-10 py-4">
            <div className="space-y-14">
              <BrandMark split={mode === "signIn"} />

              <div className="space-y-8">
                <div className="inline-flex items-center gap-3 rounded-lg border border-border/70 bg-card/45 px-4 py-3 text-base text-muted-foreground shadow-lg backdrop-blur">
                  <span className="size-3 rounded-full bg-primary shadow-[0_0_18px_hsl(170_90%_48%/0.9)]" />
                  {content.eyebrow}
                </div>

                <div className="max-w-[36rem] space-y-6">
                  <h1 className="text-4xl font-bold leading-[1.18] text-foreground sm:text-5xl xl:text-6xl">
                    {content.title}
                  </h1>
                  <p className="max-w-[34rem] text-lg leading-8 text-muted-foreground sm:text-xl">
                    {content.description}
                  </p>
                </div>

                <div className="grid max-w-[36rem] gap-3">
                  {content.features.map((feature) => (
                    <FeatureCard key={feature.title} {...feature} />
                  ))}
                </div>
              </div>
            </div>

            <div className="flex items-center gap-3 text-sm text-muted-foreground sm:text-base">
              <LockKeyhole className="size-5 text-primary" aria-hidden="true" />
              <span>{content.footer}</span>
            </div>
          </section>

          <section className="flex items-center justify-center lg:justify-end">
            {children}
          </section>
        </div>
      </div>
    </main>
  );
}

function BrandMark({ split }: { split: boolean }) {
  return (
    <div className="flex items-center gap-4">
      <div className="relative flex size-12 items-center justify-center text-primary">
        <ShieldCheck className="size-12" strokeWidth={1.8} aria-hidden="true" />
        <LockKeyhole
          className="absolute size-4"
          strokeWidth={2.2}
          aria-hidden="true"
        />
      </div>
      <span className="text-3xl font-bold text-foreground sm:text-4xl">
        Auth{split ? <span className="text-primary">Core</span> : "Core"}
      </span>
    </div>
  );
}

function FeatureCard({
  icon: Icon,
  title,
  description,
}: {
  icon: LucideIcon;
  title: string;
  description: string;
}) {
  return (
    <article className="group relative flex gap-5 rounded-lg border border-border/60 bg-card/45 p-5 shadow-xl backdrop-blur transition-colors hover:border-primary/45">
      <div className="relative flex size-16 shrink-0 items-center justify-center rounded-lg border border-primary/35 bg-primary/10 text-primary shadow-[inset_0_0_24px_hsl(170_80%_45%/0.12)]">
        <Icon className="size-8" strokeWidth={1.8} aria-hidden="true" />
        <span className="absolute -right-1 -top-1 size-4 rounded-full bg-primary shadow-[0_0_16px_hsl(170_90%_48%/0.9)]" />
      </div>
      <div className="space-y-1">
        <h2 className="text-lg font-semibold text-foreground">{title}</h2>
        <p className="text-base leading-6 text-muted-foreground">{description}</p>
      </div>
    </article>
  );
}
