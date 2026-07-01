# AuthCore Frontend

## Objetivo

Este documento padroniza o frontend `src/Frontend/AuthCore.Web` e registra como ele deve consumir o fluxo real de autenticação do backend AuthCore.

## Ortografia em Português

Todos os textos em português exibidos na UI, mensagens de erro, toasts, documentação e comentários devem usar ortografia oficial, com acentuação, cedilha (`ç`) e demais caracteres Unicode necessários.

Não converta textos em português para ASCII. Preserve diacríticos em palavras como `código`, `verificação`, `usuário`, `operação`, `requisição`, `autenticação`, `sessão`, `não` e `você`. Identificadores técnicos, nomes de classes, rotas e payloads JSON continuam seguindo suas convenções próprias.

Use este guia ao criar ou revisar telas, rotas, integracoes HTTP, componentes React e proxies locais do frontend.

O objetivo principal e evitar que o frontend crie uma feature de autenticação que force mudanca desnecessaria no backend. O backend continua sendo a fonte de verdade para autenticação, autorizacao, sessao, CSRF, login externo e regras de usuário.

## Stack

- Next.js com App Router e TypeScript.
- Tailwind CSS v4.
- shadcn/ui com `components.json` na raiz do frontend.
- Componentes oficiais em `src/components/ui`.
- Icones com `lucide-react`.
- Gerenciador de pacotes: `pnpm`.

## Fonte de Verdade

Ao implementar autenticação no frontend, siga esta ordem:

1. contratos HTTP atuais em `src/Backend/AuthCore/AuthCore.Api/Contracts`;
2. controllers atuais em `src/Backend/AuthCore/AuthCore.Api/Controllers`;
3. Gateway em `src/Backend/Gateway/Gateway.Api/ocelot.json`, quando a chamada precisar passar pela borda publica;
4. este documento;
5. estrutura atual de `src/Frontend/AuthCore.Web`.

O anexo original de estrutura frontend serve como referência de organização, mas não deve ser copiado literalmente quando entrar em conflito com o estado atual do projeto.

## Leitura do Backend Atual

O AuthCore expõe três grupos principais de autenticação:

- autenticação pública e verificação de e-mail em `AuthController`;
- autenticação web por sessão/cookie em `SessionAuthController`;
- autenticação token-based em `TokenAuthController`.

Para o frontend web, o fluxo principal deve ser a sessao por cookie:

- `POST /api/auth/session/login`;
- `GET /api/auth/session/me`;
- `POST /api/auth/session/refresh`;
- `POST /api/auth/session/logout`;
- `GET /api/auth/session/sessions`;
- `DELETE /api/auth/session/sessions/{sid}`;
- `POST /api/auth/session/logout-all`.

O modo token em `/api/auth/token/...` existe para consumidores token-based e não deve ser o caminho principal do browser AuthCore.Web.

## Rotas do Frontend

A estrutura atual do frontend usa:

- rotas publicas em `src/app/(public)`;
- rotas privadas em `src/app/(private)`;
- login em `/sign-in`;
- registro em `/register`;
- dashboard privado em `/`.

No estado atual, `/` é uma rota privada. Não mova o dashboard para `/dashboard` apenas porque o anexo sugeria essa rota. Se um dashboard público ou uma landing page forem criados no futuro, isso deve ser uma decisão explícita de produto e roteamento.

Rotas públicas atuais:

- `/sign-in`;
- `/register`.

Rotas públicas planejadas conforme o backend atual permitir:

- `/verify-email`;
- `/auth/error`;
- `/onboarding?provider=google`, quando o login Google retornar um usuário que ainda precisa completar cadastro;
- uma tela de conclusão ou fallback para login externo, se o fluxo de redirecionamento exigir experiência intermediária no frontend.

Rotas privadas planejadas:

- `/`;
- `/profile`;
- `/sessions`;
- `/security`.

Evite criar `/pricing`, `/help`, landing page ou rotas marketing no AuthCore.Web enquanto o objetivo for montar o produto de autenticação.

## Proxy de navegação

No Next.js 16, use `src/proxy.ts` no lugar de `middleware.ts`.

O proxy de navegação deve fazer apenas decisoes rapidas de UX:

- verificar se existe cookie de sessao;
- redirecionar usuário anônimo de rota privada para `/sign-in`;
- redirecionar usuário autenticado de `/sign-in` e `/register` para `/`;
- permitir rotas publicas futuras com `whenAuthenticated: "next"`.

O cookie de sessao usado como sinal rapido deve acompanhar `Auth:Cookie:SessionCookieName` do backend.

No ambiente de desenvolvimento atual, o backend usa `sid`. Em producao, a configuracao pode usar `__Host-auth.sid`. Defina o valor do frontend por `AUTHCORE_SESSION_COOKIE_NAME`.

O proxy de navegação Não deve:

- fazer chamada HTTP;
- consultar banco;
- validar JWT;
- validar sessao no Redis;
- renovar sessao;
- executar refresh token;
- decidir regra critica de segurança.

Paginas privadas que exibem dados reais devem validar a sessao por chamada normal ao backend, por exemplo `GET /api/auth/session/me`, antes de renderizar dados autenticados.

## integração HTTP

O browser deve chamar rotas locais do Next.js, Não espalhar chamadas diretas ao backend em componentes React.

Para autenticação web, use rotas locais em `/api/auth/...`.

As rotas locais encaminham para `AUTHCORE_API_BASE_URL`, com default atual `http://localhost:5012`.

Em desenvolvimento local, `AUTHCORE_API_BASE_URL` pode apontar diretamente para o `AuthCore.Api` quando o frontend consumir apenas `/api/auth/...`.

Quando o frontend consumir endpoints protegidos em `/api/users/...` usando cookies do browser, a borda recomendada e o Gateway. Motivo: `UserController` exige Bearer/JWT por `[AuthenticatedUser]`, enquanto o Gateway Já sabe transformar o access token recebido por cookie em `Authorization: Bearer ...` e aplicar CSRF nas mutacoes.

Se o frontend mantiver um route handler local para `/api/users/...`, ele deve encaminhar para o Gateway. Não replique no frontend a logica sensivel de cookie-to-bearer, validação CSRF, origem e assinatura.

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

Não espalhe `fetch` diretamente em componentes. Centralize a chamada em utilitarios de API ou em modulos por feature.

## Cookies e CSRF

No login por sessao, o backend emite:

- cookie de sessao opaco, `sid` em desenvolvimento;
- cookie de access token, `at` em desenvolvimento;
- cookie CSRF, `XSRF-TOKEN`.

Em producao, os nomes padrão podem usar prefixo `__Host-`, como `__Host-auth.sid` e `__Host-auth.at`.

O frontend Não deve ler o cookie HttpOnly de sessao nem o cookie HttpOnly de access token.

Para mutacoes autenticadas por cookie, envie o header `X-CSRF-TOKEN` com o valor do cookie `XSRF-TOKEN` quando a chamada passar por uma rota que exige CSRF.

Responsabilidades atuais:

- AuthCore valida CSRF nas rotas `/api/auth/session/...`;
- Gateway valida CSRF nas demais mutacoes autenticadas por cookie, como `/api/users/...`.

Chamadas autenticadas devem usar `credentials: "include"` quando feitas pelo browser. Em route handlers do Next.js, preserve explicitamente os headers e `Set-Cookie`.

## Fluxos Canonicos

### Cadastro

1. usuário acessa `/register`.
2. Frontend envia `POST /api/auth/register`.
3. Payload segue `RequestRegisterUserJson`: `FirstName`, `LastName`, `Email`, `Contact`.
4. Backend registra usuário pendente de verificação e envia o código OTP por e-mail.
5. Frontend solicita o código OTP.
6. Depois do código informado, frontend solicita a senha.
7. Frontend envia `POST /api/auth/complete-registration`.
8. Payload segue `RequestCompleteRegistrationJson`: `Email`, `Code`, `Password`, `ConfirmPassword`.
9. Backend valida o OTP, confirma o e-mail e grava a primeira senha.

### verificação de E-mail

1. usuário informa e-mail e código OTP.
2. Frontend envia `POST /api/auth/verify-email`.
3. Payload segue `RequestVerifyEmailJson`: `Email`, `Code`.
4. Backend retorna `204 No Content`.

Para reenviar código, use `POST /api/auth/resend-verification` com `Email`.

### Login por Sessao

1. usuário acessa `/sign-in`.
2. Frontend envia `POST /api/auth/session/login`.
3. Payload segue `RequestSessionLoginJson`: `Email`, `Password`.
4. Backend valida credenciais, cria sessao, emite cookies e retorna `ResponseAuthenticatedUserJson`.
5. Frontend redireciona para `/`.
6. Paginas privadas confirmam a sessao com `GET /api/auth/session/me`.

### Renovacao da Sessao Browser

Use `POST /api/auth/session/refresh` para renovar o access token curto da sessao por cookie.

Essa chamada exige sessao e validação CSRF. Não implemente refresh dentro de `src/proxy.ts`.

### Logout

Use `POST /api/auth/session/logout` para encerrar a sessao atual.

Essa chamada exige sessao e validação CSRF. O backend remove os cookies de autenticação.

### Gerenciamento de Sessoes

Use:

- `GET /api/auth/session/sessions` para listar sessoes ativas;
- `DELETE /api/auth/session/sessions/{sid}` para revogar uma sessao especifica;
- `POST /api/auth/session/logout-all` para revogar todas as sessoes.

