# Spec-Driven Development — Connection Pooling e Acesso PostgreSQL

## 1. Visão geral

Esta especificação define a evolução do acesso PostgreSQL de `AuthCore` e `NotificationCore`. O objetivo é manter conexões e transações abertas somente durante operações de banco, preservar concorrência entre workers e tornar os limites do pool explícitos.

O projeto continua usando .NET 10, Npgsql, ADO.NET, SQL explícito, FluentMigrator e o padrão `IDatabaseSession`/`IUnitOfWork`. EF Core e Dapper permanecem fora do escopo.

## 2. Estado anterior

- Pooling habilitado, mas com limites implícitos do Npgsql.
- Uma conexão permanecia aberta durante todo o escopo scoped.
- O dispatcher de notificações podia reter conexão durante renderização e SMTP.
- A outbox publicava no RabbitMQ dentro de transação com `FOR UPDATE`.
- A maioria das consultas não recebia cancelamento.
- Não havia métricas específicas de aquisição, lease e transação.

## 3. Objetivos

- Devolver conexões ao pool imediatamente após cada operação não transacional.
- Compartilhar uma conexão somente durante uma transação explícita.
- Não executar SMTP, RabbitMQ ou outro I/O externo com conexão PostgreSQL retida.
- Manter entrega de outbox pelo menos uma vez, com claim recuperável após crash.
- Dimensionar o pool por serviço e réplica.
- Permitir cancelamento cooperativo dos workers.
- Elevar a maturidade técnica estimada de 6/10 para pelo menos 9/10.

## 4. Arquitetura de conexão

Cada processo registra um único `NpgsqlDataSource` singleton. O data source representa o pool thread-safe do serviço.

`NpgsqlUnitOfWork` permanece scoped e implementa:

- conexão transacional compartilhada enquanto `CurrentTransaction` existir;
- lease não proprietário dentro da transação;
- lease proprietário para operações sem transação;
- liberação da transação e da conexão em commit, rollback, falha de início e dispose.

Todo repositório deve usar:

```csharp
await using var connectionLease =
    await databaseSession.AcquireConnectionAsync(cancellationToken);
var connection = connectionLease.Connection;
```

É proibido armazenar conexões em singleton, executar chamadas externas com lease aberto ou criar conexão direta no fluxo normal. Migrations e fixtures são exceções controladas.

## 5. Configuração do pool

Defaults:

```text
Pooling=true
Minimum Pool Size=0
Maximum Pool Size=30
Timeout=10
Command Timeout=30
Application Name=AuthCore|NotificationCore
```

Os valores podem ser sobrescritos pela connection string do ambiente. `Maximum Pool Size` deve ser calculado considerando `max_connections`, quantidade máxima de réplicas, serviços, jobs administrativos e margem operacional.

Aumentar o pool sem corrigir retenções não é uma correção válida.

## 6. Outbox com lease persistente

A tabela `OutboxMessages` recebe:

- `LeaseId uuid null`;
- `LeasedUntilUtc timestamp with time zone null`.

Fluxo:

1. Gerar `LeaseId`.
2. Executar claim atômico da primeira mensagem disponível usando `FOR UPDATE SKIP LOCKED`.
3. Considerar disponível mensagem sem lease ou com lease expirado.
4. Persistir lease e confirmar antes da publicação.
5. Publicar no RabbitMQ sem conexão PostgreSQL aberta.
6. Em sucesso, marcar como processada usando `Id + LeaseId`.
7. Em falha, incrementar tentativa, registrar erro sanitizado e liberar o lease usando `Id + LeaseId`.

Uma conclusão com lease antigo não pode alterar mensagem reclamada por outro worker.

## 7. NotificationCore

O dispatcher deve:

- reservar notificações em transação curta;
- confirmar e liberar conexão;
- consultar payload com lease curto;
- renderizar e enviar SMTP sem conexão aberta;
- persistir o resultado em nova transação curta;
- propagar o token de encerramento do hosted service às consultas.

## 8. Índices

AuthCore:

- índice parcial de outbox pendente por `OccurredAtUtc`;
- índice parcial de leases pendentes por `LeasedUntilUtc`.

NotificationCore:

- índice composto de despacho por `Status`, `ScheduledAtUtc`, `Priority DESC`, `CreatedAtUtc`;
- índice de expressão JSONB para `Payload ->> 'IdempotencyKey'`.

Os índices devem ser revisados com `EXPLAIN (ANALYZE, BUFFERS)` em dados representativos antes de ajustes adicionais.

## 9. Observabilidade

Cada serviço publica métricas para:

- duração da aquisição de conexão;
- falhas de aquisição;
- duração do lease proprietário;
- duração de transação;
- duração e falhas dos workers já existentes.

Logs não podem incluir connection string, senha ou payload sensível. Health checks devem adquirir e descartar uma conexão curta.

Alertas recomendados:

- falhas de aquisição maiores que zero;
- aumento sustentado da duração de aquisição;
- transações acima do limite operacional;
- crescimento de falhas ou recuperação recorrente de leases.

## 10. Compatibilidade e segurança

- Não há alteração de contrato HTTP.
- Não há alteração de semântica de autenticação.
- A outbox permanece pelo menos uma vez; consumidores continuam idempotentes.
- A migration aceita mensagens existentes sem lease.
- Rollout deve aplicar migrations antes de ativar workers com o novo fluxo.

## 11. Testes

- Invariantes de lease da entidade `OutboxMessage`.
- Claim concorrente retorna uma mensagem para apenas um worker.
- Lease expirado permite novo claim.
- Lease antigo não conclui nem registra falha.
- Pool com máximo dois não esgota após descarte de escopos.
- SMTP bloqueado não impede aquisição de conexão por outro escopo.
- RabbitMQ lento não mantém transação aberta.
- Cancelamento do worker alcança operações Npgsql.
- Falhas de begin, commit e rollback liberam recursos.

## 12. Critérios de aceite

- Nenhum repositório usa `GetOpenConnectionAsync`.
- Operações não transacionais usam lease proprietário descartável.
- Commit e rollback devolvem a conexão ao pool.
- Publicação RabbitMQ ocorre fora da transação.
- Envio SMTP ocorre sem conexão retida.
- Limites do pool estão explícitos no Docker e exemplo de ambiente.
- Migrations de AuthCore e NotificationCore criam os novos índices.
- Projetos alterados compilam e testes relevantes passam.
- Revisão `senior-backend-reviewer` não identifica vazamento ou retenção crítica.

## 13. Definition of Done

- Código, migrations, configurações e testes entregues.
- Documentação operacional atualizada.
- Build e testes registrados.
- Nenhuma credencial adicionada ao Git.
- Alterações não relacionadas preservadas.
