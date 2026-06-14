# API Contracts

## Objetivo

Este guia define o padrão para contratos HTTP da camada `AuthCore.Api`.

Use este documento ao criar ou revisar:

- controllers
- `Contracts/Requests`
- `Contracts/Responses`
- mapeamentos entre HTTP e `Command` ou `Query`
- códigos de status e respostas de erro

O objetivo é manter a API fina, previsível e alinhada ao padrão dominante do repositório.

Controllers e contratos HTTP também devem respeitar os princípios SOLID definidos em `docs/agents/solid-guidelines.md`, principalmente SRP e DIP.

## Fonte de verdade

Ao modelar contratos HTTP, siga esta ordem de referência:

1. `AGENTS.md` do repositório
2. controllers e contratos já existentes em `src/Backend/AuthCore/AuthCore.Api`
3. casos de uso da `AuthCore.Application`
4. testes de integração que verificam comportamento HTTP

O contrato HTTP adapta a aplicação para JSON. Ele não deve virar o lugar principal das regras de negócio.

## Papel do controller

Controllers da API devem permanecer finos.

Responsabilidades esperadas:

- receber a requisição HTTP
- obter dependências via `[FromServices]`
- receber payload via `[FromBody]` quando aplicável
- montar `Command` ou `Query` da `Application`
- chamar o use case correspondente
- mapear o resultado para `Response...Json`
- devolver o status code adequado

Responsabilidades que não devem ir para controller:

- regra de negócio
- validações centrais de domínio
- persistência
- composição complexa de infraestrutura
- service locator para resolver dependências que deveriam estar explícitas na action ou no construtor
- uso direto de tipos concretos da infraestrutura como fluxo de aplicação

Mesmo que a API referencie `Infrastructure` no projeto para permitir o bootstrap, controllers não devem expor tipos concretos da infraestrutura em contratos HTTP, responses, requests ou superfície pública das actions. Detalhes técnicos da infraestrutura devem permanecer encapsulados. Tipos públicos por exigência técnica de frameworks, como migrations descobertas por reflexão, não devem ser tratados como contratos disponíveis para a API.

Controllers devem depender das abstrações públicas de casos de uso, como `I...UseCase`. As implementações concretas desses casos de uso devem permanecer `internal` e ser resolvidas apenas pela composição de injeção de dependência.

O padrão atual está bem representado em:

- `Controllers/UserController.cs`
- `Controllers/AuthController.cs`
- `Controllers/SessionAuthController.cs`
- `Controllers/TokenAuthController.cs`

Responsabilidade canônica dos controllers de autenticação e usuário:

- `AuthController`: `POST /api/auth/register`, `POST /api/auth/verify-email` e `POST /api/auth/resend-verification`
- `SessionAuthController`: rotas `api/auth/session/...` para login, usuário da sessão, logout e revogação de sessões por cookie
- `TokenAuthController`: rotas `api/auth/token/...` para login JWT, refresh token e logout token-based
- `UserController`: `GET /api/users/profile`, `PUT /api/users/profile`, `PUT /api/users/change-password` e `DELETE /api/users`

`RegisterUserUseCase` é o caso de uso de autocadastro público usado por `POST /api/auth/register`. Ele não deve ser exposto por `UserController` e não representa convite ou criação administrativa multitenant. Esses fluxos estão fora do escopo atual e devem ser especificados futuramente com contratos e casos de uso próprios.

## Estrutura de contratos

Os contratos HTTP ficam organizados em:

- `src/Backend/AuthCore/AuthCore.Api/Contracts/Requests`
- `src/Backend/AuthCore/AuthCore.Api/Contracts/Responses`

O padrão de nomes é obrigatório:

- entrada HTTP: `Request...Json`
- saída HTTP: `Response...Json`

Exemplos atuais:

- `RequestRegisterUserJson`
- `RequestChangePasswordJson`
- `RequestLoginJson`
- `ResponseRegisteredUserJson`
- `ResponseUserProfileJson`
- `ResponseAuthenticatedSessionJson`

Mesmo quando o serializer expõe JSON em convenção web, as propriedades C# devem continuar nomeadas em `PascalCase`, como já acontece no projeto.

## Convenções para Request

Requests devem ser DTOs simples, focados apenas em transporte.

Diretrizes:

- usar classes `sealed`
- manter apenas dados recebidos do cliente
- não colocar comportamento
- não usar entidades de domínio como payload HTTP
- não incluir campos que a API já consegue inferir do contexto autenticado
- inicializar strings com `string.Empty` para seguir o padrão do repositório

