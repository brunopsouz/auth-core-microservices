# AuthCore Frontend

## Objetivo

Este documento padroniza o frontend `src/Frontend/AuthCore.Web` e registra como ele deve consumir o fluxo real de autenticacao do backend AuthCore.

Use este guia ao criar ou revisar telas, rotas, integracoes HTTP, componentes React e proxies locais do frontend.

O objetivo principal e evitar que o frontend crie uma feature de autenticacao que force mudanca desnecessaria no backend. O backend continua sendo a fonte de verdade para autenticacao, autorizacao, sessao, CSRF, login externo e regras de usuario.

## Stack

- Next.js com App Router e TypeScript.
- Tailwind CSS v4.
- shadcn/ui com `components.json` na raiz do frontend.
- Componentes oficiais em `src/components/ui`.
- Icones com `lucide-react`.
- Gerenciador de pacotes: `pnpm`.

## Fonte de Verdade

Ao implementar autenticacao no frontend, siga esta ordem:

1. contratos HTTP atuais em `src/Backend/AuthCore/AuthCore.Api/Contracts`;
2. controllers atuais em `src/Backend/AuthCore/AuthCore.Api/Controllers`;
3. Gateway em `src/Backend/Gateway/Gateway.Api/ocelot.json`, quando a chamada precisar passar pela borda publica;
4. este documento;
5. estrutura atual de `src/Frontend/AuthCore.Web`.

O anexo original de estrutura frontend serve como referencia de organizacao, mas nao deve ser copiado literalmente quando entrar em conflito com o estado atual do projeto.

## Leitura do Backend Atual

O AuthCore expoe tres grupos principais de autenticacao:

- autenticacao publica e verificacao de e-mail em `AuthController`;
- autenticacao web por sessao/cookie em `SessionAuthController`;
- autenticacao token-based em `TokenAuthController`.

Para o frontend web, o fluxo principal deve ser a sessao por cookie:

- `POST /api/auth/session/login`;
- `GET /api/auth/session/me`;
- `POST /api/auth/session/refresh`;
- `POST /api/auth/session/logout`;
- `GET /api/auth/session/sessions`;
- `DELETE /api/auth/session/sessions/{sid}`;
- `POST /api/auth/session/logout-all`.

O modo token em `/api/auth/token/...` existe para consumidores token-based e nao deve ser o caminho principal do browser AuthCore.Web.

## Rotas do Frontend

A estrutura atual do frontend usa:

- rotas publicas em `src/app/(public)`;
- rotas privadas em `src/app/(private)`;
- login em `/sign-in`;
- registro em `/register`;
- dashboard privado em `/`.

No estado atual, `/` e uma rota privada. Nao mova o dashboard para `/dashboard` apenas porque o anexo sugeria essa rota. Se um dashboard publico ou uma landing page forem criados no futuro, isso deve ser uma decisao explicita de produto e roteamento.

Rotas publicas atuais:

- `/sign-in`;
- `/register`.

Rotas publicas planejadas conforme o backend atual permitir:

- `/verify-email`;
- `/auth/error`;
- `/onboarding?provider=google`, quando o login Google retornar um usuario que ainda precisa completar cadastro;
- uma tela de conclusao ou fallback para login externo, se o fluxo de redirecionamento exigir experiencia intermediaria no frontend.

Rotas privadas planejadas:

- `/`;
- `/profile`;
- `/sessions`;
- `/security`.

Evite criar `/pricing`, `/help`, landing page ou rotas marketing no AuthCore.Web enquanto o objetivo for montar o produto de autenticacao.

## Proxy de Navegacao

No Next.js 16, use `src/proxy.ts` no lugar de `middleware.ts`.

O proxy de navegacao deve fazer apenas decisoes rapidas de UX:

- verificar se existe cookie de sessao;
- redirecionar usuario anonimo de rota privada para `/sign-in`;
- redirecionar usuario autenticado de `/sign-in` e `/register` para `/`;
- permitir rotas publicas futuras com `whenAuthenticated: "next"`.

O cookie de sessao usado como sinal rapido deve acompanhar `Auth:Cookie:SessionCookieName` do backend.

No ambiente de desenvolvimento atual, o backend usa `sid`. Em producao, a configuracao pode usar `__Host-auth.sid`. Defina o valor do frontend por `AUTHCORE_SESSION_COOKIE_NAME`.

O proxy de navegacao nao deve:

- fazer chamada HTTP;
- consultar banco;
- validar JWT;
- validar sessao no Redis;
- renovar sessao;
- executar refresh token;
- decidir regra critica de seguranca.

Paginas privadas que exibem dados reais devem validar a sessao por chamada normal ao backend, por exemplo `GET /api/auth/session/me`, antes de renderizar dados autenticados.

## Integracao HTTP

O browser deve chamar rotas locais do Next.js, nao espalhar chamadas diretas ao backend em componentes React.

