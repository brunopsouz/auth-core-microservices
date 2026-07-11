# Plano de implementação de observabilidade

> Estado: planejamento; nenhuma implementação de observabilidade foi realizada nesta etapa.  
> Base analisada: repositório em 10/07/2026, .NET 10, OpenTelemetry .NET estável 1.16.0 e Npgsql 10.0.2.  
> Objetivo: permitir execução task a task sem reinterpretação da arquitetura.

# 1. Estado atual

## 1.1 Estrutura e composição

| Área | Estado comprovado | Arquivos/projetos relacionados | Ação |
|---|---|---|---|
| AuthCore | Quatro camadas: Api, Application, Domain e Infrastructure. | `src/Backend/AuthCore/*`; `AuthCore.sln` | Preservar. Observabilidade fica em Api/Infrastructure, nunca em Domain. |
| NotificationCore | Quatro camadas equivalentes, com workers RabbitMQ e dispatcher. | `src/Backend/NotificationCore/*` | Preservar. Instrumentar API, worker e integrações técnicas. |
| Gateway | Processo ASP.NET Core/Ocelot fino, sem Application/Domain. | `src/Backend/Gateway/Gateway.Api` | Instrumentar entrada HTTP, encaminhamento e saúde; não absorver regra de negócio. |
| Compartilhamento | Existe apenas `src/Shared/Messaging.Contracts`, usado pelos dois serviços. Não existe building block de observabilidade. | `src/Shared/Messaging.Contracts` | Criar um building block técnico pequeno porque as três APIs precisam das mesmas opções, exporters e middlewares. Não colocar isso em `Messaging.Contracts`. |
| Testes | Há testes de domínio, aplicação e integração por serviço; não há projeto específico de observabilidade. | `tests/AuthCore.*`, `tests/NotificationCore.*`, `tests/Gateway.IntegrationTests` | Usar os testes de integração existentes; não criar outro projeto de testes inicialmente. |

## 1.2 Logging e tratamento de erros

- As três aplicações usam `Microsoft.Extensions.Logging`; não há Serilog nem outro provider externo.
- `AuthCore.Api` e `NotificationCore.Api` configuram apenas níveis em `appsettings*.json`. O Gateway mantém `Logging` em `ocelot.json`, enquanto `appsettings.json` está vazio.
- Não há configuração de logs JSON, `ActivityTrackingOptions`, OpenTelemetry Logging ou exporter OTLP.
- Há logs estruturados pontuais em:
  - `AuthCore.Api/Controllers/TokenAuthController.cs` e `SessionAuthController.cs`;
  - `AuthCore.Api/Authentication/GoogleExternalAuthenticationFlow.cs`;
  - `AuthCore.Infrastructure/Services/Messaging/OutboxProcessor.cs` e `RabbitMqNotificationRequestPublisher.cs`;
  - workers, consumer RabbitMQ e `SmtpEmailProvider` do NotificationCore;
  - middleware de cookie do Gateway.
- Há `BeginScope` no publisher/consumer RabbitMQ, outbox e SMTP, mas não existe escopo HTTP uniforme com serviço, ambiente, correlation ID, trace ID, span ID, rota, método e usuário.
- Alguns logs chamam `HttpContext.TraceIdentifier` de `TraceId`; ele não é o `Activity.TraceId` W3C e precisa ser renomeado/substituído.
- `NotificationCore.Api/Exceptions/ApiExceptionHandler.cs` usa `SensitivePayloadSanitizer`; `AuthCore.Api/Exceptions/ApiExceptionHandler.cs` ainda envia a exceção diretamente ao logger. Ambos devem seguir a mesma política de dados sensíveis e tamanho.
- `src/Shared/Messaging.Contracts/Security/SensitivePayloadSanitizer.cs` já protege payloads de mensageria. Ele pode continuar sendo usado nessa finalidade; não deve virar uma dependência genérica do building block de observabilidade.

## 1.3 Middlewares, correlation ID e propagação

- AuthCore: forwarded headers, exception handler, CORS, authentication e authorization em `AuthCore.Api/Program.cs`.
- NotificationCore: exception handler em `NotificationCore.Api/Program.cs`.
- Gateway: forwarded headers, `DownstreamForwardedHeadersMiddleware`, `CookieAccessTokenGatewayMiddleware`, identidade de rate limit e Ocelot em `Gateway.Api/Program.cs`.
- Não há middleware HTTP de correlation ID nem uso sistemático de `X-Correlation-Id`.
- O contrato `SendTransactionalNotificationRequested` já contém `CorrelationId`; o publisher o copia para `IBasicProperties.CorrelationId`, o consumidor o recupera, o NotificationCore persiste e encaminha o valor ao SMTP.
- A correlação assíncrona não preserva hoje a requisição HTTP: `EmailVerificationNotificationOutboxFactory` cria um GUID novo. O fallback legado de `OutboxProcessor` usa o ID da outbox.
- Não há propagação de `traceparent`/`tracestate` em headers RabbitMQ nem criação de span consumidor com o contexto remoto como pai.

## 1.4 HttpClient

- A busca por `AddHttpClient`, `IHttpClientFactory`, `HttpClient` e `DelegatingHandler` não encontrou uso direto em `src/Backend`.
- O Gateway/Ocelot e o handler Google realizam HTTP internamente. A instrumentação `HttpClient` deve ser habilitada nos três processos para capturar essas chamadas quando expostas por `System.Net.Http`, sem criar clientes artificiais.
- O Gateway deve inserir `X-Correlation-Id` antes do Ocelot, para que o header seja encaminhado. Futuros clientes registrados por `IHttpClientFactory` devem usar um `DelegatingHandler` compartilhado.

## 1.5 Dependências técnicas

| Dependência | Uso atual | Instrumentação atual | Lacuna |
|---|---|---|---|
| PostgreSQL/Npgsql 10.0.2 | AuthCore e NotificationCore; SQL explícito, `NpgsqlDataSource`, sessão e UoW. | `DatabaseMetrics` mede aquisição, lease, transação e falha em ambos. | Não há provider/exporter nem tracing Npgsql. O pool usa por padrão a connection string como nome, risco de expor credenciais ao coletar métricas. |
| Redis/StackExchange.Redis 2.8.31 | AuthCore: sessões, rate limit e Data Protection. | Health check apenas. | Sem métricas/spans. A instrumentação OTel disponível é beta/pré-release; não será adotada na base inicial. |
| RabbitMQ.Client 6.8.1 | AuthCore publica pela outbox; NotificationCore consome; DLQ configurada. | Logs e correlation property; métricas de outbox não equivalem a publicação/consumo. | Sem trace context distribuído, spans de producer/consumer e contadores de publish/consume/requeue/DLQ. |
| SMTP/MailKit 4.16.0 | NotificationCore envia e-mail. | Logs, correlação no header do e-mail e histograma de duração; contadores agregados do dispatcher. | Sem span SMTP específico, contador de resultado por provedor e health check. |

## 1.6 Health checks