Exemplo importante do padrão atual:

- endpoints autenticados não recebem `UserIdentifier` no body
- o identificador do usuário é lido das claims no controller

Isso aparece em:

- `UserController.GetUserProfile`
- `UserController.UpdateUserProfile`
- `UserController.ChangePassword`
- `UserController.Delete`

## Convenções para Response

Responses devem refletir apenas o que a borda HTTP precisa devolver ao cliente.

Diretrizes:

- usar classes `sealed`
- retornar apenas dados relevantes para o consumidor
- evitar expor detalhes internos de domínio ou infraestrutura
- nunca usar classes, options, modelos ou entidades auxiliares da infraestrutura como response
- preferir nomes descritivos e estáveis
- quando a operação não precisa devolver corpo, usar `204 No Content`

Padrões já usados:

- `201 Created` com payload para criação de usuário
- `200 OK` com payload para leitura ou autenticação
- `204 No Content` para atualização, logout e exclusão

## Mapeamento entre HTTP e Application

O controller deve traduzir o contrato HTTP para o modelo de aplicação de forma direta.

Padrão observado:

- request HTTP -> `...Command` ou `...Query`
- resultado do use case -> `Response...Json`

Exemplos concretos:

- `RequestRegisterUserJson` -> `RegisterUserCommand` -> `ResponseRegisteredUserJson`, via `AuthController.Register`
- `RequestLoginJson` -> `LoginCommand` -> `ResponseAuthenticatedSessionJson`
- ausência de body de saída em operações de alteração -> `NoContent()`

Quando houver dados derivados do contexto HTTP, faça o enrichment no controller, não no request DTO.

Exemplo atual:

- `UserIdentifier` vem das claims do usuário autenticado

## Rotas e versionamento implícito

Hoje a API usa rotas iniciadas por `api/...` e não há versionamento explícito por URL, header ou media type.

Padrões observados:

- `api/auth/register`
- `api/auth/verify-email`
- `api/auth/resend-verification`
- `api/auth/session/login`
- `api/auth/session/me`
- `api/auth/session/logout`
- `api/auth/session/sessions`
- `api/auth/session/sessions/{sid}`
- `api/auth/session/logout-all`
- `api/auth/token/login`
- `api/auth/token/refresh`
- `api/auth/token/logout`
- `api/users/profile`
- `api/users/change-password`
- `api/users`

No conjunto acima, `api/users` é a rota base de `DELETE /api/users` para exclusão autenticada do usuário atual. `POST /api/users` não é contrato de autocadastro público.

Diretrizes para evolução:

- preserve rotas existentes sempre que possível
- trate contratos atuais como uma versão implícita estável
- evite breaking changes em payloads já expostos
- se uma mudança quebrar consumidores, prefira introduzir um novo contrato ou rota paralela em vez de sobrescrever silenciosamente a atual

Se algum versionamento explícito for introduzido no futuro, ele deve ser aplicado de forma consistente em toda a borda HTTP, e não pontualmente em um único endpoint.

## Autenticação e endpoints protegidos

O projeto possui dois modos de autenticação para contratos protegidos:

- Bearer/JWT, usado em `UserController` e marcado com `[AuthenticatedUser]`
- sessão por cookie, usada em `SessionAuthController` e marcada com `[AuthenticatedSession]`

Padrões atuais para Bearer/JWT:

- o atributo `AuthenticatedUserAttribute` herda de `AuthorizeAttribute` e fica em `AuthCore.Api.Authentication`
- a autenticação é registrada em `ApiDependencyInjection`
- o controller extrai o identificador do usuário autenticado pelas claims

Ao criar endpoint autenticado por Bearer:

- aplique `[AuthenticatedUser]`
- não aceite no body dados que podem ser obtidos do token
- extraia claims no controller e repasse para o use case no `Command` ou `Query`

Padrões atuais para sessão por cookie:

- `SessionAuthController` usa `[AuthenticatedSession]` nas rotas protegidas de sessão
- mutações autenticadas por cookie validam CSRF com `ICsrfRequestValidator`
- o Gateway mantém `/api/auth/{everything}` sem exigência de Bearer para permitir que o AuthCore valide a sessão por cookie

No estado atual, o `UserController` procura `UserIdentifier` em aliases conhecidos de claim:

- `ClaimTypes.NameIdentifier`
- `sub`
- `user_identifier`
- `userIdentifier`

