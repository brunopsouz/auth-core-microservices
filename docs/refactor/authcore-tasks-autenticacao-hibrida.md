# AuthCore - Tasks da Autenticacao Hibrida

**Base:** `docs/refactor/authcore-sdd-autenticacao-hibrida.md`  
**Tema:** divisao da implementacao em features e tasks executaveis  
**Data:** 2026-06-01

---

## 1. Objetivo

Este documento divide a implementacao da autenticacao hibrida em features tecnicas, tasks por camada, dependencias, criterios de aceite e validacoes esperadas.

A execucao deve seguir a SDD como fonte principal e preservar:

- dominio como fonte das regras centrais;
- aplicacao como orquestradora;
- API como borda HTTP fina;
- infraestrutura como detalhe tecnico;
- PostgreSQL como fonte da verdade de sessoes longas;
- Redis apenas para dados efemeros ou cache opcional.

---

## 2. Ordem Recomendada

```txt
F00 Preparacao e inventario
F01 Dominio de sessao duravel
F02 Persistencia PostgreSQL de sessoes
F03 Emissao de access token curto vinculado a sessao
F04 Login browser/session com cookies
F05 Refresh browser/session
F06 Logout e gestao de sessoes
F07 Troca de senha e revogacao de sessoes
F08 Hardening HTTP, cookies, CSRF, CORS e Gateway
F09 Testes integrados e validacao final
```

Essa ordem evita adaptar a borda HTTP antes de estabilizar o dominio e a fonte da verdade.

---

## F00 - Preparacao e Inventario

### Objetivo

Confirmar o estado atual dos fluxos de sessao, token, refresh token, cookies, Redis e persistencia antes de alterar codigo.

### Tasks

- [ ] Mapear os arquivos atuais de dominio em `AuthCore.Domain/Users`, `AuthCore.Domain/Passports` e `AuthCore.Domain/Security/Tokens`.
- [ ] Mapear os casos de uso atuais em `UseCases/Authentication/*` e `UseCases/Users/ChangePassword`.
- [ ] Mapar os controllers atuais `SessionAuthController`, `TokenAuthController`, `AuthController` e `UserController`.
- [ ] Mapear a persistencia atual de `RefreshToken`, `User`, `Password` e `Session` em Redis.
- [ ] Identificar migracao versionada seguinte para a tabela de sessoes.
- [ ] Registrar quais testes atuais cobrem login, refresh token, sessao por cookie, logout e troca de senha.

### Criterios de Aceite

- Existe entendimento claro de quais fluxos sao browser/session e quais sao token-based.
- Nao ha dependencia nova ou refactor aplicado nesta fase.
- A proxima migracao versionada foi identificada.

### Validacao

- Nao exige teste automatizado.
- Validar com `git status --short` antes de iniciar as alteracoes funcionais.

---

## F01 - Dominio de Sessao Duravel

### Objetivo

Evoluir o modelo de dominio para que `Passports.Session` represente uma sessao principal duravel, revogavel e apta a controlar emissao de access token curto.

### Tasks de Dominio

- [ ] Evoluir `Passports.Session` para carregar estado explicito da sessao.
- [ ] Criar `SessionStatus` com estados minimos `Active`, `Expired` e `Revoked`; adicionar outros somente se forem usados.
- [ ] Criar `SessionRevocationReason` com motivos minimos `UserLogout`, `UserRevokedDevice` e `PasswordChanged`.
- [ ] Avaliar e criar `SessionIdentifier` como value object para identificador opaco, caso reduza duplicacao e proteja invariantes.
- [ ] Criar identificador publico nao secreto da sessao, separado do `sid` opaco usado no cookie.
- [ ] Adicionar `SecurityStamp` em `User` ou como value object dedicado, preservando factories `Register`, `Create`, `Read` e `Restore`.
- [ ] Adicionar comportamento em `User` para rotacionar `SecurityStamp`.
- [ ] Adicionar comportamento em `Session` para validar emissao de access token.
- [ ] Adicionar comportamento em `Session` para revogar de forma idempotente quando fizer sentido.
- [ ] Adicionar comportamento em `Session` para atualizar `LastSeenAtUtc` com validacao de data.
- [ ] Criar ou evoluir contrato de repositorio duravel de sessao no dominio.
- [ ] Criar contrato para hash de identificador de sessao, sem expor algoritmo concreto.