- AuthCore registra `postgresql`, `redis` e `outbox`; não registra RabbitMQ.
- NotificationCore registra `postgresql`, `rabbitmq` e `dispatcher`; não registra SMTP.
- Gateway registra somente o health service sem checks externos.
- Cada processo expõe apenas `/health`; não existem tags ou endpoints separados de liveness/readiness.
- Docker possui health checks nos containers PostgreSQL, Redis e RabbitMQ, mas não nas APIs.

## 1.7 Métricas e traces existentes

- Existem `Meter`s customizados, mas nenhum `MeterProvider`; portanto, não são coletados/exportados hoje:
  - `AuthCore.Database`, `AuthCore.Outbox`, `AuthCore.ExternalAuthentication`;
  - `NotificationCore.Database`, `NotificationCore.Notifications`.
- Os nomes misturam snake case Prometheus, pontos e sufixo `.ms`; faltam unidade e descrição em vários instrumentos.
- Não há `ActivitySource`, `TracerProvider`, instrumentação ASP.NET Core/HttpClient/Npgsql, sampling ou exporter.
- Métricas nativas do ASP.NET Core e do Npgsql não estão registradas em provider algum.

## 1.8 Configuração e ambiente

- Configuração local usa `appsettings.json`, `appsettings.Development.json`, `src/Backend/.env.development.example` e `src/Backend/docker-compose.yml`.
- Segredos ficam vazios nos arquivos versionados e são injetados por variáveis; esse padrão deve ser preservado.
- Não há configuração OpenTelemetry nem Collector.
- `docker-compose.yml` contém AuthCore, NotificationCore, Gateway, dois PostgreSQL, Redis e RabbitMQ em uma rede comum.

## 1.9 Fluxos de autenticação já observáveis

- Login token e browser, logout/revogação e rate limit possuem logs em controllers.
- Login Google possui contadores, duração e logs, porém usa `HttpContext.TraceIdentifier` como se fosse trace ID.
- Registro, verificação/reenvio de e-mail, refresh token e várias falhas de login não têm um conjunto coerente de métricas de negócio.
- `ApiExceptionHandler` centraliza exceções; é o ponto apropriado para contador de exceção não tratada e status do span, sem instrumentar Domain.

## 1.10 Evidências reproduzíveis da análise

- Projetos: `dotnet sln AuthCore.sln list` e `rg --files -g '*.csproj'`.
- OTel/métricas/log/correlação: `rg -n -i "OpenTelemetry|ActivitySource|Meter\(|ILogger|BeginScope|Correlation|TraceId|SpanId" src tests`.
- HttpClient: `rg -n -i "AddHttpClient|HttpClient|IHttpClientFactory|DelegatingHandler" src`; a busca retornou zero ocorrências.
- Health: `rg -n "AddHealthChecks|AddCheck|MapHealthChecks|IHealthCheck" src tests`.
- Dependências: `rg -n -i "Npgsql|Redis|RabbitMQ|SMTP|MailKit" src tests` e inspeção dos `.csproj`, `InfrastructureDependencyInjection.cs` e `Program.cs` dos três hosts.
- Configuração: inspeção de todos os `appsettings*.json`, `src/Backend/.env.development.example`, `src/Backend/docker-compose.yml` e `Gateway.Api/ocelot.json`.
- Ausência de provider/exporter foi confirmada tanto pela busca textual quanto pela inspeção de todos os `.Api.csproj` e bootstraps.

# 2. Decisões técnicas

1. **Localização.** Criar `src/Shared/Observability` somente para bootstrap OpenTelemetry, opções, constantes, middleware HTTP e propagador de correlation ID. Métricas/spans específicos permanecem no serviço e camada que executa a operação.
2. **Dependências.** O shared terá `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Exporter.Console`, `OpenTelemetry.Instrumentation.AspNetCore` e `OpenTelemetry.Instrumentation.Http`, todos 1.16.0. `Npgsql.OpenTelemetry` 10.0.2 fica nas duas Infrastructure. Não adotar pacote Redis beta.
3. **Fronteira externa.** Apenas OTLP; nenhum SDK de Grafana, Datadog, New Relic, Azure ou AWS.
4. **Recursos.** `service.name`: `authcore-api`, `notificationcore-api`, `gateway-api`; `service.namespace`: `auth-core-microservices`; `service.version`: versão do assembly; `deployment.environment.name`: ambiente do host.
5. **Métricas.** Usar lowercase com pontos, sem `_total` e sem unidade no nome; declarar `Unit` (`s`, `{request}`, `{message}`). Prefixos específicos: `authcore.*`, `notificationcore.*`, `gateway.*`; instrumentos realmente transversais podem usar `app.*`, sempre diferenciados pelo resource `service.name`. Métricas HTTP e Npgsql mantêm nomes semânticos emitidos pelas bibliotecas.
6. **Spans.** Automáticos seguem nomes das bibliotecas. Customizados usam `<sistema> <operação> <destino lógico>`, por exemplo `rabbitmq publish notification.request`, `rabbitmq consume notification.request`, `smtp send` e `notification dispatch`. Não usar SQL, URL completa, GUID ou e-mail no nome.
7. **Correlation/trace.** `correlationId` é um identificador de investigação aceito/gerado no header; `traceId` identifica o trace W3C; `spanId` muda por operação. Eles não serão igualados. O middleware adiciona `correlation.id` como tag local e escopo de log. RabbitMQ propaga os dois contextos separadamente.
8. **Validação do header.** Aceitar um único `X-Correlation-Id` com 1–128 caracteres `[A-Za-z0-9._-]`; em valor ausente/inválido/múltiplo, gerar `Guid.NewGuid().ToString("D")`. Nunca ecoar conteúdo inválido.
9. **Logs.** Console legível em Development e JSON em ambientes não Development; scopes habilitados; `ActivityTrackingOptions.TraceId | SpanId | ParentId`. O provider OTel inclui scopes, estado estruturado e mensagem formatada. Não registrar query string, corpos, cookies, Authorization, headers sensíveis, exception message ou stack trace bruta. Erros usam somente tipo/categoria estável; diagnóstico detalhado exige reprodução controlada, não aumento automático do payload de telemetria.
10. **HTTP.** Usar instrumentação ASP.NET Core nativa/OTel para `http.server.request.duration`, método, rota e status. Não criar counters HTTP duplicados. Excluir `/health/live` de traces e métricas de negócio; manter métricas HTTP técnicas para `/health/ready` somente se desejado por configuração.
11. **PostgreSQL.** Usar métricas/traces Npgsql, configurar `NpgsqlDataSourceBuilder.Name` com nome fixo e nunca expor connection string/SQL/parâmetros. Manter apenas métricas customizadas que não duplicam Npgsql (aquisição, lease e transação), em segundos.
12. **Redis.** Instrumentar manualmente operações próprias em `RedisSessionStore` e `RedisLoginRateLimiter`; nunca registrar chave, script, session ID ou e-mail. Data Protection fica sem spans Redis detalhados nesta fase.
13. **RabbitMQ.** Acrescentar `TraceParent` e `TraceState` opcionais ao JSON do envelope `SendTransactionalNotificationRequested` e preenchê-los na criação da outbox a partir de `Activity.Current`; não há migration porque a outbox já persiste o envelope em `Content`. O publisher inicia o producer span usando o contexto persistido como pai, injeta o novo contexto como `traceparent`/`tracestate` nos headers AMQP e o consumidor o extrai como pai remoto. `CorrelationId` continua separado. Mensagens legadas sem contexto iniciam novo trace. Não propagar baggage nesta fase.
14. **Auth flows.** Não criar spans filhos que apenas dupliquem o span HTTP. Enriquecer o span servidor com `auth.flow` e `auth.result` e registrar métricas de negócio no adaptador HTTP. Spans novos ficam reservados a fronteiras assíncronas/técnicas.
15. **Collector indisponível.** Exporters OTLP usam processamento em lote e não fazem parte do caminho de negócio. Falhas de exportação não falham health checks, startup nem requests. Em Development, OTLP fica desabilitado por padrão.
16. **Sampling.** A única fonte é `Observability:TraceSamplingRatio`: Development `ParentBased(AlwaysOn)` com 1,0; produção `ParentBased(TraceIdRatioBased)` com default 0,10. Não consumir `OTEL_TRACES_SAMPLER*` nesta fase. Métricas e logs não seguem sampling de traces.
17. **Cardinalidade.** Rotas usam template, nunca path resolvido. Labels têm enum/conjunto fechado. `userId`, `sessionId`, `messageId`, `email`, correlation ID, trace ID, token, URL e exception message são proibidos em métricas.
18. **Health.** Preservar `/health` como alias de readiness, criar `/health/live` sem dependências e `/health/ready` com checks tagueados `ready`. Collector nunca é dependência de readiness. Gateway não consulta downstream para evitar falha em cascata.