Ao evoluir contratos autenticados, mantenha compatibilidade com esse padrão enquanto ele for a convenção vigente.

## Validação

Validação neste projeto é distribuída por responsabilidade.

Na borda HTTP:

- o controller valida autenticação e presença do contexto necessário
- o ASP.NET faz binding do body para os DTOs
- documentação de respostas deve declarar os principais status esperados com `ProducesResponseType`

Na aplicação e no domínio:

- regras de negócio e invariantes ficam fora do controller
- exceções conhecidas são convertidas em resposta HTTP padronizada

Em termos práticos:

- não replique regra de negócio no request DTO
- não transforme o controller em camada de validação rica
- valide no controller apenas o que for próprio da borda HTTP ou do contexto autenticado

## Resposta de erro padrão

O formato de erro atual é `ResponseErrorJson`, com a propriedade:

- `Errors: IList<string>`

Esse contrato é usado para erros previsíveis da aplicação e do domínio.

Mapeamento atual do handler global:

- `DomainException` -> `400 Bad Request`
- `ValidationException` da aplicação -> `400 Bad Request`
- `UnauthorizedAccessException` -> `401 Unauthorized`
- `NotFoundException` da aplicação -> `404 Not Found`
- `ConflictException` da aplicação -> `409 Conflict`
- exceções não tratadas -> `500 Internal Server Error`

Os controllers de autenticação também possuem mapeamento local para exceções conhecidas. Ao criar novos endpoints, preserve consistência com o formato de erro já exposto pela API.

## Swagger e documentação do contrato

A API publica Swagger em ambiente de desenvolvimento e inclui XML docs dos controllers.

Ao adicionar ou alterar contrato HTTP:

- revise `summary`, `param` e `returns` dos controllers e DTOs públicos
- atualize `ProducesResponseType` para refletir o contrato real
- garanta que o endpoint fique legível no Swagger

As descrições devem seguir o padrão do projeto:

- classes: `Representa ...`
- métodos: `Operação para ...`

## Checklist para novos contratos

Antes de concluir uma mudança em contrato HTTP, confirme:

1. o nome do DTO segue `Request...Json` ou `Response...Json`
2. o controller apenas adapta HTTP para `Command` ou `Query`
3. o endpoint usa o status code correto
4. erros previsíveis estão documentados com `ProducesResponseType`
5. endpoints autenticados usam o atributo correto: `[AuthenticatedUser]` para Bearer ou `[AuthenticatedSession]` para sessão por cookie
6. dados obtidos do token ou da sessão não foram duplicados no body
7. nomes de rota, action e DTO estão consistentes com o caso de uso
8. a mudança preserva compatibilidade com contratos já expostos ou trata claramente a evolução
9. nenhum tipo concreto de `Infrastructure` foi exposto na assinatura pública do endpoint ou no contrato JSON
10. o controller não acessa infraestrutura diretamente quando existe ou deve existir um caso de uso
11. as dependências do endpoint estão explícitas e alinhadas ao checklist SOLID

## Arquivos de referência

Arquivos mais úteis para seguir o padrão atual:

- `src/Backend/AuthCore/AuthCore.Api/Controllers/AuthController.cs`
- `src/Backend/AuthCore/AuthCore.Api/Controllers/SessionAuthController.cs`
- `src/Backend/AuthCore/AuthCore.Api/Controllers/TokenAuthController.cs`
- `src/Backend/AuthCore/AuthCore.Api/Controllers/UserController.cs`
- `src/Backend/AuthCore/AuthCore.Api/Contracts/Requests`
- `src/Backend/AuthCore/AuthCore.Api/Contracts/Responses`
- `src/Backend/AuthCore/AuthCore.Api/Authentication/AuthenticatedUserAttribute.cs`
- `src/Backend/AuthCore/AuthCore.Api/ApiDependencyInjection.cs`
- `src/Backend/AuthCore/AuthCore.Api/Exceptions/ApiExceptionHandler.cs`
- `src/Backend/AuthCore/AuthCore.Api/Contracts/Responses/ResponseErrorJson.cs`
- `tests/AuthCore.IntegrationTests/Authentication/AuthControllerIntegrationTests.cs`
- `tests/AuthCore.IntegrationTests/Authentication/UserSecurityIntegrationTests.cs`
- `tests/AuthCore.IntegrationTests/Exceptions/ApiExceptionHandlerTests.cs`
