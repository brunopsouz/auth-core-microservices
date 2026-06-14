# Plano de Implementação - Login com Google no AuthCore

## 1. Fonte e premissas

A Spec original da feature é `@spec-driven-development-login-google-authcore.md` e deve ser consultada sempre que houver dúvida sobre requisitos, regras de negócio, fluxos OAuth/OIDC, segurança, configuração por ambiente ou critérios de aceite.

Este plano transforma a Spec em sprints e tasks técnicas pequenas para implementação posterior. Ele não implementa código e não substitui a Spec: quando houver conflito ou lacuna, a Spec original deve ser consultada e a decisão deve ser registrada em "Decisões pendentes".

Premissas extraídas da Spec e do projeto atual:

- Google será tratado como provedor externo de identidade, não como emissor de credenciais internas do AuthCore.
- O AuthCore continuará criando/controlando `User`, sessão server-side, cookies, JWT interno, refresh token interno, autorização e revogação.
- O identificador principal do vínculo externo deve ser `provider + providerUserId`, onde `providerUserId` vem da claim OIDC `sub`.
- Tokens do Google não devem ser persistidos nem usados para autenticar APIs internas.
- Escopos iniciais: `openid`, `profile`, `email`.
- `returnUrl` deve ser validado por allowlist para evitar open redirect.
- A implementação deve respeitar as camadas atuais: `Api`, `Application`, `Domain`, `Infrastructure` e `tests/AuthCore.*`.
- A implementação deve seguir `AGENTS.md`, `docs/agents/*`, `.agents/skills/domain-modeling/SKILL.md`, `.agents/skills/application-use-cases/SKILL.md` e `.agents/skills/npgsql-repository/SKILL.md`.

## 2. Estado atual do projeto

O repositório está organizado como uma solução .NET com múltiplos serviços. O recorte desta feature é o `AuthCore`, com apoio pontual do `Gateway`.

Projetos relevantes:

- `src/Backend/AuthCore/AuthCore.Api`: controllers, contratos JSON, autenticação, autorização, cookies, Swagger, health checks e bootstrap HTTP.
- `src/Backend/AuthCore/AuthCore.Application`: casos de uso organizados por vertical slice.
- `src/Backend/AuthCore/AuthCore.Domain`: entidades, value objects, invariantes, enums e contratos centrais.
- `src/Backend/AuthCore/AuthCore.Infrastructure`: Npgsql, FluentMigrator, Redis, tokens, sessões, cookies, repositórios e DI técnico.
- `src/Backend/Gateway/Gateway.Api`: Ocelot, rate limiting, autenticação JWT e passagem de rotas públicas/protegidas.
- `tests/AuthCore.Domain.UnitTests`: testes de domínio.
- `tests/AuthCore.Application.UnitTests`: testes de orquestração de use cases.
- `tests/AuthCore.IntegrationTests`: testes de HTTP, autenticação, bootstrap, persistência e infraestrutura.

Padrões existentes que devem orientar a implementação:

- Controllers finos em `AuthCore.Api/Controllers`, com dependências de use case via `[FromServices]`.
- Contratos HTTP em `Contracts/Requests` e `Contracts/Responses`, com sufixos `Request...Json` e `Response...Json`.
- Use cases em `Application/UseCases/...`, com `I...UseCase`, implementação `internal sealed`, `...Command`, `...Query` e `...Result`.
- Domínio com factories como `Create`, `Register`, `Restore` e `Read`, propriedades encapsuladas e documentação XML em português.
- Persistência manual com Npgsql, SQL explícito e transação compartilhada via `IDatabaseSession`/`IUnitOfWork`.
- Migrations em `Infrastructure/Persistences/Migrations/Versions`, com versionamento `Version0000001`, ..., atualmente chegando a `Version0000010`.
- Configurações via `appsettings.Development.json`, `.env.development.example`, variáveis de ambiente e `docker-compose.yml`.
- Sessão browser/PWA já usa cookies `sid`, `at` e `XSRF-TOKEN`; APIs/mobile usam Bearer JWT.

## 3. Mapeamento arquitetural

### Domain

Responsabilidades:

- Modelar vínculo externo como conceito de domínio.
- Proteger invariantes de `ExternalLogin`: usuário obrigatório, provider válido, providerUserId obrigatório, e-mail obrigatório e uso registrado.
- Garantir que regras centrais, como impedir autenticação de usuário bloqueado, fiquem no domínio ou sejam chamadas a partir dele.

Classes e arquivos prováveis:

- `src/Backend/AuthCore/AuthCore.Domain/Users/ExternalLogin.cs`
- `src/Backend/AuthCore/AuthCore.Domain/Users/ExternalLoginProvider.cs`
- `src/Backend/AuthCore/AuthCore.Domain/Users/Repositories/IExternalLoginRepository.cs`
- `src/Backend/AuthCore/AuthCore.Domain/Users/Repositories/IExternalLoginReadRepository.cs`, se a separação leitura/escrita for necessária para consumidores distintos.
- `src/Backend/AuthCore/AuthCore.Domain/Users/User.cs`, somente se for confirmada criação de usuário via login externo ou status/onboarding específico.

Pode depender de:

- Tipos do próprio domínio, como `DomainException`, `Email`, `User`, `UserStatus`, `Role` e contratos de repositório.

Não pode depender de:

- ASP.NET Core, Google SDK, `HttpContext`, Npgsql, Redis, variáveis de ambiente, cookies, JWT, Gateway ou Application.

Cuidados:

- Não armazenar token Google no domínio.
- Não usar e-mail como identificador primário do vínculo externo.
- Não criar abstrações genéricas para múltiplos provedores além do necessário para `ExternalLoginProvider.Google`.
- Se `User` exigir `Contact`, registrar decisão pendente antes de criar usuário automaticamente sem esse dado.

### Application

Responsabilidades:

- Orquestrar o fluxo de conclusão do login externo.
- Validar `returnUrl` via política/serviço pequeno e testável.
- Buscar vínculo externo por provider/providerUserId.
- Buscar usuário por e-mail verificado quando não houver vínculo.
- Criar/vincular `ExternalLogin` e emitir sessão interna reutilizando contratos existentes.
- Coordenar transação, commit e rollback.

Classes e arquivos prováveis:

- `src/Backend/AuthCore/AuthCore.Application/UseCases/Authentication/ExternalLogin/CompleteGoogleLoginUseCase.cs`
- `src/Backend/AuthCore/AuthCore.Application/UseCases/Authentication/ExternalLogin/ICompleteGoogleLoginUseCase.cs`
- `src/Backend/AuthCore/AuthCore.Application/UseCases/Authentication/ExternalLogin/CompleteGoogleLoginCommand.cs`
- `src/Backend/AuthCore/AuthCore.Application/UseCases/Authentication/ExternalLogin/CompleteGoogleLoginResult.cs`
- `src/Backend/AuthCore/AuthCore.Application/UseCases/Authentication/ExternalLogin/ValidateExternalReturnUrlUseCase.cs`, ou uma policy interna se não houver motivo para use case público.
- `src/Backend/AuthCore/AuthCore.Application/ApplicationDependencyInjection.cs`

Pode depender de:

- `AuthCore.Domain`, contratos de repositório, `IUnitOfWork`, contratos de sessão/token já existentes no domínio.

Não pode depender de:

- `AuthCore.Infrastructure`, `HttpContext`, `AuthenticationProperties`, handlers Google, cookies HTTP, Ocelot ou Npgsql.

Cuidados:

- Não ler claims Google diretamente na Application; a API deve transformar claims em command.
- Não reimplementar regra de status do usuário; usar comportamento do domínio.
- Não abrir transação para validações puras.
- Garantir rollback em falhas depois de iniciar transação.

### Api

Responsabilidades:

- Configurar autenticação externa com Google e cookie temporário externo.
- Expor endpoint para iniciar challenge Google.
- Expor callback para autenticar principal externo, extrair claims necessárias e chamar use case.
- Emitir cookies/sessão interna do AuthCore usando padrão já existente.
- Redirecionar para frontend seguro ou rota de erro.

Classes e arquivos prováveis:

- `src/Backend/AuthCore/AuthCore.Api/Controllers/ExternalAuthController.cs`
- `src/Backend/AuthCore/AuthCore.Api/ApiDependencyInjection.cs`
- `src/Backend/AuthCore/AuthCore.Api/AuthCore.Api.csproj`
- `src/Backend/AuthCore/AuthCore.Api/Contracts/Responses/ResponseExternalLoginErrorJson.cs`, somente se o fluxo exigir resposta JSON além de redirect.
- `src/Backend/AuthCore/AuthCore.Api/appsettings.Development.json`

Pode depender de:

- `AuthCore.Application`, `AuthCore.Infrastructure` apenas para bootstrap já existente, ASP.NET Core Authentication, options e contratos JSON.

Não pode depender de:

- Repositórios concretos, Npgsql, regras de domínio escritas no controller ou modelos auxiliares internos da infraestrutura.

Cuidados:

- Controller deve permanecer fino.
- Não logar authorization code, id_token, access_token, refresh token, cookies ou ClientSecret.
- A rota deve seguir decisão pendente: Spec usa `/auth/external/google`, mas o padrão atual do projeto é `/api/auth/...`.
- Callback deve limpar cookie temporário externo.
- Em produção, cookies devem respeitar `Secure`, `HttpOnly` e `SameSite`.

### Infrastructure

Responsabilidades:

- Persistir `ExternalLogin` com SQL explícito.
- Materializar domínio por factories como `Restore`.
- Criar migration `external_logins`.
- Registrar repositórios e options.
- Fornecer configurações técnicas necessárias sem absorver regra de negócio.