### Allowlists obrigatórias

| Dimensão | Valores permitidos |
|---|---|
| `mode` | `token`, `browser`, `google` |
| `result` | `success`, `failure`, `cancelled`, `rejected`, `ack`, `requeue`, `dead_letter` |
| `reason` | `invalid_credentials`, `locked`, `unverified_email`, `rate_limited`, `invalid_token`, `expired`, `reused`, `revoked`, `not_found`, `validation`, `conflict`, `timeout`, `connectivity`, `authentication`, `protocol`, `cancelled`, `unknown` |
| `operation` | `get`, `set`, `delete`, `eval`, `expire`, `publish`, `consume`, `dispatch`, `send` |
| `queue` | `notification_requests` (nome lógico, não valor livre de configuração) |
| `event_type` | `send_transactional_notification_requested` |
| `notification_type` | `email_verification`, `test_email`, `other_transactional` |
| `error.type` customizado | `timeout`, `connectivity`, `authentication`, `protocol`, `cancelled`, `unknown` |

# 3. Escopo por microserviço

## AuthCore.Api

- Bootstrap OTel, logs estruturados, correlation middleware, HTTP server/client instrumentation.
- Métricas de login token/browser/Google, refresh, sessão, registro e solicitação de verificação.
- Enriquecimento seguro do span HTTP e tratamento uniforme de exceção.
- Readiness: PostgreSQL, Redis, outbox e RabbitMQ.

## NotificationCore.Api

- Mesmo bootstrap HTTP/log/correlation.
- Contexto consumidor RabbitMQ, métricas de consumo/DLQ/requeue e spans do worker.
- Métricas/spans de dispatcher e envio SMTP.
- Readiness: PostgreSQL, RabbitMQ, dispatcher e SMTP.

## Gateway

- Bootstrap HTTP/log/correlation e tracing do encaminhamento Ocelot/HttpClient.
- Propagação de `X-Correlation-Id`, `traceparent` e `tracestate` ao downstream.
- Liveness/readiness apenas do processo; não duplicar checks dos serviços.

## Componentes compartilhados

- Opções, resource, exporters, convenções de logging, correlation middleware, handler para futuros `IHttpClientFactory` e helpers de propagação.
- Nenhuma referência a Domain/Application/Infrastructure ou contratos de negócio.

## Docker e documentação

- Collector opcional via profile, config OTLP gRPC/HTTP e exporter `debug` para validação.
- Variáveis padrão OTel nas três APIs; nenhuma dependência `depends_on` para Collector.
- Criar `docs/observability.md` após a implementação.

# 4. Plano de execução

## Fase 1 — Fundação e configuração

### OBS-001 — Criar building block compartilhado

- **Objetivo:** centralizar somente código transversal repetido pelas três APIs.
- **Contexto técnico:** não existe shared técnico; três hosts precisam do mesmo bootstrap.
- **Projetos:** novo `src/Shared/Observability/Shared.Observability.csproj`; `AuthCore.sln`.
- **Alterar:** `AuthCore.sln`, `src/Backend/Backend.sln`, `src/Backend/AuthCore/AuthCore.Service.sln`, `src/Backend/NotificationCore/NotificationCore.Service.sln`, `src/Backend/Gateway/Gateway.Service.sln`; os três `.Api.csproj`.
- **Criar:** `ObservabilityOptions.cs`, `ObservabilityServiceDescriptor.cs`, `ObservabilityExtensions.cs`, `ObservabilityConstants.cs`.
- **Passos:** criar projeto net10; adicionar pacotes OTel 1.16.0; adicionar `FrameworkReference Microsoft.AspNetCore.App`; definir opções validadas; configurar resource, logging, trace/meter providers e exporters condicionais; aceitar listas de meter/activity source específicas do host.
- **Dependências:** nenhuma.
- **Aceite:** projeto sem referência às camadas de negócio; três APIs compilam com a referência; OTLP só é adicionado quando habilitado.
- **Validação:** `dotnet build AuthCore.sln`; teste de composição com OTLP desligado.
- **Riscos/cuidados:** não transformar a extension em service locator; não tornar opções mutáveis/publicamente amplas.
- **Fora do escopo:** métricas de negócio e instrumentação de dependências.

### OBS-002 — Conectar a fundação aos três hosts

- **Objetivo:** criar providers e resources por processo sem mudar comportamento funcional.
- **Contexto técnico:** os três `Program.cs` são composition roots independentes e hoje usam somente os providers padrão de logging.
- **Projetos:** três APIs e testes de integração.
- **Alterar:** três `Program.cs`; três `appsettings.json`; três `appsettings.Development.json`; smoke tests.
- **Criar:** nenhum arquivo obrigatório além de testes `Observability/ObservabilityBootstrapTests.cs` em cada projeto de integração.
- **Passos:** chamar `AddObservability` imediatamente após `CreateBuilder`; informar nomes fixos de serviço; registrar meters existentes; Development com console exporter opt-in e OTLP false; demais ambientes com console JSON e OTLP configurável; garantir uma única instância de cada provider. `Enabled=false` desliga SDK/providers/exporters, mas mantém logging local e correlation middleware. `OtlpEnabled=true` sem endpoint válido falha na validação de options; endpoint inacessível após startup apenas gera falha interna do exporter.
- **Dependências:** OBS-001.
- **Aceite:** resource correto por host; aplicação inicia com endpoint inexistente e OTLP desligado; logs incluem trace/span quando há Activity.
- **Validação:** smoke tests e inicialização de cada host.
- **Riscos/cuidados:** não duplicar ConsoleLogger; mover `Logging` do `ocelot.json` para appsettings sem quebrar carregamento das rotas.
- **Fora do escopo:** Collector e instrumentação customizada.

