# Observabilidade

Este guia descreve a observabilidade operacional dos serviços `AuthCore`, `NotificationCore` e `Gateway`. O objetivo é permitir validação local reproduzível, explicar os sinais disponíveis e registrar limites de segurança antes da adoção de um backend definitivo de visualização.

## Arquitetura

A base de observabilidade fica em `src/Shared/Observability` e é registrada pelos três hosts via `AddObservability`. Esse projeto compartilhado contém apenas bootstrap OpenTelemetry, opções, correlação HTTP, logging de requisição, health checks e métricas transversais. Métricas e spans específicos permanecem no serviço e na camada que executa a operação.

Cada host define `Observability:ServiceName` de forma fixa:

| Host | ServiceName | Sinais principais |
| --- | --- | --- |
| AuthCore.Api | `authcore-api` | HTTP, logs de requisição, PostgreSQL/Npgsql, Redis, RabbitMQ publisher, Outbox, métricas de autenticação e exceções inesperadas. |
| NotificationCore.Api | `notificationcore-api` | HTTP, logs de requisição, PostgreSQL/Npgsql, RabbitMQ consumer, SMTP, dispatcher, métricas de notificação e exceções inesperadas. |
| Gateway.Api | `gateway-api` | HTTP, logs de requisição, forwarding Ocelot/HttpClient, correlação e health checks locais. |

O resource OpenTelemetry inclui o namespace `auth-core-microservices`, nome do serviço, versão do assembly e `deployment.environment.name`. `service.name` é definido pelo appsettings; `OTEL_SERVICE_NAME` não é a fonte autoritativa nesta fase.

## Correlação

Toda requisição passa pelo middleware de correlation ID.

- Header de entrada e saída: `X-Correlation-Id`.
- Valor válido: até 128 caracteres, usando letras, números, `.`, `_` ou `-`.
- Sem header ou com valor inválido: o serviço gera um GUID novo e não ecoa o valor inválido.
- O correlation ID é colocado em `HttpContext.Items`, no escopo de log e na tag local `correlation.id`.
- O Gateway encaminha o correlation ID para os serviços downstream.
- RabbitMQ propaga correlation ID e contexto W3C (`traceparent`/`tracestate`) separadamente no envelope/headers de mensagem.

`correlationId`, `traceId` e `spanId` têm papéis diferentes. O correlation ID é um identificador de investigação; o trace ID é o identificador W3C do trace; o span ID muda por operação.

## Logging

O request logging registra conclusão segura de requisição com campos estruturados:

- `ServiceName`
- `EnvironmentName`
- `CorrelationId`
- `TraceId`
- `SpanId`
- `HttpMethod`
- `Route`
- `StatusCode`
- `ElapsedMilliseconds`
- `ErrorCategory`
- `IsSlowRequest`
- `UserId`, somente quando `Observability:RequestLogging:IncludeUserId=true` e o usuário autenticado possui identificador estável.

Por padrão, requisições bem-sucedidas, client errors e health checks não geram log de conclusão para reduzir ruído. Erros 5xx, rate limit e requisições lentas continuam registráveis conforme configuração.

O log padronizado de conclusão de requisição não deve conter body, query string, headers sensíveis, cookies, tokens, senha, e-mail, payload de mensagem, connection string, SQL, parâmetros SQL, stack trace ou conteúdo SMTP.

Logs gerais da aplicação também são enviados ao provider OpenTelemetry quando o exporter OTLP de logs está habilitado. Portanto, a validação de segurança precisa cobrir request logs e logs emitidos por publishers, workers, providers e exception handlers. O Collector local com exporter `debug` imprime esses logs no stdout do container; use apenas em ambiente local e sem dados reais de usuário.

## Traces

Os hosts usam instrumentação OpenTelemetry para ASP.NET Core e HttpClient. As integrações específicas adicionam spans customizados:

| Origem | Serviço | Observação |
| --- | --- | --- |
| ASP.NET Core | Todos | Span servidor por requisição; health pode ser excluído por `Observability:ExcludeHealthChecks`. |
| HttpClient/Ocelot | Todos, principalmente Gateway | Span cliente sem query string nem headers sensíveis. |
| Npgsql | AuthCore e NotificationCore | Spans de banco com pool nomeado e sem SQL/parâmetros sensíveis. |
| Redis | AuthCore | Spans `redis <operation>` sem key, script ou valor. |
| RabbitMQ publisher | AuthCore | Span `rabbitmq publish notification_requests`. |
| RabbitMQ consumer | NotificationCore | Span `rabbitmq consume notification_requests`. |
| SMTP | NotificationCore | Span `smtp send` sem destinatário, assunto ou corpo. |
| Dispatcher | NotificationCore | Span `notification dispatch`, sem identificador de notificação no nome/tags. |

