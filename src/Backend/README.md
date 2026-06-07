# Backend

Este diretorio concentra os servicos backend do projeto. A raiz do repositorio pode conter outros clientes ou aplicacoes, como um frontend Angular, sem misturar o ciclo de build do backend.

## Estrutura

```text
src/Backend
|-- AuthCore
|   |-- AuthCore.Api
|   |-- AuthCore.Application
|   |-- AuthCore.Domain
|   |-- AuthCore.Infrastructure
|   `-- AuthCore.Service.sln
|-- Backend.sln
|-- Gateway
|   |-- Gateway.Api
|   `-- Gateway.Service.sln
|-- NotificationCore
|   |-- NotificationCore.Api
|   |-- NotificationCore.Application
|   |-- NotificationCore.Domain
|   |-- NotificationCore.Infrastructure
|   `-- NotificationCore.Service.sln
|-- docker-compose.yml
`-- .env.development.example
```

Cada servico possui sua propria solucao (`*.Service.sln`) com projetos de producao. A solucao `Backend.sln` funciona como agregadora de producao do backend. A solucao da raiz do repositorio funciona apenas como agregadora global do monorepo.

## Servicos

| Servico | Responsabilidade | Solucao |
| --- | --- | --- |
| AuthCore | Autenticacao, sessao, credenciais e emissao de eventos de notificacao. | `src/Backend/AuthCore/AuthCore.Service.sln` |
| NotificationCore | Consumo de eventos, persistencia e envio de notificacoes. | `src/Backend/NotificationCore/NotificationCore.Service.sln` |
| Gateway | Borda de entrada HTTP, validacao JWT, suporte a JWT via cookie HttpOnly e roteamento das APIs. | `src/Backend/Gateway/Gateway.Service.sln` |

## Rotas canonicas do AuthCore

Quando a aplicacao completa roda via Docker Compose, o Gateway em `http://localhost:8080` e a borda publica recomendada.

Responsabilidades atuais:

- `AuthController`: registro publico em `POST /api/auth/register`, verificacao em `POST /api/auth/verify-email` e reenvio em `POST /api/auth/resend-verification`.
- `SessionAuthController`: autenticacao e gerenciamento de sessao por cookie em `api/auth/session/...`.
- `TokenAuthController`: login JWT, refresh token e logout token-based em `api/auth/token/...`.
- `UserController`: operacoes autenticadas de perfil, senha e exclusao em `GET /api/users/profile`, `PUT /api/users/profile`, `PUT /api/users/change-password` e `DELETE /api/users`.

`RegisterUserUseCase` representa o autocadastro publico usado por `POST /api/auth/register`. `POST /api/users` nao e contrato de registro publico. Convite de usuario e criacao administrativa multitenant estao fora do escopo atual e devem ser especificados futuramente em fluxos proprios.

## Autenticacao na borda

O backend suporta dois fluxos principais:

- Browser/PWA: `POST /api/auth/session/login` cria uma sessao server-side no AuthCore e emite cookies `sid`, `at` e `XSRF-TOKEN`. O Gateway aceita o JWT curto do cookie `at`, valida o token de forma stateless e encaminha `Authorization: Bearer` para o servico downstream.
- API/mobile: `POST /api/auth/token/login` retorna access token e refresh token no corpo da resposta. O cliente envia `Authorization: Bearer <access-token>` nas rotas protegidas.

Quando `Authorization: Bearer` esta presente, ele tem prioridade sobre o cookie `at`. Mutacoes autenticadas via cookie (`POST`, `PUT`, `PATCH`, `DELETE`) exigem `X-CSRF-TOKEN` valido. Requisicoes autenticadas por Bearer nao exigem CSRF.

As rotas `/api/auth/...` permanecem sob responsabilidade do AuthCore, inclusive login, refresh, logout, sessao por cookie e validacao CSRF propria dessas operacoes.

## Solucoes

| Solucao | Uso recomendado |
| --- | --- |
| `src/Backend/AuthCore/AuthCore.Service.sln` | Desenvolvimento, build e pipeline de producao do AuthCore. |
| `src/Backend/NotificationCore/NotificationCore.Service.sln` | Desenvolvimento, build e pipeline de producao do NotificationCore. |
| `src/Backend/Gateway/Gateway.Service.sln` | Desenvolvimento, build e pipeline de producao do Gateway. |
| `src/Backend/Backend.sln` | Visao agregada dos projetos de producao do backend para abrir todos os servicos ou validar mudancas transversais. |
| `AuthCore.sln` | Visao global do repositorio quando for necessario validar o monorepo inteiro. |

## Pre-requisitos

- .NET SDK 10.
- Docker e Docker Compose.
- Bash para usar `run.sh`.

