"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";
import { LogOut } from "lucide-react";

import { Button } from "@/components/ui/button";
import { logoutSession } from "@/features/auth/api/session-auth";

export function LogoutButton() {
  const router = useRouter();
  const [isPending, setIsPending] = useState(false);

  async function handleLogout() {
    setIsPending(true);

    try {
      const response = await logoutSession();

      if (!response.ok) {
        toast.error("Nao foi possivel encerrar a sessao.");
        return;
      }

      toast.success("Sessao encerrada.");
      router.replace("/sign-in");
      router.refresh();
    } finally {
      setIsPending(false);
    }
  }

  return (
    <Button
      type="button"
      variant="outline"
      size="sm"
      onClick={handleLogout}
      disabled={isPending}
    >
      <LogOut className="size-4" aria-hidden="true" />
      {isPending ? "Saindo..." : "Sair"}
    </Button>
  );
}
