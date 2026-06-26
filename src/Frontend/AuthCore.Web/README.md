# AuthCore.Web

Frontend web do AuthCore com Next.js, Tailwind CSS e shadcn/ui.

## Requisitos

- Node.js compativel com Next.js 16.
- pnpm.
- AuthCore.Api em execucao.

## Configuracao

Copie `.env.example` para `.env.local` quando precisar alterar o endereco do backend.

```bash
AUTHCORE_API_BASE_URL=http://localhost:5012
AUTHCORE_SESSION_COOKIE_NAME=sid
```

Em desenvolvimento, o backend usa `sid`. Em producao, alinhe este valor ao cookie configurado em `Auth:Cookie:SessionCookieName`, por exemplo `__Host-auth.sid`.

## Comandos

```bash
pnpm install
pnpm dev --hostname 127.0.0.1 --port 3000
pnpm lint
pnpm build
```

## Rotas

- `/sign-in`: login por sessao.
- `/register`: registro publico.
- `/`: dashboard privado.

As chamadas do browser usam `/api/auth/...`, e o route handler local encaminha para `AUTHCORE_API_BASE_URL`.

Para mutacoes autenticadas por cookie, o proxy local preserva `Cookie`, `Origin`, `Referer`, `X-CSRF-TOKEN` e `Set-Cookie`.
