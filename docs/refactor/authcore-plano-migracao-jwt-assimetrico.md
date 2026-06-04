# AuthCore - Plano Tecnico de Migracao JWT Assimetrico

## Objetivo

Migrar a emissao atual HS256 para assinatura assimetrica sem misturar a mudanca com a entrega imediata da autenticacao hibrida.

## Etapas propostas

1. Introduzir `JwtSigningAlgorithm` por configuracao com suporte inicial explicito a `HS256` e `RS256`.
2. Extrair a resolucao de credenciais de assinatura para um provider dedicado, separado do emissor de claims.
3. Armazenar a chave privada assimetrica fora da configuracao comum, preferencialmente via secret manager ou arquivo protegido.
4. Publicar a chave publica para Gateway e consumidores internos por configuracao versionada ou endpoint de descoberta interno.
5. Adicionar `kid` no cabecalho do JWT para suportar rotacao gradual de chaves.
6. Permitir validacao concorrente de chave antiga e nova durante a janela de rotacao.
7. Cobrir bootstrap, emissao e validacao com testes de integracao especificos para o algoritmo assimetrico.

## Riscos controlados

- reduzir distribuicao de segredo compartilhado entre servicos;
- impedir que consumidores validadores consigam assinar tokens;
- preparar rotacao de chave sem indisponibilidade do fluxo autenticado.
