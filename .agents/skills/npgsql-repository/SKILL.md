# Npgsql Repository

Use este guia quando a mudanca principal estiver em `*.Infrastructure` com persistencia PostgreSQL.

## Objetivo

Preservar o padrao atual de persistencia manual com `Npgsql`, SQL explicito e materializacao consistente com o dominio.

## Diretrizes

- Prefira SQL explicito em vez de ORMs.
- Mantenha separacao entre leitura e escrita quando ela ja existir no modulo.
- Use raw string para SQL quando esse ja for o padrao do arquivo.
- Materialize agregados e entidades por metodos como `Restore(...)` quando disponiveis.
- Preserve o uso compartilhado de `IDatabaseSession` e `IUnitOfWork`.
- Mantenha migracoes versionadas com `Version0000001`, `Version0000002`, etc.

## Evite

- Introduzir EF Core ou abstracoes pesadas sem pedido explicito.
- Mover regra de negocio para repositorios.
- Misturar contratos de leitura e escrita sem necessidade.

## Testes esperados

- Atualize testes unitarios ou de integracao do modulo afetado.
- Quando alterar SQL critico, priorize os testes de integracao existentes do contexto correspondente.

## Leitura complementar

- `docs/agents/architecture-overview.md`
- `docs/agents/csharp-style.md`
- `docs/agents/testing.md`