## Fase 2 — Correlation ID e contexto de logs

### OBS-003 — Implementar correlation middleware e propagação HTTP

- **Objetivo:** garantir `X-Correlation-Id` em toda requisição/resposta e downstream.
- **Contexto técnico:** não há middleware de correlação; o Gateway encaminha requests pelo Ocelot e futuros `IHttpClientFactory` precisam do mesmo comportamento.
- **Projetos:** Shared.Observability e três APIs.
- **Alterar:** três `Program.cs`; `Gateway.Api/Authentication/DownstreamForwardedHeadersMiddleware.cs`, que copiará o correlation ID já validado para o request encaminhado pelo Ocelot.
- **Criar:** `CorrelationIdMiddleware.cs`, `CorrelationIdConstants.cs`, `CorrelationIdDelegatingHandler.cs`, `CorrelationIdApplicationBuilderExtensions.cs`.
- **Passos:** validar/gerar ID; gravar em `HttpContext.Items`, response header, log scope e tag da Activity; posicionar após forwarded headers e antes do exception handler; no Gateway, garantir header no request encaminhado; registrar handler para futuros named/typed clients.
- **Dependências:** OBS-002.
- **Aceite:** ID válido é reutilizado; ausente/inválido gera novo; response contém o mesmo; Gateway e downstream observam o mesmo ID.
- **Validação:** testes com `WebApplication`, incluindo header inválido, múltiplo e propagação Ocelot.
- **Riscos/cuidados:** header injection e IDs enormes; response já iniciada.
- **Fora do escopo:** baggage arbitrário e IDs em labels.

### OBS-004 — Padronizar request logs e exceções

- **Objetivo:** produzir um log de conclusão HTTP seguro e contexto uniforme.
- **Contexto técnico:** logs atuais são pontuais, scopes não cobrem todo o request e `TraceIdentifier` é confundido com W3C TraceId.
- **Projetos:** Shared.Observability, AuthCore.Api, NotificationCore.Api e Gateway.Api.
- **Alterar:** handlers de exceção e logs que chamam `TraceIdentifier` de TraceId.
- **Criar:** `RequestLoggingMiddleware.cs` no shared.
- **Passos:** nos três hosts, ordenar `UseForwardedHeaders` (quando existir) → `UseCorrelationId` → `UseRouting` → CORS/autenticação → `UseRequestLogging` → `UseExceptionHandler` (quando existir) → autorização → endpoints/Ocelot; scope com service/environment/correlation/trace/span; após autenticação acrescentar `userId` quando claim válida; medir elapsed; logar método, template de rota, status e categoria de erro; remover duplicação de logs de conclusão; nunca logar query/body/headers/cookies/tokens/message/stack.
- **Dependências:** OBS-003.
- **Aceite:** um log de conclusão por request; logs de exceção têm IDs e campos estruturados; teste prova ausência de token/cookie/email do payload.
- **Validação:** provider de log em memória nos testes de integração.
- **Riscos/cuidados:** `userId` só em logs, nunca métrica/span; health logs em nível Debug para reduzir ruído.
- **Fora do escopo:** auditoria de negócio.

## Fase 3 — Instrumentação automática HTTP

### OBS-005 — Habilitar ASP.NET Core e HttpClient

- **Objetivo:** coletar métricas HTTP e traces distribuídos sem duplicação manual.
- **Contexto técnico:** .NET 10 emite métricas HTTP nativas, mas não existe MeterProvider/TracerProvider para escutá-las; Ocelot/Google usam HTTP internamente.
- **Projetos:** Shared.Observability e três APIs.
- **Alterar:** `ObservabilityExtensions.cs` e testes bootstrap.
- **Criar:** nenhuma classe de métrica HTTP.
- **Passos:** `AddAspNetCoreInstrumentation` e `AddHttpClientInstrumentation`; registrar explicitamente os meters `Microsoft.AspNetCore.Hosting`, `Microsoft.AspNetCore.Server.Kestrel` e `System.Net.Http`; registrar os ActivitySources pelas duas instrumentações; filtrar liveness; usar rota template; enriquecer apenas com campos seguros; verificar Ocelot e Google backchannel; definir propagador somente W3C TraceContext, sem BaggagePropagator.
- **Dependências:** OBS-002/003.
- **Aceite:** `http.server.request.duration` contém method/route/status; 5xx mensurável; trace atravessa Gateway→AuthCore/NotificationCore; chamadas HttpClient produzem client span.
- **Validação:** `MeterListener`/exporter em memória e teste Ocelot com `traceparent`.
- **Riscos/cuidados:** não usar path bruto; não registrar URL query; evitar dupla instrumentação do Ocelot.
- **Fora do escopo:** dashboards e P95 calculado dentro da aplicação; P95/P99 são agregações do backend.

## Fase 4 — Métricas existentes e técnicas

### OBS-006 — Normalizar meters customizados atuais

- **Objetivo:** tornar os meters existentes exportáveis e consistentes.
- **Contexto técnico:** cinco meters já existem, com nomes/unidades incompatíveis entre si e sem listener registrado.
- **Projetos:** AuthCore.Api/Infrastructure e NotificationCore.Infrastructure.
- **Alterar:** `ExternalAuthenticationMetrics.cs`, ambos `DatabaseMetrics.cs`, `OutboxMetrics.cs`, `NotificationMetrics.cs`, DI e testes relacionados.
- **Criar:** constantes locais de meter/instrument names quando necessário.
- **Passos:** adicionar descrição/unidade; converter duração ms→s; nomes com pontos; limitar `reason/type/provider` a allowlists; expor apenas `MeterName` internal; registrar nomes no provider.
- **Dependências:** OBS-002.
- **Aceite:** todas as métricas atuais chegam ao listener; nenhum label aceita texto livre; não há IDs.
- **Validação:** testes com `MeterListener` para nome, unidade e tags.
- **Riscos/cuidados:** nomes atuais ainda não são exportados, então a renomeação não quebra contrato operacional conhecido; documentar mesmo assim.
- **Fora do escopo:** compatibilidade com dashboards inexistentes.

### OBS-007 — Instrumentar exceções não tratadas

- **Objetivo:** contar falhas 5xx fora das métricas HTTP e marcar spans.
- **Contexto técnico:** os dois exception handlers centralizam falhas, mas não emitem métricas e hoje podem registrar detalhes excessivos.
- **Projetos:** AuthCore.Api e NotificationCore.Api; Gateway somente via middleware se houver handler equivalente.
- **Alterar:** `ApiExceptionHandler.cs`; `RequestLoggingMiddleware.cs`.
- **Criar:** `src/Shared/Observability/HttpExceptionMetrics.cs`, com o único instrumento transversal `app.exceptions.unhandled` e resource identificando o serviço.
- **Passos:** incrementar somente exceção não tratada/5xx; tags `error.type` e `operation` pelas allowlists; marcar `ActivityStatusCode.Error`; adicionar evento manual contendo apenas categoria/tipo normalizado, nunca `RecordException`, exception message ou stack.
- **Dependências:** OBS-004/006.
- **Aceite:** uma exceção gera um incremento, status 500 e span Error; exceções 4xx conhecidas não entram no contador unhandled.
- **Validação:** ampliar `ApiExceptionHandlerTests` dos dois serviços.
- **Riscos/cuidados:** impedir contagem dupla entre handler e request middleware.
- **Fora do escopo:** alertas.

