<div align="center">

# Back-end

<p>
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white">
  <img alt="PostgreSQL 17" src="https://img.shields.io/badge/PostgreSQL-17-4169E1?style=for-the-badge&logo=postgresql&logoColor=white">
  <img alt="Redis 7" src="https://img.shields.io/badge/Redis-7-DC382D?style=for-the-badge&logo=redis&logoColor=white">
  <img alt="RabbitMQ 3" src="https://img.shields.io/badge/RabbitMQ-3-FF6600?style=for-the-badge&logo=rabbitmq&logoColor=white">
  <img alt="Docker Compose" src="https://img.shields.io/badge/Docker_Compose-local-2496ED?style=for-the-badge&logo=docker&logoColor=white">
</p>

</div>

Este diretório concentra os serviços backend do projeto. A raiz do repositório também contém outros módulos do monorepo, como o frontend `AuthCore.Web`, sem misturar o ciclo de build dos serviços .NET.

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

## Padroes de engenharia

Os servicos backend seguem Clean Architecture, DDD tatico e principios SOLID.

Na pratica:

- controllers permanecem finos e nao acessam infraestrutura diretamente quando existe fluxo de aplicacao;
- use cases orquestram o fluxo e dependem de contratos pequenos, orientados ao consumidor;
- dominio concentra regras de negocio, invariantes e value objects;
- infraestrutura implementa detalhes tecnicos por meio de Npgsql, Redis, RabbitMQ, SMTP, migrations e providers concretos;
- interfaces devem ser criadas por necessidade real de consumidor, teste, infraestrutura ou variacao de comportamento.
- abstracoes consumidas entre camadas podem ser publicas, mas implementacoes concretas devem ser `internal` por padrao.

O guia completo de SOLID fica em `../../docs/agents/solid-guidelines.md` e complementa os documentos de padrao em `../../docs/agents/`.

## Papel do Gateway

O `Gateway.Api` e a borda HTTP recomendada quando a aplicacao roda completa via Docker Compose. Ele centraliza preocupacoes de entrada sem absorver regra de negocio dos servicos internos.

| Responsabilidade | Papel |
| --- | --- |
| Roteamento | Encaminha chamadas externas para AuthCore e NotificationCore conforme `ocelot.json`. |
| Autenticacao na borda | Valida JWT para rotas protegidas e aceita token via header `Authorization` ou cookie `HttpOnly` no fluxo web. |
| Protecao HTTP | Aplica rate limiting e suporte a CSRF para mutacoes autenticadas por cookie. |
| Composicao externa | Mantem uma entrada publica unica para clientes, preservando AuthCore e NotificationCore como servicos internos. |

O Gateway deve permanecer fino: ele adapta a borda, valida preocupacoes transversais e roteia chamadas. Regras de autenticacao, usuario, sessao e notificacao continuam nos contextos proprietarios.

## Padrao de camadas

AuthCore e NotificationCore seguem o mesmo desenho em camadas. Cada camada tem um motivo principal para mudar e depende apenas das camadas permitidas pelo contexto.

| Camada | Papel | Exemplos |
| --- | --- | --- |
| Api | Recebe HTTP, mapeia contratos JSON e chama casos de uso. | Controllers, requests, responses, autenticacao HTTP, Swagger e health checks. |
| Application | Orquestra casos de uso, transacoes e consome contratos definidos nas camadas permitidas. | Commands, queries, use cases, results e interfaces consumidas pela aplicacao. |
| Domain | Concentra regras de negocio, invariantes, entidades, agregados e value objects. | `User`, `Password`, `Notification`, eventos e contratos centrais. |
| Infrastructure | Implementa detalhes tecnicos sem mover regra de negocio. | Repositorios Npgsql, migrations, Redis, RabbitMQ, SMTP, JWT e providers. |

Fluxo de dependencia esperado por contexto:

```text
Api -> Application
Api -> Infrastructure
Application -> Domain
Infrastructure -> Domain
```

## Workers e processamento assincrono

Os workers ficam na composicao dos servicos e executam processamento de background sem transformar a API em lugar de regra de negocio.

| Worker | Contexto | Papel |
| --- | --- | --- |
| `OutboxHostedService` | AuthCore | Publica mensagens pendentes geradas junto com transacoes de negocio. |
| `RabbitMqNotificationConsumerHostedService` | NotificationCore | Consome mensagens do RabbitMQ, registra recebimento e evita processamento duplicado via Inbox. |
| `NotificationDispatcherHostedService` | NotificationCore | Renderiza templates e envia notificacoes por provedores como SMTP. |

O fluxo assincrono principal e: AuthCore grava o estado e a mensagem de Outbox na mesma transacao, o worker publica no RabbitMQ, NotificationCore consome a mensagem, persiste a notificacao e executa o envio.

## Banco e infraestrutura

Cada contexto possui seu proprio banco PostgreSQL em desenvolvimento, preservando isolamento entre AuthCore e NotificationCore. Redis, RabbitMQ e o servico SMTP configurado por ambiente suportam execucao local e integracoes tecnicas.

