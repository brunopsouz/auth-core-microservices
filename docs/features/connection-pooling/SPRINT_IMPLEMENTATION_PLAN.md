# Plano de Implementação — Connection Pooling e Acesso PostgreSQL

## 1. Fonte

A fonte normativa é `spec-driven-development-connection-pooling.md`. Este plano organiza a entrega em etapas verificáveis.

## 2. Sprint 1 — Ciclo de vida de conexão

- Registrar `NpgsqlDataSource` singleton em ambos os serviços.
- Substituir `GetOpenConnectionAsync` por `AcquireConnectionAsync`.
- Implementar lease proprietário e não proprietário.
- Liberar conexão em commit, rollback, falha de begin e dispose.
- Instrumentar aquisição, lease e transação.
- Validar build de Domain, Application e Infrastructure.

## 3. Sprint 2 — Repositórios e cancelamento

- Migrar todos os repositórios para `await using` do lease.
- Propagar `CancellationToken` nos fluxos dos workers.
- Confirmar que health checks usam conexão curta.
- Adicionar testes de descarte e pool reduzido.

## 4. Sprint 3 — Outbox

- Evoluir `OutboxMessage` com estado de lease restaurável.
- Adicionar migration `Version0000012`.
- Implementar claim atômico e recuperação de lease expirado.
- Publicar RabbitMQ fora da transação.
- Condicionar sucesso e falha ao `LeaseId`.
- Atualizar testes unitários e de persistência.

## 5. Sprint 4 — NotificationCore

- Garantir que o claim do lote seja transação curta.
- Liberar conexão antes de renderização e SMTP.
- Persistir resultados em transações curtas.
- Adicionar migration `Version0000004` com índices de dispatcher e inbox.
- Validar concorrência e cancelamento.

## 6. Sprint 5 — Configuração e operação

- Tornar limites do pool explícitos no Docker e exemplo de ambiente.
- Documentar cálculo por réplica e margem de `max_connections`.
- Validar planos com `EXPLAIN (ANALYZE, BUFFERS)`.
- Configurar dashboards e alertas para métricas de banco.

## 7. Validação final

- `dotnet test tests/AuthCore.Domain.UnitTests/AuthCore.Domain.UnitTests.csproj`
- `dotnet test tests/AuthCore.Application.UnitTests/AuthCore.Application.UnitTests.csproj`
- `dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj`
- `dotnet test tests/NotificationCore.Domain.UnitTests/NotificationCore.Domain.UnitTests.csproj`
- `dotnet test tests/NotificationCore.Application.UnitTests/NotificationCore.Application.UnitTests.csproj`
- `dotnet test tests/NotificationCore.IntegrationTests/NotificationCore.IntegrationTests.csproj`
- Executar revisão `senior-backend-reviewer`.

## 8. Critério de encerramento

A entrega termina quando não houver conexão retida durante I/O externo, o fluxo de outbox tolerar crash por lease expirável, os limites do pool forem explícitos e as validações relevantes passarem.
