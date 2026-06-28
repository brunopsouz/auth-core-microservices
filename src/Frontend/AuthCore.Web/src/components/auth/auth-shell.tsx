"use client";

import { LockKeyhole, ShieldCheck } from "lucide-react";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";

type AuthShellProps = {
  children: ReactNode;
};

const shellContent = {
  signIn: {
    eyebrow: "Autenticacao moderna e segura",
    title: "Autenticacao segura para aplicacoes modernas",
    description:
      "Login, sessoes protegidas, controle de acesso e protecao de contas prontos para o seu projeto desde o primeiro dia.",
  },
  register: {
    eyebrow: "Autenticacao segura e escalavel",
    title: "Crie sua conta e comece com autenticacao segura",
    description:
      "Gerencie usuarios, autenticacoes e permissoes com uma base solida, pronta para proteger seu produto desde o primeiro dia.",
  },
  onboarding: {
    eyebrow: "Cadastro Google com onboarding seguro",
    title: "Complete sua conta com dados verificados pelo Google",
    description:
      "O backend valida sua conta Google, protege o ticket temporario e solicita apenas os dados finais do cadastro.",
  },
} as const;

export function AuthShell({ children }: AuthShellProps) {
  const pathname = usePathname();
  const mode = pathname === "/register"
    ? "register"
    : pathname === "/onboarding"
      ? "onboarding"
      : "signIn";
  const content = shellContent[mode];

  return (
    <main className="flex min-h-svh items-center justify-center overflow-x-hidden overflow-y-auto bg-[#eef1f5] px-3 py-6 text-[#101318] sm:px-4 sm:py-8 lg:py-4">
      <div className="grid w-full max-w-[70rem] overflow-hidden rounded-[1.75rem] border border-[#d8dee8] bg-[#fbfcfd] shadow-[0_22px_70px_rgba(15,23,42,0.12)] lg:min-h-[min(44rem,calc(100svh-2rem))] lg:grid-cols-[minmax(22rem,0.9fr)_minmax(27rem,1fr)]">
        <section className="relative hidden min-h-0 overflow-hidden bg-[#111317] text-white lg:flex lg:flex-col">
          <div className="absolute inset-0 bg-[linear-gradient(180deg,rgba(255,255,255,0.18),rgba(255,255,255,0.04)_18%,rgba(0,0,0,0.72)),radial-gradient(circle_at_80%_18%,rgba(255,255,255,0.16),transparent_25%)]" />
          <div className="absolute inset-x-[-16%] bottom-[-8%] h-[58%] rotate-[-5deg] rounded-[50%] bg-[radial-gradient(ellipse_at_center,rgba(255,255,255,0.14),transparent_58%),linear-gradient(180deg,#37393d,#090a0c)]" />
          <div className="absolute inset-x-[-18%] bottom-[12%] h-[38%] rotate-[4deg] rounded-[50%] bg-[linear-gradient(180deg,#202226,#07080a)] opacity-95" />
          <div className="absolute inset-x-[-18%] bottom-[30%] h-[28%] rotate-[-7deg] rounded-[50%] bg-[linear-gradient(180deg,#47494d,#101114)] opacity-70" />

          <div className="relative z-10 p-7 pb-0">
            <BrandMark />
          </div>

          <div className="relative z-10 flex flex-1 flex-col justify-between px-7 pb-7 pt-14">
            <div className="max-w-[25rem] space-y-4">
              <div className="inline-flex items-center gap-2 rounded-md border border-white/12 bg-white/8 px-3 py-2 text-sm text-white/72 backdrop-blur">
                <span className="size-2 rounded-full bg-[#9aa3b2]" />
                {content.eyebrow}
              </div>
              <div className="space-y-3">
                <h1 className="max-w-[23rem] text-[2rem] font-bold leading-[1.08] text-white">
                  {content.title}
                </h1>
                <p className="text-sm leading-6 text-white/68">{content.description}</p>
              </div>
            </div>
            <div className="flex items-center gap-2 text-xs text-white/58">
              <LockKeyhole className="size-4 text-[#c4cad3]" aria-hidden="true" />
              <span>Seguro por padrao - Privacidade respeitada</span>
            </div>
          </div>
        </section>

        <section className="flex min-h-0 items-center justify-center px-5 py-6 sm:px-8 lg:px-8 lg:py-8">
          {children}
        </section>
      </div>
    </main>
  );
}

function BrandMark() {
  return (
    <div className="flex items-center gap-3">
      <div className="relative grid size-10 place-items-center rounded-lg border border-white/16 bg-white/10 text-white">
        <ShieldCheck className="size-6" strokeWidth={1.8} aria-hidden="true" />
      </div>
      <span className="text-2xl font-bold text-white">AuthCore</span>
    </div>
  );
}