## Fase 5 — Dependências

### OBS-008 — Instrumentar PostgreSQL com segurança

- **Objetivo:** obter duração/falha de operações Npgsql sem expor conexão ou SQL.
- **Contexto técnico:** os dois serviços usam Npgsql 10.0.2 e `NpgsqlDataSource`; o nome padrão do pool pode conter a connection string.
- **Projetos:** duas Infrastructure e duas APIs.
- **Alterar:** dois `.Infrastructure.csproj`, `InfrastructureDependencyInjection.cs`, `NpgsqlConnectionFactory.cs`, métricas/tests.
- **Criar:** nenhum wrapper de comando.
- **Passos:** adicionar `Npgsql.OpenTelemetry` 10.0.2; trocar `NpgsqlDataSource.Create` por builder com `Name = AuthCore/NotificationCore`; habilitar `AddNpgsql`; registrar meter `Npgsql`; manter defaults sem command text/parâmetros; revisar custom metrics para não duplicar `db.client.operation.duration`.
- **Dependências:** OBS-005/006.
- **Aceite:** span DB filho do request/worker; `db.client.operation.duration` exportada; pool name fixo; nenhum password, connection string, SQL literal ou parâmetro.
- **Validação:** testes PostgreSQL existentes + inspeção de export em cenário de integração.
- **Riscos/cuidados:** tracing Npgsql é documentado como experimental; fixar versão e cobrir tags de segurança.
- **Fora do escopo:** instrumentar cada repository manualmente.

### OBS-009 — Instrumentar Redis sem pacote beta

- **Objetivo:** medir operações Redis próprias com API estável.
- **Contexto técnico:** Redis existe apenas no AuthCore; a instrumentação contrib disponível é pré-release e as operações próprias estão concentradas em duas classes.
- **Projetos:** AuthCore.Api e AuthCore.Infrastructure.
- **Alterar:** `RedisSessionStore.cs`, `RedisLoginRateLimiter.cs`; DI e testes de autenticação.
- **Criar:** `AuthCore.Infrastructure/Observability/RedisTelemetry.cs` e equivalente pequeno na Api caso o rate limiter não possa consumir o tipo internal da Infrastructure.
- **Passos:** ActivitySource e histogram/counter; wrappers `try/finally`; operation allowlist (`get`, `set`, `delete`, `eval`, `expire`), result (`success`, `failure`); nunca key/script/value; registrar source/meter no AuthCore host.
- **Dependências:** OBS-006.
- **Aceite:** duração e falha mensuráveis; spans filhos preservam trace; Data Protection continua funcional sem instrumentação detalhada.
- **Validação:** testes com fake/multiplexer de integração disponível e listeners.
- **Riscos/cuidados:** não criar uma abstração genérica de cache; evitar medir duas vezes a mesma chamada.
- **Fora do escopo:** pacote `OpenTelemetry.Instrumentation.StackExchangeRedis` pré-release e profiling de Data Protection.

### OBS-010 — Instrumentar RabbitMQ e propagar trace context

- **Objetivo:** ligar request/outbox/publish/consume no mesmo trace e medir resultados.
- **Contexto técnico:** correlation property já atravessa o broker, mas nasce desconectada do request e o atraso da outbox exige persistir W3C context no envelope.
- **Projetos:** Shared.Messaging.Contracts, duas Infrastructure, NotificationCore.Api e Shared.Observability.
- **Alterar:** `EmailVerificationNotificationOutboxFactory.cs`, `OutboxProcessor.cs`, `RabbitMqNotificationRequestPublisher.cs`, `RabbitMqNotificationConsumer.cs`, hosted consumer e testes.
- **Criar:** `RabbitMqTelemetry.cs` em cada Infrastructure; helper de propagação no shared somente se não carregar RabbitMQ.Client.
- **Passos:** adicionar `TraceParent`/`TraceState` opcionais e retrocompatíveis em `src/Shared/Messaging.Contracts/Notifications/SendTransactionalNotificationRequested.cs`; na factory, preencher correlation ID a partir da tag local `correlation.id` e trace context a partir de `Activity.Current`, com GUID/contexto vazio como fallback; criar producer span; injetar W3C em headers; extrair no consumidor como pai remoto e criar consumer span; métricas publish/consume/failure/requeue/dead_letter/retry com queue/result; status/exception seguros.
- **Dependências:** OBS-003/006.
- **Aceite:** correlation ID HTTP chega à mensagem/Notification/SMTP; o producer usa o `TraceParent` persistido e o consumer é filho do contexto injetado pelo producer; DLQ/requeue distinguíveis; mensagens legadas funcionam; sem body/recipient/message ID em tags.
- **Validação:** ampliar testes de outbox/publisher/consumer e integração RabbitMQ.
- **Riscos/cuidados:** mensagens antigas sem headers iniciam novo root correlacionado somente por `CorrelationId`; codificar headers W3C como UTF-8 e cobrir interoperabilidade; não propagar baggage.
- **Fora do escopo:** upgrade RabbitMQ.Client e mudança de topologia.

### OBS-011 — Instrumentar SMTP e dispatcher

- **Objetivo:** localizar latência/falha de entrega.
- **Contexto técnico:** SMTP já mede duração agregada e o dispatcher já retorna `DispatchCounters`, ponto seguro para transportar contagens sem acoplar Application ao OTel.
- **Projetos:** NotificationCore.Api, NotificationCore.Application e NotificationCore.Infrastructure.
- **Alterar:** `NotificationCore.Application/UseCases/Notifications/DispatchPendingNotification/DispatchCounters.cs` e `PendingNotificationDispatcher.cs`; `SmtpEmailProvider.cs`, `NotificationDispatcherHostedService.cs`, `NotificationMetrics.cs` e testes.
- **Criar:** `NotificationCore.Infrastructure/Observability/SmtpTelemetry.cs` e `NotificationActivitySource.cs`.
- **Passos:** manter Application sem referência a OTel; ampliar apenas `DispatchCounters` com contadores `EmailVerificationSent/Failed` e `DeliveryRetries`; em `PendingNotificationDispatcher`, mapear exatamente `TemplateKey == "auth.email-confirmation"` para `email_verification` e todos os demais para as categorias allowlisted, incrementando os counters após o resultado persistido; no hosted service, converter counters em `NotificationMetrics`; criar span `notification dispatch` por ciclo e `smtp send` por tentativa no provider; métricas duration/attempts com provider/result; registrar categoria de erro, não exception type/message livre; preservar correlation em scope, não em labels.
- **Dependências:** OBS-006/010.
- **Aceite:** tentativa, sucesso, falha e duração SMTP mensuráveis; `notification_type=email_verification` é emitido somente para o template fechado `auth.email-confirmation`; retry identificado; nenhum tipo livre vira label; trace do consumer chega ao send quando o processamento é imediato, ou novo trace com link/correlação quando persistido e retomado depois.
- **Validação:** `SmtpEmailProviderTests` e `NotificationDispatcherHostedServiceTests`.
- **Riscos/cuidados:** o dispatcher assíncrono pode perder Activity após persistência; não fingir parentesco inválido, usar link/correlation.
- **Fora do escopo:** conteúdo/destinatário do e-mail.

