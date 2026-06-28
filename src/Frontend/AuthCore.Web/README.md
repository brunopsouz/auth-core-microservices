<div align="center">

# AuthCore.Web

<p>
  <img alt="Next.js 16" src="https://img.shields.io/badge/Next.js-16-000000?style=for-the-badge&logo=nextdotjs&logoColor=white">
  <img alt="React 19" src="https://img.shields.io/badge/React-19-61DAFB?style=for-the-badge&logo=react&logoColor=20232A">
  <img alt="TypeScript 5" src="https://img.shields.io/badge/TypeScript-5-3178C6?style=for-the-badge&logo=typescript&logoColor=white">
  <img alt="Tailwind CSS 4" src="https://img.shields.io/badge/Tailwind_CSS-4-06B6D4?style=for-the-badge&logo=tailwindcss&logoColor=white">
</p>
</div>

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
