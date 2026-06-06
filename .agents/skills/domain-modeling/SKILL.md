# Domain Modeling

Use este guia quando a mudanca principal estiver em `*.Domain`.

## Objetivo

Preservar DDD tatico, encapsulamento e invariantes do dominio, seguindo o padrao dominante de `AuthCore.Domain` e `NotificationCore.Domain`.

## Diretrizes

- Mantenha comportamento dentro de agregados, entidades e value objects.
- Prefira metodos de fabrica estaticos como `Create`, `Register` e `Restore` quando o modulo ja usa esse padrao.
- Use construtores privados quando a criacao controlada for importante para a consistencia do estado.
- Proteja invariantes no momento da criacao e das transicoes de estado.
- Evite setters publicos sem necessidade clara.
- Evite mover regra de negocio para `Application` ou `Infrastructure`.

## Materializacao

- Ao reconstruir objetos vindos da persistencia, siga o padrao existente do modulo.
- Se o agregado ja expoe `Restore(...)`, preserve esse contrato.

## Testes esperados

- Atualize ou adicione testes em `tests/*Domain.UnitTests`.
- Prefira nomes no formato `Metodo_WhenCondicao_ShouldResultado`.

## Leitura complementar

- `docs/agents/architecture-overview.md`
- `docs/agents/csharp-style.md`
- `docs/agents/testing.md`