Para autenticacao web, use rotas locais em `/api/auth/...`.

As rotas locais encaminham para `AUTHCORE_API_BASE_URL`, com default atual `http://localhost:5012`.

Em desenvolvimento local, `AUTHCORE_API_BASE_URL` pode apontar diretamente para o `AuthCore.Api` quando o frontend consumir apenas `/api/auth/...`.

Quando o frontend consumir endpoints protegidos em `/api/users/...` usando cookies do browser, a borda recomendada e o Gateway. Motivo: `UserController` exige Bearer/JWT por `[AuthenticatedUser]`, enquanto o Gateway ja sabe transformar o access token recebido por cookie em `Authorization: Bearer ...` e aplicar CSRF nas mutacoes.

Se o frontend mantiver um route handler local para `/api/users/...`, ele deve encaminhar para o Gateway. Nao replique no frontend a logica sensivel de cookie-to-bearer, validacao CSRF, origem e assinatura.

Headers minimos que o proxy local deve preservar:

- `Accept`;
- `Content-Type`;
- `Cookie`;
- `Origin`;
- `Referer`;
- `User-Agent`;
- `X-CSRF-TOKEN`;
- `Set-Cookie`;
- `Location`;
- `Retry-After`.

Nao espalhe `fetch` diretamente em componentes. Centralize a chamada em utilitarios de API ou em modulos por feature.

## Cookies e CSRF

No login por sessao, o backend emite:

- cookie de sessao opaco, `sid` em desenvolvimento;
- cookie de access token, `at` em desenvolvimento;
- cookie CSRF, `XSRF-TOKEN`.

Em producao, os nomes padrao podem usar prefixo `__Host-`, como `__Host-auth.sid` e `__Host-auth.at`.

O frontend nao deve ler o cookie HttpOnly de sessao nem o cookie HttpOnly de access token.

Para mutacoes autenticadas por cookie, envie o header `X-CSRF-TOKEN` com o valor do cookie `XSRF-TOKEN` quando a chamada passar por uma rota que exige CSRF.

Responsabilidades atuais:

- AuthCore valida CSRF nas rotas `/api/auth/session/...`;
- Gateway valida CSRF nas demais mutacoes autenticadas por cookie, como `/api/users/...`.

Chamadas autenticadas devem usar `credentials: "include"` quando feitas pelo browser. Em route handlers do Next.js, preserve explicitamente os headers e `Set-Cookie`.

## Fluxos Canonicos

### Cadastro

1. Usuario acessa `/register`.
2. Frontend envia `POST /api/auth/register`.
3. Payload segue `RequestRegisterUserJson`: `FirstName`, `LastName`, `Email`, `Contact`, `Password`, `ConfirmPassword`.
4. Backend registra usuario pendente de verificacao.
5. Frontend direciona para verificacao de e-mail ou orienta o usuario conforme a experiencia implementada.

### Verificacao de E-mail

1. Usuario informa e-mail e codigo OTP.
2. Frontend envia `POST /api/auth/verify-email`.
3. Payload segue `RequestVerifyEmailJson`: `Email`, `Code`.
4. Backend retorna `204 No Content`.

Para reenviar codigo, use `POST /api/auth/resend-verification` com `Email`.

### Login por Sessao

1. Usuario acessa `/sign-in`.
2. Frontend envia `POST /api/auth/session/login`.
3. Payload segue `RequestSessionLoginJson`: `Email`, `Password`.
4. Backend valida credenciais, cria sessao, emite cookies e retorna `ResponseAuthenticatedUserJson`.
5. Frontend redireciona para `/`.
6. Paginas privadas confirmam a sessao com `GET /api/auth/session/me`.

### Renovacao da Sessao Browser

Use `POST /api/auth/session/refresh` para renovar o access token curto da sessao por cookie.

Essa chamada exige sessao e validacao CSRF. Nao implemente refresh dentro de `src/proxy.ts`.

### Logout

Use `POST /api/auth/session/logout` para encerrar a sessao atual.

Essa chamada exige sessao e validacao CSRF. O backend remove os cookies de autenticacao.

### Gerenciamento de Sessoes

Use:

- `GET /api/auth/session/sessions` para listar sessoes ativas;
- `DELETE /api/auth/session/sessions/{sid}` para revogar uma sessao especifica;
- `POST /api/auth/session/logout-all` para revogar todas as sessoes.

Se a sessao atual for revogada, o backend remove os cookies e o frontend deve voltar para `/sign-in`.

### Login com Google

O login Google deve ser tratado como redirecionamento externo iniciado pelo backend.

Fluxo esperado:

