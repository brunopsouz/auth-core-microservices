# AuthCore

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-Web%20API-512BD4)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-4169E1?logo=postgresql&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-7-DC382D?logo=redis&logoColor=white)
![RabbitMQ](https://img.shields.io/badge/RabbitMQ-3-FF6600?logo=rabbitmq&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-green)

AuthCore é uma solução backend em .NET 8 para autenticação, gestão de usuários e notificações transacionais. O projeto evoluiu para uma arquitetura com microserviços, API Gateway, mensageria assíncrona e serviços organizados por camadas com influência de Clean Architecture e DDD tático.

O objetivo é oferecer um núcleo de autenticação robusto para aplicações backend, mantendo regras de negócio no domínio, casos de uso na aplicação, detalhes técnicos na infraestrutura e comunicação entre serviços por contratos explícitos.

## Sumário

- [Funcionalidades](#funcionalidades)
- [Serviços](#serviços)
- [Tecnologias](#tecnologias)
- [Arquitetura](#arquitetura)
- [Requisitos](#requisitos)
- [Instalação](#instalação)
- [Uso](#uso)
- [Configuração](#configuração)
- [Endpoints principais](#endpoints-principais)
- [Testes](#testes)
- [Estrutura do projeto](#estrutura-do-projeto)
- [Licença](#licença)

## Funcionalidades

- Registro de usuários com validação de dados e senha.
- Verificação de e-mail por código OTP.
- Login por sessão com cookie HTTP.
- Login token-based com access token JWT e refresh token.
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
- SMTP local para testes de envio de e-mail em desenvolvimento.

## Serviços

- `Gateway.Api`: API Gateway com Ocelot, autenticação JWT e roteamento para os serviços internos.
- `AuthCore.Api`: serviço de autenticação e usuários.
- `NotificationCore.Api`: serviço de notificações transacionais, templates e envio de e-mail.
- `BuildingBlocks.Messaging.Contracts`: contratos compartilhados de mensageria e utilitários de payload sensível.

## Tecnologias

- .NET 8
- ASP.NET Core Web API
- Ocelot
- PostgreSQL 17
- Redis 7
- RabbitMQ 3
- SMTP4Dev
- Docker e Docker Compose
- Npgsql
- FluentMigrator
- BCrypt.Net
- JWT Bearer Authentication
- xUnit
- Swagger/OpenAPI

## Arquitetura

A solução combina microserviços e organização em camadas dentro de cada serviço de negócio:

```mermaid
flowchart TD
    Client[Cliente HTTP] --> Gateway[Gateway.Api]
    Gateway --> AuthApi[AuthCore.Api]
    Gateway --> NotificationApi[NotificationCore.Api]

    AuthApi --> AuthApplication[AuthCore.Application]
    AuthApi --> AuthInfrastructure[AuthCore.Infrastructure]
    AuthApplication --> AuthDomain[AuthCore.Domain]
    AuthInfrastructure --> AuthDomain
    AuthInfrastructure --> Contracts[BuildingBlocks.Messaging.Contracts]

    NotificationApi --> NotificationApplication[NotificationCore.Application]
    NotificationApi --> NotificationInfrastructure[NotificationCore.Infrastructure]
    NotificationApplication --> NotificationDomain[NotificationCore.Domain]
    NotificationApplication --> Contracts
    NotificationInfrastructure --> NotificationDomain
    NotificationInfrastructure --> Contracts

    AuthInfrastructure --> RabbitMQ[(RabbitMQ)]
    RabbitMQ --> NotificationInfrastructure
    AuthInfrastructure --> AuthPostgres[(AuthCore PostgreSQL)]
    AuthInfrastructure --> Redis[(Redis)]
    NotificationInfrastructure --> NotificationPostgres[(NotificationCore PostgreSQL)]
    NotificationInfrastructure --> SMTP[SMTP4Dev/SMTP]
```

Responsabilidades principais:

- `Gateway.Api`: borda pública em Docker Compose, roteamento, rate limiting e validação JWT para rotas protegidas.
- `AuthCore.Api`: controllers HTTP, contratos JSON, autenticação, autorização, Swagger e health checks.
- `AuthCore.Application`: orquestração dos casos de uso de autenticação e usuários.
- `AuthCore.Domain`: agregados, entidades, value objects, invariantes, eventos e contratos centrais de autenticação.
- `AuthCore.Infrastructure`: persistência PostgreSQL, Redis, criptografia, tokens, migrações, Outbox e publicação RabbitMQ.
- `NotificationCore.Api`: controllers HTTP administrativos, contratos JSON, Swagger e health checks.
- `NotificationCore.Application`: orquestração de consultas, busca e solicitações de envio de notificações.
- `NotificationCore.Domain`: entidades, value objects, enums e regras de notificação.
- `NotificationCore.Infrastructure`: persistência PostgreSQL, consumo RabbitMQ, Inbox, templates, renderização e envio SMTP.
- `BuildingBlocks.Messaging.Contracts`: mensagens compartilhadas entre serviços.
- `tests`: testes unitários de domínio, aplicação e testes de integração por serviço.

## Requisitos

Para executar localmente:

- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0)
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

Antes de executar o projeto pela primeira vez, crie o arquivo local a partir do modelo e preencha os valores vazios quando necessário:

```bash
cp src/Backend/.env.development.example src/Backend/.env.development
```

Serviços padrão em desenvolvimento:

| Serviço | Host | Porta |
| --- | --- | --- |
| Gateway Docker | `localhost` | `8080` |
| AuthCore Docker | `localhost` | `8081` |
| AuthCore local | `localhost` | `5012` |
| PostgreSQL AuthCore | `localhost` | `5432` |
| PostgreSQL NotificationCore | `localhost` | `5433` |
| Redis | `localhost` | `6379` |
| RabbitMQ | `localhost` | `5672` |
| RabbitMQ Management | `localhost` | `15672` |
| SMTP local | `localhost` | `1025` |
| SMTP4Dev UI | `localhost` | `1080` |

Credenciais, senhas e chave de assinatura JWT devem ficar no `.env.development` local ou no mecanismo de segredos do ambiente de deploy. O `docker-compose.yml` apenas referencia essas variáveis.

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
| `GET` | `/api/users/profile` | Consulta perfil autenticado |
| `PUT` | `/api/users/profile` | Atualiza perfil autenticado |
| `PUT` | `/api/users/change-password` | Altera senha |
| `DELETE` | `/api/users` | Exclui usuário autenticado |

### NotificationCore

| Método | Rota | Descrição |
| --- | --- | --- |
| `GET` | `/api/notifications/{id}` | Consulta uma notificação pelo identificador |
| `POST` | `/api/notifications/test-email` | Envia uma notificação de teste |

As rotas administrativas `GET /api/notifications` e `GET /api/templates` existem no `NotificationCore.Api`, mas não possuem rota base publicada no Gateway atual. Para usá-las via Gateway, adicione rotas explícitas no `ocelot.json` ou exponha o serviço de notificação diretamente em um ambiente controlado.

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

Em desenvolvimento, a solicitação de verificação de e-mail é publicada pelo AuthCore e processada pelo NotificationCore quando a aplicação completa está em execução. As mensagens enviadas por SMTP local podem ser consultadas na UI do SMTP4Dev:

```text
http://localhost:1080
```

### Exemplo: verificar e-mail

```bash
curl -X POST http://localhost:8080/api/auth/verify-email \
  -H "Content-Type: application/json" \
  -d '{
    "email": "ana.silva@example.com",
    "code": "<codigo-otp>"
  }'
```

### Exemplo: login com token

```bash
curl -X POST http://localhost:8080/api/auth/token/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "ana.silva@example.com",
    "password": "Senha@123456"
  }'
```

### Exemplo: consultar perfil autenticado

```bash
curl http://localhost:8080/api/users/profile \
  -H "Authorization: Bearer <access-token>"
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
│   ├── BuildingBlocks
│   │   └── BuildingBlocks.Messaging.Contracts
│   └── Frontend
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