Classes e arquivos prováveis:

- `src/Backend/AuthCore/AuthCore.Infrastructure/Persistences/Migrations/Versions/Version0000011.cs`
- `src/Backend/AuthCore/AuthCore.Infrastructure/Persistences/Migrations/Versions/DatabaseVersions.cs`
- `src/Backend/AuthCore/AuthCore.Infrastructure/Persistences/Write/PostgreSQL/Repositories/ExternalLoginRepository.cs`
- `src/Backend/AuthCore/AuthCore.Infrastructure/Persistences/Read/PostgreSQL/Repositories/ExternalLoginReadRepository.cs`, se houver separação de leitura.
- `src/Backend/AuthCore/AuthCore.Infrastructure/Configurations/GoogleAuthenticationOptions.cs`, se as options ficarem na infraestrutura; avaliar se devem ficar na API por serem de autenticação HTTP.
- `src/Backend/AuthCore/AuthCore.Infrastructure/InfrastructureDependencyInjection.cs`

Pode depender de:

- `AuthCore.Domain`, Npgsql, FluentMigrator, Microsoft options.

Não pode depender de:

- Controllers, commands de Application, `HttpContext`, Google principal, cookies ou decisões HTTP.

Cuidados:

- Não introduzir EF Core ou ORM.
- Não persistir tokens Google.
- Criar índice único em `provider + provider_user_id`.
- Usar transaction atual de `IDatabaseSession`.

### Tests

Responsabilidades:

- Validar invariantes de domínio.
- Validar orquestração dos use cases com dublês manuais.
- Validar persistência, bootstrap, rotas e autenticação em integração.

Arquivos prováveis:

- `tests/AuthCore.Domain.UnitTests/Aggregates/Users/ExternalLoginTests.cs`
- `tests/AuthCore.Domain.UnitTests/Aggregates/Users/UserTests.cs`, se `User` mudar.
- `tests/AuthCore.Application.UnitTests/UseCases/Authentication/ExternalLogin/CompleteGoogleLoginUseCaseTests.cs`
- `tests/AuthCore.Application.UnitTests/UseCases/Authentication/ExternalLogin/ExternalLoginReturnUrlValidatorTests.cs`
- `tests/AuthCore.Application.UnitTests/UseCases/Authentication/Support/AuthenticationTestDoubles.cs`
- `tests/AuthCore.IntegrationTests/Authentication/ExternalAuthControllerIntegrationTests.cs`
- `tests/AuthCore.IntegrationTests/Passports/ExternalLoginPersistenceIntegrationTests.cs`

Cuidados:

- Usar xUnit e nomes `Metodo_WhenCondicao_ShouldResultado`.
- Preferir fakes/spies manuais.
- Não depender de Google real em testes automatizados.
- Testes de callback devem simular principal externo ou autenticação externa fake.

## 4. Estratégia de sprints

### Sprint 1 - Fundação de domínio e contratos

Objetivo:

- Criar a base de domínio para representar vínculo externo sem envolver OAuth, HTTP ou banco.

Escopo incluído:

- `ExternalLoginProvider`.
- Entidade `ExternalLogin`.
- Contratos de repositório necessários.
- Avaliação mínima de `User` para criação com e-mail verificado.
- Testes de domínio.

Escopo excluído:

- Migrations, Npgsql, Google middleware, controllers, Gateway, Docker Compose e fluxo ponta a ponta.

Entregáveis técnicos:

- Tipos de domínio com XML docs em português.
- Invariantes cobertas por testes.
- Decisões pendentes registradas para campos obrigatórios faltantes.

Critérios de aceite:

- Domínio compila.
- `ExternalLogin` impede providerUserId vazio e usuário vazio.
- `ExternalLogin` registra uso sem quebrar invariantes.
- Nenhum tipo de domínio depende de infraestrutura ou API.

Riscos:

- `User` atual exige `Contact`, o que pode impedir criação automática com dados Google.
- Criar abstração genérica demais para futuros provedores.

Dependências:

- Spec original.
- Padrões de domínio existentes.

Ordem recomendada:

1. Entender `User` atual.
2. Criar enum.
3. Criar entidade.
4. Criar contratos.
5. Criar testes.

### Sprint 2 - Persistência PostgreSQL

Objetivo:

- Persistir e consultar vínculos externos com Npgsql e FluentMigrator.

Escopo incluído:

- Migration `external_logins`.
- Índice único `provider + provider_user_id`.
- Repositórios concretos.
- DI.
- Testes de persistência.

Escopo excluído:

- Login Google, controller, cookies, Gateway e configuração OAuth.

Entregáveis técnicos:

- `Version0000011`.
- Repositórios internal.
- Contratos resolvidos por DI.

Critérios de aceite:

- Migration cria tabela e índices corretos.
- Repositório insere, consulta e atualiza uso.
- Constraint única impede duplicidade de provider/providerUserId.

Riscos:

- Divergência entre nomes de tabela atuais (`Users`) e nova tabela em snake_case.
- Materialização inconsistente com factories do domínio.

Dependências:

- Sprint 1 concluída.

Ordem recomendada:

1. Migration.
2. Escrita.
3. Leitura.
4. DI.
5. Integração.

### Sprint 3 - Application / fluxo de login externo

Objetivo:

- Orquestrar login externo com Google usando domínio, repositórios, sessão interna e transação.

Escopo incluído:

- Command/result.
- Use case `CompleteGoogleLogin`.
- Validação de `returnUrl`.
- Regras de vínculo existente, vínculo por e-mail verificado, conflito e usuário bloqueado.
- Testes de aplicação.

Escopo excluído:

- Challenge HTTP, middleware Google, callback real e Gateway.

Entregáveis técnicos:

- Use case `internal sealed`.
- Interface pública para consumo pela API.
- Testes com fakes/spies.

Critérios de aceite:

- Vínculo existente autentica usuário interno.
- Usuário existente com e-mail verificado pode receber vínculo.
- E-mail não verificado não cria/vincula automaticamente.
- Usuário bloqueado não autentica.
- `returnUrl` malicioso é rejeitado.

Riscos:

- Duplicar lógica de emissão de sessão já existente em `LoginSessionUseCase`.
- Misturar validação HTTP na Application.

Dependências:

- Sprints 1 e 2.
- Decisão sobre criação automática versus onboarding.

Ordem recomendada:

1. Validador de returnUrl.
2. Command/result.
3. Fluxo vínculo existente.
4. Fluxo usuário existente por e-mail.
5. Fluxo usuário novo/onboarding.
6. Testes.

### Sprint 4 - API e autenticação Google

Objetivo:

- Expor endpoints HTTP para iniciar Google Login e concluir callback com sessão AuthCore.

Escopo incluído:

- Pacote `Microsoft.AspNetCore.Authentication.Google`, se necessário.
- Configuração `AddGoogle`.
- Cookie temporário externo.
- Controller `ExternalAuthController`.
- Extração de claims `sub`, `email`, `email_verified`, `name`, `picture`.
- Emissão dos cookies internos seguindo padrão de `SessionAuthController`.
- Swagger.

Escopo excluído:

- Persistência nova além da já feita.
- Regras de domínio no controller.
- Testes com Google real.

Entregáveis técnicos:

- Endpoints de challenge e callback.
- Configuração Google por options.
- Testes de integração do contrato HTTP.

Critérios de aceite:

- `GET /api/auth/external/google` inicia challenge.
- Callback sem principal externo retorna erro seguro ou redireciona para erro configurado.
- Callback válido chama use case e emite autenticação interna.
- Cookie externo é limpo.
- Swagger documenta status principais.

Riscos:

- Rota divergir da Spec.
- Cookie/correlation falhar atrás do Gateway por forwarded headers.
- Logar dados sensíveis por engano.

Dependências:

- Sprint 3.
- Decisão de rota.

Ordem recomendada:

1. Options e pacote.
2. Configuração authentication.
3. Controller challenge.
4. Controller callback.
5. Emissão de cookies internos.
6. Testes.

### Sprint 5 - Gateway, ambiente e segurança

Objetivo:

- Preparar execução local, homologação e produção sem versionar secrets.

Escopo incluído:

- `appsettings.Development.json` com placeholders.
- `.env.development.example` com variáveis Google sem segredo real.
- `docker-compose.yml` com env vars.
- `ocelot.json` com rotas necessárias.
- CORS, allowed origins, cookies e forwarded headers.
- Rate limiting nas rotas externas.

Escopo excluído:

- Configurar projeto real no Google Cloud dentro do repositório.
- Versionar ClientSecret.
- Criar scripts de deploy complexos.

Entregáveis técnicos:

- Configuração local segura.
- Rotas via Gateway.
- Documentação operacional mínima.

Critérios de aceite:

- Nenhum secret real versionado.
- Gateway encaminha endpoints Google corretamente.
- `AllowedReturnUrls` e `AllowedOrigins` estão separados e claros.
- Produção exige HTTPS e cookies `Secure`.

Riscos:

- Confundir CORS allowed origins com returnUrl allowlist.
- Redirect URI cadastrado no Google apontar para host diferente do Gateway público.

Dependências:

- Sprint 4.

Ordem recomendada:

1. Definir nomes de env vars.
2. Atualizar appsettings placeholders.
3. Atualizar `.env.development.example`.
4. Atualizar Docker Compose.
5. Atualizar Ocelot.
6. Validar segurança.

### Sprint 6 - Hardening e validação ponta a ponta

Objetivo:

- Fechar segurança, observabilidade e validação manual/integrada.

Escopo incluído:

- Logs estruturados sem dados sensíveis.
- Eventos esperados da Spec.
- Métricas, se houver padrão disponível.
- Testes de integração adicionais.
- Checklist manual em development/homologação.
- Revisão arquitetural final.

Escopo excluído:

- Integrações com Gmail, Drive, Calendar ou revogação de token Google.
- Login com outros provedores.
- Google Cloud Identity Platform.

Entregáveis técnicos:

- Evidência de testes.
- Checklist de segurança.
- Fluxo ponta a ponta validado.

Critérios de aceite:

- Login novo, login existente, cancelamento, returnUrl inválido e usuário bloqueado testados.
- Logs não possuem token, code, secret, cookie ou e-mail puro quando evitável.
- Nenhuma camada viola dependência arquitetural.

Riscos:

- Testes automatizados cobrirem só contrato e não correlation real.
- Produção falhar por reverse proxy/HTTPS incorreto.

Dependências:

- Sprints 1 a 5.

Ordem recomendada:

1. Testes de integração.
2. Logs/auditoria.
3. Checklist manual.
4. Revisão de segurança.
5. Revisão final de arquitetura.

## 5. Backlog técnico por sprint

## Task 1.1 - Mapear impacto no domínio atual

Status: concluída em 2026-06-14.

### Objetivo

Entender como `ExternalLogin` se encaixa no domínio atual antes de criar tipos novos.

### Contexto

A Spec recomenda criar ou vincular usuário interno após autenticação Google. O `User` atual exige nome, sobrenome, contato e e-mail verificado para autenticação ativa.

### Escopo incluído

- Ler `User`, `UserStatus`, `Email`, `Role`, `SecurityStamp` e contratos atuais.
- Identificar se `User` pode ser criado automaticamente com dados disponíveis do Google.
- Registrar lacunas como decisão pendente no ticket/PR da sprint.

### Escopo excluído

- Alterar `User`.
- Criar `ExternalLogin`.
- Criar migrations.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Domain/Users/User.cs`
- `src/Backend/AuthCore/AuthCore.Domain/Users/UserStatus.cs`
- `src/Backend/AuthCore/AuthCore.Domain/Users/Email.cs`

### Passos de implementação

1. Revisar factories atuais de `User`.
2. Conferir validações obrigatórias.
3. Comparar com claims Google disponíveis na Spec.
4. Documentar decisões pendentes.

### Critérios de aceite

- Impacto de `Contact` obrigatório está claro.
- Impacto de `FirstName`/`LastName` a partir de `name` Google está claro.
- Nenhuma alteração de produção foi feita nesta task.

### Testes esperados

- Não aplicável.

### Observações de arquitetura

Não criar atalhos na Application para preencher dados obrigatórios sem regra de domínio explícita.

### Comando de validação sugerido

```bash
dotnet build AuthCore.sln
```

### Resultado da análise

Arquivos de domínio consultados:

- `src/Backend/AuthCore/AuthCore.Domain/Users/User.cs`
- `src/Backend/AuthCore/AuthCore.Domain/Users/UserStatus.cs`
- `src/Backend/AuthCore/AuthCore.Domain/Users/Email.cs`
- `src/Backend/AuthCore/AuthCore.Domain/Users/Role.cs`
- `src/Backend/AuthCore/AuthCore.Domain/Users/SecurityStamp.cs`
- `src/Backend/AuthCore/AuthCore.Domain/Users/Repositories/IUserRepository.cs`
- `src/Backend/AuthCore/AuthCore.Domain/Users/Repositories/IUserReadRepository.cs`

Arquivos de apoio consultados:

- `tests/AuthCore.Domain.UnitTests/Aggregates/Users/UserTests.cs`
- `src/Backend/AuthCore/AuthCore.Application/UseCases/Users/RegisterUser/RegisterUserUseCase.cs`
- `src/Backend/AuthCore/AuthCore.Application/UseCases/Authentication/Login/LoginUseCase.cs`
- `src/Backend/AuthCore/AuthCore.Application/UseCases/Authentication/LoginSession/LoginSessionUseCase.cs`
- `src/Backend/AuthCore/AuthCore.Infrastructure/Persistences/Write/PostgreSQL/Repositories/UserRepository.cs`
- `src/Backend/AuthCore/AuthCore.Infrastructure/Persistences/Read/PostgreSQL/Repositories/UserReadRepository.cs`
- `src/Backend/AuthCore/AuthCore.Infrastructure/Persistences/Migrations/Versions/Version0000001.cs`
- `src/Backend/AuthCore/AuthCore.Infrastructure/Persistences/Migrations/Versions/Version0000003.cs`
- `src/Backend/AuthCore/AuthCore.Infrastructure/Persistences/Migrations/Versions/Version0000006.cs`
- `src/Backend/AuthCore/AuthCore.Infrastructure/Persistences/Migrations/Versions/Version0000009.cs`

Impactos no domínio atual:

- `User` exige `FirstName`, `LastName`, `FullName`, `Email`, `Contact`, `Role`, `UserIdentifier`, `UserStatus` válido e `SecurityStamp`.
- `Contact` é invariante obrigatória no domínio e coluna `NOT NULL` na tabela `Users`; os escopos Google previstos (`openid`, `profile`, `email`) não fornecem telefone.
- `FirstName` e `LastName` são invariantes obrigatórias; o Google pode retornar `given_name` e `family_name`, mas a Spec também prevê `name`, que não deve ser parseado de forma frágil como regra de domínio.
- `UserStatus` possui apenas `PendingEmailVerification`, `Active` e `Blocked`; não existe estado `PendingOnboarding`.
- `CanSignIn` exige usuário ativo, `Status == Active` e `EmailVerifiedAt` preenchido.
- As factories atuais `Register`, `Create`, `Read` e `Restore` criam ou materializam usuários com as invariantes atuais; não há factory explícita para criação por login externo.
- `RegisterUserUseCase` cria senha, verificação de e-mail e outbox, então não deve ser reaproveitado diretamente para criação de usuário via Google.
- `IUserReadRepository.GetByEmailAsync` permite localizar usuário existente por e-mail normalizado, o que apoia o fluxo de vincular Google a usuário local quando `email_verified=true`.

Viabilidade da criação automática via Google:

- É viável com o modelo atual somente se o Google fornecer ou o fluxo já possuir `FirstName`, `LastName` e `Contact`, e se `email_verified=true` for aceito para preencher `EmailVerifiedAt` e ativar o usuário.
- Com os escopos mínimos previstos na Spec, a criação automática completa não é segura de forma geral, porque `Contact` não vem do Google e `LastName` pode faltar.
- A alternativa mais aderente ao domínio atual é criar/vincular automaticamente apenas quando os dados obrigatórios estiverem completos; caso contrário, iniciar onboarding ou retornar erro controlado até coletar os campos exigidos.

Decisões pendentes registradas:

- Definir se o domínio deve ganhar `UserStatus.PendingOnboarding`.
- Definir se será criada uma factory explícita como `User.CreateFromExternalLogin(...)`.
- Definir se `Contact` continuará obrigatório para todo `User` ou se onboarding será obrigatório quando o Google não fornecer telefone.
- Definir regra para ausência de `family_name`: usar claims específicas quando existirem e exigir onboarding quando faltar sobrenome.
- Definir como usuários criados por login externo se relacionam com `Password`, já que login tradicional exige senha, mas login Google não deve inventar senha local.

## Task 1.2 - Criar ExternalLoginProvider

Status: concluida em 2026-06-14.

### Objetivo

Representar o provedor externo suportado inicialmente.

### Contexto

A Spec define Google como provider inicial e recomenda deixar espaço para provedores futuros sem abstrair demais.

### Escopo incluído

- Criar enum `ExternalLoginProvider`.
- Incluir valor `Google = 1`.
- Adicionar XML docs em português.

### Escopo excluído

- Criar strategy por provider.
- Adicionar Meta, Microsoft, GitHub ou Apple.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Domain/Users/ExternalLoginProvider.cs`

### Passos de implementação

1. Criar enum no namespace `AuthCore.Domain.Users`.
2. Documentar enum e valor público.
3. Usar nomenclatura consistente com `UserStatus` e `Role`.

### Critérios de aceite

- Enum compila.
- Não há dependência fora do Domain.
- Apenas Google foi adicionado.

### Testes esperados

- Não é necessário teste isolado para enum simples.

### Observações de arquitetura

Evitar strings mágicas de provider nos use cases; converter da borda para enum antes de aplicar regra.

### Comando de validação sugerido

```bash
dotnet test tests/AuthCore.Domain.UnitTests/AuthCore.Domain.UnitTests.csproj
```

## Task 1.3 - Criar entidade ExternalLogin

Status: concluida em 2026-06-14.

### Objetivo

Modelar o vínculo entre usuário interno e conta Google.

### Contexto

A Spec exige vínculo por `Provider + ProviderUserId`, usando `sub` como identificador externo principal.

### Escopo incluído

- Criar entidade `ExternalLogin`.
- Criar factory `LinkGoogle`.
- Criar factory `Restore`.
- Criar método `RegisterUsage`.
- Validar `UserId`, provider, `ProviderUserId`, e-mail e datas.

### Escopo excluído