## Fase 6 — Métricas de negócio e fluxos AuthCore

### OBS-012 — Implementar métricas de autenticação

- **Objetivo:** medir resultados de negócio sem contaminar Domain/Application.
- **Contexto técnico:** endpoints Auth/Token/Session compartilham resultados HTTP, enquanto somente o login Google possui métricas parciais.
- **Projetos:** AuthCore.Api.
- **Alterar:** `AuthCore.Api/Program.cs`, `GoogleExternalAuthenticationFlow.cs`, exception/request middleware e testes; não adicionar chamadas de métrica aos controllers.
- **Criar:** `AuthBusinessMetrics.cs` e `AuthFlowTelemetryMiddleware.cs` em `AuthCore.Api/Observability`.
- **Passos:** middleware único, depois de routing/autenticação e por fora do exception handler, mapeia os templates exatos de Auth/Token/Session para flow/result a partir do status final; `ApiExceptionHandler` grava somente uma categoria allowlisted em `HttpContext.Items` para distinguir razões que compartilham o mesmo status, sem mensagem; contabiliza tentativas e desfechos uma vez; sessions/refresh/registro/verificação; enriquece span servidor com `auth.flow` e `auth.result`; o fluxo Google chama o mesmo `AuthBusinessMetrics` somente nos pontos que não retornam por controller, sem duplicação.
- **Dependências:** OBS-005/006/007.
- **Aceite:** matriz da seção 6 emitida; falhas 400/401/403/409 classificadas sem e-mail/user ID; controller permanece adaptador fino.
- **Validação:** testes de integração de endpoints com `MeterListener`.
- **Riscos/cuidados:** não contar rate limit como credencial inválida; evitar duplicar token login e session login; `email_verification.requested` não significa e-mail entregue.
- **Fora do escopo:** métricas dentro de entidades/value objects.

## Fase 7 — Health checks

### OBS-013 — Separar liveness/readiness e completar dependências

- **Objetivo:** oferecer probes coerentes sem acoplar ao Collector.
- **Contexto técnico:** existe apenas `/health`, sem tags; AuthCore não testa RabbitMQ e NotificationCore não testa SMTP.
- **Projetos:** três APIs, Gateway ocelot e testes.
- **Alterar:** `ApiDependencyInjection.cs`, três `Program.cs`, `ocelot.json`, health tests.
- **Criar:** `AuthCore.Api/HealthChecks/RabbitMqHealthCheck.cs`, `NotificationCore.Api/HealthChecks/SmtpHealthCheck.cs` e `src/Shared/Observability/HealthCheckResponseWriter.cs` para o JSON uniforme.
- **Passos:** tag `ready`; mapear `/health/live` com predicate false, `/health/ready` com ready e `/health` alias; PostgreSQL/Redis/outbox/RabbitMQ falham como Unhealthy após timeout de 5 s; SMTP participa da readiness com `failureStatus: Degraded`, timeout de 5 s, apenas DNS/TCP/TLS e sem envio/autenticação; status Degraded retorna HTTP 200; dependência desabilitada retorna Healthy; Gateway self-only; expor rotas downstream live/ready mantendo antigas.
- **Dependências:** OBS-010/011.
- **Aceite:** liveness não falha por dependência; readiness retorna 503 para dependências críticas e 200/Degraded para SMTP; serviços desabilitados são Healthy; Collector fora dos checks.
- **Validação:** smoke tests e indisponibilidade controlada de cada dependência.
- **Riscos/cuidados:** SMTP health sem enviar e-mail; evitar cascading health no Gateway.
- **Fora do escopo:** SLO/alerta.

## Fase 8 — Ambiente local e documentação

### OBS-014 — Adicionar Collector opcional ao Docker

- **Objetivo:** validar OTLP local sem tornar o stack dependente dele.
- **Contexto técnico:** o compose atual já contém todas as dependências de negócio, mas não possui receiver OTLP nem profile de observabilidade.
- **Projetos:** ambiente Docker.
- **Alterar:** `src/Backend/docker-compose.yml`, `src/Backend/.env.development.example` e `src/Backend/README.md`.
- **Criar:** `src/Backend/observability/otel-collector-config.yaml`.
- **Passos:** serviço `otel-collector` em profile `observability`; receivers OTLP gRPC/HTTP; batch/memory limiter; exporter debug; portas 4317/4318; env nas três APIs; nenhum `depends_on` e restart desacoplado.
- **Dependências:** OBS-002/005.
- **Aceite:** stack sobe sem profile; com profile recebe três sinais; parar Collector não derruba APIs.
- **Validação:** `docker compose config`, execução com/sem profile e inspeção dos logs do Collector.
- **Riscos/cuidados:** fixar tag de imagem, não `latest`; não expor endpoint OTLP publicamente em produção.
- **Fora do escopo:** Grafana/Prometheus/Loki/Tempo/Jaeger.

### OBS-015 — Documentar e validar ponta a ponta

- **Objetivo:** fechar critérios de aceite e produzir guia operacional.
- **Contexto técnico:** não existe `docs/observability.md`; a validação precisa combinar listeners em memória, integrações reais e execução Docker.
- **Projetos:** docs e todos os testes.
- **Alterar:** README apenas para linkar o guia.
- **Criar:** `docs/observability.md`.
- **Passos:** documentar arquitetura, execução, sinais, nomes, segurança, sampling, troubleshooting e evolução de backends; executar checklist da seção 9; registrar evidências e limitações.
- **Dependências:** todas as tasks anteriores.
- **Aceite:** todos os critérios verificáveis passam; documento não contém segredo; CI/build/test verdes.
- **Validação:** `dotnet test AuthCore.sln`, testes com dependências disponíveis e smoke Docker.
- **Riscos/cuidados:** distinguir testes pulados por infraestrutura ausente de sucesso real.
- **Fora do escopo:** dashboards, alertas, retenção e SLOs.

# 5. Estratégia incremental

| Marco | Tasks | Estado funcional ao final |
|---|---|---|
| Fundação | OBS-001–002 | Aplicações iguais funcionalmente; providers configuráveis e desligáveis. |
| Correlação/logs | OBS-003–004 | Requests rastreáveis por correlation/trace; logs seguros. |
| HTTP automático | OBS-005 | Latência, status e traces HTTP disponíveis. |
| Métricas técnicas | OBS-006–007 | Meters existentes coletados e 5xx mensuráveis. |
| Dependências | OBS-008–011 | PostgreSQL, Redis, RabbitMQ e SMTP investigáveis. |
| Negócio | OBS-012 | Funil de autenticação mensurável. |
| Operação | OBS-013 | Probes semânticos e compatíveis. |
| Local/docs | OBS-014–015 | Validação OTLP reproduzível e documentação completa. |

