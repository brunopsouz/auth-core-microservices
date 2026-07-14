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

#### Mapeamento intermediário OBS-006

| Métrica anterior | Métrica final | Tipo | Unidade | Labels |
| ---------------- | ------------- | ---- | ------- | ------ |
| `authcore.database.connection.acquisition.duration.ms` | `authcore.db.connection.acquire.duration` | Histogram | `s` | `result` |
| `authcore.database.connection.lease.duration.ms` | `authcore.db.connection.lease.duration` | Histogram | `s` | `result` |
| `authcore.database.transaction.duration.ms` | `authcore.db.transaction.duration` | Histogram | `s` | `result` |
| `authcore.database.connection.acquisition.failures` | `authcore.db.connection.acquire.failures` | Counter | `{connection}` | `error.type` |
| `authcore.outbox.messages.processed` | `authcore.outbox.messages.processed` | Counter | `{message}` | `event_type`, `result` |
| `authcore.outbox.messages.failed` | `authcore.outbox.messages.failed` | Counter | `{message}` | `event_type`, `reason` |
| `authcore.outbox.processing.duration.ms` | `authcore.outbox.processing.duration` | Histogram | `s` | `result` |
| `auth_google_login_started_total` | Removida na OBS-012 | Counter | `{request}` | redirect inicial não representa login concluído |
| `auth_google_login_succeeded_total` | `authcore.authentication.attempts` | Counter | `{attempt}` | `flow`, `result`, `reason` |
| `auth_google_login_failed_total` | `authcore.authentication.attempts` | Counter | `{attempt}` | `flow`, `result`, `reason` |
| `auth_google_login_cancelled_total` | `authcore.authentication.attempts` | Counter | `{attempt}` | `flow`, `result`, `reason` |
| `auth_google_callback_duration_ms` | Removida na OBS-012 | Histogram | `s` | duração duplicava HTTP server metrics |
| `notificationcore.database.connection.acquisition.duration.ms` | `notificationcore.db.connection.acquire.duration` | Histogram | `s` | `result` |
| `notificationcore.database.connection.lease.duration.ms` | `notificationcore.db.connection.lease.duration` | Histogram | `s` | `result` |
| `notificationcore.database.transaction.duration.ms` | `notificationcore.db.transaction.duration` | Histogram | `s` | `result` |
| `notificationcore.database.connection.acquisition.failures` | `notificationcore.db.connection.acquire.failures` | Counter | `{connection}` | `error.type` |
| `notificationcore.notifications.pending` | `notificationcore.notifications.pending` | Counter | `{notification}` | nenhuma |
| `notificationcore.notifications.sent` | `notificationcore.notifications.sent` | Counter | `{notification}` | nenhuma |
| `notificationcore.notifications.failed` | `notificationcore.notifications.failed` | Counter | `{notification}` | nenhuma |
| `notificationcore.notifications.dispatch.duration.ms` | `notificationcore.notifications.dispatch.duration` | Histogram | `s` | nenhuma |
| `notificationcore.notifications.send.duration.ms` | removida em OBS-011 | Histogram | `s` | `provider` |

### OBS-007 — Instrumentar exceções não tratadas

- **Objetivo:** contar falhas 5xx fora das métricas HTTP e marcar spans.
- **Contexto técnico:** os dois exception handlers centralizam falhas, mas não emitem métricas e hoje podem registrar detalhes excessivos.
- **Projetos:** AuthCore.Api e NotificationCore.Api; Gateway somente via middleware se houver handler equivalente.
- **Alterar:** `ApiExceptionHandler.cs`; `RequestLoggingMiddleware.cs`.
- **Criar:** `src/Shared/Observability/UnhandledExceptionMetrics.cs`, com o único instrumento transversal `app.exceptions.unhandled` no meter `app.observability`.
- **Passos:** incrementar somente no ramo de exceção inesperada dos handlers globais; tag única `error.type` por allowlist; marcar `ActivityStatusCode.Error`; não adicionar evento manual, `RecordException`, exception message ou stack.
- **Dependências:** OBS-004/006.
- **Aceite:** uma exceção gera um incremento, status 500 e span Error; exceções 4xx conhecidas não entram no contador unhandled.
- **Validação:** ampliar `ApiExceptionHandlerTests` dos dois serviços.
- **Riscos/cuidados:** impedir contagem dupla entre handler e request middleware.
- **Fora do escopo:** alertas.