### Tasks de Testes

- [ ] Adicionar testes de criacao de sessao valida.
- [ ] Adicionar testes de restauracao de sessao persistida.
- [ ] Adicionar testes de sessao expirada impedindo emissao de access token.
- [ ] Adicionar teste garantindo que `ExpiresAtUtc <= now` impede emissao mesmo quando o status persistido ainda e `Active`.
- [ ] Adicionar testes de sessao revogada impedindo emissao de access token.
- [ ] Adicionar testes de `SecurityStamp` divergente impedindo emissao de access token.
- [ ] Adicionar testes de revogacao e dados de revogacao.
- [ ] Adicionar testes de rotacao de `SecurityStamp` em `User`.

### Criterios de Aceite

- Regras centrais de sessao vivem no dominio.
- `Session` nao conhece JWT, cookie, Redis, PostgreSQL ou ASP.NET Core.
- `User` preserva invariantes atuais de status e e-mail verificado.
- `EnsureCanIssueAccessToken` nega emissao quando `ExpiresAtUtc <= now`, independentemente do status persistido.
- Se `Expired` for persistido, a feature define quem executa a transicao; caso contrario, expiracao e tratada como estado derivado.
- Identificador publico de sessao nao e segredo e nao substitui o `sid` opaco do cookie.
- XML docs publicas permanecem em portugues e no padrao do projeto.

### Validacao

```bash
dotnet test tests/AuthCore.Domain.UnitTests/AuthCore.Domain.UnitTests.csproj
```

---

## F02 - Persistencia PostgreSQL de Sessoes

### Objetivo

Persistir a sessao principal no banco como fonte da verdade.

### Tasks de Infrastructure

- [ ] Criar migracao versionada para tabela `auth_sessions`.
- [ ] Adicionar colunas para identificador interno, identificador publico nao secreto, usuario, hash do identificador opaco, status, security stamp, device, IP, user-agent, datas e motivo de revogacao.
- [ ] Criar indice unico para o identificador publico de sessao.
- [ ] Criar indice unico em `session_identifier_hash`.
- [ ] Criar indices por `user_id + status`, `expires_at_utc`, `status + expires_at_utc` e `user_id + created_at_utc`.
- [ ] Implementar repositorio Npgsql de escrita para adicionar e atualizar sessao.
- [ ] Implementar consulta por hash do identificador opaco.
- [ ] Implementar listagem de sessoes por usuario.
- [ ] Implementar revogacao de sessoes ativas por usuario.
- [ ] Materializar `Session` usando factory de restauracao do dominio.
- [ ] Manter classes concretas de infraestrutura como `internal`.
- [ ] Registrar repositorios e servicos no `InfrastructureDependencyInjection`.

### Tasks de Testes

- [ ] Adicionar teste de integracao de persistencia para criar e ler sessao.
- [ ] Adicionar teste de integracao de revogacao de sessao.
- [ ] Adicionar teste de integracao de listagem por usuario.
- [ ] Adicionar teste de materializacao preservando status, datas e motivo.

### Criterios de Aceite

- Redis nao e usado como fonte unica da sessao longa.
- Sessao persistida e restaurada preserva invariantes do dominio.
- `sid` opaco nunca e retornado em body, listagem ou path.
- Responses e `DELETE api/auth/session/sessions/{...}` usam apenas identificador publico nao secreto.
- SQL e explicito e usa Npgsql.
- Migracao segue padrao `Version000000X`.

### Validacao

```bash
dotnet test tests/AuthCore.Domain.UnitTests/AuthCore.Domain.UnitTests.csproj
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
```

Se PostgreSQL nao estiver disponivel, registrar a limitacao da validacao de integracao.

