<div align="center">

# AuthCore

<p>
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white">
  <img alt="PostgreSQL 17" src="https://img.shields.io/badge/PostgreSQL-17-4169E1?style=for-the-badge&logo=postgresql&logoColor=white">
  <img alt="Next.js 16" src="https://img.shields.io/badge/Next.js-16-000000?style=for-the-badge&logo=nextdotjs&logoColor=white">
  <img alt="React 19" src="https://img.shields.io/badge/React-19-61DAFB?style=for-the-badge&logo=react&logoColor=20232A">
  <img alt="Tailwind CSS 4" src="https://img.shields.io/badge/Tailwind_CSS-4-06B6D4?style=for-the-badge&logo=tailwindcss&logoColor=white">
</p>

### [Back-end](src/Backend/README.md) - [Front-end](src/Frontend/AuthCore.Web/README.md)

</div>

AuthCore é uma solução full-stack para autenticação, gestão de usuários e notificações transacionais. A base combina backend em .NET 10, API Gateway, mensageria assíncrona, frontend web em Next.js e camadas internas com influência de Clean Architecture e DDD tático.

O objetivo é oferecer um núcleo de autenticação robusto para aplicações web e backend, mantendo regras de negócio no domínio, casos de uso na aplicação, detalhes técnicos na infraestrutura, comunicação entre serviços por contratos explícitos e uma experiência frontend alinhada ao fluxo real do AuthCore.

## Sumário

