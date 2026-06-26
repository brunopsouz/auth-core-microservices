import { ShieldCheck } from "lucide-react";

import { LogoutButton } from "@/components/session/logout-button";

export default function PrivateLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <main className="min-h-screen bg-background">
      <header className="border-b bg-card/80">
        <div className="mx-auto flex h-16 w-full max-w-6xl items-center justify-between px-4 sm:px-6">
          <div className="flex items-center gap-3">
            <div className="flex size-9 items-center justify-center rounded-md bg-primary text-primary-foreground">
              <ShieldCheck className="size-5" aria-hidden="true" />
            </div>
            <div>
              <p className="text-sm font-semibold leading-none">AuthCore</p>
              <p className="text-xs text-muted-foreground">Sessao autenticada</p>
            </div>
          </div>
          <LogoutButton />
        </div>
      </header>
      {children}
    </main>
  );
}