Exceções inesperadas são tratadas pelo exception handler global. A métrica transversal é registrada uma vez por requisição e o span atual é marcado como erro sem expor mensagem ou stack trace.

## Métricas

Métricas técnicas nativas e customizadas são exportadas pelos meters registrados em cada host.

| Meter | Métricas |
| --- | --- |
| `app.observability` | `app.exceptions.unhandled` |
| `authcore.database` | `authcore.db.connection.acquire.duration`, `authcore.db.connection.lease.duration`, `authcore.db.transaction.duration`, `authcore.db.connection.acquire.failures` |
| `notificationcore.database` | `notificationcore.db.connection.acquire.duration`, `notificationcore.db.connection.lease.duration`, `notificationcore.db.transaction.duration`, `notificationcore.db.connection.acquire.failures` |
| `Npgsql` | Métricas nativas do Npgsql, incluindo duração de operação e métricas de pool quando pooling está ativo. |
| `authcore.redis` | `authcore.redis.operation.duration`, `authcore.redis.operation.failures` |
| `authcore.rabbitmq` | `authcore.rabbitmq.messages.published` |
| `notificationcore.rabbitmq` | `notificationcore.rabbitmq.messages.consumed` |
| `authcore.outbox` | `authcore.outbox.messages.processed`, `authcore.outbox.messages.failed`, `authcore.outbox.processing.duration` |
| `authcore.authentication` | `authcore.authentication.attempts`, `authcore.refresh_tokens.operations`, `authcore.sessions.operations`, `authcore.registration.attempts`, `authcore.email_verification.attempts` |
| `notificationcore.notifications` | `notificationcore.notifications.pending`, `notificationcore.notifications.sent`, `notificationcore.notifications.failed`, `notificationcore.notifications.dispatch.duration`, `notificationcore.notifications.delivery.retries` |
| `notificationcore.smtp` | `notificationcore.smtp.send.duration`, `notificationcore.smtp.send.attempts` |

Labels devem ter cardinalidade controlada. São proibidos em labels de métricas, nomes de spans e tags de spans: `userId`, e-mail, token, cookie, access/refresh token, senha, session/message/request/correlation/trace ID, URL/path resolvido, SQL, parâmetros SQL, chave Redis, payload, destinatário SMTP, exception message e stack trace.

Logs estruturados podem conter `CorrelationId`, `TraceId` e `SpanId` para investigação. `UserId` só pode aparecer em logs quando `Observability:RequestLogging:IncludeUserId=true`, houver usuário autenticado e a política operacional aceitar esse identificador em logs. Esses identificadores continuam proibidos em métricas e spans.

## Configuração

Configuração base:

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

Variáveis relevantes:

| Variável | Uso |
| --- | --- |
| `OBSERVABILITY__ENABLED` | Desliga providers/exporters quando `false`; correlation ID e request logging permanecem no pipeline. |
| `OBSERVABILITY__OTLPENABLED` | Habilita exporter OTLP quando `true`. Em desenvolvimento fica `false` por padrão. |
| `OBSERVABILITY__CONSOLEEXPORTERENABLED` | Habilita exporter de console para diagnóstico local pontual. |
| `OBSERVABILITY__TRACESAMPLINGRATIO` | Razão de sampling de traces entre `0` e `1`. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Endpoint OTLP absoluto quando OTLP está habilitado. No Docker com Collector: `http://otel-collector:4317`. |
| `OTEL_EXPORTER_OTLP_HEADERS` | Apenas via secret store da plataforma, nunca versionado. |
| `OTEL_RESOURCE_ATTRIBUTES` | Atributos extras de deploy, sem PII nem segredo. |

Se `Observability:OtlpEnabled=true`, o endpoint OTLP precisa ser absoluto e usar `http` ou `https`; ausência ou formato inválido falha no startup. Se o endpoint fica indisponível depois do startup, os exporters falham internamente, mas não devem falhar requests, startup ou health checks.

## Sampling

Traces usam `ParentBased`.

