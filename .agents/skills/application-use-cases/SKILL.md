# Application Use Cases

Use este guia quando a mudanca principal estiver em `*.Application`.

## Objetivo

Manter a camada de aplicacao como orquestradora de caso de uso, sem absorver regra de negocio central.

## Diretrizes

- Organize por modulo e caso de uso.
- Preserve o agrupamento vertical com `I...UseCase`, `...UseCase`, `...Command`, `...Query` e `...Result` quando aplicavel.
- Valide argumentos com `ArgumentNullException.ThrowIfNull` quando fizer sentido.
- Busque entidades por repositorio e delegue regras ao dominio.
- Controle transacao na camada apropriada, com `Commit` e `Rollback` coerentes com o fluxo existente.
- Retorne DTOs de resultado apenas quando isso realmente melhorar clareza do contrato.

## Evite

- Reimplementar regra de negocio do dominio.
- Inchar use case com detalhes de infraestrutura.
- Colocar mapeamento HTTP nesta camada.

## Testes esperados

- Atualize ou adicione testes em `tests/*Application.UnitTests`.
- Reforce cenarios de orquestracao, excecoes e transacoes quando relevante.

## Leitura complementar

- `docs/agents/architecture-overview.md`
- `docs/agents/csharp-style.md`
- `docs/agents/testing.md`