- Persistência.
- Consulta por e-mail.
- Token Google.
- Auditoria.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Domain/Users/ExternalLogin.cs`
- `tests/AuthCore.Domain.UnitTests/Aggregates/Users/ExternalLoginTests.cs`

### Passos de implementação

1. Criar classe `public sealed`.
2. Usar construtor privado.
3. Implementar `LinkGoogle`.
4. Implementar `Restore`.
5. Implementar `RegisterUsage`.
6. Cobrir invariantes nos testes.

### Critérios de aceite

- Não permite `Guid.Empty` para usuário.
- Não permite provider inválido.
- Não permite providerUserId vazio.
- Não permite e-mail vazio.
- `RegisterUsage` atualiza `LastUsedAtUtc`.

### Testes esperados

- `LinkGoogle_WhenProviderUserIdIsEmpty_ShouldThrowDomainException`
- `LinkGoogle_WhenUserIdIsEmpty_ShouldThrowDomainException`
- `LinkGoogle_WhenEmailIsEmpty_ShouldThrowDomainException`
- `RegisterUsage_WhenValidDate_ShouldUpdateLastUsedAtUtc`
- `Restore_WhenValidState_ShouldCreateExternalLogin`

### Observações de arquitetura

Manter regra de vínculo no domínio. A Application decide quando chamar a factory, mas não deve duplicar validações centrais.

### Comando de validação sugerido

```bash
dotnet test tests/AuthCore.Domain.UnitTests/AuthCore.Domain.UnitTests.csproj
```

## Task 1.4 - Definir contratos de repositório de ExternalLogin

Status: concluida em 2026-06-14.

### Objetivo

Expor operações necessárias para Application sem depender de infraestrutura.

### Contexto

O fluxo precisa buscar vínculo existente, criar vínculo e atualizar último uso.

### Escopo incluído

- Criar contrato de escrita.
- Criar contrato de leitura se houver consumidores distintos.
- Métodos mínimos: adicionar, atualizar, buscar por provider/providerUserId, buscar por userId/provider.

### Escopo excluído

- Implementação Npgsql.
- Consultas administrativas.
- Métodos genéricos não usados pela feature.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Domain/Users/Repositories/IExternalLoginRepository.cs`
- `src/Backend/AuthCore/AuthCore.Domain/Users/Repositories/IExternalLoginReadRepository.cs`

### Passos de implementação

1. Definir consumidores esperados na Application.
2. Criar interfaces pequenas.
3. Documentar métodos públicos em português.
4. Evitar interface com operações não usadas.

### Critérios de aceite

- Application consegue depender apenas dos contratos.
- Interfaces não têm métodos artificiais.
- Nenhuma implementação concreta foi criada.

### Testes esperados

- Não aplicável diretamente.

### Observações de arquitetura

Aplicar ISP: separar leitura e escrita se isso reduzir dependência dos use cases.

### Comando de validação sugerido

```bash
dotnet build AuthCore.sln
```

## Task 1.5 - Definir estratégia de criação de User por login externo

Status: concluida em 2026-06-14.

Decisão registrada:

- Não criar `RegisterFromExternalLogin` nesta task sem decisão confirmada para dados obrigatórios ausentes.
- Criação automática via Google só é segura quando `email_verified=true` e todos os dados obrigatórios do `User` atual estiverem disponíveis: `FirstName`, `LastName`, `Email` e `Contact`.
- Com os escopos mínimos do Google (`openid`, `profile`, `email`), `Contact` não é fornecido e `LastName` pode faltar; nesses casos, o fluxo deve seguir para onboarding ou retornar erro controlado.
- Não usar placeholders para `Contact`, `LastName` ou qualquer outro campo obrigatório.
- Não adicionar `UserStatus.PendingOnboarding` sem task específica para domínio, persistência, autenticação e autorização.

### Objetivo

Preparar o domínio para criação ou onboarding sem inventar requisito fora da Spec.

### Contexto

A Spec recomenda criação automática com `email_verified=true`, mas o modelo atual exige `Contact`, que o Google pode não fornecer.

### Escopo incluído

- Avaliar se `User` precisa de factory `RegisterFromExternalLogin` ou equivalente.
- Se houver decisão confirmada, implementar factory preservando invariantes.
- Se não houver decisão, manter como decisão pendente e não alterar `User`.

### Escopo excluído

- Criar onboarding completo.
- Criar placeholders fictícios para contato.
- Alterar schema de usuário sem decisão.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Domain/Users/User.cs`
- `tests/AuthCore.Domain.UnitTests/Aggregates/Users/UserTests.cs`

### Passos de implementação

1. Confirmar decisão de produto/técnica.
2. Se confirmado, criar factory no domínio.
3. Garantir que usuário ativo tenha e-mail verificado.
4. Adicionar testes.
5. Se não confirmado, registrar decisão pendente.

### Critérios de aceite

- Nenhum usuário incompleto é criado silenciosamente.
- Regras atuais de `CanSignIn` continuam válidas.
- Testes cobrem factory se ela for criada.

### Testes esperados

Condicionais à criação futura da factory:

- `RegisterFromExternalLogin_WhenEmailIsNotVerified_ShouldThrowDomainException`
- `RegisterFromExternalLogin_WhenRequiredDataIsMissing_ShouldFollowConfirmedDecision`

### Observações de arquitetura

Esta é uma decisão de alto impacto. Sem confirmação, a IA executora deve preferir onboarding/rejeição controlada a preencher dados falsos.

### Comando de validação sugerido

```bash
dotnet test tests/AuthCore.Domain.UnitTests/AuthCore.Domain.UnitTests.csproj
```

## Task 2.1 - Criar migration external_logins

Status: concluida em 2026-06-14.

### Objetivo

Criar estrutura persistente para vínculos externos.

### Contexto

A Spec exige impedir duplicidade de provider/providerUserId e vincular cada login externo a um usuário interno.

### Escopo incluído

- Criar `Version0000011`.
- Atualizar `DatabaseVersions`.
- Criar tabela `external_logins`.
- Criar foreign key para `Users`.
- Criar índice único `provider + provider_user_id`.
- Criar índice por `user_id`.

### Escopo excluído

- Alterar tabela `Users`, salvo decisão confirmada.
- Persistir tokens Google.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Infrastructure/Persistences/Migrations/Versions/Version0000011.cs`
- `src/Backend/AuthCore/AuthCore.Infrastructure/Persistences/Migrations/Versions/DatabaseVersions.cs`

### Passos de implementação

1. Identificar próximo número em `DatabaseVersions`.
2. Criar migration `ForwardOnlyMigration`.
3. Definir colunas: `id`, `user_id`, `provider`, `provider_user_id`, `email`, `email_verified`, `linked_at_utc`, `last_used_at_utc`.
4. Criar índices e FK.
5. Validar nomenclatura com padrões existentes.

### Critérios de aceite

- Migration aplica sem erro.
- Constraint única existe.
- FK aponta para `Users(Id)`.

### Testes esperados

- Teste de integração de migration/bootstrap.
- Teste de persistência cobrindo duplicidade.

### Observações de arquitetura

Aceitar `public sealed` para migration se FluentMigrator exigir discovery por reflexão.

### Comando de validação sugerido

```bash
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
```

## Task 2.2 - Implementar escrita de ExternalLogin

Status: concluida em 2026-06-14.

### Objetivo

Persistir criação e atualização do vínculo externo.

### Contexto

O use case precisará criar vínculo e atualizar `LastUsedAtUtc`.

### Escopo incluído

- Implementar `AddAsync`.
- Implementar `UpdateAsync` ou método específico para atualizar uso.
- Usar `IDatabaseSession`.
- Usar SQL explícito em raw string.

### Escopo excluído

- Materialização complexa de User.
- Queries administrativas.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Infrastructure/Persistences/Write/PostgreSQL/Repositories/ExternalLoginRepository.cs`

### Passos de implementação

1. Criar classe `internal sealed`.
2. Injetar `IDatabaseSession`.
3. Criar comandos parametrizados.
4. Usar transação atual.
5. Adicionar testes de integração.

### Critérios de aceite

- Insert persiste todos os campos.
- Update não altera provider/providerUserId indevidamente.
- SQL não concatena valores externos.

### Testes esperados

- `AddAsync_WhenExternalLoginIsValid_ShouldPersist`
- `UpdateAsync_WhenUsageIsRegistered_ShouldPersistLastUsedAtUtc`

### Observações de arquitetura

Repositório não deve decidir se o vínculo pode existir; apenas persiste estado válido.

### Comando de validação sugerido

```bash
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
```

## Task 2.3 - Implementar leitura de ExternalLogin

Status: concluida em 2026-06-14.

### Objetivo

Permitir que a Application resolva vínculo existente e conflitos.

### Contexto

O fluxo precisa procurar por `provider + providerUserId` e, no link/unlink, consultar vínculo por usuário.

### Escopo incluído

- Buscar por provider/providerUserId.
- Buscar por userId/provider, se necessário para link/unlink.
- Materializar por `ExternalLogin.Restore`.

### Escopo excluído

- Queries paginadas administrativas.
- Retorno de DTO de infraestrutura.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Infrastructure/Persistences/Read/PostgreSQL/Repositories/ExternalLoginReadRepository.cs`
- ou `ExternalLoginRepository.cs`, se um único repositório atender sem violar ISP.

### Passos de implementação

1. Criar método de busca por provider/providerUserId.
2. Criar método de busca por userId/provider somente se consumidor existir.
3. Materializar domínio.
4. Cobrir nulos quando não encontrado.

### Critérios de aceite

- Busca retorna domínio ou `null`.
- Não expõe tipos de infraestrutura.
- Consulta usa parâmetros.

### Testes esperados

- `GetByProviderAsync_WhenExternalLoginExists_ShouldReturnExternalLogin`
- `GetByProviderAsync_WhenExternalLoginDoesNotExist_ShouldReturnNull`

### Observações de arquitetura

Separar leitura/escrita se os use cases precisarem apenas de leitura em alguns fluxos.

### Comando de validação sugerido

```bash
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
```

## Task 2.4 - Registrar repositórios no DI