---

## F03 - Access Token Curto Vinculado a Sessao

### Objetivo

Evoluir a emissao de access token para incluir `sid` e manter expiracao curta no fluxo browser/session.

### Tasks de Domain/Application

- [ ] Evoluir contrato de emissao de access token para receber usuario e sessao ou um modelo de entrada equivalente.
- [ ] Garantir que a aplicacao solicite emissao somente apos validar que a sessao pode emitir acesso.
- [ ] Evitar claim com `SecurityStamp` bruto.
- [ ] Usar claim derivada e nao sensivel, como `sst`, apenas se houver necessidade real.

### Tasks de Infrastructure

- [ ] Atualizar emissor JWT para incluir `sid`.
- [ ] Garantir expiracao curta por configuracao, preferencialmente proxima de 5 minutos.
- [ ] Validar `iss` e `aud` por configuracao.
- [ ] Adicionar validacao fail-fast para segredo/chave ausente ou fraco.
- [ ] Enquanto HS256 existir, exigir segredo com no minimo 256 bits de entropia.
- [ ] Registrar plano tecnico para migracao RS256 ou ES256, se nao for implementado agora.

### Tasks de Testes

- [ ] Adicionar testes de emissao contendo `sid`.
- [ ] Adicionar testes de expiracao curta configurada.
- [ ] Adicionar testes de ausencia de `SecurityStamp` bruto em claims.
- [ ] Adicionar testes de bootstrap falhando com segredo ausente, curto, fraco ou abaixo de 256 bits de entropia quando HS256 estiver em uso.

### Criterios de Aceite

- Access token do fluxo browser contem `sid`.
- Token nao expoe `SecurityStamp` bruto.
- Token curto continua separado do refresh token do fluxo token-based.
- Configuracao insegura falha cedo.
- Configuracao JWT insegura deve falhar no bootstrap, sem depender de suporte futuro.

### Validacao

```bash
dotnet test tests/AuthCore.Application.UnitTests/AuthCore.Application.UnitTests.csproj
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
```

---

## F04 - Login Browser/Session com Cookies

### Objetivo

Adaptar o login por sessao para criar sessao duravel, emitir access token curto e gravar cookies seguros, sem retornar JWT no body.

### Tasks de Application

- [ ] Atualizar `LoginSessionUseCase` para criar `Session` duravel.
- [ ] Persistir sessao no repositorio PostgreSQL.
- [ ] Resetar tentativas de login quando necessario, preservando regra atual de `Password`.
- [ ] Emitir access token curto apos criar sessao valida.
- [ ] Retornar resultado contendo dados publicos do usuario, identificador opaco da sessao e access token para a API escrever cookies.
- [ ] Manter falha publica generica para credenciais invalidas.
- [ ] Usar transacao quando senha/tentativas e sessao forem alteradas no mesmo fluxo.

### Tasks de Api

- [ ] Atualizar `SessionAuthController.Login` para gravar cookie `sid`.
- [ ] Gravar cookie de access token curto.
- [ ] Garantir que response de login nao exponha JWT no body.
- [ ] Emitir `XSRF-TOKEN` imprevisivel no login ou no bootstrap autenticado da sessao.
- [ ] Vincular token CSRF a sessao por estado server-side ou assinatura/HMAC.
- [ ] Manter IP e user-agent derivados do contexto HTTP, nao do body.
- [ ] Garantir `ProducesResponseType` coerente com o contrato.
- [ ] Configurar cookies `__Host-*` com `Secure=true`, `Path=/` e sem `Domain`.
- [ ] Se `Domain` for necessario por ambiente, remover prefixo `__Host-`.

### Tasks de Testes

- [ ] Testar login valido criando sessao duravel.
- [ ] Testar cookies emitidos com flags esperadas.
- [ ] Testar que JWT nao aparece no body do login browser/session.
- [ ] Testar emissao do token CSRF no login ou bootstrap autenticado.
- [ ] Testar que token CSRF fica vinculado a sessao.
- [ ] Testar falha generica para credencial invalida.