#### Definição OBS-007

| Meter | Instrumento | Tipo | Unidade | Descrição | Labels | Hosts | Condição de incremento |
| ----- | ----------- | ---- | ------- | --------- | ------ | ----- | ---------------------- |
| `app.observability` | `app.exceptions.unhandled` | Counter | `{exception}` | Number of unexpected exceptions handled by the application's global exception handler. | `error.type` | AuthCore.Api, NotificationCore.Api | Exceção não derivada das exceções esperadas da aplicação chega ao `ApiExceptionHandler`, gera resposta 500 segura e ainda não foi registrada no `HttpContext` atual. |

## Fase 5 — Dependências

### OBS-008 — Instrumentar PostgreSQL com segurança

- **Objetivo:** obter duração/falha de operações Npgsql sem expor conexão ou SQL.
- **Contexto técnico:** os dois serviços usam Npgsql 10.0.2 e `NpgsqlDataSource`; o nome padrão do pool pode conter a connection string.
- **Projetos:** duas Infrastructure e duas APIs.
- **Alterar:** dois `.Infrastructure.csproj`, `InfrastructureDependencyInjection.cs`, `NpgsqlConnectionFactory.cs`, métricas/tests.
- **Criar:** nenhum wrapper de comando.
- **Passos:** adicionar `Npgsql.OpenTelemetry` 10.0.2; trocar `NpgsqlDataSource.Create` por builder com `Name = authcore-postgresql/notificationcore-postgresql`; habilitar `AddNpgsql`; registrar meter `Npgsql`; manter defaults sem command text/parâmetros; revisar custom metrics para não duplicar `db.client.operation.duration`.
- **Dependências:** OBS-005/006.
- **Aceite:** span DB filho do request/worker; `db.client.operation.duration` exportada; pool name fixo; nenhum password, connection string, SQL literal ou parâmetro.
- **Validação:** testes PostgreSQL existentes + inspeção de export em cenário de integração.
- **Riscos/cuidados:** tracing Npgsql é documentado como experimental; fixar versão e cobrir tags de segurança.
- **Fora do escopo:** instrumentar cada repository manualmente.

#### Definição OBS-008

- Pacote adicionado: `Npgsql.OpenTelemetry` 10.0.2 em `AuthCore.Infrastructure` e `NotificationCore.Infrastructure`. O `Shared.Observability` permanece sem dependência de Npgsql/PostgreSQL.
- Extension point criado em `ObservabilityServiceDescriptor`: callbacks opcionais para `TracerProviderBuilder` e `MeterProviderBuilder`, aplicados dentro dos providers existentes, antes dos exporters.
- Hosts instrumentados: `AuthCore.Api` e `NotificationCore.Api`. O `Gateway.Api` não registra pacote, meter, `AddNpgsql()` ou data source PostgreSQL.
- Tracing: `AddNpgsql()` é chamado nos dois hosts por meio do descriptor do host, usando a namespace real `Npgsql` do pacote 10.0.2.
- Métricas nativas: o meter `Npgsql` é registrado nos dois hosts e `AddNpgsqlInstrumentation()` é conectado ao mesmo `MeterProvider`.
- Data sources: `NpgsqlDataSourceBuilder.Name` usa nomes fixos de baixa cardinalidade: `authcore-postgresql` e `notificationcore-postgresql`.
- Atributos permitidos: atributos técnicos estáveis emitidos pelo Npgsql, como sistema/operação, status de erro e nome lógico do pool.
- Atributos proibidos: connection string, `Host=`, `Username=`, `Password=`, SQL completo, parâmetros, valores de parâmetros, e-mail, token, correlation ID, trace ID e IDs de negócio.
- Métricas customizadas da OBS-006 preservadas: aquisição, lease, transação e falhas de aquisição continuam separadas das métricas nativas Npgsql.
- Limitação real do driver: `NpgsqlDataSourceBuilder.Name` existe, mas `NpgsqlDataSource` não expõe `Name` publicamente; a validação do pool name deve usar os atributos exportados por spans/métricas.
- Correção de validação pós-OBS-010: a regressão dos testes reais `AddObservability_WhenNpgsqlCommandRuns_ShouldExportSafeChildSpanAndNativeMetrics` foi de configuração de execução, não de código RabbitMQ/Redis/Npgsql. Os comandos estavam usando connection strings com `Pooling=false`, enquanto a asserção valida métricas nativas de pool `db.client.connection.*`; com pooling desabilitado o Npgsql exporta `db.client.operation.duration`, mas não emite métricas de conexão do pool.
- Resultado da investigação: os testes falham isoladamente com `Pooling=false` e passam isolados, juntos, após Redis real e nas duas ordens com RabbitMQ real quando executados com `Pooling=true;Minimum Pool Size=0;Maximum Pool Size=5`. Não foi comprovada interferência entre providers, processors, exporters ou estado global do OpenTelemetry.
- Regra de execução do gate real OBS-008: quando `OBSERVABILITY_POSTGRES_REQUIRED=true`, as variáveis `AUTHCORE_TEST_POSTGRES` e `NOTIFICATIONCORE_TEST_POSTGRES` devem manter pooling habilitado para validar simultaneamente spans Npgsql, `db.client.operation.duration` e métricas nativas `db.client.connection.*`.