Status: concluida em 2026-06-14.

### Objetivo

Disponibilizar contratos de `ExternalLogin` para Application.

### Contexto

O padrão atual registra implementações concretas `internal` na infraestrutura e expõe contratos do domínio.

### Escopo incluído

- Atualizar `InfrastructureDependencyInjection`.
- Registrar leitura e escrita conforme interfaces criadas.
- Validar bootstrap.

### Escopo excluído

- Registrar use cases.
- Configurar Google Authentication.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Infrastructure/InfrastructureDependencyInjection.cs`
- `tests/AuthCore.IntegrationTests/SmokeTests/BootstrapSmokeTests.cs`

### Passos de implementação

1. Adicionar using necessário.
2. Registrar concrete repository.
3. Registrar interfaces.
4. Executar teste de bootstrap.

### Critérios de aceite

- DI resolve contratos.
- Implementações permanecem `internal`.
- Application não referencia Infrastructure.

### Testes esperados

- Bootstrap smoke test existente deve passar.
- Adicionar teste se houver cobertura explícita de DI.

### Observações de arquitetura

Não expor repositório concreto como contrato público.

### Comando de validação sugerido

```bash
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
```

## Task 3.1 - Criar validação de returnUrl

### Objetivo

Bloquear open redirect no início e conclusão do login externo.

### Contexto

A Spec exige allowlist e bloqueio de `javascript:`, `data:`, URLs protocol-relative e domínios arbitrários.

### Escopo incluído

- Criar contrato/policy de validação de `returnUrl`.
- Ler allowed return URLs de configuração por abstração testável.
- Normalizar URLs.
- Retornar URL segura ou falha previsível.

### Escopo excluído

- CORS.
- Redirect HTTP no controller.
- Configuração real de produção.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Application/UseCases/Authentication/ExternalLogin/ExternalReturnUrlValidator.cs`
- `src/Backend/AuthCore/AuthCore.Application/UseCases/Authentication/ExternalLogin/ExternalAuthenticationOptions.cs`, se options forem da aplicação.
- `tests/AuthCore.Application.UnitTests/UseCases/Authentication/ExternalLogin/ExternalReturnUrlValidatorTests.cs`

### Passos de implementação

1. Definir contrato pequeno se houver dependência de configuração.
2. Validar nulo/vazio com fallback seguro.
3. Bloquear esquemas perigosos.
4. Comparar origem contra allowlist.
5. Criar testes de URL válida e maliciosa.

### Critérios de aceite

- `https://app...` permitido quando allowlisted.
- `javascript:alert(1)` bloqueado.
- `data:text/html` bloqueado.
- `//site-malicioso.com` bloqueado.
- Domínio não allowlisted bloqueado.

### Testes esperados

- `Validate_WhenReturnUrlIsAllowed_ShouldReturnUrl`
- `Validate_WhenReturnUrlIsExternal_ShouldThrowValidationException`
- `Validate_WhenReturnUrlUsesJavascriptScheme_ShouldThrowValidationException`
- `Validate_WhenReturnUrlIsProtocolRelative_ShouldThrowValidationException`

### Observações de arquitetura

Não confundir `AllowedReturnUrls` com CORS `AllowedOrigins`.

### Comando de validação sugerido

```bash
dotnet test tests/AuthCore.Application.UnitTests/AuthCore.Application.UnitTests.csproj
```

## Task 3.2 - Criar command e result do login externo

### Objetivo

Definir contrato entre API e Application para conclusão do login Google.

### Contexto

A API extrai claims do principal externo e a Application orquestra domínio/persistência/sessão.

### Escopo incluído

- Criar `CompleteGoogleLoginCommand`.
- Criar `CompleteGoogleLoginResult`.
- Incluir campos mínimos: providerUserId, email, emailVerified, fullName, pictureUrl, returnUrl, ipAddress, userAgent.

### Escopo excluído

- Receber token Google.
- Receber `ClaimsPrincipal` na Application.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Application/UseCases/Authentication/ExternalLogin/CompleteGoogleLoginCommand.cs`
- `src/Backend/AuthCore/AuthCore.Application/UseCases/Authentication/ExternalLogin/CompleteGoogleLoginResult.cs`

### Passos de implementação

1. Criar classes `public sealed`.
2. Inicializar strings com `string.Empty`.
3. Documentar propriedades públicas quando necessário.
4. Evitar tipos HTTP.

### Critérios de aceite

- Command não depende de ASP.NET.
- Result contém dados necessários para API emitir cookie/redirect.
- Não há tokens Google no contrato.

### Testes esperados

- Não aplicável diretamente.

### Observações de arquitetura

O contrato de Application deve ser estável e independente do middleware Google.

### Comando de validação sugerido

```bash
dotnet build AuthCore.sln
```

## Task 3.3 - Criar CompleteGoogleLoginUseCase

### Objetivo

Concluir login Google e devolver autenticação interna do AuthCore.

### Contexto

A Spec exige identificar vínculo existente, criar/vincular usuário interno e emitir sessão/JWT próprio.

### Escopo incluído

- Buscar `ExternalLogin` por Google/sub.
- Se existir, carregar `User` interno.
- Se não existir, buscar usuário por e-mail verificado.
- Criar vínculo quando permitido.
- Criar usuário ou retornar onboarding conforme decisão pendente confirmada.
- Emitir sessão interna e access token como no fluxo browser/PWA.
- Usar transação para alterações persistentes.

### Escopo excluído

- Callback HTTP.
- Challenge Google.
- Persistir token Google.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Application/UseCases/Authentication/ExternalLogin/ICompleteGoogleLoginUseCase.cs`
- `src/Backend/AuthCore/AuthCore.Application/UseCases/Authentication/ExternalLogin/CompleteGoogleLoginUseCase.cs`
- `src/Backend/AuthCore/AuthCore.Application/ApplicationDependencyInjection.cs`
- `tests/AuthCore.Application.UnitTests/UseCases/Authentication/ExternalLogin/CompleteGoogleLoginUseCaseTests.cs`

### Passos de implementação

1. Criar interface pública.
2. Criar implementação `internal sealed`.
3. Injetar repositórios, sessão, token e unit of work.
4. Validar command.
5. Implementar fluxo vínculo existente.
6. Implementar fluxo usuário por e-mail verificado.
7. Implementar fluxo usuário novo/onboarding conforme decisão.
8. Persistir e emitir sessão.
9. Adicionar testes com fakes/spies.

### Critérios de aceite

- Google já vinculado autentica usuário.
- Vínculo novo é persistido em transação.
- Sessão interna é emitida.
- Rollback ocorre em falha após begin.
- Nenhum token Google é armazenado.

### Testes esperados

- `Execute_WhenExternalLoginExists_ShouldAuthenticateLinkedUser`
- `Execute_WhenVerifiedEmailMatchesExistingUser_ShouldLinkAndAuthenticate`
- `Execute_WhenEmailIsNotVerified_ShouldRejectAutomaticLink`
- `Execute_WhenUserIsBlocked_ShouldThrowForbiddenException`
- `Execute_WhenPersistenceFails_ShouldRollbackTransaction`

### Observações de arquitetura

Avaliar extração de serviço interno para emissão de sessão se houver duplicação com `LoginSessionUseCase`. Não criar abstração ampla se apenas duplicação pequena existir.

### Comando de validação sugerido

```bash
dotnet test tests/AuthCore.Application.UnitTests/AuthCore.Application.UnitTests.csproj
```

## Task 3.4 - Criar fluxo de vínculo Google a usuário autenticado

### Objetivo

Permitir vincular uma conta Google a usuário já autenticado, se confirmado no escopo.

### Contexto

A Spec cita `POST /auth/external/google/link`, mas pode ser entregue após login/callback inicial.

### Escopo incluído

- Criar command/use case de link.
- Validar usuário autenticado.
- Impedir Google já vinculado a outro usuário.
- Persistir vínculo.

### Escopo excluído

- Endpoint HTTP, se sprint atual for apenas Application.
- Desvincular conta.
- Trocar provider principal.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Application/UseCases/Authentication/ExternalLogin/LinkGoogleLoginUseCase.cs`
- `src/Backend/AuthCore/AuthCore.Application/UseCases/Authentication/ExternalLogin/LinkGoogleLoginCommand.cs`
- `tests/AuthCore.Application.UnitTests/UseCases/Authentication/ExternalLogin/LinkGoogleLoginUseCaseTests.cs`

### Passos de implementação

1. Confirmar se link está no escopo inicial.
2. Criar use case pequeno.
3. Buscar vínculo por provider/providerUserId.
4. Rejeitar conflito.
5. Criar vínculo.
6. Testar conflito e sucesso.

### Critérios de aceite

- Não permite tomar conta Google já vinculada.
- Não exige senha local para usuário já autenticado, salvo decisão futura.
- Registra transação corretamente.

### Testes esperados

- `Execute_WhenGoogleAccountIsAlreadyLinkedToAnotherUser_ShouldThrowConflictException`
- `Execute_WhenGoogleAccountIsAvailable_ShouldLinkToAuthenticatedUser`

### Observações de arquitetura

Se não confirmado, manter como task planejada para sprint futura e não implementar.

### Comando de validação sugerido

```bash
dotnet test tests/AuthCore.Application.UnitTests/AuthCore.Application.UnitTests.csproj
```

## Task 3.5 - Criar fluxo de desvincular Google

### Objetivo

Permitir desvincular Google de usuário autenticado, se confirmado no escopo.

### Contexto

A Spec menciona desvincular Google, mas não detalha regras suficientes.

### Escopo incluído

- Registrar decisão pendente sobre permitir usuário sem senha e sem provedor externo.
- Criar use case apenas após decisão.
- Impedir usuário ficar sem método de autenticação, se essa regra for confirmada.

### Escopo excluído

- Revogar token Google.
- Consumir API Google.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Application/UseCases/Authentication/ExternalLogin/UnlinkGoogleLoginUseCase.cs`
- `src/Backend/AuthCore/AuthCore.Application/UseCases/Authentication/ExternalLogin/UnlinkGoogleLoginCommand.cs`

