# Backend

Este diretorio concentra os microservicos backend do projeto. A raiz do repositorio pode conter outros clientes ou aplicacoes, como um frontend Angular, sem misturar o ciclo de build do backend.

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

Cada microservico possui sua propria solucao (`*.Service.sln`) com projetos de producao. A solucao `Backend.sln` funciona como agregadora de producao do backend. A solucao da raiz do repositorio funciona apenas como agregadora global do monorepo.

## Microservicos

| Servico | Responsabilidade | Solucao |
| --- | --- | --- |
| AuthCore | Autenticacao, sessao, credenciais e emissao de eventos de notificacao. | `src/Backend/AuthCore/AuthCore.Service.sln` |
| NotificationCore | Consumo de eventos, persistencia e envio de notificacoes. | `src/Backend/NotificationCore/NotificationCore.Service.sln` |
| Gateway | Borda de entrada HTTP para roteamento das APIs. | `src/Backend/Gateway/Gateway.Service.sln` |

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

O build padrao compila apenas os projetos de producao dos microservicos. Esse e o comando recomendado para pipeline de build quando o objetivo e validar se as APIs compilam:

```bash
./run.sh build
```

Build por microservico:

```bash
./run.sh build-authcore
./run.sh build-notificationcore
./run.sh build-gateway
```

Tambem e possivel entrar na pasta do microservico e executar `dotnet build`, porque a `*.Service.sln` local contem apenas os projetos de producao:

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

Use `build-backend` como diagnostico dos projetos de producao do backend e `build-all` como diagnostico global do monorepo. Para microservicos, prefira os builds por servico.

## Testes

Executar apenas a suite atualmente estavel:

```bash
./run.sh test
```

Executar testes por microservico. Estes comandos executam os projetos de teste diretamente, nao as `*.Service.sln`:

```bash
./run.sh test-authcore
./run.sh test-notificationcore
./run.sh test-gateway
```

Executar a validacao completa por projetos de teste dos microservicos:

```bash
./run.sh test-all
```

Observacao: `test-all` e intencionalmente mais amplo e pode revelar pendencias em suites que ainda estao sendo ajustadas. Para um gate estavel de CI, use `build` e `test`.

## Convencao para crescimento

Ao adicionar um novo microservico:

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

Evite dependencias diretas entre microservicos. A integracao entre servicos deve ser feita por contratos explicitos, mensageria ou chamadas HTTP atraves de uma borda bem definida.