- Development: `TraceSamplingRatio=1.0`, equivalente a always-on quando não há pai.
- Configuração base/prod: `TraceSamplingRatio=0.1`.
- Métricas e logs não seguem sampling de traces.
- `OTEL_TRACES_SAMPLER*` não é usado nesta fase.

Para validar propagação sem exportar spans customizados, use `TraceSamplingRatio=0`; as métricas e headers de trace continuam funcionais.

## Health Checks

Os três hosts expõem endpoints padronizados:

| Endpoint | Semântica |
| --- | --- |
| `/health/live` | Liveness sem dependências externas. |
| `/health/ready` | Readiness com dependências marcadas como `ready`. |
| `/health/dependencies` | Lista sanitizada e ordenada de dependências técnicas. |
| `/health` | Alias de readiness para compatibilidade. |

AuthCore registra `self`, PostgreSQL, Redis e RabbitMQ quando a Outbox está habilitada. NotificationCore registra `self`, PostgreSQL, RabbitMQ quando o consumer está habilitado e SMTP quando o dispatcher está habilitado. Gateway registra apenas `self` e não faz fan-out para AuthCore ou NotificationCore.

O Collector nunca é dependência de readiness.

## Execução Local

Sem Collector:

```bash
./run.sh docker
```

Com Collector opcional:

```bash
docker compose --env-file src/Backend/.env.development -f src/Backend/docker-compose.yml --profile observability up --build
```

Antes de usar o profile de observabilidade, altere no `.env.development`:

```env
OBSERVABILITY__OTLPENABLED=true
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

O Collector escuta:

- OTLP gRPC: `localhost:4317`
- OTLP HTTP: `localhost:4318`

A configuração local do Collector fica em `src/Backend/observability/otel-collector-config.yaml` e usa exporter `debug`. Os logs do Collector são a forma local de inspecionar traces, métricas e logs sem Grafana/Tempo/Prometheus/Loki. Como o exporter `debug` imprime o conteúdo dos sinais recebidos, não use esse profile com tráfego real, credenciais reais ou payloads de usuário.

Comandos úteis:

```bash
docker compose --env-file src/Backend/.env.development -f src/Backend/docker-compose.yml --profile observability logs -f otel-collector
docker compose --env-file src/Backend/.env.development -f src/Backend/docker-compose.yml --profile observability stop otel-collector
docker compose --env-file src/Backend/.env.development -f src/Backend/docker-compose.yml down --remove-orphans
```

Parar o Collector não deve derrubar APIs, health checks ou fluxo de negócio.

## Troubleshooting

`OtlpEnabled=true` e a aplicação falha no startup:

- Verifique se `OTEL_EXPORTER_OTLP_ENDPOINT` está preenchido.
- Confirme que o endpoint começa com `http://` ou `https://`.
- Em Docker Compose, use `http://otel-collector:4317`, não `localhost`, porque `localhost` dentro do container aponta para o próprio container da API.

Collector sem sinais:

- Confirme que o profile foi ativado com `--profile observability`.
- Confirme que `OBSERVABILITY__OTLPENABLED=true` está no `.env.development` usado pelo compose.
- Gere tráfego HTTP ou execute fluxos que acionem Redis, RabbitMQ, SMTP ou banco.
- Verifique os logs com `docker compose ... logs -f otel-collector`.

Métricas Npgsql de pool ausentes:

- Confirme que a connection string usa pooling ativo.
- Em testes de integração, connection strings com `Pooling=false` podem emitir duração de operação, mas não métricas de pool.

Ruído em logs locais:

- Mantenha `ConsoleExporterEnabled=false`.
- Mantenha `LogSuccessfulRequests=false` e `LogHealthChecks=false` salvo diagnóstico pontual.

Dados sensíveis em sinais:

- Interrompa a validação e trate como bug.
- Procure por sentinelas em logs, spans e métricas capturados em memória.
- Não resolva mascarando no backend de observabilidade; corrija a origem do sinal.

## Validação

Validação mínima antes de concluir mudanças de observabilidade:

```bash
dotnet build AuthCore.sln
dotnet test AuthCore.sln
docker compose --env-file src/Backend/.env.development.example -f src/Backend/docker-compose.yml config --quiet
docker compose --env-file src/Backend/.env.development.example -f src/Backend/docker-compose.yml --profile observability config --quiet
```

Quando Docker e dependências reais estiverem disponíveis, execute também testes de integração com PostgreSQL, Redis e RabbitMQ reais. Alguns testes pulam ou reduzem cobertura quando a infraestrutura está ausente; isso deve ser tratado como limitação da validação, não como sucesso ponta a ponta.