### Criterios de Aceite

- Login browser/session usa banco como fonte da sessao.
- Browser recebe access token apenas por cookie.
- Browser recebe token CSRF pronto para refresh/logout/revogacao autenticados por cookie.
- Controller permanece fino.
- Contratos HTTP seguem `Request...Json` e `Response...Json`.

### Validacao

```bash
dotnet test tests/AuthCore.Application.UnitTests/AuthCore.Application.UnitTests.csproj
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
```

---

## F05 - Refresh Browser/Session

### Objetivo

Adicionar refresh do access token no fluxo browser/session, validando sessao principal no banco e CSRF obrigatorio.

### Tasks de Application

- [ ] Criar ou adaptar use case de refresh por sessao separado do refresh token-based.
- [ ] Receber identificador opaco da sessao vindo da API.
- [ ] Calcular hash do identificador.
- [ ] Buscar sessao duravel no repositorio.
- [ ] Buscar usuario dono da sessao.
- [ ] Validar `Session.EnsureCanIssueAccessToken(...)`.
- [ ] Validar usuario ainda apto a autenticar.
- [ ] Validar `SecurityStamp` quando implementado.
- [ ] Emitir novo access token curto.
- [ ] Atualizar `LastSeenAtUtc` com throttling.
- [ ] Persistir alteracao somente quando houver touch.

### Tasks de Api

- [ ] Adicionar `POST api/auth/session/refresh` em `SessionAuthController`.
- [ ] Ler `sid` do cookie.
- [ ] Validar CSRF obrigatoriamente.
- [ ] Exigir header CSRF antes de chamar use case com efeitos.
- [ ] Gravar novo cookie de access token.
- [ ] Retornar `204 No Content` ou resposta minima definida.
- [ ] Nao misturar este endpoint com `TokenAuthController.Refresh`.

### Tasks de Testes

- [ ] Testar refresh de sessao ativa.
- [ ] Testar refresh de sessao revogada.
- [ ] Testar refresh de sessao expirada.
- [ ] Testar refresh com `SecurityStamp` divergente.
- [ ] Testar refresh sem CSRF retornando falha esperada.
- [ ] Testar que refresh sem header CSRF nao chama use case com efeitos.
- [ ] Testar que novo cookie de access token e emitido.

### Criterios de Aceite

- Refresh por cookie valida CSRF.
- Refresh depende de sessao ativa no banco.
- Refresh nao usa refresh token do fluxo token-based.
- `LastSeenAtUtc` nao gera escrita excessiva.

### Validacao

```bash
dotnet test tests/AuthCore.Domain.UnitTests/AuthCore.Domain.UnitTests.csproj
dotnet test tests/AuthCore.Application.UnitTests/AuthCore.Application.UnitTests.csproj
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
```

---

## F06 - Logout e Gestao de Sessoes

### Objetivo

Migrar logout, logout-all, listagem e revogacao de sessao especifica para a sessao duravel no banco.

### Tasks de Application

- [ ] Atualizar `LogoutCurrentSessionUseCase` para buscar e revogar sessao duravel.
- [ ] Atualizar `LogoutAllSessionsUseCase` para revogar sessoes ativas no banco.
- [ ] Atualizar `RevokeUserSessionUseCase` para validar ownership antes de revogar.
- [ ] Atualizar `GetUserSessionsUseCase` para listar sessoes duraveis.
- [ ] Invalidar cache Redis opcional por contrato, se existir.
- [ ] Usar motivos de revogacao do dominio.

### Tasks de Api

- [ ] Garantir CSRF em logout.
- [ ] Garantir CSRF em logout-all.
- [ ] Garantir CSRF em revogacao de sessao especifica.
- [ ] Exigir header CSRF antes de chamar use cases com efeitos.
- [ ] Expirar cookies quando a sessao atual for encerrada ou revogada.
- [ ] Expirar ou rotacionar token CSRF no logout e logout-all.
- [ ] Manter responses `204 No Content` para mutacoes sem corpo.
- [ ] Manter response de listagem sem expor dados sensiveis.
- [ ] Garantir que listagem e rota de revogacao usam identificador publico nao secreto, nunca o `sid` opaco.

