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

Logs gerais da aplicação também são enviados ao provider OpenTelemetry quando o exporter OTLP de logs está habilitado. Portanto, a validação de segurança precisa cobrir request logs e logs emitidos por publishers, workers, providers e exception handlers. A stack local documentada nesta seção envia métricas para Prometheus, traces para Jaeger e logs para Loki por meio do OpenTelemetry Collector.

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

## Stack local de observabilidade

A stack local de observabilidade segue o fluxo:

```text
Aplicações .NET
  -> OTLP gRPC/HTTP
  -> OpenTelemetry Collector
    -> métricas -> Prometheus -> Grafana
    -> traces   -> Jaeger     -> Grafana
    -> logs     -> Loki       -> Grafana
```

Responsabilidades:

- OpenTelemetry SDK: permanece nos hosts e continua responsável por instrumentar logs, traces e métricas. O exporter OTLP é habilitado por `OBSERVABILITY__OTLPENABLED=true`.
- OpenTelemetry Collector: recebe OTLP/gRPC e OTLP/HTTP, aplica `memory_limiter` e `batch`, expõe métricas em formato Prometheus, encaminha traces para Jaeger por OTLP/gRPC e encaminha logs para Loki por OTLP/HTTP.
- Prometheus: coleta o exporter Prometheus do Collector e armazena métricas locais por sete dias.
- Jaeger: armazena e consulta traces em modo all-in-one local com armazenamento em memória.
- Loki: armazena logs locais em single binary com filesystem, structured metadata e retenção configurada.
- Grafana: consulta Prometheus, Jaeger e Loki com data sources provisionados por arquivo e correlação trace/log.

Versões locais fixas:

| Componente | Imagem |
| --- | --- |
| OpenTelemetry Collector | `otel/opentelemetry-collector-contrib:0.104.0` |
| Prometheus | `prom/prometheus:v2.53.0` |
| Grafana | `grafana/grafana:11.1.0` |
| Jaeger | `jaegertracing/jaeger:2.19.0` |
| Loki | `grafana/loki:3.5.7` |

Arquivos operacionais:

| Finalidade | Arquivo |
| --- | --- |
| Collector | `src/Backend/observability/otel-collector/otel-collector.yml` |
| Prometheus | `src/Backend/observability/prometheus/prometheus.yml` |
| Loki | `src/Backend/observability/loki/loki.yml` |
| Data sources Grafana | `src/Backend/observability/grafana/provisioning/datasources/prometheus.yml` |
| Provider de dashboards | `src/Backend/observability/grafana/provisioning/dashboards/providers.yml` |
| Dashboard | `src/Backend/observability/grafana/dashboards/authcore-overview.json` |

Componentes e portas locais:

| Componente | Responsabilidade | Porta local |
| --- | --- | ---: |
| Grafana | Visualização e correlação | 3000 |
| Prometheus | Métricas | 9090 |
| Jaeger | Traces | 16686 |
| Loki | Logs | 3100 |
| OTel Collector | Recepção e roteamento OTLP | 4317, 4318, 8889, 13133 |

Como iniciar a stack completa com observabilidade:

```bash
docker compose --env-file src/Backend/.env.development -f src/Backend/docker-compose.yml --profile observability up -d
```

Esse comando também sobe os serviços sem profile do `docker-compose.yml`, incluindo APIs, bancos, Redis e RabbitMQ. Use-o quando quiser executar o ambiente local completo com Collector, Prometheus, Jaeger, Loki e Grafana.

Como iniciar a stack completa com rebuild das APIs:

```bash
docker compose --env-file src/Backend/.env.development -f src/Backend/docker-compose.yml --profile observability up -d --build
```

Antes de usar o profile de observabilidade com telemetria das APIs, configure:

```env
OBSERVABILITY__OTLPENABLED=true
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
OTEL_EXPORTER_OTLP_PROTOCOL=grpc
```

Para APIs executadas diretamente no host, use:

```env
OBSERVABILITY__OTLPENABLED=true
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
OTEL_EXPORTER_OTLP_PROTOCOL=grpc
```

Como encerrar:

```bash
docker compose --env-file src/Backend/.env.development -f src/Backend/docker-compose.yml --profile observability down
```

Como remover os dados locais:

```bash
docker compose --env-file src/Backend/.env.development -f src/Backend/docker-compose.yml --profile observability down -v
```

O uso de `-v` apaga histórico do Prometheus, configurações persistidas do Grafana, dados locais do Loki e estado local dos demais containers.

Endpoints locais:

- Collector OTLP gRPC: `localhost:4317`
- Collector OTLP HTTP: `localhost:4318`
- Collector Prometheus exporter: `localhost:8889/metrics`
- Collector health: `localhost:13133`
- Prometheus: `localhost:9090`
- Grafana: `localhost:3000`
- Jaeger UI/API: `localhost:16686`
- Loki API: `localhost:3100`

As credenciais locais do Grafana vêm de `GRAFANA_ADMIN_USER` e `GRAFANA_ADMIN_PASSWORD`. Esses valores devem ser preenchidos apenas no `.env.development` local e não devem ser versionados.