Checklist funcional:

- Request sem header recebe `X-Correlation-Id`.
- Request com header válido preserva `X-Correlation-Id`.
- Request com header inválido recebe novo ID e não ecoa o valor inválido.
- Gateway e downstream usam o mesmo correlation ID.
- Logs estruturados contêm serviço, ambiente, correlation ID, trace ID, span ID, método, rota template, status e duração.
- `userId` só aparece quando explicitamente habilitado e autenticado.
- Métricas HTTP usam rota template, método e status, nunca path bruto.
- Exceção inesperada incrementa `app.exceptions.unhandled` uma vez por requisição.
- Npgsql não exporta connection string, SQL nem parâmetros.
- Redis não exporta keys, scripts nem valores.
- RabbitMQ propaga correlation ID e W3C trace context; mensagens legadas continuam aceitas.
- SMTP mede tentativa, duração e resultado sem destinatário, assunto ou conteúdo.
- Métricas de negócio usam allowlists de `flow`, `operation`, `result`, `reason`, `notification_type`, `channel` e `provider`.
- Logs gerais exportados via OTLP não contêm segredo, payload, token, senha, e-mail real, connection string, SQL com parâmetros nem stack trace indevido.
- `/health/live` funciona sem dependências.
- `/health/ready` e `/health` refletem readiness.
- Collector desligado não altera startup, request ou health.
- Stack Docker é válida com e sem profile `observability`.

## Evidências Desta Implementação

Comandos executados durante a OBS-015:

```bash
docker compose --env-file src\Backend\.env.development.example -f src\Backend\docker-compose.yml config --quiet
docker compose --env-file src\Backend\.env.development.example -f src\Backend\docker-compose.yml --profile observability config --quiet
git diff --check -- docs\observability.md src\Backend\README.md
dotnet build AuthCore.sln
dotnet test AuthCore.sln --no-build
dotnet list AuthCore.sln package --vulnerable --include-transitive
```

Resultado:

- a configuração Docker é válida com e sem o profile `observability`;
- não houve erro de whitespace no escopo validado;
- `dotnet build AuthCore.sln --no-restore` passou sem avisos;
- `dotnet test AuthCore.sln --no-build` passou com 525 testes aprovados, 15 ignorados e 0 falhas;
- `dotnet list AuthCore.sln package --vulnerable --include-transitive` não encontrou pacotes vulneráveis nas fontes atuais.

Validação com infraestrutura real executada após subir PostgreSQL, Redis, RabbitMQ e o Collector opcional via Docker Compose:

- `NpgsqlInstrumentationTests`: 8 testes aprovados, 0 ignorados;
- `RedisInstrumentationTests`: 6 testes aprovados, 0 ignorados;
- `RabbitMqInstrumentationTests`: 10 testes aprovados, 0 ignorados;
- health checks dos containers reconstruídos: `/health`, `/health/live`, `/health/ready`, `/authcore/health` e `/notificationcore/health` retornaram `200`.

Limitação registrada: a suíte padrão mantém os 15 testes de integração real ignorados quando as variáveis `AUTHCORE_TEST_POSTGRES`, `NOTIFICATIONCORE_TEST_POSTGRES`, `AUTHCORE_TEST_REDIS`, `OBSERVABILITY_POSTGRES_REQUIRED`, `OBSERVABILITY_REDIS_REQUIRED` e `OBSERVABILITY_RABBITMQ_REQUIRED` não estão configuradas. Isso é intencional para permitir execução local sem infraestrutura. Para validação ponta a ponta, execute os filtros de integração real com essas variáveis apontando para dependências disponíveis.

## Evolução

Fora do escopo atual:

- dashboards;
- alertas;
- SLOs;
- política de retenção;
- escolha definitiva de backend de visualização;
- integração proprietária com Grafana Cloud, Datadog, New Relic, Azure Monitor ou AWS.

Próximos passos recomendados:

- escolher backend OTLP de produção;
- definir retenção por tipo de sinal;
- criar dashboards de HTTP, dependências, autenticação e notificações;
- definir alertas de erro 5xx, latência, falha de dependências, DLQ/requeue e SMTP;
- revisar sampling com volume e custo reais;
- revalidar tags ao atualizar OpenTelemetry, Npgsql ou RabbitMQ.Client.
