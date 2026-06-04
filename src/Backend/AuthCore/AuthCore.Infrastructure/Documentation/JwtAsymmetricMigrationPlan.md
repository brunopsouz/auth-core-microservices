# Plano de migracao para assinatura assimetrica

## Objetivo

Migrar a emissao e validacao de JWT de `HS256` para assinatura assimetrica sem quebrar os clientes atuais.

## Etapas

1. Introduzir configuracao dedicada para chave publica e chave privada, preservando `HS256` apenas como compatibilidade temporaria.
2. Ensinar o emissor do AuthCore a publicar `kid` no header do JWT e rotacionar chaves por versao.
3. Adaptar Gateway e servicos consumidores para validar a chave publica correspondente ao `kid`.
4. Rodar periodo de dupla validacao controlada, aceitando `HS256` antigo apenas durante a janela de migracao.
5. Revogar o segredo simetrico compartilhado e remover a compatibilidade legado quando todos os consumidores estiverem em producao com validacao assimetrica.

## Requisitos operacionais

- As chaves privadas devem sair do repositrio e ser fornecidas apenas por secret manager.
- A rotacao precisa de inventario explicito de consumidores por ambiente.
- A troca definitiva deve incluir invalidacao planejada de tokens emitidos com o segredo legado.