### OBS-009 — Instrumentar Redis sem pacote beta

- **Objetivo:** medir operações Redis próprias com API estável.
- **Contexto técnico:** Redis existe apenas no AuthCore; a instrumentação contrib disponível é pré-release e as operações próprias estão concentradas em `RedisSessionStore` e `RedisLoginRateLimiter`.
- **Projetos:** AuthCore.Api e AuthCore.Infrastructure.
- **Alterar:** `RedisSessionStore.cs`, `RedisLoginRateLimiter.cs`, `AuthCore.Api/Program.cs`; testes de integração de observabilidade Redis.
- **Criar:** `AuthCore.Infrastructure/Observability/RedisTelemetry.cs`.
- **MeterName:** `authcore.redis`.
- **ActivitySource:** `authcore.redis`.
- **Instrumentos:** `authcore.redis.operation.duration` (`Histogram<double>`, unidade `s`, descrição `Duration of Redis operations performed by AuthCore.`, labels `operation` e `result`) e `authcore.redis.operation.failures` (`Counter<long>`, unidade `{operation}`, descrição `Number of failed Redis operations performed by AuthCore.`, labels `operation` e `error.type`).
- **Allowlists:** `operation`: `get`, `set`, `delete`, `eval`, `expire`; `result`: `success`, `failure`, `cancelled`; `error.type`: `timeout`, `connectivity`, `authentication`, `protocol`, `cancelled`, `unknown`.
- **Operações instrumentadas:** `RedisSessionStore` mede `StringGetAsync`/`KeyExistsAsync`/`SetMembersAsync` como `get`, `SetRemoveAsync`/`KeyDeleteAsync` como `delete` e `ScriptEvaluateAsync` como `eval`; `RedisLoginRateLimiter` mede `StringIncrementAsync` como `set`, `KeyExpireAsync` como `expire` e `KeyTimeToLiveAsync` como `get`.
- **Componentes deliberadamente não instrumentados:** Redis do Data Protection, `RedisHealthCheck`, profiling/administração do servidor Redis e infraestrutura do container.
- **Estratégia de testes reais:** testes de integração com Redis do `docker-compose.yml`, prefixo isolado por teste, limpeza apenas de keys do prefixo, skip real quando Redis é opcional e falha obrigatória com `OBSERVABILITY_REDIS_REQUIRED=true`.
- **Limitações:** o rate limiter real não usa Lua; portanto `eval` é observado nas operações de script já existentes do `RedisSessionStore`, não no rate limiter. A classificação `authentication` usa `RedisConnectionException.FailureType` quando a versão atual do StackExchange.Redis expõe essa informação.
- **Dependências:** OBS-006.
- **Aceite:** duração e falha mensuráveis; spans filhos preservam trace; Data Protection continua funcional sem instrumentação detalhada.
- **Validação:** testes com fake/multiplexer de integração disponível e listeners.
- **Riscos/cuidados:** não criar uma abstração genérica de cache; evitar medir duas vezes a mesma chamada.
- **Fora do escopo:** pacote `OpenTelemetry.Instrumentation.StackExchangeRedis` pré-release e profiling de Data Protection.

### OBS-010 — Instrumentar RabbitMQ e propagar trace context