Os comandos `./run.sh` devem ser executados a partir da raiz do repositorio:

```bash
cd D:/Projects/auth-core-microservices
```

Em Windows sem Bash disponivel, use os comandos `dotnet` diretamente para build/test. Para infraestrutura e execucao via Docker, use os comandos `docker compose` documentados abaixo.

## Configuracao local

Crie o arquivo de ambiente local a partir do exemplo:

```bash
cp src/Backend/.env.development.example src/Backend/.env.development
```

O arquivo `.env.development` e usado pelo `docker-compose.yml` e pelo `run.sh`.

## Execucao local

Subir infraestrutura e executar o `AuthCore.Api` localmente:

```bash
./run.sh dev
```

Executar com hot reload:

```bash
./run.sh watch
```

Subir apenas infraestrutura:

```bash
./run.sh infra
```

Subir a aplicacao completa via Docker Compose:

```bash
./run.sh docker
```

Encerrar containers:

```bash
./run.sh down
```

Comandos equivalentes sem Bash para Docker Compose:

```bash
docker compose --env-file src/Backend/.env.development -f src/Backend/docker-compose.yml up -d authcore-postgres notificationcore-postgres redis rabbitmq smtp
docker compose --env-file src/Backend/.env.development -f src/Backend/docker-compose.yml up --build
docker compose --env-file src/Backend/.env.development -f src/Backend/docker-compose.yml down --remove-orphans
```

Sem Bash, a execucao local direta da API tambem exige exportar as variaveis esperadas pelo projeto antes de chamar `dotnet run`. Para esse fluxo, prefira usar Bash ou Docker Compose.

## Builds separados

O build padrao compila apenas os projetos de producao dos servicos. Esse e o comando recomendado para pipeline de build quando o objetivo e validar se as APIs compilam:

```bash
./run.sh build
```

Build por servico:

```bash
./run.sh build-authcore
./run.sh build-notificationcore
./run.sh build-gateway
```

Tambem e possivel entrar na pasta do servico e executar `dotnet build`, porque a `*.Service.sln` local contem apenas os projetos de producao:

```bash
cd src/Backend/AuthCore
dotnet build
```

Comandos equivalentes sem Bash:

```bash
dotnet build src/Backend/AuthCore/AuthCore.Api/AuthCore.Api.csproj -c Release
dotnet build src/Backend/NotificationCore/NotificationCore.Api/NotificationCore.Api.csproj -c Release
dotnet build src/Backend/Gateway/Gateway.Api/Gateway.Api.csproj -c Release
```

Para validar a solucao agregadora da raiz:

```bash
./run.sh build-all
```

Para validar a solucao agregadora de producao do backend:

```bash
./run.sh build-backend
```

Comando equivalente sem Bash:

```bash
dotnet build src/Backend/Backend.sln -c Release
```

Use `build-backend` como diagnostico dos projetos de producao do backend e `build-all` como diagnostico global do monorepo. Para servicos especificos, prefira os builds por servico.

## Testes

Executar apenas a suite atualmente estavel:

```bash
./run.sh test
```

Executar testes por servico. Estes comandos executam os projetos de teste diretamente, nao as `*.Service.sln`:

```bash
./run.sh test-authcore
./run.sh test-notificationcore
./run.sh test-gateway
```

Executar a validacao completa por projetos de teste dos servicos:

```bash
./run.sh test-all
```

Observacao: `test-all` e intencionalmente mais amplo e pode revelar pendencias em suites que ainda estao sendo ajustadas. Para um gate estavel de CI, use `build` e `test`.

## Convencao para crescimento

Ao adicionar um novo servico:

1. Crie uma pasta em `src/Backend/<NomeDoServico>`.
2. Mantenha projetos separados por camada quando o servico tiver dominio proprio: `Api`, `Application`, `Domain` e `Infrastructure`.
3. Crie uma solucao local `NomeDoServico.Service.sln`.
4. Adicione a solucao local apenas os projetos de producao do servico e dependencias compartilhadas necessarias.
5. Adicione comandos proprios no `run.sh` para build e teste do novo servico.
6. Inclua o servico em `src/Backend/Backend.sln` para manter a visao agregada do backend.
7. So inclua o servico na solucao agregadora da raiz quando fizer sentido validar o repositorio inteiro.

## Direcao arquitetural

Os servicos devem preservar a separacao de responsabilidades:

- `Api` adapta HTTP e chama casos de uso.
- `Application` orquestra casos de uso.
- `Domain` concentra regras de negocio e invariantes.
- `Infrastructure` implementa persistencia, mensageria, cache, migracoes e integracoes tecnicas.

Evite dependencias diretas entre servicos. A integracao entre servicos deve ser feita por contratos explicitos, mensageria ou chamadas HTTP atraves de uma borda bem definida.