Persistência e retenção local:

- Jaeger all-in-one usa armazenamento em memória. Reiniciar ou recriar o container remove os traces. Essa topologia é apenas para desenvolvimento e validação local; produção precisa de backend persistente e dimensionado separadamente.
- Loki usa o volume Docker `loki-data`, montado em `/loki`. A retenção local padrão é `168h` e pode ser ajustada por `LOKI_RETENTION_PERIOD`.
- Para limpar logs locais, execute `docker compose --env-file src/Backend/.env.development -f src/Backend/docker-compose.yml --profile observability down -v`.
- O Loki local roda em single binary com filesystem. Produção deve avaliar volume diário, retenção, disponibilidade, object storage, replicação e operação antes de escolher a topologia.

Consultas úteis:

```logql
{service_name="gateway-api"}
{service_name="authcore-api", deployment_environment_name="Development"}
{service_name="notificationcore-api"} | trace_id = "<trace-id>"
{service_namespace="auth-core-microservices"} | correlationId = "obs-016-register"
```

O Loki normaliza atributos OTLP com ponto para labels como `service_name`, `service_namespace` e `deployment_environment_name`. A configuração local restringe labels indexadas a esses atributos estáveis. Trace ID, Span ID, Correlation ID, `service.instance.id`, IDs de usuário, request IDs, URL completa e identificadores únicos de negócio não devem ser promovidos manualmente como labels indexadas. Esses valores devem permanecer como structured metadata ou atributos consultáveis.

Correlação no Grafana:

- O data source `Jaeger` usa UID `jaeger` e aponta para `http://jaeger:16686`.
- O data source `Loki` usa UID `loki` e aponta para `http://loki:3100`.
- Em traces, `tracesToLogsV2` consulta Loki usando janela temporal do span e `trace_id`.
- Em logs, `derivedFields` tenta extrair `TraceId`, `trace_id` ou `traceid` da linha renderizada e cria link para Jaeger. Como a validação local mostrou `trace_id` e `span_id` em structured metadata, a navegação mais confiável é consultar `| trace_id = "<trace-id>"` no Explore; a extração por regex depende de como o Grafana renderiza a linha.

Validação operacional:

```bash
docker compose --env-file src/Backend/.env.development.example -f src/Backend/docker-compose.yml --profile observability config --quiet
docker compose --env-file src/Backend/.env.development -f src/Backend/docker-compose.yml --profile observability ps
docker compose --env-file src/Backend/.env.development -f src/Backend/docker-compose.yml --profile observability logs otel-collector
docker compose --env-file src/Backend/.env.development -f src/Backend/docker-compose.yml --profile observability logs prometheus
docker compose --env-file src/Backend/.env.development -f src/Backend/docker-compose.yml --profile observability logs jaeger
docker compose --env-file src/Backend/.env.development -f src/Backend/docker-compose.yml --profile observability logs loki
docker compose --env-file src/Backend/.env.development -f src/Backend/docker-compose.yml --profile observability logs grafana
```

Valide também:

- `http://localhost:13133` para health do Collector. O endpoint existe, mas o container do Collector não usa `healthcheck` do Docker porque a imagem oficial não contém shell ou cliente HTTP para executar essa validação internamente.
- `http://localhost:8889/metrics` para métricas expostas pelo Collector.
- `http://localhost:9090/targets` para confirmar o target `authcore-otel-collector` como `UP`.
- `http://localhost:16686/api/services` para confirmar serviços no Jaeger.
- `http://localhost:3100/ready` para readiness do Loki.
- `http://localhost:3100/loki/api/v1/series?match[]={service_name="gateway-api"}` para confirmar labels reais de streams novas. Se o volume `loki-data` já tiver dados antigos, `/labels` pode continuar listando labels históricas até limpeza ou retenção.
- `http://localhost:3000` para confirmar login, data sources `Prometheus`, `Jaeger` e `Loki`, pasta `AuthCore` e dashboard `AuthCore / Overview`.
- Séries com `service_name` iguais a `authcore-api`, `notificationcore-api` e `gateway-api` após gerar tráfego real.

O dashboard usa os labels de resource `service_name`, `service_namespace`, `service_version` e `deployment_environment_name` convertidos pelo Collector. Esses atributos são estáveis e de baixa cardinalidade no projeto. Não use `OTEL_RESOURCE_ATTRIBUTES` para incluir `UserId`, `CorrelationId`, `TraceId`, `SpanId`, `SessionId`, e-mail, token, connection string, URL completa, query string, payload ou mensagens de exceção.

## Troubleshooting

`OtlpEnabled=true` e a aplicação falha no startup:

- Verifique se `OTEL_EXPORTER_OTLP_ENDPOINT` está preenchido.
- Confirme que o endpoint começa com `http://` ou `https://`.
- Em Docker Compose, use `http://otel-collector:4317`, não `localhost`, porque `localhost` dentro do container aponta para o próprio container da API.

Collector sem sinais:

- Confirme que o profile foi ativado com `--profile observability`.
- Confirme que `OBSERVABILITY__OTLPENABLED=true` está no `.env.development` usado pelo compose.
- Gere tráfego HTTP ou execute fluxos que acionem Redis, RabbitMQ, SMTP ou banco.
- Verifique `http://localhost:8889/metrics` e os logs com `docker compose ... logs otel-collector`.

Jaeger sem traces:

- Confirme que `OBSERVABILITY__OTLPENABLED=true` foi aplicado antes de recriar as APIs.
- Confirme que o Collector usa `otlp/jaeger` com endpoint `jaeger:4317`.
- Consulte `http://localhost:16686/api/services`.
- Gere tráfego via Gateway para criar spans de servidor e cliente.

Loki sem logs:

- Confirme que o Collector usa `otlphttp/loki` com endpoint `http://loki:3100/otlp`.
- Não configure `/otlp/v1/logs` no endpoint do Collector; o exporter `otlphttp` adiciona `/v1/logs`.
- Confirme `allow_structured_metadata: true`, schema `v13` e index `tsdb` em `loki.yml`.
- Consulte labels reais antes de fixar queries ou links.

Prometheus target `DOWN`:

- Confirme que o Collector está healthy.
- Confirme que `src/Backend/observability/prometheus/prometheus.yml` usa `otel-collector:8889`, não `localhost:8889`.
- Confirme que os serviços estão na mesma network do Docker Compose.

Dashboard sem dados:

- Confirme que houve tráfego depois que `OBSERVABILITY__OTLPENABLED=true` foi aplicado.
- Confirme se as APIs no Docker usam `http://otel-collector:4317`.
- Confirme se APIs no host usam `http://localhost:4317`.
- Consulte `http://localhost:8889/metrics` para identificar os nomes Prometheus reais; pontos são convertidos para underscores e counters tendem a receber sufixo `_total`.
- Se o Grafana tiver volumes antigos, remova os volumes locais e suba novamente a stack.

Falha de provisionamento do Grafana:

- Verifique os logs de `grafana`.
- Confirme que o data source usa `http://prometheus:9090`.
- Confirme que os data sources usam `http://jaeger:16686` e `http://loki:3100`.
- Confirme que o dashboard está em `src/Backend/observability/grafana/dashboards/authcore-overview.json`.

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
dotnet build src/Backend/Backend.sln
dotnet test src/Backend/Backend.sln
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
- Jaeger recebe traces reais.
- Loki recebe logs reais.
- Prometheus continua com target do Collector `UP`.
- Logs permitem consultar por `service_name`, ambiente e `trace_id`.
- Um `trace_id` encontrado no Loki retorna trace no Jaeger.
- Um trace no Jaeger permite consultar logs relacionados no Loki pelo mesmo `trace_id`.

## Evidências Desta Atualização

Comandos executados durante a atualização da stack local com Jaeger e Loki:

```bash
docker compose --env-file .env.development --profile observability config --quiet
docker compose --env-file .env.development --profile observability up -d --build
docker compose --env-file .env.development --profile observability up -d --force-recreate otel-collector
docker compose --env-file .env.development --profile observability up -d --force-recreate authcore-api notificationcore-api gateway-api grafana
docker compose --env-file .env.development --profile observability ps
Invoke-WebRequest http://localhost:13133
Invoke-WebRequest http://localhost:3100/ready
Invoke-WebRequest http://localhost:16686/api/services
Invoke-WebRequest http://localhost:3100/loki/api/v1/labels
Invoke-WebRequest http://localhost:9090/api/v1/targets
```

Resultado:

- `docker compose --env-file .env.development --profile observability config --quiet` passou sem erros.
- Jaeger, Loki, Prometheus, Grafana, PostgreSQL, Redis e RabbitMQ ficaram `healthy`.
- O Collector respondeu `{"status":"Server available"}` em `http://localhost:13133` e não apresentou export failures contínuos nos logs.
- Jaeger retornou serviços `authcore-api`, `gateway-api`, `notificationcore-api` e `jaeger`.
- Loki retornou séries novas com labels reais estáveis `deployment_environment_name`, `service_name` e `service_namespace` após a configuração explícita de labels OTLP. O endpoint `/labels` ainda pode listar `service_instance_id` enquanto houver dados antigos no volume local.
- Loki retornou logs dos serviços `authcore-api`, `gateway-api` e `notificationcore-api`.
- O fluxo `POST /api/auth/register` via Gateway retornou 201 na primeira chamada e 409 na repetição, gerando logs correlacionados.
- O trace `e0718fa16146bf4dc2d60a1995cbf945` apareceu no Loki e retornou no Jaeger com spans Gateway -> AuthCore -> Npgsql -> RabbitMQ publish -> RabbitMQ consume -> NotificationCore -> Npgsql.
- Consultas Loki por `| trace_id = "e0718fa16146bf4dc2d60a1995cbf945"` retornaram logs relacionados em `gateway-api`, `authcore-api` e `notificationcore-api`.
- Prometheus manteve o target `authcore-otel-collector` como `UP`.
- A API autenticada do Grafana não foi validada por credencial porque o volume local já possuía senha anterior; os arquivos de provisionamento foram montados e os logs não indicaram falha nos data sources.

## Evolução

Fora do escopo atual:

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