A ordem desloca spans customizados para junto das dependências, pois spans HTTP redundantes não agregam valor. Cada fase pode ser entregue com OTLP desligado.

# 6. Matriz de métricas

**Labels proibidas em cada uma das métricas abaixo:** `userId`, e-mail, session/message/request/correlation/trace ID, token, cookie, URL/path resolvido, exception class/message/stack, SQL, chave Redis e qualquer texto livre. Essa política é parte de cada linha da matriz, não apenas uma recomendação geral.

| Nome | Tipo | Serviço | Descrição | Labels permitidas | Local | Task |
|---|---|---|---|---|---|---|
| `http.server.request.duration` | Histogram | Todos | Duração HTTP nativa, base de P95/P99/erros. | method, route template, status_code, error.type | ASP.NET Core | OBS-005 |
| `app.exceptions.unhandled` | Counter | Todos | Exceções 5xx não tratadas. | operation, error.type allowlist | Exception handler | OBS-007 |
| `db.client.operation.duration` | Histogram | Auth/Notification | Duração de comandos Npgsql. | db.system, operation.name, pool.name fixo, error.type | Npgsql | OBS-008 |
| `authcore.db.connection.acquire.duration` | Histogram | Auth | Aquisição de conexão, segundos. | result | ConnectionFactory | OBS-006 |
| `notificationcore.db.connection.acquire.duration` | Histogram | Notification | Idem. | result | ConnectionFactory | OBS-006 |
| `authcore.db.connection.lease.duration` | Histogram | Auth | Tempo de lease de conexão, segundos. | result | DatabaseConnectionLease | OBS-006 |
| `notificationcore.db.connection.lease.duration` | Histogram | Notification | Idem. | result | DatabaseConnectionLease | OBS-006 |
| `authcore.db.transaction.duration` | Histogram | Auth | Duração de transação, segundos. | result | NpgsqlUnitOfWork | OBS-006 |
| `notificationcore.db.transaction.duration` | Histogram | Notification | Idem. | result | NpgsqlUnitOfWork | OBS-006 |
| `authcore.db.connection.acquire.failures` | Counter | Auth | Falhas de aquisição. | error.type allowlist | ConnectionFactory | OBS-006 |
| `notificationcore.db.connection.acquire.failures` | Counter | Notification | Idem. | error.type allowlist | ConnectionFactory | OBS-006 |
| `authcore.redis.operation.duration` | Histogram | Auth | Duração Redis própria. | operation, result | SessionStore/rate limiter | OBS-009 |
| `authcore.redis.operation.failures` | Counter | Auth | Falhas Redis. | operation, error.type allowlist | SessionStore/rate limiter | OBS-009 |
| `authcore.rabbitmq.messages.published` | Counter | Auth | Publicações confirmadas/falhas. | queue, result, event_type allowlist | Publisher | OBS-010 |
| `notificationcore.rabbitmq.messages.consumed` | Counter | Notification | Consumos por disposition. | queue, result (`ack`,`requeue`,`dead_letter`,`failure`) | Consumer | OBS-010 |
| `notificationcore.rabbitmq.requeues` | Counter | Notification | Mensagens devolvidas à fila. | queue, reason allowlist | Consumer | OBS-010 |
| `notificationcore.notifications.delivery.retries` | Counter | Notification | Novas tentativas de entrega. | notification_type | Hosted service a partir de DispatchCounters | OBS-011 |
| `notificationcore.smtp.send.duration` | Histogram | Notification | Duração SMTP em segundos. | provider, result | SmtpEmailProvider | OBS-011 |
| `notificationcore.smtp.send.attempts` | Counter | Notification | Tentativas SMTP. | provider, result | SmtpEmailProvider | OBS-011 |
| `notificationcore.notifications.dispatch.duration` | Histogram | Notification | Ciclo de despacho. | result | Worker | OBS-006/011 |
| `notificationcore.notifications.deliveries` | Counter | Notification | Entregas por tipo e resultado, incluindo verificação de e-mail. | notification_type allowlist, channel, result | Hosted service a partir de DispatchCounters | OBS-011 |
| `notificationcore.notifications.pending` | Counter | Notification | Notificações pendentes encontradas por ciclo. | channel | Hosted service a partir de DispatchCounters | OBS-006/011 |
| `authcore.outbox.messages.processed` | Counter | Auth | Outbox processada. | event_type allowlist, result | OutboxProcessor | OBS-006 |
| `authcore.outbox.messages.failed` | Counter | Auth | Falha final/temporária na outbox. | event_type, reason allowlist | OutboxProcessor | OBS-006 |
| `authcore.outbox.processing.duration` | Histogram | Auth | Duração do ciclo da outbox, segundos. | result | OutboxProcessor | OBS-006 |
| `notificationcore.notifications.sent` | Counter | Notification | Notificações entregues. | notification_type, channel | Hosted service a partir de DispatchCounters | OBS-006/011 |
| `notificationcore.notifications.failed` | Counter | Notification | Notificações com falha final. | notification_type, channel | Hosted service a partir de DispatchCounters | OBS-006/011 |
| `authcore.auth.login.attempts` | Counter | Auth | Login token/browser/Google. | mode | API/Google flow | OBS-012 |
| `authcore.auth.login.results` | Counter | Auth | Desfecho de login. | mode, result, reason allowlist | API | OBS-012 |
| `authcore.auth.external.callback.duration` | Histogram | Auth | Duração do callback Google, segundos. | result | GoogleExternalAuthenticationFlow | OBS-006/012 |
| `authcore.auth.sessions.created` | Counter | Auth | Sessões browser/token criadas. | mode | API | OBS-012 |
| `authcore.auth.sessions.revoked` | Counter | Auth | Revogação current/single/all. | mode, scope, result | API | OBS-012 |
| `authcore.auth.refresh_tokens.issued` | Counter | Auth | Token inicial/rotacionado. | grant (`login`,`refresh`) | API | OBS-012 |
| `authcore.auth.refresh_tokens.rejected` | Counter | Auth | Refresh rejeitado. | reason allowlist | API | OBS-012 |
| `authcore.users.registered` | Counter | Auth | Registro concluído. | method (`password`,`google`) | API | OBS-012 |
| `authcore.email_verification.requested` | Counter | Auth | Notificação de verificação enfileirada; não entrega. | operation (`register`,`resend`) | API/outbox boundary | OBS-012 |

`service` e `environment` são resource attributes, não labels repetidas em cada medição. `queue`, `mode`, `result`, `reason`, `operation`, `provider` e `event_type` devem ter allowlists fechadas no código.

# 7. Matriz de traces