> Implementado: foi adotado `MessageEnvelope<T>` com `MessageEnvelopeMetadata` em `Shared.Messaging.Contracts`, contendo `CorrelationId`, `TraceParent` e `TraceState` opcionais. O contrato de negócio `SendTransactionalNotificationRequested` permaneceu sem campos de observabilidade. `Shared.Observability` permanece sem RabbitMQ. AuthCore registra `authcore.rabbitmq` como MeterName/ActivitySource e NotificationCore registra `notificationcore.rabbitmq`; Gateway não registra telemetria RabbitMQ. Os spans são `rabbitmq publish notification_requests` (`Producer`) e `rabbitmq consume notification_requests` (`Consumer`). As métricas criadas são `authcore.rabbitmq.messages.published` (`queue`, `result`, `event_type`) e `notificationcore.rabbitmq.messages.consumed` (`queue`, `result`). Apenas `traceparent` e `tracestate` são propagados; `baggage` é removido. Mensagens legadas continuam aceitas por fallback de desserialização. Publisher confirms já existiam (`ConfirmSelect`/`WaitForConfirmsOrDie`), portanto `success` representa confirmação do broker. `notificationcore.rabbitmq.requeues` não foi criada porque `messages.consumed{result=requeue}` cobre o evento sem métrica redundante.
>
> Validação real: executada contra o serviço `rabbitmq` do `src/Backend/docker-compose.yml` (`rabbitmq:3-management-alpine`, container `backend-rabbitmq-1`) com recursos isolados por teste. O RabbitMQ Client 6.8.1 devolveu os headers W3C aceitos pelo carrier seguro (`byte[]` UTF-8 ou `string`). Foi validado o fluxo Activity original -> Outbox -> publisher real com confirms -> broker real -> consumer real, com producer e consumer no mesmo `TraceId` e consumer filho do producer. Foram validados `ack`, `requeue`, `dead_letter`, métrica de falha de publish por conflito real de topologia, sampling `0`, observabilidade desabilitada e ausência de sentinelas em spans/métricas RabbitMQ. No ambiente local, o volume RabbitMQ existente tinha credencial divergente de `.env.development`; a validação obrigatória foi executada com senha temporária de processo para o usuário existente, sem imprimir segredo, e depois a senha foi restaurada para o valor local.

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

- **Objetivo:** localizar latência/falha do despacho e da tentativa SMTP sem afirmar entrega na caixa do destinatário.
- **Contexto técnico:** `notificationcore.notifications.send.duration` media exatamente a tentativa técnica do `SmtpEmailProvider`; por isso foi substituída por métricas SMTP específicas para evitar duplicidade. O dispatcher já retornava `DispatchCounters`, ponto seguro para transportar contagens sem acoplar Application ao OTel.
- **Projetos:** NotificationCore.Api, NotificationCore.Application e NotificationCore.Infrastructure.
- **Alterar:** `RegisterNotificationRequestCommand.cs`, `RegisterNotificationRequestUseCase.cs`, `DispatchCounters.cs`, `PendingNotificationDispatcher.cs`, `DispatchPendingNotificationResult.cs`, `SmtpEmailProvider.cs`, `NotificationDispatcherHostedService.cs`, `NotificationMetrics.cs`, `InboxRepository.cs`, `Program.cs` e testes.
- **Criar:** `NotificationCore.Infrastructure/Observability/SmtpTelemetry.cs`, `NotificationCore.Api/Observability/NotificationDispatchTelemetry.cs`, `NotificationCore.Api/Observability/NotificationDispatchTelemetryContext.cs`, `NotificationCore.Api/Observability/NotificationDispatchTelemetryResult.cs` e `NotificationCore.Api/Observability/NotificationDispatchTelemetryContextReader.cs`.
- **Passos:** manter Application sem referência a OpenTelemetry, `Meter`, `Counter`, `Histogram` ou `ActivitySource`; preservar o payload original do RabbitMQ no inbox quando a mensagem chega envelopada; extrair `TraceParent`/`TraceState` do envelope persistido durante a retomada; criar `notification dispatch` no composition root da Api com `ActivityLink` para o contexto original quando não houver `Activity.Current`; criar `smtp send` no provider técnico como filho natural do dispatch; renomear `notificationcore.notifications.send.duration` para `notificationcore.smtp.send.duration`; criar `notificationcore.smtp.send.attempts`; criar `notificationcore.notifications.delivery.retries` somente para retry agendado; registrar categoria de erro allowlisted, não exception type/message livre; preservar correlation em scope/header de e-mail, nunca em labels/tags.
- **Dependências:** OBS-006/010.
- **Aceite:** tentativa, sucesso, falha e duração SMTP mensuráveis; `notification_type=email_verification` é emitido somente para o template fechado `auth.email-confirmation`; retry identificado; nenhum tipo livre vira label; dispatch retomado usa link, não parent remoto falso; SMTP é filho do dispatch; mensagens legadas sem envelope continuam processáveis.
- **Validação:** `SmtpEmailProviderTests`, `NotificationDispatcherHostedServiceTests`, `CustomMetricsTests`, `SmtpAndDispatcherTelemetryTests`, testes de Application do dispatcher e regressões reais PostgreSQL/Redis/RabbitMQ.
- **Riscos/cuidados:** o dispatcher assíncrono perde o parent natural após persistência; não fingir parentesco inválido, usar link/correlation. Não há servidor SMTP local no compose; os testes automatizados usam adapter/fake seguro e telemetria em memória, sem envio real a usuários.
- **Fora do escopo:** conteúdo/destinatário do e-mail.