### Passos de implementação

1. Confirmar regra de segurança.
2. Buscar vínculo do usuário.
3. Validar método alternativo de login, se aplicável.
4. Remover vínculo ou marcar como desvinculado conforme schema.
5. Testar.

### Critérios de aceite

- Usuário não perde todo acesso por acidente.
- Operação só afeta o usuário autenticado.

### Testes esperados

- `Execute_WhenExternalLoginDoesNotBelongToUser_ShouldThrowNotFoundException`
- `Execute_WhenUserWouldLoseLastLoginMethod_ShouldThrowValidationException`

### Observações de arquitetura

Pode exigir alteração de schema para soft delete. Sem decisão, não implementar.

### Comando de validação sugerido

```bash
dotnet test tests/AuthCore.Application.UnitTests/AuthCore.Application.UnitTests.csproj
```

## Task 4.1 - Adicionar configuração Google OAuth/OIDC

### Objetivo

Configurar autenticação externa com Google no AuthCore.Api.

### Contexto

A Spec recomenda usar middleware ASP.NET Core para state/correlation, evitando callback OAuth manual.

### Escopo incluído

- Adicionar pacote `Microsoft.AspNetCore.Authentication.Google`, se necessário.
- Criar options para ClientId, ClientSecret e CallbackPath.
- Configurar cookie externo temporário.
- Configurar scopes `openid`, `profile`, `email`.

### Escopo excluído

- Persistir tokens Google.
- Implementar callback manual.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Api/AuthCore.Api.csproj`
- `src/Backend/AuthCore/AuthCore.Api/ApiDependencyInjection.cs`
- `src/Backend/AuthCore/AuthCore.Api/appsettings.Development.json`

### Passos de implementação

1. Adicionar package reference compatível.
2. Criar options internas se necessário.
3. Configurar authentication builder.
4. Definir scheme Google e scheme externo.
5. Não habilitar `SaveTokens`.

### Critérios de aceite

- API compila.
- Google scheme está registrado.
- Tokens Google não são salvos.
- ClientSecret vem de configuração/env var.

### Testes esperados

- Bootstrap smoke test.

### Observações de arquitetura

Options de autenticação HTTP podem ficar na API. Se ficarem na Infrastructure, não devem vazar para contratos HTTP.

### Comando de validação sugerido

```bash
dotnet build AuthCore.sln
```

## Task 4.2 - Criar endpoint para iniciar login Google

### Objetivo

Criar rota pública que inicia challenge Google.

### Contexto

O frontend chama o AuthCore, que valida `returnUrl` e redireciona para o Google.

### Escopo incluído

- Criar action `GET /api/auth/external/google`, se decisão de rota seguir padrão atual.
- Validar `returnUrl`.
- Criar `AuthenticationProperties`.
- Incluir `returnUrl` em properties seguras.
- Retornar `Challenge`.

### Escopo excluído

- Concluir callback.
- Criar sessão.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Api/Controllers/ExternalAuthController.cs`

### Passos de implementação

1. Criar controller com `[Route("api/auth/external")]`.
2. Injetar use case/validator via `[FromServices]`.
3. Validar returnUrl.
4. Definir RedirectUri para callback.
5. Retornar `Challenge`.

### Critérios de aceite

- `returnUrl` inválido é bloqueado antes do challenge.
- `Challenge` usa scheme Google.
- Controller não acessa repositório.

### Testes esperados

- `Google_WhenReturnUrlIsInvalid_ShouldReturnBadRequestOrRedirectToError`
- `Google_WhenReturnUrlIsAllowed_ShouldReturnChallenge`

### Observações de arquitetura

GET público não deve exigir CSRF, mas deve ter rate limit no Gateway.

### Comando de validação sugerido

```bash
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
```

## Task 4.3 - Criar callback Google

### Objetivo

Receber retorno do Google, extrair claims e chamar Application.

### Contexto

O middleware valida state/correlation. A API transforma principal externo em command.

### Escopo incluído

- Criar action `GET /api/auth/external/google/callback`.
- Usar `AuthenticateAsync` no scheme externo.
- Extrair `sub`, `email`, `email_verified`, `name`, `picture`.
- Chamar `ICompleteGoogleLoginUseCase`.
- Limpar cookie externo.
- Redirecionar para resultado seguro.

### Escopo excluído

- Validar regra de criação/vínculo no controller.
- Persistir claims diretamente.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Api/Controllers/ExternalAuthController.cs`

### Passos de implementação

1. Autenticar resultado externo.
2. Tratar falha/cancelamento.
3. Validar presença de `sub` e `email` como entrada HTTP mínima.
4. Montar command.
5. Chamar use case.
6. Emitir cookies internos.
7. Fazer sign out do scheme externo.
8. Redirecionar.

### Critérios de aceite

- Callback inválido não autentica usuário.
- Claims obrigatórias ausentes geram erro seguro.
- Cookie temporário externo é removido.
- Tokens Google não são logados.

### Testes esperados

- `Callback_WhenExternalAuthenticationFails_ShouldRedirectToError`
- `Callback_WhenRequiredClaimsAreMissing_ShouldReturnBadRequestOrRedirectToError`
- `Callback_WhenExternalAuthenticationSucceeds_ShouldCallUseCase`

### Observações de arquitetura

Manter controller como adapter. Toda decisão de usuário/vínculo fica no use case.

### Comando de validação sugerido

```bash
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
```

## Task 4.4 - Emitir cookies internos após login Google

### Objetivo

Reutilizar o padrão atual de sessão browser/PWA após autenticação externa.

### Contexto

O AuthCore já emite cookies `sid`, `at` e `XSRF-TOKEN` no login por sessão.

### Escopo incluído

- Reutilizar lógica equivalente à de `SessionAuthController`.
- Emitir cookie de sessão.
- Emitir cookie de access token.
- Emitir cookie CSRF.
- Respeitar `AuthCookieOptions` e `CsrfOptions`.

### Escopo excluído

- Expor access token Google.
- Retornar JWT interno no body para fluxo browser, salvo decisão específica.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Api/Controllers/ExternalAuthController.cs`
- Possível helper compartilhado em `src/Backend/AuthCore/AuthCore.Api/Authentication`, se a duplicação ficar relevante.

### Passos de implementação

1. Avaliar duplicação com `SessionAuthController`.
2. Extrair helper interno se necessário e seguro.
3. Emitir cookies com policies existentes.
4. Testar flags básicas em integração.

### Critérios de aceite

- Cookies internos são emitidos com nomes configurados.
- `sid` e `at` são `HttpOnly`.
- CSRF token é emitido para browser.
- Produção pode usar `Secure`.

### Testes esperados

- `Callback_WhenLoginSucceeds_ShouldAppendAuthenticationCookies`

### Observações de arquitetura

Não criar dependência da Application para cookies. A emissão é responsabilidade da API.

### Comando de validação sugerido

```bash
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
```

## Task 4.5 - Documentar Swagger e erros HTTP

### Objetivo

Manter contratos HTTP previsíveis e documentados.

### Contexto

O projeto usa XML docs e `ProducesResponseType`.

### Escopo incluído

- XML docs no controller.
- `ProducesResponseType` para sucesso, erro previsível, não autorizado e conflito quando aplicável.
- Usar `ResponseErrorJson` se houver resposta JSON.

### Escopo excluído

- Criar documentação operacional extensa.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Api/Controllers/ExternalAuthController.cs`
- `src/Backend/AuthCore/AuthCore.Api/Contracts/Responses/ResponseErrorJson.cs`

### Passos de implementação

1. Adicionar summaries em português.
2. Declarar responses esperadas.
3. Validar Swagger em desenvolvimento.

### Critérios de aceite

- Swagger mostra endpoints.
- Erros previsíveis usam padrão existente.
- Nenhum tipo de infraestrutura aparece no contrato HTTP.

### Testes esperados

- Teste de integração de rota, se já houver padrão.

### Observações de arquitetura

Fluxos de redirect podem não retornar JSON; documentar claramente status real.

### Comando de validação sugerido

```bash
dotnet build AuthCore.sln
```

## Task 5.1 - Configurar appsettings de desenvolvimento

### Objetivo

Adicionar placeholders seguros para Google Login em desenvolvimento.

### Contexto

A Spec mostra `Authentication:Google` e `AllowedReturnUrls`; o projeto já tem `Authentication:Jwt` e `Auth:Csrf`.

### Escopo incluído

- Adicionar ClientId placeholder.
- Não adicionar ClientSecret real.
- Adicionar CallbackPath.
- Adicionar AllowedReturnUrls.

### Escopo excluído

- Configurar homologação/produção com valores reais.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Api/appsettings.Development.json`

### Passos de implementação

1. Definir seção final conforme options.
2. Inserir placeholders.
3. Garantir que secrets reais serão env vars/user-secrets.

### Critérios de aceite

- Arquivo não contém secret real.
- CallbackPath corresponde à rota implementada.
- AllowedReturnUrls contém localhost do frontend.

### Testes esperados

- Bootstrap smoke test.

### Observações de arquitetura

Usar nomes de seção consistentes e não duplicar configurações em locais conflitantes.

### Comando de validação sugerido

