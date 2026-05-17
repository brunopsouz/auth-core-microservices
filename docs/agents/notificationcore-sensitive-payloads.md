# NotificationCore Sensitive Payloads

## Objetivo

Registrar o tratamento mínimo de payloads sensíveis no fluxo `AuthCore -> NotificationCore`.

## Diretrizes

- `confirmationCode`, `password`, `accessToken`, `refreshToken` e `sessionId` nunca devem ser escritos em logs, respostas administrativas ou mensagens de erro.
- Use `SensitivePayloadSanitizer` antes de persistir mensagens de erro técnicas ou expor dados em endpoints administrativos.
- A inbox do `NotificationCore` mantém o payload bruto da solicitação porque o dispatcher precisa renderizar templates transacionais, incluindo o código de confirmação.
- A DLQ do RabbitMQ pode conter o payload bruto publicado originalmente. O acesso operacional à DLQ deve ser restrito a operadores autorizados e tratado como acesso a dado sensível.
- Exportações, dumps ou consultas administrativas da tabela `InboxMessages.Payload` e da DLQ devem sanitizar o conteúdo antes de compartilhamento fora do ambiente operacional restrito.