### Tasks de Testes

- [ ] Testar logout atual impedindo novos refreshes.
- [ ] Testar logout-all invalidando todas as sessoes ativas.
- [ ] Testar revogacao de sessao propria.
- [ ] Testar que usuario nao revoga sessao alheia.
- [ ] Testar listagem com indicacao da sessao atual.
- [ ] Testar expiracao de cookies quando aplicavel.
- [ ] Testar expiracao ou rotacao do token CSRF no logout/logout-all.
- [ ] Testar que listagem nao expoe o `sid` opaco.

### Criterios de Aceite

- Operacoes de gestao de sessao nao dependem apenas de JWT curto.
- Sessao revogada permanece auditavel no banco.
- Rota `DELETE api/auth/session/sessions/{...}` usa identificador publico nao secreto.
- Redis, se usado, e apenas cache invalidado.
- Controllers permanecem finos.

### Validacao

```bash
dotnet test tests/AuthCore.Application.UnitTests/AuthCore.Application.UnitTests.csproj
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
```

---

## F07 - Troca de Senha e Revogacao de Sessoes

### Objetivo

Garantir que troca de senha rotacione `SecurityStamp`, revogue sessoes browser/session e exija novo login.

### Tasks de Domain

- [ ] Garantir comportamento em `User` para rotacionar `SecurityStamp`.
- [ ] Garantir que `Password.Change` preserve invariantes atuais.

### Tasks de Application

- [ ] Atualizar `ChangePasswordUseCase` para carregar usuario mutavel quando necessario.
- [ ] Alterar senha.
- [ ] Rotacionar `SecurityStamp` do usuario.
- [ ] Revogar sessoes ativas com motivo `PasswordChanged`.
- [ ] Revogar refresh tokens token-based existentes, preservando comportamento atual.
- [ ] Executar senha, usuario e sessoes na mesma unidade transacional quando estiverem no mesmo banco/UoW.
- [ ] Se algum artefato ficar fora da transacao, documentar estrategia de compensacao e cobrir falha parcial.
- [ ] Aplicar rollback em falha.

### Tasks de Api

- [ ] Garantir que endpoints sensiveis autenticados por cookie validem sessao ativa e CSRF.
- [ ] Garantir que troca de senha nao aceite identificador de usuario no body quando ele vem da autenticacao.

### Tasks de Testes

- [ ] Testar troca de senha rotacionando `SecurityStamp`.
- [ ] Testar troca de senha revogando sessoes browser.
- [ ] Testar refresh antigo falhando apos troca de senha.
- [ ] Testar refresh token-based antigo mantendo comportamento esperado de revogacao.
- [ ] Testar rollback quando uma etapa transacional falha.
- [ ] Testar falha parcial quando algum artefato nao puder participar da mesma transacao.

### Criterios de Aceite

- Apos troca de senha, sessoes browser antigas nao renovam acesso.
- Usuario precisa autenticar novamente.
- Quando `Password`, `User` e `Session` estiverem no mesmo banco/UoW, transacao unica e obrigatoria.
- Se houver recurso fora da transacao, compensacao deve estar documentada e testada.
- Fluxo token-based continua separado, mas tambem e protegido conforme regra atual.

### Validacao

```bash
dotnet test tests/AuthCore.Domain.UnitTests/AuthCore.Domain.UnitTests.csproj
dotnet test tests/AuthCore.Application.UnitTests/AuthCore.Application.UnitTests.csproj
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
```

---

## F08 - Hardening HTTP, Cookies, CSRF, CORS e Gateway

### Objetivo

Fechar lacunas de seguranca HTTP e garantir que o Gateway trate cookies como detalhe de borda.

### Tasks de Api