```bash
dotnet build AuthCore.sln
```

## Task 5.2 - Atualizar env example e Docker Compose

### Objetivo

Permitir configuração local via variáveis sem versionar segredo.

### Contexto

O projeto usa `.env.development.example` e `docker-compose.yml` para ambiente local.

### Escopo incluído

- Adicionar variáveis `AUTHENTICATION__GOOGLE__CLIENTID`, `AUTHENTICATION__GOOGLE__CLIENTSECRET`, `AUTHENTICATION__GOOGLE__CALLBACKPATH` ou nomes equivalentes definidos.
- Adicionar allowed return URLs.
- Mapear env vars no serviço `authcore-api`.

### Escopo excluído

- Preencher ClientSecret real.
- Alterar infraestrutura de banco/Redis/RabbitMQ.

### Arquivos prováveis

- `src/Backend/.env.development.example`
- `src/Backend/docker-compose.yml`

### Passos de implementação

1. Adicionar variáveis ao example com valores vazios/placeholders.
2. Mapear variáveis no Compose.
3. Garantir que `.env.development` continua ignorado.

### Critérios de aceite

- Nenhum secret real.
- Compose injeta configuração esperada.
- Ambiente local continua subindo quando Google não está configurado, se essa for a decisão de bootstrap.

### Testes esperados

- `dotnet build`.
- Teste manual com Docker após preencher secrets localmente.

### Observações de arquitetura

Se ClientId/Secret forem obrigatórios no bootstrap, desenvolvimento sem Google configurado pode quebrar. Registrar decisão.

### Comando de validação sugerido

```bash
dotnet build AuthCore.sln
```

## Task 5.3 - Atualizar Gateway/Ocelot

### Objetivo

Publicar rotas Google Login via Gateway quando Gateway for ponto de entrada.

### Contexto

A Spec recomenda redirect URI apontando para o Gateway quando ele for a borda pública.

### Escopo incluído

- Adicionar rotas públicas para `/api/auth/external/google` e callback.
- Aplicar rate limit.
- Não exigir Bearer nessas rotas.

### Escopo excluído

- Validação de regra de negócio no Gateway.
- Reescrever callback.

### Arquivos prováveis

- `src/Backend/Gateway/Gateway.Api/ocelot.json`

### Passos de implementação

1. Adicionar rota específica com prioridade adequada.
2. Definir métodos GET.
3. Configurar downstream para AuthCore.
4. Configurar rate limit.
5. Validar rota genérica `/api/auth/{everything}`.

### Critérios de aceite

- Gateway encaminha challenge e callback.
- Rotas não exigem Bearer.
- Rate limit está ativo.

### Testes esperados

- Teste de integração Gateway, se existir cobertura adequada.
- Teste manual via `http://localhost:8080`.

### Observações de arquitetura

Gateway deve permanecer fino e não absorver regra de AuthCore.

### Comando de validação sugerido

```bash
dotnet test tests/Gateway.IntegrationTests/Gateway.IntegrationTests.csproj
```

## Task 5.4 - Revisar forwarded headers, CORS e cookies

### Objetivo

Garantir funcionamento atrás de proxy/Gateway e segurança de browser.

### Contexto

OAuth callback e cookies dependem de host/proto corretos.

### Escopo incluído

- Revisar `UseForwardedHeaders`.
- Revisar `ReverseProxy` options.
- Revisar cookies `Secure`, `SameSite`, `HttpOnly`.
- Separar CORS origins de return URLs.

### Escopo excluído

- Criar proxy Nginx real.
- Configurar certificados reais.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Api/ApiDependencyInjection.cs`
- `src/Backend/AuthCore/AuthCore.Api/Program.cs`
- `src/Backend/AuthCore/AuthCore.Api/appsettings.Development.json`
- `src/Backend/docker-compose.yml`

### Passos de implementação

1. Validar ordem de middleware.
2. Validar headers encaminhados.
3. Validar cookie policy existente.
4. Criar checklist manual.

### Critérios de aceite

- Callback público consegue montar URLs corretas.
- Cookies são seguros em produção.
- CORS não aceita origem arbitrária.

### Testes esperados

- Testes de integração existentes de CORS.
- Teste manual via Gateway.

### Observações de arquitetura

OAuth state/correlation pode falhar se SameSite/Secure/proxy estiverem incoerentes.

### Comando de validação sugerido

```bash
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
```

## Task 6.1 - Adicionar testes de integração HTTP

### Objetivo

Validar contrato HTTP e composição de autenticação externa sem depender de Google real.

### Contexto

Fluxos OAuth reais são difíceis de automatizar, mas challenge, callback inválido e composição podem ser testados.

### Escopo incluído

- Testar endpoint de início.
- Testar returnUrl inválido.
- Testar callback sem principal externo.
- Testar configuração/DI.

### Escopo excluído

- Abrir navegador real.
- Chamar Google real.

### Arquivos prováveis

- `tests/AuthCore.IntegrationTests/Authentication/ExternalAuthControllerIntegrationTests.cs`

### Passos de implementação

1. Criar testes com WebApplicationFactory ou padrão existente.
2. Simular configuração Google fake.
3. Validar status/headers.
4. Validar erro seguro.

### Critérios de aceite

- Testes passam sem internet.
- Testes não exigem ClientSecret real.
- Contrato HTTP básico está coberto.

### Testes esperados

- `Google_WhenReturnUrlIsAllowed_ShouldChallengeGoogle`
- `Google_WhenReturnUrlIsInvalid_ShouldRejectRequest`
- `Callback_WhenExternalPrincipalIsMissing_ShouldReturnSafeFailure`

### Observações de arquitetura

Não transformar teste de integração em teste de Google. O objetivo é validar o AuthCore.

### Comando de validação sugerido

```bash
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
```

## Task 6.2 - Validar logs, auditoria e métricas

### Objetivo

Garantir observabilidade sem expor dados sensíveis.

### Contexto

A Spec pede eventos como `GoogleLoginStarted`, `GoogleLoginSucceeded`, `GoogleLoginFailed` e conflitos.

### Escopo incluído

- Adicionar logs estruturados nos pontos principais.
- Evitar e-mail puro quando possível.
- Não logar code, token, cookie, ClientSecret ou id_token.
- Avaliar métricas se houver padrão existente.

### Escopo excluído

- Criar stack de observabilidade nova.
- Criar dashboards completos.

### Arquivos prováveis

- `src/Backend/AuthCore/AuthCore.Api/Controllers/ExternalAuthController.cs`
- `src/Backend/AuthCore/AuthCore.Application/UseCases/Authentication/ExternalLogin/CompleteGoogleLoginUseCase.cs`

### Passos de implementação

1. Mapear eventos da Spec.
2. Adicionar logs mínimos.
3. Revisar payload dos logs.
4. Criar testes quando houver contrato verificável.

### Critérios de aceite

- Sucesso e falha são rastreáveis.
- Nenhum segredo é logado.
- Logs têm correlation id quando disponível.

### Testes esperados

- Testes unitários se houver logger fake já usado.
- Revisão manual de logs em execução local.

### Observações de arquitetura

Auditoria persistida não deve ser inventada sem padrão existente; começar por logs estruturados se não houver mecanismo de auditoria.

### Comando de validação sugerido

```bash
dotnet test tests/AuthCore.Application.UnitTests/AuthCore.Application.UnitTests.csproj
```

## Task 6.3 - Executar checklist manual em development/homologação

### Objetivo

Validar o fluxo real com Google fora dos testes automatizados.

### Contexto

State/correlation, cookies, browser e Google Console exigem validação manual.

### Escopo incluído

- Login com usuário novo.
- Login com usuário existente.
- Cancelamento no Google.
- ReturnUrl válido.
- ReturnUrl malicioso.
- Callback via Gateway.
- Logout após login Google.
- Usuário bloqueado.

### Escopo excluído

- Produção real, salvo aprovação específica.
- Testes com escopos além de `openid profile email`.

### Arquivos prováveis

- Não há alteração obrigatória.
- Registrar evidências em PR/ticket, não necessariamente no repositório.

### Passos de implementação

1. Configurar Google OAuth Client local.
2. Preencher secrets localmente.
3. Subir infraestrutura.
4. Executar cenários.
5. Registrar resultado.

### Critérios de aceite

- Fluxos principais passam.
- Falhas redirecionam para rota segura.
- Cookies e headers estão corretos.

### Testes esperados

- Testes manuais documentados.

### Observações de arquitetura

Produção exige domínio, HTTPS, privacy policy, terms e redirect URI público corretos.

### Comando de validação sugerido

```bash
./run.sh docker
```

## Task 6.4 - Revisão final de arquitetura e segurança

### Objetivo

Garantir que a entrega final respeita Spec e padrões do repositório.

### Contexto

A feature atravessa todas as camadas e tem riscos de segurança.

### Escopo incluído

- Revisar dependências entre camadas.
- Revisar secrets.
- Revisar logs.
- Revisar testes.
- Revisar Swagger.
- Revisar checklist da Spec.

### Escopo excluído

- Refactors amplos não relacionados.

### Arquivos prováveis

- Todos os arquivos alterados nas sprints.

### Passos de implementação

1. Executar build.
2. Executar testes de domínio.
3. Executar testes de aplicação.
4. Executar testes de integração.
5. Revisar diffs.
6. Executar revisão técnica especializada se disponível.

### Critérios de aceite

- Build passa.
- Testes passam ou limitações estão documentadas.
- Nenhuma camada viola dependência.
- Nenhum secret versionado.
- Nenhum token sensível em log.

### Testes esperados

- Todos os testes relevantes de AuthCore.

### Observações de arquitetura

Não finalizar com decisão pendente que bloqueie segurança do fluxo.

### Comando de validação sugerido

```bash
dotnet build AuthCore.sln
dotnet test tests/AuthCore.Domain.UnitTests/AuthCore.Domain.UnitTests.csproj
dotnet test tests/AuthCore.Application.UnitTests/AuthCore.Application.UnitTests.csproj
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
```

## 6. Estratégia de configuração por ambiente

### Desenvolvimento local

Configurações esperadas:

- `src/Backend/AuthCore/AuthCore.Api/appsettings.Development.json` com placeholders seguros.
- `src/Backend/.env.development.example` com variáveis Google vazias ou placeholders.
- `src/Backend/.env.development` local ignorado pelo Git para valores reais.
- User Secrets podem ser usados para `ClientSecret` quando rodar fora do Docker.

Variáveis prováveis:

- `AUTHENTICATION__GOOGLE__CLIENTID`
- `AUTHENTICATION__GOOGLE__CLIENTSECRET`
- `AUTHENTICATION__GOOGLE__CALLBACKPATH`
- `AUTHENTICATION__ALLOWEDRETURNURLS__0`
- `AUTHENTICATION__ALLOWEDRETURNURLS__1`

URLs locais prováveis:

- AuthCore direto: `http://localhost:5012/api/auth/external/google/callback`, se HTTP local continuar padrão.
- AuthCore Docker direto: `http://localhost:8081/api/auth/external/google/callback`.
- Gateway Docker: `http://localhost:8080/api/auth/external/google/callback`.