#### Definição OBS-011

- `notificationcore.notifications.send.duration` foi removida para não duplicar o cronômetro do SMTP.
- `notificationcore.smtp.send.duration`: `Histogram<double>`, unidade `s`, labels `provider` e `result`, valores fechados `provider=smtp` e `result=success|failure|cancelled`.
- `notificationcore.smtp.send.attempts`: `Counter<long>`, unidade `{attempt}`, mesmas labels e valores.
- `notificationcore.notifications.delivery.retries`: `Counter<long>`, unidade `{retry}`, label `notification_type`; semântica escolhida: retry agendado após falha temporária do provider. Valores: `email_verification`, `test_email`, `other_transactional`.
- Métricas preservadas: `notificationcore.notifications.pending`, `notificationcore.notifications.sent`, `notificationcore.notifications.failed` e `notificationcore.notifications.dispatch.duration`; `sent` significa aceitação pelo provider sem erro retornado ao cliente, não entrega na caixa do destinatário.
- ActivitySource de dispatch: `notificationcore.notifications`, span `notification dispatch`, `ActivityKind.Internal`, tags permitidas `notification.type`, `notification.channel`, `notification.result` e `notification.retry`.
- ActivitySource SMTP: `notificationcore.smtp`, span `smtp send`, `ActivityKind.Client`, tags permitidas `server.address`, `server.port`, `notification.provider`, `notification.result` e `error.type`.
- `error.type` SMTP usa allowlist: `timeout`, `connectivity`, `authentication`, `protocol`, `cancelled`, `unknown`.
- Refinamento arquitetural posterior da OBS-011: a implementação inicial usou `INotificationDispatchTelemetry` na Application; essa interface foi removida. A Application passou a retornar apenas `DispatchPendingNotificationResult`/`DispatchCounters` neutros, sem callbacks de observabilidade, `TraceParent`, `TraceState`, spans ou nomes de métricas.
- A telemetria do dispatch ficou no boundary técnico da Api: `NotificationDispatcherHostedService` lê contextos seguros por `NotificationDispatchTelemetryContextReader`, chama o caso de uso dentro de `NotificationDispatchTelemetry.TrackAsync`, aplica status/tags no span e emite métricas a partir do resultado funcional.
- Não houve migration: o payload original envelopado é preservado em `InboxMessages.Payload`, e `InboxRepository` busca tanto `IdempotencyKey` legado no topo quanto `Payload.IdempotencyKey` no envelope. Registros antigos continuam válidos.
- Semântica de SMTP success: sucesso é registrado somente quando connect/auth/send/disconnect terminam sem exceção, preservando o comportamento existente do provider.

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

#### Implementado na OBS-012

- **Inventário real:** havia somente `ExternalAuthenticationMetrics` em `AuthCore.Api`, com meter `authcore.auth.external`, contadores `authcore.auth.external.login.started`, `authcore.auth.external.login.succeeded`, `authcore.auth.external.login.failed`, `authcore.auth.external.login.cancelled` e histograma `authcore.auth.external.callback.duration`.
- **Métricas Google removidas:** o contador de redirect inicial e o histograma de duração do callback foram removidos. O callback inválido deixou de gerar `cancelled` e `failed` simultaneamente.
- **Meter final:** `authcore.authentication`, registrado apenas no host `AuthCore.Api`.
- **Instrumentos finais:**
  - `authcore.authentication.attempts`, Counter, unidade `{attempt}`, labels `flow`, `result`, `reason`.
  - `authcore.refresh_tokens.operations`, Counter, unidade `{operation}`, labels `operation`, `result`, `reason`.
  - `authcore.sessions.operations`, Counter, unidade `{operation}`, labels `operation`, `result`, `reason`.
  - `authcore.registration.attempts`, Counter, unidade `{attempt}`, labels `result`, `reason`.
  - `authcore.email_verification.attempts`, Counter, unidade `{attempt}`, labels `result`, `reason`.