- [ ] Revisar `AuthCookieOptions` e garantir suporte a `Secure`, `HttpOnly`, `SameSite`, `Path` e ausencia de `Domain` quando `__Host-*` for usado.
- [ ] Garantir que `XSRF-TOKEN` seja imprevisivel.
- [ ] Validar token CSRF por estado server-side ou assinatura/HMAC vinculada a sessao.
- [ ] Complementar CSRF com `Origin`/`Referer` em operacoes sensiveis quando aplicavel.
- [ ] Revisar CORS para credentials com allowlist explicita.
- [ ] Garantir que nao exista `AllowAnyOrigin()` com `AllowCredentials()`.
- [ ] Revisar atributos de autenticacao para diferenciar session, bearer e rotas sensiveis.

### Tasks de Gateway

- [ ] Validar que requests comuns usam JWT curto.
- [ ] Encaminhar `Authorization: Bearer` internamente quando necessario.
- [ ] Manter `api/auth/session/*` ancorado no AuthCore e na sessao principal.
- [ ] Revisar audiences por servico.

### Tasks de Infrastructure

- [ ] Reforcar validacao de configuracao JWT.
- [ ] Garantir fail-fast no bootstrap para chave/segredo ausente, curto, fraco ou abaixo de 256 bits de entropia em HS256.
- [ ] Registrar plano de migracao para assinatura assimetrica.
- [ ] Garantir que segredos reais nao estejam versionados.

### Tasks de Testes

- [ ] Testar flags de cookies.
- [ ] Testar falha de CSRF em refresh, logout, logout-all e revogacao.
- [ ] Testar CORS sem `AllowAnyOrigin` com credentials.
- [ ] Testar rotas do Gateway relevantes.
- [ ] Testar validacao de audience quando houver cobertura existente.
- [ ] Testar fail-fast de configuracao JWT insegura.

### Criterios de Aceite

- Cookies `__Host-*` cumprem requisitos do prefixo.
- Refresh por cookie exige CSRF.
- Mutacoes por cookie exigem CSRF.
- Configuracao JWT insegura falha no bootstrap.
- Gateway nao transforma cookie em contrato interno dos microservicos.

### Validacao

```bash
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
dotnet test tests/Gateway.IntegrationTests/Gateway.IntegrationTests.csproj
```

---

## F09 - Testes Integrados e Validacao Final

### Objetivo

Validar a refatoracao completa atravessando dominio, aplicacao, API, infraestrutura e Gateway.

### Tasks

- [ ] Executar testes de dominio.
- [ ] Executar testes de aplicacao.
- [ ] Executar testes de integracao do AuthCore.
- [ ] Executar testes de Gateway quando rotas forem alteradas.
- [ ] Revisar Swagger e XML docs publicas dos endpoints alterados.
- [ ] Revisar contratos JSON para garantir que JWT nao aparece no body do fluxo browser/session.
- [ ] Revisar contratos JSON para garantir que `sid` opaco nao aparece no body, listagem ou path.
- [ ] Revisar contratos JSON para garantir que gestao de sessoes usa identificador publico nao secreto.
- [ ] Revisar `git diff` para confirmar que nao houve refactor amplo nao relacionado.
- [ ] Revisar classes de infraestrutura para confirmar `internal`, exceto DI.
- [ ] Revisar que `AuthCore.Application` nao referencia `AuthCore.Infrastructure`.
- [ ] Executar revisao final com `senior-backend-reviewer`.

### Criterios de Aceite

- Todos os criterios globais da SDD foram atendidos.
- Testes relevantes passaram ou limitacoes de ambiente foram registradas.
- Fluxos browser/session e token-based continuam separados.
- Identificador publico de sessao esta separado do `sid` opaco.
- CSRF e emitido, validado, expirado ou rotacionado conforme o ciclo da sessao.
- O documento de SDD continua coerente com a implementacao final.

### Validacao

```bash
dotnet test tests/AuthCore.Domain.UnitTests/AuthCore.Domain.UnitTests.csproj
dotnet test tests/AuthCore.Application.UnitTests/AuthCore.Application.UnitTests.csproj
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
dotnet test tests/Gateway.IntegrationTests/Gateway.IntegrationTests.csproj
```