Decisão pendente:

- A Spec mostra HTTPS localhost. O projeto atual documenta HTTP local. Confirmar se Google OAuth local usará HTTPS profile ou Gateway HTTP em desenvolvimento.

### Homologação

Configurações esperadas:

- ClientId e ClientSecret próprios de homologação.
- Redirect URI cadastrado no Google apontando para Gateway se ele for a borda pública.
- AllowedReturnUrls restritos ao frontend de homologação.
- Cookies `Secure=true` quando houver HTTPS.
- Forwarded headers confiáveis configurados.

Variáveis/segredos:

- Injetar por pipeline, secret manager ou variáveis seguras.
- Não versionar ClientSecret.

### Produção

Configurações esperadas:

- Projeto/OAuth Client Google de produção separado.
- OAuth Consent Screen com domínio autorizado, privacy policy e terms.
- Redirect URI público fixo.
- HTTPS obrigatório.
- HSTS e cookies `Secure`.
- AllowedReturnUrls somente de apps oficiais.
- Logs e métricas ativos.

Cuidados:

- Não usar ClientId/Secret de desenvolvimento.
- Não aceitar returnUrl arbitrário.
- Não permitir callback público diferente do cadastrado.
- Não logar tokens, authorization code, cookies, ClientSecret ou id_token.

### Redis

Redis já apoia sessão/cache. O login Google deve usar a mesma estratégia de sessão interna já existente, sem armazenar token Google.

### Banco de dados

PostgreSQL deve receber apenas vínculo externo e dados internos necessários. Não persistir access token, refresh token ou id token do Google.

### JWT e sessão

Após callback Google bem-sucedido:

- Browser/PWA: emitir sessão server-side e cookies internos como no login por sessão.
- API/mobile: emissão de JWT/refresh token interno só se houver decisão explícita para esse contrato.

### CORS e AllowedOrigins

CORS controla origens que podem chamar a API no browser. Não deve ser usado como substituto de `AllowedReturnUrls`.

### Observabilidade

Eventos mínimos recomendados:

- `GoogleLoginStarted`
- `GoogleLoginSucceeded`
- `GoogleLoginFailed`
- `ExternalLoginLinked`
- `ExternalLoginConflictDetected`
- `ExternalLoginBlockedUser`
- `ExternalLoginRequiresOnboarding`

## 7. Checklist final de pronto

- [ ] Código compilando.
- [ ] Testes de domínio passando.
- [ ] Testes de aplicação passando.
- [ ] Testes de integração relevantes passando.
- [ ] Configurações revisadas por ambiente.
- [ ] ClientSecret fora do repositório.
- [ ] Nenhum token Google persistido.
- [ ] Nenhum token, cookie, code, id_token ou ClientSecret em log.
- [ ] `returnUrl` validado por allowlist.
- [ ] CORS revisado sem origem arbitrária.
- [ ] Cookies revisados com `HttpOnly`, `Secure` em produção e `SameSite` adequado.
- [ ] Forwarded headers validados atrás do Gateway/proxy.
- [ ] Gateway roteando challenge e callback.
- [ ] Rate limit aplicado nas rotas externas.
- [ ] Swagger validado.
- [ ] Fluxo usuário novo testado.
- [ ] Fluxo usuário existente testado.
- [ ] Fluxo Google já vinculado testado.
- [ ] Fluxo usuário bloqueado testado.
- [ ] Fluxo e-mail não verificado testado conforme decisão.
- [ ] Fluxo cancelado no Google tratado.
- [ ] Logout após login Google validado.
- [ ] Nenhuma camada violando dependência arquitetural.
- [ ] Nenhum secret versionado.
- [ ] Critérios da Spec atendidos ou decisões pendentes documentadas.

## 8. Decisões pendentes

### Decisão 1 - Rota pública final

Impacto:

- Afeta controller, Gateway, Google Redirect URI, documentação e frontend.

Opções possíveis:

- Usar rota da Spec: `/auth/external/google`.
- Usar padrão atual do repo: `/api/auth/external/google`.

Recomendação técnica:

- Usar `/api/auth/external/google` e `/api/auth/external/google/callback`, preservando o padrão atual de rotas do AuthCore.

### Decisão 2 - Criação automática de usuário novo

Impacto:

- Afeta domínio, Application, onboarding, banco e UX.

Opções possíveis:

- Criar usuário automaticamente quando `email_verified=true`.
- Criar pré-cadastro/onboarding.
- Negar até cadastro local completo.

Recomendação técnica:

- Seguir a Spec: criar automaticamente somente quando dados obrigatórios do domínio existirem; caso contrário, iniciar onboarding ou retornar erro controlado.

### Decisão 3 - Campo Contact obrigatório

Impacto:

- Google normalmente não retorna telefone no escopo básico. O `User` atual exige `Contact`.

Opções possíveis:

- Exigir onboarding para coletar contato.
- Tornar contato opcional no domínio.
- Adotar valor placeholder.

Recomendação técnica:

- Não usar placeholder. Preferir onboarding ou decisão explícita de tornar contato opcional com alteração de domínio e testes.

### Decisão 4 - Separação de firstName/lastName

Impacto:

- Google pode retornar `name`, `given_name` e `family_name`, mas nem sempre todos estarão presentes.

Opções possíveis:

- Usar `given_name`/`family_name` quando disponíveis.
- Fazer parsing de `name`.
- Exigir onboarding quando faltar sobrenome.

Recomendação técnica:

- Usar claims específicas quando disponíveis e cair para onboarding quando dados obrigatórios faltarem.

### Decisão 5 - Link/unlink no escopo inicial

Impacto:

- Aumenta superfície HTTP e regras de segurança.

Opções possíveis:

- Implementar login/callback primeiro.
- Implementar link junto.
- Implementar link e unlink juntos.

Recomendação técnica:

- Implementar login/callback primeiro. Planejar link/unlink em sprint posterior, salvo necessidade imediata.

### Decisão 6 - Estratégia de erro no callback

Impacto:

- Afeta UX e segurança de redirecionamento.

Opções possíveis:

- Redirecionar para `/auth/error?reason=...`.
- Retornar JSON de erro.
- Redirecionar para `returnUrl` com query de erro.

Recomendação técnica:

- Para fluxo browser, redirecionar para rota de erro allowlisted. Para testes/contratos, manter erro seguro e padronizado.

### Decisão 7 - Emissão JWT para mobile/API no login Google

Impacto:

- Afeta contrato público, segurança e storage de refresh token.

Opções possíveis:

- Apenas cookies browser/PWA inicialmente.
- Criar contrato token-based separado para mobile.

Recomendação técnica:

- Começar por cookies/sessão browser/PWA, alinhado ao fluxo web da Spec, e planejar token-based separado se houver consumidor mobile/API.

### Decisão 8 - Auditoria persistida versus logs estruturados

Impacto:

- Pode exigir nova tabela, events ou integração de observabilidade.

Opções possíveis:

- Logs estruturados inicialmente.
- Auditoria persistida em tabela própria.
- Métricas + logs.

Recomendação técnica:

- Usar logs estruturados inicialmente se não houver mecanismo de auditoria persistida já estabelecido. Não inventar tabela de auditoria sem Spec adicional.

### Decisão 9 - Obrigatoriedade de ClientId/ClientSecret no bootstrap

Impacto:

- Pode quebrar ambiente local sem Google configurado.

Opções possíveis:

- Exigir config válida sempre.
- Registrar Google somente quando config estiver presente.
- Exigir config apenas fora de desenvolvimento.

Recomendação técnica:

- Em desenvolvimento, permitir bootstrap sem Google se endpoints retornarem erro configurado. Em homologação/produção, falhar bootstrap quando Google Login estiver habilitado e secrets estiverem ausentes.

### Decisão 10 - Redirect URI local

Impacto:

- Afeta Google Console e testes manuais.

Opções possíveis:

- Usar AuthCore direto.
- Usar Gateway.
- Usar HTTPS local.

Recomendação técnica:

- Usar Gateway quando o fluxo real de produção também passar pelo Gateway. Confirmar se ambiente local terá HTTPS para compatibilidade com Google.