- **Allowlists:** `flow` usa `password_session`, `password_token`, `google_session`; `operation` usa `create`, `rotate`, `revoke`, `revoke_all`; `result` e `reason` são normalizados por instrumento no código, sem valores livres.
- **Boundary:** `AuthBusinessMetrics`, `AuthFlowTelemetryMiddleware` e o fluxo Google ficaram em `AuthCore.Api/Observability` ou no boundary HTTP. Domain e Application não receberam dependências de OpenTelemetry nem abstrações de telemetria.
- **Antiduplicação:** o middleware não observa o redirect `GET /api/auth/external/google`; o fluxo Google registra sucesso apenas quando o callback cria sessão. Quando o callback exige onboarding, o sucesso `google_session` é contado no `POST /api/auth/external/google/onboarding`, evitando contagem dupla. Login por sessão registra também `sessions.operations{operation=create}` somente em sucesso.
- **Validação adicionada:** `CustomMetricsTests` valida catálogo, unidades e ausência das métricas legadas; `AuthFlowTelemetryMiddlewareTests` valida classificação de login por sessão, rate limit, redirect Google e refresh token.
- **Desvios do plano:** não foi alterado `ApiExceptionHandler` para escrever razões em `HttpContext.Items`, porque os status atuais cobrem os casos seguros principais sem acoplar o handler a nomes de métrica. Reuse, expiração e revogação de refresh token continuam agregados como `invalid_token` no middleware porque a Application lança exceção genérica segura. Também não foram adicionadas tags `auth.flow`/`auth.result` em spans servidor, pois a execução solicitada restringiu a OBS-012 a métricas e proibiu novos spans de autenticação.

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
- **Inventário OBS-013:** antes da alteração existia apenas `/health` nos três hosts. AuthCore registrava `postgresql`, `redis` e `outbox`, sem tags. NotificationCore registrava `postgresql`, `rabbitmq` e `dispatcher`, sem tags. Gateway registrava health checks sem checks específicos e expunha `/health` com `Degraded` mapeado para HTTP 200. Não havia pacotes `AspNetCore.HealthChecks.*` nem HealthChecks UI; o uso era do `Microsoft.Extensions.Diagnostics.HealthChecks` do framework. O response writer existente era o padrão do middleware, sem JSON sanitizado próprio. O Gateway/Ocelot e testes/documentação consumiam `/health`, portanto o endpoint legado foi preservado como alias de readiness.
- **Inventário de dependências:** PostgreSQL já usava `NpgsqlDataSource` singleton em AuthCore e NotificationCore; os health checks antigos passavam por `IDbConnectionFactory`, que emite métricas técnicas de conexão. Redis já usava `IConnectionMultiplexer` singleton. RabbitMQ não tinha conexão singleton compartilhada para health; publisher/consumer abrem conexões próprias com `RabbitMQ.Client`. SMTP usava `SmtpEmailProvider`/MailKit para envio; o health check não reutiliza envio. Outbox, consumer RabbitMQ, dispatcher e SMTP são controlados por `Outbox:Enabled`, `RabbitMq:Enabled`/`NOTIFICATIONCORE_RABBITMQ_CONSUMER_ENABLED` e `NotificationDispatcher:Enabled`.
- **Endpoints finais:** os três hosts expõem `/health/live`, `/health/ready`, `/health/dependencies` e preservam `/health` como alias de `/health/ready`. Todos desabilitam cache, usam `AllowAnonymous`, retornam `Healthy`/`Degraded` como HTTP 200 e `Unhealthy` como HTTP 503.
- **Tags finais:** `live`, `ready`, `dependency`, `critical`, `optional`, centralizadas em `Shared.Observability.HealthCheckTags`.
- **Resposta:** `/health/live`, `/health/ready` e `/health` retornam somente `{ "status": "Healthy" }`. `/health/dependencies` retorna JSON seguro com `status` agregado e `checks` ordenados por nome, contendo apenas `name`, `status` e `durationMs`. Não são serializados `description`, `exception`, `data`, mensagens, stack trace, host, porta, banco, endpoint, queue, exchange, connection string ou credenciais.
- **Checks finais por serviço:** AuthCore registra `self` (`live`, `ready`), `postgresql` (`ready`, `dependency`, `critical`), `redis` (`ready`, `dependency`, `critical`) e `rabbitmq` (`dependency`, `optional`) somente quando `Outbox:Enabled=true`. NotificationCore registra `self`, `postgresql` crítico, `rabbitmq` crítico somente quando consumer habilitado e `smtp` opcional somente quando dispatcher habilitado. Gateway registra apenas `self`, sem fan-out para AuthCore, NotificationCore ou dependências internas deles.
- **Implementação das dependências:** PostgreSQL usa o `NpgsqlDataSource` existente, abre conexão, executa `SELECT 1` e descarta a conexão. Redis usa o `IConnectionMultiplexer` existente e executa `PING`, sem `SCAN`, `GET`, `SET` ou métricas de negócio. RabbitMQ abre conexão/canal curtos quando necessário, não publica, não consome e não declara topologia. SMTP cria `SmtpClient`, conecta/TLS quando configurado e desconecta, sem autenticar, sem criar mensagem e sem chamar `SmtpEmailProvider.SendAsync`.
- **Timeouts:** checks de dependência usam `HealthChecks:DependencyTimeoutSeconds`, padrão local `5`, limitado entre `1` e `30` segundos, com `CancellationToken` propagado.
- **Baixo ruído:** `RequestLoggingMiddleware` já tratava `/health` e `/health/*` como health e não logava sucesso por padrão. A instrumentação ASP.NET Core já excluía `/health` e `/health/*` quando `Observability:ExcludeHealthChecks=true`. Nenhuma métrica customizada de health check foi adicionada.
- **Docker:** não havia `HEALTHCHECK` nos containers das APIs, então não foi adicionado `curl`/`wget` nem aumento de imagem. Checks nativos de PostgreSQL, Redis e RabbitMQ foram preservados. O `depends_on` do AuthCore para RabbitMQ foi relaxado de `service_healthy` para `service_started`, alinhando RabbitMQ como dependência opcional do AuthCore quando a Outbox desacopla publicação. NotificationCore mantém RabbitMQ como `service_healthy`, pois o consumer é crítico quando habilitado.
- **Validação adicionada:** testes de smoke validam registros, nomes e tags por host; testes de endpoints validam status HTTP, cache desabilitado, resposta sanitizada e ordenação determinística; teste arquitetural valida que Application/Domain não referenciam health checks, que `Shared.Observability` não referencia Npgsql/Redis/RabbitMQ/MailKit/AuthCore/NotificationCore e que nenhum pacote HealthChecks UI/AspNetCore.HealthChecks foi adicionado.
- **Desvios do plano:** `outbox` deixou de ser health check registrado porque era consulta funcional de repositório e não dependência técnica padronizada. SMTP não participa de readiness; foi classificado como `dependency/optional` para evitar retirar tráfego enquanto há persistência e retry. Dependência desabilitada não retorna `Healthy("disabled")`; o check não é registrado, evitando falso sinal de execução. PostgreSQL passou a usar `NpgsqlDataSource` diretamente, não `IDbConnectionFactory`, para evitar métricas funcionais/técnicas de conexão durante probes.

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
| `app.exceptions.unhandled` | Counter | Auth/Notification | Exceções inesperadas tratadas pelo handler global. | error.type allowlist | Exception handler | OBS-007 |
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
| `authcore.authentication.attempts` | Counter | Auth | Tentativas de autenticação password session/token e Google. | flow, result, reason | API/Google flow | OBS-012 |
| `authcore.refresh_tokens.operations` | Counter | Auth | Operações de refresh token. | operation, result, reason | API | OBS-012 |
| `authcore.sessions.operations` | Counter | Auth | Operações de sessão browser. | operation, result, reason | API | OBS-012 |
| `authcore.registration.attempts` | Counter | Auth | Tentativas de registro. | result, reason | API | OBS-012 |
| `authcore.email_verification.attempts` | Counter | Auth | Tentativas de verificação de e-mail. | result, reason | API | OBS-012 |

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
- [ ] Exceção inesperada tratada pelo handler global incrementa `app.exceptions.unhandled` uma vez e marca span Error.
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