- [Funcionalidades](#funcionalidades)
- [Serviços](#serviços)
- [Stack](#stack)
- [Arquitetura](#arquitetura)
- [Padrões de engenharia](#padrões-de-engenharia)
- [Requisitos](#requisitos)
- [Instalação](#instalação)
- [Uso](#uso)
- [Configuração](#configuração)
- [Autenticação](#autenticação)
- [Endpoints principais](#endpoints-principais)
- [Testes](#testes)
- [Estrutura do projeto](#estrutura-do-projeto)
- [Licença](#licença)

## Funcionalidades

- Registro de usuários com validação de dados e senha.
- Verificação de e-mail por código OTP.
- Login por sessão com cookies `HttpOnly`.
- Login token-based com access token JWT e refresh token.
- Autenticação híbrida para Browser/PWA com sessão server-side e JWT curto em cookie `HttpOnly`.
- Renovação e revogação de sessões.
- Logout da sessão atual, logout por token e logout global.
- Listagem e revogação de sessões ativas do usuário.
- Consulta e atualização do perfil autenticado.
- Troca de senha.
- Exclusão de usuário autenticado.
- Rate limiting de rotas sensíveis no Gateway.
- Proteção CSRF para mutações autenticadas por cookie.
- Health checks por serviço.
- Publicação assíncrona de solicitações de notificação pelo AuthCore.
- Consumo, registro, renderização e despacho de notificações transacionais pelo NotificationCore.
- SMTP configurável para envio de e-mail em desenvolvimento.
- Frontend web com Next.js App Router, sessão por cookie `HttpOnly`, route handlers locais para rotas de autenticação e telas iniciais de login, registro e dashboard privado.

## Serviços

- `Gateway.Api`: API Gateway com Ocelot, autenticação JWT, suporte a JWT via cookie `HttpOnly`, proteção CSRF para mutações por cookie e roteamento para os serviços internos.
- `AuthCore.Api`: serviço de autenticação e usuários.
- `NotificationCore.Api`: serviço de notificações transacionais, templates e envio de e-mail.
- `AuthCore.Web`: frontend web em Next.js para login, registro, sessão por cookie e área autenticada.
- `Shared.Messaging.Contracts`: contratos compartilhados de mensageria e utilitários de payload sensível.

## Stack

| Camada | Tecnologia | Responsabilidade |
| --- | --- | --- |
| Frontend | Next.js 16 + React 19 + TypeScript 5 | Interface web, rotas locais de autenticação e experiência autenticada |
| UI | Tailwind CSS v4 + shadcn/ui + lucide-react | Estilização, componentes visuais e ícones |
| Backend | .NET 10 + ASP.NET Core Web API | APIs, autenticação, usuários, notificações e regras de negócio |
| Gateway | Ocelot | Roteamento, borda pública, rate limiting e proteção de rotas |
| Banco | PostgreSQL 17 + Npgsql | Persistência principal dos contextos AuthCore e NotificationCore |
| Cache | Redis 7 | Armazenamento técnico para sessões e suporte à autenticação |
| Mensageria | RabbitMQ 3 | Comunicação assíncrona entre AuthCore e NotificationCore |
| E-mail local | SMTP4Dev | Simulação de envio SMTP em desenvolvimento |
| Infra local | Docker + Docker Compose | Ambiente local de desenvolvimento e execução dos serviços |
| Migrações | FluentMigrator | Versionamento e aplicação de mudanças no banco |
| Segurança | BCrypt.Net + JWT Bearer Authentication | Hash de senhas e autenticação por token |
| Testes e documentação | xUnit + Swagger/OpenAPI | Testes automatizados e documentação interativa das APIs |
| Pacotes frontend | pnpm | Instalação e gerenciamento de dependências web |

## Arquitetura

A solução organiza serviços backend com separação de responsabilidades e camadas internas por contexto de negócio. A evolução do código segue Clean Architecture, DDD tático e princípios SOLID para manter baixo acoplamento, alta testabilidade e responsabilidades claras.

```mermaid
flowchart TD
    Browser[Browser] -->|HTTP| Web[AuthCore.Web]
    Web -->|/api/auth route handler| AuthApi
    Web -->|rotas protegidas fora de /api/auth devem usar Gateway| Gateway
    Client[Cliente HTTP/API] -->|HTTP| Gateway[Gateway.Api]
    Gateway -->|HTTP/Ocelot| AuthApi[AuthCore.Api]
    Gateway -->|HTTP/Ocelot| NotificationApi[NotificationCore.Api]

    AuthApi --> AuthApplication[AuthCore.Application]
    AuthApi --> AuthInfrastructure[AuthCore.Infrastructure]
    AuthApplication --> AuthDomain[AuthCore.Domain]
    AuthInfrastructure --> AuthDomain

    NotificationApi --> NotificationApplication[NotificationCore.Application]
    NotificationApi --> NotificationInfrastructure[NotificationCore.Infrastructure]
    NotificationApplication --> NotificationDomain[NotificationCore.Domain]
    NotificationInfrastructure --> NotificationDomain

    AuthInfrastructure -. compile-time .-> Contracts[Shared.Messaging.Contracts]
    NotificationApplication -. compile-time .-> Contracts
    NotificationInfrastructure -. compile-time .-> Contracts

    AuthApi -->|OutboxHostedService| AuthInfrastructure
    AuthInfrastructure -->|publish| RabbitMQ[(RabbitMQ)]
    RabbitMQ -->|consume| NotificationApi
    NotificationApi -->|worker/use case| NotificationApplication

    AuthInfrastructure --> AuthPostgres[(AuthCore PostgreSQL)]
    AuthInfrastructure --> Redis[(Redis)]

    NotificationInfrastructure --> NotificationPostgres[(NotificationCore PostgreSQL)]
    NotificationInfrastructure --> SMTP[SMTP4Dev/SMTP]
```

Responsabilidades principais:

- `Gateway.Api`: borda pública em Docker Compose, roteamento, rate limiting, validação JWT para rotas protegidas e suporte ao fluxo Browser/PWA com JWT em cookie `HttpOnly`.
- `AuthCore.Web`: frontend Next.js com App Router, telas de autenticação, dashboard privado, proxy de navegação por cookie e route handlers locais para chamadas ao AuthCore.
- `AuthCore.Api`: controllers HTTP, contratos JSON, autenticação, autorização, Swagger e health checks.
- `AuthCore.Application`: orquestração dos casos de uso de autenticação e usuários.
- `AuthCore.Domain`: agregados, entidades, value objects, invariantes, eventos e contratos centrais de autenticação.
- `AuthCore.Infrastructure`: persistência PostgreSQL, Redis, criptografia, tokens, migrações, Outbox e publicação RabbitMQ.
- `NotificationCore.Api`: controllers HTTP administrativos, contratos JSON, Swagger e health checks.
- `NotificationCore.Application`: orquestração de consultas, busca e solicitações de envio de notificações.
- `NotificationCore.Domain`: entidades, value objects, enums e regras de notificação.
- `NotificationCore.Infrastructure`: persistência PostgreSQL, consumo RabbitMQ, Inbox, templates, renderização e envio SMTP.
- `Shared.Messaging.Contracts`: mensagens compartilhadas entre serviços.
- `tests`: testes unitários de domínio, aplicação e testes de integração por serviço.

## Padrões de engenharia

O projeto adota SOLID como padrão transversal de design e revisão técnica:

- SRP: classes, controllers, use cases e repositórios devem ter um motivo principal para mudar.
- OCP: variações reais de comportamento devem ser tratadas por abstrações, policies, strategies, factories ou providers, sem concentrar branches em classes centrais.
- LSP: implementações devem cumprir integralmente os contratos que expõem, sem métodos artificiais ou `NotSupportedException`.
- ISP: interfaces devem ser pequenas e orientadas ao consumidor.
- DIP: `Application` e `Domain` dependem de abstrações; detalhes técnicos ficam em `Infrastructure`.

Como regra de visibilidade, abstrações consumidas entre camadas podem ser públicas, enquanto implementações concretas devem ser `internal` por padrão, exceto contratos de entrada, tipos de domínio e exigências técnicas de frameworks.

O guia completo fica em `docs/agents/solid-guidelines.md`. Ele complementa os padrões de arquitetura, estilo C#, contratos HTTP, persistência e testes em `docs/agents/`.

## Requisitos

Para executar localmente:

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- Node.js compatível com Next.js 16
- pnpm
- Docker
- Docker Compose ou plugin `docker compose`
- Bash, para usar o script `run.sh`

## Instalação

Clone o repositório e acesse a pasta do projeto:

```bash
git clone <url-do-repositorio>
cd auth_core
```

Restaure as dependências:

```bash
dotnet restore AuthCore.sln
```

Compile a solução:

```bash
dotnet build AuthCore.sln
```

Instale as dependências do frontend:

```bash
cd src/Frontend/AuthCore.Web
pnpm install
```

## Uso

O projeto possui um script principal para facilitar a execução local.

### Executar AuthCore local com infraestrutura em Docker

```bash
./run.sh dev
```

Esse comando sobe PostgreSQL, Redis, RabbitMQ e SMTP4Dev via Docker Compose e executa `AuthCore.Api` localmente com o profile `http`.

O AuthCore local fica disponível em:

```text
http://localhost:5012
```

Em ambiente de desenvolvimento, o Swagger do AuthCore fica disponível em:

```text
http://localhost:5012/swagger
```

### Executar AuthCore com hot reload

```bash
./run.sh watch
```

### Subir apenas a infraestrutura

```bash
./run.sh infra
```

Esse comando sobe bancos, Redis, RabbitMQ e SMTP4Dev. Ele não executa as APIs.

### Executar toda a aplicação com Docker Compose

```bash
./run.sh docker
```

Nesse modo, o ponto de entrada público é o Gateway:

```text
http://localhost:8080
```

O AuthCore também fica exposto diretamente para depuração local:

```text
http://localhost:8081
```

O NotificationCore roda dentro da rede Docker e é acessado pelo Gateway.

### Executar o frontend AuthCore.Web

Em outro terminal, execute:

```bash
cd src/Frontend/AuthCore.Web
pnpm dev --hostname 127.0.0.1 --port 3000
```

O frontend fica disponível em:

```text
http://127.0.0.1:3000
```

Por padrão, o route handler local do frontend encaminha `/api/auth/...` para:

```text
http://localhost:5012
```

### Encerrar containers

```bash
./run.sh down
```

## Configuração

As configurações de desenvolvimento estão em:

- `src/Backend/.env.development.example`, modelo versionado sem segredos
- `src/Backend/.env.development`, arquivo local ignorado pelo Git
- `src/Backend/AuthCore/AuthCore.Api/appsettings.Development.json`
- `src/Backend/NotificationCore/NotificationCore.Api/appsettings.Development.json`
- `src/Backend/Gateway/Gateway.Api/ocelot.json`
- `src/Frontend/AuthCore.Web/.env.example`
- `src/Frontend/AuthCore.Web/.env.local`, arquivo local ignorado pelo Git

Antes de executar o projeto pela primeira vez, crie o arquivo local a partir do modelo e preencha os valores vazios quando necessário:

```bash
cp src/Backend/.env.development.example src/Backend/.env.development
```

Para alterar o destino das chamadas do frontend, crie o `.env.local` a partir do exemplo:

```bash
cp src/Frontend/AuthCore.Web/.env.example src/Frontend/AuthCore.Web/.env.local
```

Variáveis principais do frontend:

| Variável | Padrão | Descrição |
| --- | --- | --- |
| `AUTHCORE_API_BASE_URL` | `http://localhost:5012` | Base server-side usada pelos route handlers locais para encaminhar `/api/auth/...` |
| `AUTHCORE_SESSION_COOKIE_NAME` | `sid` em desenvolvimento | Nome do cookie usado pelo `src/proxy.ts` apenas como sinal rápido de sessão; em produção deve acompanhar `Auth:Cookie:SessionCookieName`, por exemplo `__Host-auth.sid` |

Serviços padrão em desenvolvimento:

| Serviço | Host | Porta |
| --- | --- | --- |
| AuthCore.Web | `127.0.0.1` | `3000` |
| Gateway Docker | `localhost` | `8080` |
| AuthCore Docker | `localhost` | `8081` |
| AuthCore local | `localhost` | `5012` |
| PostgreSQL AuthCore | `localhost` | `5432` |
| PostgreSQL NotificationCore | `localhost` | `5433` |
| Redis | `localhost` | `6379` |
| RabbitMQ | `localhost` | `5672` |
| RabbitMQ Management | `localhost` | `15672` |

## Autenticação

O projeto suporta três modalidades principais de autenticação:

- login web com sessão autenticada;
- login token-based para clientes API e mobile;
- login com Google para o fluxo web.

As credenciais OAuth do Google devem ser fornecidas por ambiente e nunca versionadas. Para detalhes operacionais, fluxo manual e validações da feature, consulte a documentação em `docs/features/google-login/`.

O frontend `AuthCore.Web` usa o fluxo web por cookie `HttpOnly`. O `src/proxy.ts` faz apenas redirecionamento rápido por presença do cookie de sessão; a validação real de autenticação, autorização, CSRF e sessão permanece no AuthCore e no Gateway.

## Endpoints principais

Quando a aplicação completa está em Docker, prefira acessar as rotas publicadas pelo Gateway em `http://localhost:8080`.

### AuthCore

| Método | Rota | Descrição |
| --- | --- | --- |
| `POST` | `/api/auth/register` | Registra usuário pendente de verificação |
| `POST` | `/api/auth/verify-email` | Valida código de verificação de e-mail |
| `POST` | `/api/auth/resend-verification` | Reenvia código de verificação |
| `POST` | `/api/auth/session/login` | Autentica por sessão com cookie |
| `GET` | `/api/auth/session/me` | Retorna usuário da sessão atual |
| `GET` | `/api/auth/session/sessions` | Lista sessões ativas |
| `DELETE` | `/api/auth/session/sessions/{sid}` | Revoga uma sessão específica |
| `POST` | `/api/auth/session/logout` | Encerra sessão atual |
| `POST` | `/api/auth/session/logout-all` | Encerra todas as sessões |
| `POST` | `/api/auth/token/login` | Autentica por JWT e refresh token |
| `POST` | `/api/auth/token/refresh` | Renova uma sessão token-based |
| `POST` | `/api/auth/token/logout` | Revoga refresh token |
| `GET` | `/api/auth/external/google` | Inicia login com Google |
| `GET` | `/api/auth/external/google/callback` | Recebe callback tecnico do Google |
| `GET` | `/api/auth/external/google/complete` | Conclui login Google e redireciona com sessao autenticada |
| `GET` | `/api/users/profile` | Consulta perfil autenticado |
| `PUT` | `/api/users/profile` | Atualiza perfil autenticado |
| `PUT` | `/api/users/change-password` | Altera senha |
| `DELETE` | `/api/users` | Exclui usuário autenticado |

`POST /api/auth/register` é a única entrada pública de autocadastro. Esse endpoint pertence ao `AuthController` e usa `RegisterUserUseCase` para criar usuário pendente de verificação, senha, verificação de e-mail e mensagem de Outbox na mesma transação.

`UserController` fica restrito às operações autenticadas de perfil, senha e exclusão. `POST /api/users` não é endpoint de registro público. Convite de usuário e criação administrativa multitenant estão fora do escopo atual e devem ser especificados futuramente como fluxos próprios.

### NotificationCore

| Método | Rota | Descrição |
| --- | --- | --- |
| `GET` | `/api/notifications/{id}` | Consulta uma notificação pelo identificador |
| `POST` | `/api/notifications/test-email` | Envia uma notificação de teste |

As rotas de `NotificationCore` publicadas pelo Gateway exigem autenticação. Clientes browser podem usar o cookie `at` emitido pelo fluxo de sessão; clientes API/mobile podem usar `Authorization: Bearer`.

### Health checks

| Método | Rota | Descrição |
| --- | --- | --- |
| `GET` | `/health` | Health check do Gateway |
| `GET` | `/authcore/health` | Health check do AuthCore via Gateway |
| `GET` | `/notificationcore/health` | Health check do NotificationCore via Gateway |
| `GET` | `/health` | Health check direto de cada API quando acessada fora do Gateway |

### Exemplo: registrar usuário via Gateway

```bash
curl -X POST http://localhost:8080/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "Ana",
    "lastName": "Silva",
    "email": "ana.silva@example.com",
    "contact": "+5511999999999",
    "password": "Senha@123456",
    "confirmPassword": "Senha@123456"
  }'
```

Em desenvolvimento, a solicitação de verificação de e-mail é publicada pelo AuthCore e processada pelo NotificationCore quando a aplicação completa está em execução. O destino do envio depende do provedor SMTP configurado no ambiente, como Brevo.

### Exemplo: verificar e-mail

```bash
curl -X POST http://localhost:8080/api/auth/verify-email \
  -H "Content-Type: application/json" \
  -d '{
    "email": "ana.silva@example.com",
    "code": "<codigo-otp>"
  }'
```

### Exemplo: login com token para API/mobile

```bash
curl -X POST http://localhost:8080/api/auth/token/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "ana.silva@example.com",
    "password": "Senha@123456"
  }'
```

### Exemplo: consultar perfil autenticado com Bearer

```bash
curl http://localhost:8080/api/users/profile \
  -H "Authorization: Bearer <access-token>"
```

### Exemplo: consultar perfil autenticado com cookies do browser

Depois do login em `POST /api/auth/session/login`, o navegador envia os cookies automaticamente quando a chamada usa credenciais:

```javascript
await fetch("http://localhost:8080/api/users/profile", {
  method: "GET",
  credentials: "include"
});
```

### Exemplo: enviar e-mail de teste

```bash
curl -X POST http://localhost:8080/api/notifications/test-email \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <access-token>" \
  -d '{
    "recipient": "ana.silva@example.com",
    "correlationId": "manual-test-001"
  }'
```

## Testes

Execute todos os testes da solução:

```bash
./run.sh test
```

Ou diretamente com o .NET CLI:

```bash
dotnet test AuthCore.sln
```

Para executar testes por área:

```bash
dotnet test tests/AuthCore.Domain.UnitTests/AuthCore.Domain.UnitTests.csproj
dotnet test tests/AuthCore.Application.UnitTests/AuthCore.Application.UnitTests.csproj
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
dotnet test tests/NotificationCore.Domain.UnitTests/NotificationCore.Domain.UnitTests.csproj
dotnet test tests/NotificationCore.Application.UnitTests/NotificationCore.Application.UnitTests.csproj
dotnet test tests/NotificationCore.IntegrationTests/NotificationCore.IntegrationTests.csproj
dotnet test tests/Gateway.IntegrationTests/Gateway.IntegrationTests.csproj
```

Para validar o frontend:

```bash
cd src/Frontend/AuthCore.Web
pnpm lint
pnpm build
```

## Estrutura do projeto

```text
.
├── AuthCore.sln
├── run.sh
├── src
│   ├── Backend
│   │   ├── docker-compose.yml
│   │   ├── AuthCore
│   │   │   ├── AuthCore.Api
│   │   │   ├── AuthCore.Application
│   │   │   ├── AuthCore.Domain
│   │   │   └── AuthCore.Infrastructure
│   │   ├── Gateway
│   │   │   └── Gateway.Api
│   │   └── NotificationCore
│   │       ├── NotificationCore.Api
│   │       ├── NotificationCore.Application
│   │       ├── NotificationCore.Domain
│   │       └── NotificationCore.Infrastructure
│   ├── Shared
│   │   └── Messaging.Contracts
│   └── Frontend
│       └── AuthCore.Web
└── tests
    ├── AuthCore.Application.UnitTests
    ├── AuthCore.Domain.UnitTests
    ├── AuthCore.IntegrationTests
    ├── Gateway.IntegrationTests
    ├── NotificationCore.Application.UnitTests
    ├── NotificationCore.Domain.UnitTests
    └── NotificationCore.IntegrationTests
```

## Licença

Este projeto está licenciado sob a licença MIT. Consulte o arquivo [LICENSE](LICENSE) para mais detalhes.