| Nome/origem | Serviço | Fluxo | Tipo | Tags permitidas | Dados proibidos | Task |
|---|---|---|---|---|---|---|
| Span servidor ASP.NET Core | Todos | Toda requisição, inclusive auth | Automático | method, route, status, protocol, auth.flow/result | body, query, headers, user/email/token | OBS-005/012 |
| Span cliente HttpClient | Todos | Ocelot, Google/futuros clients | Automático | method, server.address, status | URL query, Authorization/cookies | OBS-005 |
| Span Npgsql | Auth/Notification | Operação DB | Automático | db.system, operation, pool fixo, error.type | connection string, SQL/parâmetros | OBS-008 |
| `redis <operation>` | Auth | Sessão/rate limit | Customizado | db.system, operation, result | key, script, value, IDs | OBS-009 |
| `rabbitmq publish notification.request` | Auth | Outbox→broker | Customizado producer | system, destination, operation, result, event type | payload, recipient, IDs | OBS-010 |
| `rabbitmq consume notification.request` | Notification | Broker→consumer | Customizado consumer | system, destination, operation, result, event type | payload, recipient, IDs | OBS-010 |
| `notification dispatch` | Notification | Ciclo/tentativa | Customizado internal | result, provider, retry | notification ID, recipient/body | OBS-011 |
| `smtp send` | Notification | Entrega | Customizado client | server.address, provider, result, error.type | username/password, recipient, subject/body | OBS-011 |

Login, refresh, criação/revogação de sessão, registro e verificação usam o span servidor enriquecido; não haverá span filho puramente duplicado nesta fase.

# 8. Configurações

## 8.1 Seção appsettings

```json
{
  "Observability": {
    "Enabled": true,
    "ServiceName": "authcore-api",
    "ServiceNamespace": "auth-core-microservices",
    "OtlpEnabled": false,
    "ConsoleExporterEnabled": false,
    "TraceSamplingRatio": 1.0,
    "ExcludeHealthChecks": true
  }
}
```

`ServiceName` muda por host. `Enabled`, `ServiceName` e `ServiceNamespace` são obrigatórios. `OtlpEnabled`, console, sampling e filtro são opcionais com defaults acima. Validar ratio entre 0 e 1.

## 8.2 Variáveis

| Variável | Obrigatória | Default/uso |
|---|---|---|
| `OBSERVABILITY__ENABLED` | Não | `true`. |
| `OBSERVABILITY__OTLPENABLED` | Não | `false` em Development; `true` por decisão de deploy. |
| `OBSERVABILITY__CONSOLEEXPORTERENABLED` | Não | `false`; ativar só para diagnóstico. |
| `OBSERVABILITY__TRACESAMPLINGRATIO` | Não | `1.0` Development; `0.10` produção. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Sim se OTLP habilitado | Docker: `http://otel-collector:4317`. |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | Não | `grpc`; permitir `http/protobuf`. |
| `OTEL_EXPORTER_OTLP_HEADERS` | Não | Somente secret store da plataforma; nunca versionar. |
| `OTEL_RESOURCE_ATTRIBUTES` | Não | Atributos extras de deploy sem PII/segredo. |
| `OTEL_SERVICE_NAME` | Não | Não usar nesta fase; `Observability:ServiceName` fixo por host é a fonte autoritativa. |

## 8.3 Comportamento

- **Development sem Collector:** logs locais; SDK ativo; nenhum exporter de rede; aplicação funciona normalmente.
- **Development com diagnóstico:** console exporter explicitamente habilitado ou profile Docker com OTLP.
- **Docker:** OTLP opt-in por profile; endpoint interno do Collector; desligar/remover Collector não afeta os serviços.
- **Produção:** logs JSON e OTLP; sampling 10% inicial; endpoint/headers vindos da plataforma; console trace/metric desligado.
- **Observability desabilitada:** logging local, correlation ID e request logging permanecem; apenas SDK/providers/exporters e instrumentos escutados deixam de produzir/exportar sinais.
- **Configuração inválida:** `OtlpEnabled=true` exige `OTEL_EXPORTER_OTLP_ENDPOINT` absoluto com `http`/`https`; ausência/formato inválido falha no startup. Indisponibilidade de rede depois da validação não falha o processo.

# 9. Validação final

- [ ] Request sem header recebe `X-Correlation-Id`; request válido preserva; inválido não é ecoado.
- [ ] Gateway e downstream exibem o mesmo correlation ID.
- [ ] Logs contêm service, environment, correlationId, traceId, spanId, método, rota template, status e elapsed.
- [ ] `userId` aparece somente quando autenticado e somente em logs autorizados.
- [ ] `http.server.request.duration` permite agrupar por método/rota/status e calcular P95/P99 no backend.
- [ ] Resposta 500 incrementa `app.exceptions.unhandled` uma vez e marca span Error.
- [ ] Span servidor contém client spans Ocelot/HttpClient/Npgsql/Redis/Rabbit/SMTP conforme o fluxo.
- [ ] Npgsql pool name não contém connection string; SQL e parâmetros não aparecem.
- [ ] Redis não contém keys/scripts/valores.
- [ ] RabbitMQ preserva correlation e trace context; mensagem legada continua processável.
- [ ] Métricas publish/consume/requeue/DLQ/retry existem.
- [ ] SMTP mede tentativa, duração e resultado sem destinatário/conteúdo.
- [ ] Métricas AuthCore diferenciam mode/result/reason por allowlist.
- [ ] Testes emitem valores-sentinela distintos para password, access/refresh token, cookie, Authorization, e-mail e payload e provam que nenhum aparece nos logs, métricas ou spans capturados em memória.
- [ ] Nenhuma métrica usa path bruto ou identificador único.
- [ ] `/health/live` funciona sem dependências; `/health/ready` e `/health` refletem dependências.
- [ ] Collector parado não altera startup, request nem health.
- [ ] Stack Docker funciona com e sem profile de observabilidade.
- [ ] `docs/observability.md` explica execução, sinais, segurança, limitações e evolução.
- [ ] `dotnet build AuthCore.sln` e testes unitários/integração afetados passam.

# 10. Pendências e incertezas

1. **Backend de visualização futuro:** não definido. Não bloqueia OTLP nem faz parte desta implementação.
2. **Política de sampling de produção:** 10% é default seguro inicial, mas deve ser ajustado com volume/custo reais.
3. **SMTP readiness:** confirmar se autenticação periódica é aceita pelo provedor. Se não, limitar o check a DNS/TCP/TLS e deixar autenticação para métrica operacional.
4. **Npgsql tracing experimental:** manter versão fixada e revalidar tags ao atualizar Npgsql.
5. **Redis:** a instrumentação oficial/contrib continua beta em 10/07/2026; reavaliar futuramente, sem bloquear spans manuais.
6. **`clientId`/`tenantId`:** não existe contexto multitenant confirmado. Não criar campos até existir conceito real.
7. **`userId` em logs:** confirmar política de privacidade/retenção; se proibido, usar indicador `isAuthenticated` e consultar auditoria separada.
8. **Versão de serviço:** usar assembly version inicialmente; CI pode futuramente injetar commit/release sem mudar o código.

## Referências técnicas verificadas

- OpenTelemetry .NET 1.16.0: signals estáveis e configuração de providers/exporters.
- Instrumentação ASP.NET Core: métricas nativas em .NET 8+ e `http.server.request.duration`.
- Npgsql 10: métricas `System.Diagnostics.Metrics`, tracing via `Npgsql.OpenTelemetry` e necessidade de nome fixo para o data source/pool.
- `ActivityTrackingOptions`: escopo automático com TraceId/SpanId/ParentId.
- OTLP: endpoint padrão por `OTEL_EXPORTER_OTLP_ENDPOINT` e exportação desacoplada do backend final.