| Recurso | Uso |
| --- | --- |
| PostgreSQL AuthCore | Persistencia de usuarios, credenciais, sessoes, tokens, verificacoes e outbox. |
| PostgreSQL NotificationCore | Persistencia de notificacoes, templates, inbox e historico de processamento. |
| Redis | Armazenamento tecnico para sessoes, tokens ou controles de autenticacao quando configurado. |
| RabbitMQ | Transporte de mensagens assincronas entre AuthCore e NotificationCore. |
| Servico SMTP | Envio real de e-mails por provedor configurado via variaveis `SMTP_HOST`, `SMTP_PORT`, `SMTP_USERNAME` e demais chaves SMTP do `.env.development`. |
| Docker Compose | Sobe bancos, Redis, RabbitMQ, Gateway e APIs conforme o modo de execucao. |
| OpenTelemetry Collector, Prometheus, Jaeger, Loki e Grafana | Stack local opcional para receber, armazenar e visualizar métricas, traces e logs via profile `observability`. |
| FluentMigrator | Versiona e aplica mudancas de schema de cada contexto. |

O guia operacional de observabilidade fica em [../../docs/observability.md](../../docs/observability.md).

## Servicos

| Servico | Responsabilidade | Solucao |
| --- | --- | --- |
| AuthCore | Autenticacao, sessao, credenciais e emissao de eventos de notificacao. | `src/Backend/AuthCore/AuthCore.Service.sln` |
| NotificationCore | Consumo de eventos, persistencia e envio de notificacoes. | `src/Backend/NotificationCore/NotificationCore.Service.sln` |
| Gateway | Borda de entrada HTTP, validacao JWT, suporte a JWT via cookie HttpOnly e roteamento das APIs. | `src/Backend/Gateway/Gateway.Service.sln` |

## CI do Backend

O Backend CI executa restore de dependências, build, testes, publish de artifacts e Docker build das APIs. A publicação de imagens Docker fica no workflow Docker Publish.

Imagens backend publicadas no GHCR:

- `ghcr.io/brunopsouz/authcore-api`
- `ghcr.io/brunopsouz/notificationcore-api`
- `ghcr.io/brunopsouz/gateway-api`

O processo atual publica imagens para uso futuro, mas ainda não configura deploy automático, staging ou produção. Consulte o processo detalhado em [../../docs/ci-cd/release-process.md](../../docs/ci-cd/release-process.md).

## Rotas canonicas do AuthCore

Quando a aplicacao completa roda via Docker Compose, o Gateway em `http://localhost:8080` e a borda publica recomendada.

Responsabilidades atuais:

- `AuthController`: início de registro em `POST /api/auth/register`, conclusão com OTP e senha em `POST /api/auth/complete-registration`, verificação em `POST /api/auth/verify-email` e reenvio em `POST /api/auth/resend-verification`.
- `SessionAuthController`: autenticacao e gerenciamento de sessao por cookie em `api/auth/session/...`.
- `TokenAuthController`: login JWT, refresh token e logout token-based em `api/auth/token/...`.
- `UserController`: operacoes autenticadas de perfil, senha e exclusao em `GET /api/users/profile`, `PUT /api/users/profile`, `PUT /api/users/change-password` e `DELETE /api/users`.

`RegisterUserUseCase` inicia o autocadastro público usado por `POST /api/auth/register`. `CompleteRegistrationUseCase` conclui o cadastro com OTP e senha em `POST /api/auth/complete-registration`. `POST /api/users` não é contrato de registro público. Convite de usuário e criação administrativa multitenant estão fora do escopo atual e devem ser especificados futuramente em fluxos próprios.

### AuthCore

| Método | Rota | Descrição |
| --- | --- | --- |
| `POST` | `/api/auth/register` | Inicia registro de usuário pendente de verificação |
| `POST` | `/api/auth/complete-registration` | Conclui registro com código OTP e senha |
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
| `GET` | `/api/auth/external/google/callback` | Recebe callback técnico do Google |
| `GET` | `/api/auth/external/google/complete` | Conclui login Google e redireciona com sessão autenticada |
| `GET` | `/api/users/profile` | Consulta perfil autenticado |
| `PUT` | `/api/users/profile` | Atualiza perfil autenticado |
| `PUT` | `/api/users/change-password` | Altera senha |
| `DELETE` | `/api/users` | Exclui usuário autenticado |

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

### Exemplos de chamadas

Iniciar registro de usuário via Gateway:

```bash
curl -X POST http://localhost:8080/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "Ana",
    "lastName": "Silva",
    "email": "ana.silva@example.com",
    "contact": "+5511999999999"
  }'
```

Em desenvolvimento, a solicitação de verificação de e-mail é publicada pelo AuthCore e processada pelo NotificationCore quando a aplicação completa está em execução. O destino do envio depende do provedor SMTP configurado no ambiente, como Brevo.

Concluir registro com OTP e senha:

```bash
curl -X POST http://localhost:8080/api/auth/complete-registration \
  -H "Content-Type: application/json" \
  -d '{
    "email": "ana.silva@example.com",
    "code": "<codigo-otp>",
    "password": "Senha@123456",
    "confirmPassword": "Senha@123456"
  }'
```

Verificar e-mail:

```bash
curl -X POST http://localhost:8080/api/auth/verify-email \
  -H "Content-Type: application/json" \
  -d '{
    "email": "ana.silva@example.com",
    "code": "<codigo-otp>"
  }'
```

Login com token para API/mobile:

```bash
curl -X POST http://localhost:8080/api/auth/token/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "ana.silva@example.com",
    "password": "Senha@123456"
  }'
```

Consultar perfil autenticado com Bearer:

```bash
curl http://localhost:8080/api/users/profile \
  -H "Authorization: Bearer <access-token>"
```

Consultar perfil autenticado com cookies do browser:

```javascript
await fetch("http://localhost:8080/api/users/profile", {
  method: "GET",
  credentials: "include"
});
```

Enviar e-mail de teste:

```bash
curl -X POST http://localhost:8080/api/notifications/test-email \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <access-token>" \
  -d '{
    "recipient": "ana.silva@example.com",
    "correlationId": "manual-test-001"
  }'
```

## Autenticacao na borda

O backend suporta autenticacao web por sessao, autenticacao token-based para API e mobile e login com Google no AuthCore.

As rotas publicas de autenticacao permanecem sob responsabilidade do AuthCore e sao expostas conforme a configuracao do ambiente. Credenciais Google OAuth devem permanecer fora de arquivos versionados.

Para detalhes operacionais do login com Google, consulte `../../docs/features/google-login/`.

## Soluções

| Solução | Uso recomendado |
| --- | --- |
| `src/Backend/AuthCore/AuthCore.Service.sln` | Desenvolvimento e build isolado do AuthCore. |
| `src/Backend/NotificationCore/NotificationCore.Service.sln` | Desenvolvimento e build isolado do NotificationCore. |
| `src/Backend/Gateway/Gateway.Service.sln` | Desenvolvimento e build isolado do Gateway. |
| `src/Backend/Backend.sln` | Visão agregada dos projetos de produção do backend para abrir todos os serviços ou validar mudanças transversais. |
| `AuthCore.sln` | Visão global do repositório usada para validação global e pelo Backend CI atual. |

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

O arquivo `.env.development` e usado pelo `docker-compose.yml`, pelo `run.sh` e pelo perfil `AuthCore.Api Launch` do VS Code. Preencha nele senhas, chaves JWT/CSRF, credenciais SMTP e as credenciais Google OAuth.

As configuracoes consumidas diretamente pelo .NET usam `__` para representar a hierarquia das secoes. Por exemplo, `AUTH__CSRF__SIGNINGKEY` corresponde a `Auth:Csrf:SigningKey`. O mesmo nome e reutilizado pelo VS Code, pelo script e pelo Docker Compose, sem traducao intermediaria.

Os arquivos `appsettings.Development.json` sao versionados e devem conter apenas configuracoes nao sensiveis, como URLs, portas, timeouts e nomes logicos. Nao coloque credenciais, tokens ou chaves nesses arquivos.

Depois de preencher o `.env.development`, use `./run.sh dev`, `./run.sh watch`, `./run.sh docker` ou execute o perfil `AuthCore.Api Launch` pelo VS Code.

Se um segredo tiver sido salvo anteriormente em arquivo versionado, revogue-o no provedor antes de gerar e configurar o substituto.

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

Subir o ambiente completo com a stack local de observabilidade:

```bash
docker compose --env-file src/Backend/.env.development -f src/Backend/docker-compose.yml --profile observability up -d --build
```

O Collector recebe OTLP por gRPC em `localhost:4317` e HTTP em `localhost:4318`, expõe métricas em `localhost:8889/metrics`, envia traces para Jaeger e envia logs para Loki. O Prometheus fica em `localhost:9090`, o Jaeger em `localhost:16686`, o Loki em `localhost:3100` e o Grafana em `localhost:3000`. As credenciais do Grafana devem ser preenchidas apenas no `.env.development` local. As APIs enviam para `OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317` quando `OBSERVABILITY__OTLPENABLED=true`. A stack é operacional e não deve virar dependência de readiness das APIs.

Para manter o exporter OTLP opcional, o exemplo de ambiente deixa `OBSERVABILITY__OTLPENABLED=false`. Altere para `true` no `.env.development` apenas quando for executar com `--profile observability`.

Encerrar containers:

```bash
./run.sh down
```
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

Executar a suíte padrão:

```bash
./run.sh test
```

Executar testes por serviço. Estes comandos executam os projetos de teste diretamente, não as `*.Service.sln`:

```bash
./run.sh test-authcore
./run.sh test-notificationcore
./run.sh test-gateway
```

Executar a validação completa por projetos de teste dos serviços:

```bash
./run.sh test-all
```

Use `test` para a suíte padrão e `test-all` quando precisar validar todos os projetos de teste dos serviços.