---

## 3. Dependencias Entre Features

```txt
F00 -> F01
F01 -> F02
F01 -> F03
F02 + F03 -> F04
F04 -> F05
F02 + F05 -> F06
F01 + F02 + F06 -> F07
F04 + F05 + F06 -> F08
F01..F08 -> F09
```

---

## 4. Backlog Resumido por Camada

### Domain

- [ ] Evoluir `Session`.
- [ ] Criar `SessionStatus`.
- [ ] Criar `SessionRevocationReason`.
- [ ] Avaliar `SessionIdentifier`.
- [ ] Adicionar identificador publico nao secreto de sessao.
- [ ] Adicionar `SecurityStamp`.
- [ ] Adicionar contrato de repositorio duravel.
- [ ] Adicionar contrato de hash de identificador.
- [ ] Cobrir invariantes em `AuthCore.Domain.UnitTests`.

### Application

- [ ] Atualizar `LoginSessionUseCase`.
- [ ] Criar ou adaptar refresh por sessao.
- [ ] Atualizar logout atual.
- [ ] Atualizar logout-all.
- [ ] Atualizar revogacao de sessao.
- [ ] Atualizar listagem de sessoes.
- [ ] Atualizar troca de senha.
- [ ] Cobrir orquestracao em `AuthCore.Application.UnitTests`.

### Api

- [ ] Atualizar `SessionAuthController`.
- [ ] Adicionar `POST api/auth/session/refresh`.
- [ ] Atualizar contratos `Request...Json` e `Response...Json` quando necessario.
- [ ] Escrever e remover cookies seguros.
- [ ] Emitir, validar, expirar ou rotacionar token CSRF.
- [ ] Aplicar CSRF em mutacoes por cookie.
- [ ] Atualizar Swagger e `ProducesResponseType`.

### Infrastructure

- [ ] Criar migracao `auth_sessions`.
- [ ] Implementar repositorio Npgsql.
- [ ] Implementar hash de identificador de sessao.
- [ ] Persistir identificador publico nao secreto de sessao.
- [ ] Evoluir emissor JWT.
- [ ] Manter Redis como cache opcional.
- [ ] Revisar configuracoes JWT, cookies, CORS e Redis.

### Gateway

- [ ] Preservar cookie como detalhe de borda.
- [ ] Encaminhar bearer internamente quando necessario.
- [ ] Revisar audiences por servico.
- [ ] Testar rotas impactadas.

---

## 5. Criterios de Pronto por Feature

Uma feature esta pronta quando:

- tasks da feature foram implementadas ou explicitamente descartadas com justificativa;
- testes relevantes foram adicionados ou atualizados;
- menor validacao relevante foi executada;
- responsabilidades de camada foram preservadas;
- XML docs publicas seguem portugues e padrao do projeto;
- nenhum tipo concreto de infraestrutura vazou para API ou Application;
- alteracoes nao relacionadas foram evitadas.

---

## 6. Criterios de Pronto da Refatoracao

A refatoracao completa esta pronta quando:

- sessao browser/session e persistida no banco;
- Redis nao e fonte unica de sessao longa;
- refresh por cookie valida CSRF e sessao ativa;
- listagem e revogacao usam identificador publico nao secreto;
- `sid` opaco nao aparece em body, listagem ou path;
- access token curto contem `sid`;
- JWT nao e retornado no body no fluxo browser/session;
- cookies de autenticacao sao `HttpOnly`, `Secure` e coerentes com `__Host-*`;
- logout atual, logout-all e revogacao por dispositivo usam sessao duravel;
- troca de senha revoga sessoes antigas;
- troca de senha usa transacao unica para senha, usuario e sessoes quando estiverem no mesmo banco/UoW;
- configuracao JWT insegura falha no bootstrap;
- fluxo token-based continua separado;
- Gateway nao espalha detalhes de cookie para microservicos;
- testes de dominio, aplicacao, integracao e Gateway relevantes passam.