1. usuario clica em entrar com Google;
2. frontend navega para `GET /api/auth/external/google`, opcionalmente com `returnUrl`;
3. backend valida `returnUrl`, cria challenge e redireciona para Google;
4. Google retorna para `/api/auth/external/google/callback`;
5. middleware do backend conclui a autenticacao externa e encaminha para `/api/auth/external/google/complete`;
6. backend cria sessao, emite cookies e redireciona para a URL segura de retorno;
7. frontend apenas recebe o usuario ja redirecionado, exibe tela de erro quando o backend redirecionar para `/auth/error?reason=external_callback_failed`, ou trata `/onboarding?provider=google` quando o login externo exigir completude cadastral.

O frontend nao deve processar `code` nem `state` do Google.

## Contratos Consumidos

Contratos de autenticacao e sessao para o frontend web:

| Metodo | Rota | Uso |
| --- | --- | --- |
| `POST` | `/api/auth/register` | Cadastro publico |
| `POST` | `/api/auth/verify-email` | Confirmacao de e-mail por OTP |
| `POST` | `/api/auth/resend-verification` | Reenvio de verificacao |
| `POST` | `/api/auth/session/login` | Login por cookie |
| `GET` | `/api/auth/session/me` | Usuario da sessao atual |
| `POST` | `/api/auth/session/refresh` | Renovacao browser/session |
| `POST` | `/api/auth/session/logout` | Logout da sessao atual |
| `GET` | `/api/auth/session/sessions` | Listagem de sessoes |
| `DELETE` | `/api/auth/session/sessions/{sid}` | Revogacao de sessao |
| `POST` | `/api/auth/session/logout-all` | Revogacao global |
| `GET` | `/api/auth/external/google` | Inicio do login Google |
| `GET` | `/api/auth/external/google/complete` | Conclusao backend do login Google |

Contratos de usuario protegidos:

| Metodo | Rota | Uso |
| --- | --- | --- |
| `GET` | `/api/users/profile` | Perfil do usuario autenticado |
| `PUT` | `/api/users/profile` | Atualizacao de perfil |
| `PUT` | `/api/users/change-password` | Alteracao de senha |
| `DELETE` | `/api/users` | Exclusao do usuario atual |

Para `/api/users/...`, considere a observacao da secao de integracao: o Gateway e parte relevante do fluxo quando a autenticacao vem por cookies.

## Estrutura Recomendada

A estrutura atual e simples e deve evoluir incrementalmente. Nao crie camadas genericas de `core`, `domain` ou `application` no frontend.

Estrutura alvo para crescimento:

```txt
src/
  app/
    (public)/
      sign-in/
      register/
      verify-email/
    (private)/
      (dashboard)/
      profile/
      sessions/
      security/
    api/
      auth/
        [...path]/
      users/
        [...path]/
  components/
    auth/
    session/
    user/
    layout/
    ui/
  features/
    auth/
      api/
      components/
      types/
    session/
      api/
      components/
      types/
    user/
      api/
      components/
      types/
  lib/
    auth-routes.ts
    authcore-api.ts
    http-errors.ts
    utils.ts
  proxy.ts
```

Use `components/ui` antes de criar componentes novos. Componentes de UI nao devem conhecer detalhes de API. Modulos de API nao devem conter regra visual.

`features/` deve ser introduzido quando houver volume real por funcionalidade. Enquanto o frontend estiver pequeno, `components/auth`, `components/session`, `components/user` e `lib` sao aceitaveis.

## UI

Use shadcn/ui como base. Nao recrie componentes ja existentes em `src/components/ui`.

Diretrizes:

- telas de formulario usam `Card`, `Field`, `Input`, `Button` e `Alert`;
- comandos usam icones do `lucide-react`;
- mantenha cards com raio de 8px ou menor;
- evite landing page para fluxos de autenticacao;
- mantenha formularios completos e responsivos;
- nao crie regra de negocio no componente React;
- mantenha texto curto, direto e orientado ao fluxo.

## O Que Nao Fazer

Nao crie:

- validacao real de sessao no frontend;
- chamada HTTP dentro de `src/proxy.ts`;
- refresh token ou renovacao de sessao no proxy de navegacao;
- leitura de cookie HttpOnly no JavaScript;
- armazenamento de access token em `localStorage` ou estado global;
- regra de negocio em componente React;
- `POST /api/users` como cadastro publico;
- fluxo administrativo ou convite reaproveitando `POST /api/auth/register`;
- Redux/Zustand antes de haver necessidade real;
- estrutura frontend espelhando Clean Architecture do backend.

## Validacao

Antes de concluir mudancas no frontend, rode:

```bash
pnpm lint
pnpm build
```

Se a mudanca tocar somente documentacao, a validacao pode ser leitura local e `git diff`.

## Skill ou Agent

Para padronizar o desenvolvimento frontend deste projeto, use Skill.

Motivo: a necessidade recorrente e carregar convencoes, estrutura, comandos e limites de arquitetura. Um Agent faz sentido para revisao independente, especialmente quando a mudanca altera comportamento backend, seguranca ou contratos.

A skill local fica em `.agents/skills/authcore-frontend-nextjs/SKILL.md`.