Se a sessao atual for revogada, o backend remove os cookies e o frontend deve voltar para `/sign-in`.

### Login com Google

O login Google deve ser tratado como redirecionamento externo iniciado pelo backend.

Fluxo esperado:

1. usuário clica em entrar com Google;
2. frontend navega para `GET /api/auth/external/google`, opcionalmente com `returnUrl`;
3. backend valida `returnUrl`, cria challenge e redireciona para Google;
4. Google retorna para `/api/auth/external/google/callback`;
5. middleware do backend conclui a autenticação externa e encaminha para `/api/auth/external/google/complete`;
6. backend cria sessao, emite cookies e redireciona para a URL segura de retorno;
7. frontend apenas recebe o usuário Já redirecionado, exibe tela de erro quando o backend redirecionar para `/auth/error?reason=external_callback_failed`, ou trata `/onboarding?provider=google` quando o login externo exigir completude cadastral.

O frontend Não deve processar `code` nem `state` do Google.

## Contratos Consumidos

Contratos de autenticação e sessao para o frontend web:

| Metodo | Rota | Uso |
| --- | --- | --- |
| `POST` | `/api/auth/register` | Cadastro público |
| `POST` | `/api/auth/verify-email` | confirmação de e-mail por OTP |
| `POST` | `/api/auth/complete-registration` | conclusão do cadastro com OTP e senha |
| `POST` | `/api/auth/resend-verification` | Reenvio de verificação |
| `POST` | `/api/auth/session/login` | Login por cookie |
| `GET` | `/api/auth/session/me` | usuário da sessao atual |
| `POST` | `/api/auth/session/refresh` | Renovacao browser/session |
| `POST` | `/api/auth/session/logout` | Logout da sessao atual |
| `GET` | `/api/auth/session/sessions` | Listagem de sessoes |
| `DELETE` | `/api/auth/session/sessions/{sid}` | Revogacao de sessao |
| `POST` | `/api/auth/session/logout-all` | Revogacao global |
| `GET` | `/api/auth/external/google` | Início do login Google |
| `GET` | `/api/auth/external/google/complete` | conclusão backend do login Google |

Contratos de usuário protegidos:

| Metodo | Rota | Uso |
| --- | --- | --- |
| `GET` | `/api/users/profile` | Perfil do usuário autenticado |
| `PUT` | `/api/users/profile` | Atualizacao de perfil |
| `PUT` | `/api/users/change-password` | Alteracao de senha |
| `DELETE` | `/api/users` | Exclusao do usuário atual |

Para `/api/users/...`, considere a observacao da secao de integração: o Gateway e parte relevante do fluxo quando a autenticação vem por cookies.

## Estrutura Recomendada

A estrutura atual e simples e deve evoluir incrementalmente. Não crie camadas genericas de `core`, `domain` ou `application` no frontend.

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

Use `components/ui` antes de criar componentes novos. Componentes de UI Não devem conhecer detalhes de API. Modulos de API Não devem conter regra visual.

`features/` deve ser introduzido quando houver volume real por funcionalidade. Enquanto o frontend estiver pequeno, `components/auth`, `components/session`, `components/user` e `lib` sao aceitaveis.

## UI

Use shadcn/ui como base. Não recrie componentes Já existentes em `src/components/ui`.

Diretrizes:

- telas de formulario usam `Card`, `Field`, `Input`, `Button` e `Alert`;
- comandos usam icones do `lucide-react`;
- mantenha cards com raio de 8px ou menor;
- evite landing page para fluxos de autenticação;
- mantenha formularios completos e responsivos;
- Não crie regra de negocio no componente React;
- mantenha texto curto, direto e orientado ao fluxo.

## O Que Não Fazer

Não crie:

- validação real de sessao no frontend;
- chamada HTTP dentro de `src/proxy.ts`;
- refresh token ou renovacao de sessao no proxy de navegação;
- leitura de cookie HttpOnly no JavaScript;
- armazenamento de access token em `localStorage` ou estado global;
- regra de negocio em componente React;
- `POST /api/users` como cadastro público;
- fluxo administrativo ou convite reaproveitando `POST /api/auth/register`;
- Redux/Zustand antes de haver necessidade real;
- estrutura frontend espelhando Clean Architecture do backend.

## validação

Antes de concluir mudancas no frontend, rode:

```bash
pnpm lint
pnpm build
```

Se a mudanca tocar somente documentação, a validação pode ser leitura local e `git diff`.

## Skill ou Agent

Para padronizar o desenvolvimento frontend deste projeto, use Skill.

Motivo: a necessidade recorrente e carregar convencoes, estrutura, comandos e limites de arquitetura. Um Agent faz sentido para revisao independente, especialmente quando a mudanca altera comportamento backend, segurança ou contratos.

A skill local fica em `.agents/skills/authcore-frontend-nextjs/SKILL.md`.
