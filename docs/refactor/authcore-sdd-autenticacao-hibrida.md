# AuthCore - Spec-Driven Development da Autenticacao Hibrida

**Tema:** sessao server-side duravel + JWT curto em cookie HttpOnly  
**Base principal:** `docs/refactor/authcore-fluxo-autenticacao-hibrida.md`  
**Documento auxiliar:** `docs/refactor/authcore-modelagem-ddd-autenticacao-hibrida.md`  
**Fontes normativas do projeto:** `docs/agents/architecture-overview.md` e skill `domain-modeling`  
**Data:** 2026-06-01

---

## 1. Objetivo

Esta especificacao transforma a decisao de autenticacao hibrida em um plano de desenvolvimento guiado por requisitos, invariantes, contratos, desenho por camada, etapas de entrega e criterios de aceite.

O objetivo funcional e adotar, para Browser/PWA:

- sessao server-side de longa duracao como fonte da verdade;
- JWT de curta duracao para acesso comum;
- cookies `HttpOnly`, `Secure` e `SameSite` para transporte no browser;
- defesa anti-CSRF em operacoes state-changing autenticadas por cookie;
- banco de dados como fonte duravel da sessao principal;
- Redis apenas para dados efemeros e cache opcional.

Esta especificacao nao substitui os documentos de arquitetura. Quando houver conflito, prevalecem:

1. `docs/agents/architecture-overview.md`;
2. skill `domain-modeling`;
3. padrao recente da camada `AuthCore.Domain`;
4. `authcore-fluxo-autenticacao-hibrida.md`;
5. este documento.

---

## 2. Decisao Arquitetural

### 2.1. Decisao principal

O AuthCore deve usar autenticacao hibrida no fluxo browser/session:

```txt
Sessao server-side revogavel imediatamente
  -> usada para emitir JWT curto
  -> JWT curto transportado por cookie HttpOnly
  -> refresh valida a sessao principal no banco
```

A sessao e a fonte da verdade. O JWT curto e uma credencial de acesso temporaria.

### 2.2. Limite correto da promessa de revogacao

Nao documentar nem implementar o fluxo como "JWT com revogacao imediata absoluta".

Regra correta:

```txt
Ao revogar a sessao principal:
  -> novos JWTs deixam de ser emitidos;
  -> refresh falha;
  -> endpoints sensiveis que validam sessao ativa falham;
  -> JWTs ja emitidos continuam validos ate o exp.
```

Com access token de aproximadamente 5 minutos, a janela residual de risco e limitada.

### 2.3. Decisao sobre armazenamento

```txt
Banco de dados
  -> fonte da verdade de sessoes longas, revogacao, auditoria e historico

Redis
  -> codigos temporarios, OTP/MFA, rate limit, cache opcional de sessao ativa
```

Redis nao deve ser a unica fonte da sessao principal em producao.

---

## 3. Escopo

### 3.1. Dentro do escopo

- Evoluir o fluxo `api/auth/session/...` para sessao principal persistida em banco.
- Emitir access token curto vinculado a sessao.
- Transportar session id e access token por cookies seguros no fluxo browser.
- Validar a sessao principal no refresh e em operacoes sensiveis.
- Listar, revogar uma sessao e revogar todas as sessoes do usuario autenticado.
- Revogar sessoes ativas em troca de senha.
- Aplicar protecao anti-CSRF em mutacoes autenticadas por cookie.
- Manter fluxo token-based separado para API/mobile/integracoes.
- Fortalecer configuracao de assinatura JWT e audiences.

### 3.2. Fora do escopo inicial

- OAuth 2.0/OIDC completo.
- MFA/passkeys.
- Autorizacao granular por permissions.
- Convite administrativo multitenant.
- Reescrita completa do modelo `User` para `Account`.
- Tornar Gateway responsavel por regra de dominio.

---

## 4. Linguagem e Nomes do Modelo

O documento auxiliar sugere `IdentityAccess`, `Account` e `AuthSession`. Esses conceitos sao uteis, mas a base atual do AuthCore ja possui linguagem consolidada:

- `Users/User` representa a conta de usuario autenticavel;
- `Users/Email` e `Passports/Password` protegem credenciais e identidade;
- `Passports/Session` representa a sessao autenticada por cookie;
- `Passports/RefreshToken` representa o fluxo token-based com refresh token;
- `Security/Tokens` contem contratos de emissao de access token.

Para respeitar o padrao atual, a implementacao deve evoluir os nomes existentes em vez de criar um contexto paralelo chamado `IdentityAccess`.

Decisao de nomenclatura:

```txt
Manter:
  User
  Password
  Session
  RefreshToken

Adicionar ou evoluir:
  SessionIdentifier
  SessionStatus
  SessionRevocationReason
  DeviceInfo
  SecurityStamp
```

Se futuramente houver renomeacao de `User` para `Account`, ela deve ser especificada como refatoracao propria, com compatibilidade e migracao explicitas.

---

## 5. Requisitos Funcionais

### RF01 - Login por sessao browser

Dado um usuario com e-mail verificado, ativo e senha valida, o sistema deve:

1. autenticar as credenciais;
2. criar uma sessao principal persistida no banco;
3. registrar metadados de dispositivo, IP e user-agent;
4. emitir JWT curto contendo `sub`, `sid`, `jti`, `iss`, `aud`, `iat`, `exp` e, quando necessario, uma versao nao sensivel do stamp de seguranca;
5. gravar cookie da sessao e cookie do access token;
6. retornar somente dados publicos do usuario, sem expor o JWT no body.

### RF02 - Falha publica generica de login

Falhas de credencial devem retornar mensagem publica generica unica, sem revelar se:

- o e-mail existe;
- a senha esta incorreta;
- a conta possui senha cadastrada;
- houve tentativa anterior.

Estados que bloqueiam autenticacao podem continuar usando falha previsivel quando isso ja for contrato exposto, mas a evolucao recomendada e reduzir enumeracao no fluxo publico.

### RF03 - Refresh de access token por sessao

Dado um cookie de sessao valido, o sistema deve:

1. validar CSRF;
2. localizar a sessao principal no banco pelo identificador opaco ou hash do identificador;
3. validar se a sessao esta ativa;
4. validar se a sessao nao expirou;
5. validar se o usuario ainda pode autenticar;
6. validar `SecurityStamp`, quando implementado;
7. emitir novo JWT curto;
8. atualizar `LastSeenAtUtc` com throttling;
9. gravar novo cookie de access token.

### RF04 - Logout da sessao atual

Ao solicitar logout autenticado por cookie, o sistema deve:

1. validar CSRF;
2. revogar a sessao atual no banco;
3. invalidar cache opcional no Redis;
4. expirar cookies de sessao e access token;
5. impedir novos refreshes daquela sessao.

### RF05 - Logout de todas as sessoes

Ao solicitar logout global, o sistema deve:

1. validar CSRF;
2. revogar todas as sessoes ativas do usuario autenticado;
3. invalidar caches opcionais;
4. expirar os cookies do request atual;
5. manter historico de auditoria no banco.

### RF06 - Revogar sessao especifica

O usuario autenticado deve conseguir revogar uma sessao propria por identificador publico seguro.

Regras:

- nao permitir revogar sessao de outro usuario;
- validar CSRF;
- se a sessao revogada for a atual, expirar cookies no response;
- retornar `404` quando a sessao alvo nao pertencer ao usuario ou nao existir, conforme padrao atual de privacidade.

### RF07 - Listar sessoes do usuario

O usuario autenticado deve conseguir listar suas sessoes ativas ou recentes com:

- identificador publico;
- indicacao de sessao atual;
- data de criacao;
- data de expiracao;
- ultimo uso;
- IP;
- user-agent ou device name normalizado;
- status.

### RF08 - Troca de senha revoga sessoes

Ao trocar a senha com sucesso, o sistema deve:

1. alterar a senha no dominio;
2. rotacionar `SecurityStamp`, quando implementado;
3. revogar sessoes ativas de browser;
4. exigir novo login;
5. realizar tudo dentro da unidade transacional aplicavel.

### RF09 - Fluxo token-based separado

O fluxo de API/mobile/integracoes deve permanecer separado:

```txt
api/auth/token/login
api/auth/token/refresh
api/auth/token/logout
```

Ele nao deve depender de cookie HttpOnly nem misturar semantica de CSRF do browser.

### RF10 - Rotas sensiveis validam sessao ativa

Operacoes sensiveis autenticadas por cookie devem validar:

- JWT curto ou identidade autenticada;
- sessao principal ativa;
- CSRF;
- usuario ainda apto a autenticar.

Exemplos:

- trocar senha;
- excluir conta;
- revogar sessoes;
- alterar e-mail;
- alterar credencial ou configuracao de seguranca.

---

## 6. Requisitos Nao Funcionais

### RNF01 - Separacao de camadas

Preservar fluxo:

```txt
AuthCore.Api -> AuthCore.Application
AuthCore.Api -> AuthCore.Infrastructure
AuthCore.Application -> AuthCore.Domain
AuthCore.Infrastructure -> AuthCore.Domain
```

`AuthCore.Application` nao deve depender de `AuthCore.Infrastructure`.

### RNF02 - Dominio forte

Invariantes de sessao, usuario, senha e security stamp devem ficar no dominio.

A aplicacao orquestra. A API adapta HTTP. A infraestrutura persiste, emite token e escreve detalhes tecnicos.

### RNF03 - Banco como fonte da verdade

Sessoes principais devem ser persistidas em PostgreSQL com SQL explicito e Npgsql.

Redis pode ser cache, mas cache miss deve poder consultar o banco.

### RNF04 - Baixa carga no banco

Requests comuns devem validar JWT curto sem consultar banco.

Separacao obrigatoria por esquema:

- endpoints `api/auth/session/*` devem usar a sessao principal por `sid` e validar a fonte da verdade quando gerenciam sessao;
- APIs comuns e Gateway podem usar `__Host-auth.access` ou `Authorization: Bearer` para validacao stateless do JWT curto;
- rotas sensiveis devem validar JWT curto e sessao principal ativa;
- listagem, logout, logout-all, refresh e revogacao de sessoes nao devem depender apenas do JWT curto.

Banco deve ser consultado principalmente em:

- login;
- refresh;
- logout;
- listagem e revogacao de sessoes;
- operacoes sensiveis;
- validacao ativa quando politica exigir.

### RNF05 - Seguranca de cookies

Cookies de autenticacao devem usar:

- `HttpOnly`;
- `Secure`;
- `SameSite=Lax` por padrao same-site;
- `SameSite=None; Secure` apenas para cross-site necessario;
- prefixo `__Host-` quando os requisitos do prefixo forem atendidos.

Quando o prefixo `__Host-` for usado, os cookies devem:

- usar `Secure=true`;
- usar `Path=/`;
- nao definir `Domain`.

Se for necessario definir `Domain`, o cookie nao deve usar o prefixo `__Host-`.

### RNF06 - CORS seguro

Quando cookies forem usados:

- habilitar credentials somente para origens explicitas;
- nunca combinar `AllowAnyOrigin()` com `AllowCredentials()`;
- validar `Origin`/`Referer` em operacoes sensiveis quando aplicavel.

### RNF07 - Material criptografico

Curto prazo:

- se HS256 continuar em uso, exigir segredo com pelo menos 256 bits de entropia;
- falhar no bootstrap quando segredo/chave estiver ausente ou invalido;
- nao versionar segredo real.

Medio prazo:

- planejar migracao para RS256 ou ES256;
- publicar/gerenciar chave publica para Gateway e microservicos;
- usar `kid` quando houver rotacao de chaves.

---

## 7. Modelo de Dominio

### 7.1. User

`User` continua representando a conta autenticavel no AuthCore.

Responsabilidades mantidas:

- registrar usuario;
- proteger e-mail por value object;
- controlar status funcional;
- verificar e-mail;
- bloquear ou ativar usuario;
- informar se pode autenticar.

Evolucao recomendada:

- adicionar `SecurityStamp` como value object ou propriedade encapsulada;
- rotacionar `SecurityStamp` em troca de senha, alteracao de e-mail sensivel, MFA futuro ou acao administrativa;
- expor comportamento de dominio para validar autenticacao sem mover regra para use case.

Invariantes:

- usuario ativo deve possuir e-mail verificado;
- usuario bloqueado nao pode autenticar;
- usuario pendente de verificacao nao pode iniciar sessao;
- `UserIdentifier` nao pode ser vazio;
- `SecurityStamp`, quando existir, nao pode ser vazio.

### 7.2. Password

`Password` continua representando a credencial de senha do usuario.

Responsabilidades:

- proteger status da senha;
- controlar tentativas de login;
- bloquear por falhas;
- resetar tentativas apos login valido;
- trocar senha sem expor regra na aplicacao.

Evolucao recomendada:

- quando a senha for alterada com sucesso, coordenar na aplicacao a rotacao do `SecurityStamp` do `User` e a revogacao de sessoes.

### 7.3. Session

`Passports.Session` deve evoluir de sessao operacional em Redis para sessao principal duravel.

Responsabilidades:

- representar login ativo de um usuario em um dispositivo/navegador;
- proteger status da sessao;
- validar se pode emitir access token;
- registrar criacao, expiracao, ultimo uso e revogacao;
- carregar security stamp do usuario no momento do login;
- carregar metadados de dispositivo.

Campos conceituais:

```txt
Id
UserId
SessionIdentifierHash
Status
SecurityStamp
CreatedAtUtc
ExpiresAtUtc
LastSeenAtUtc
RevokedAtUtc
RevocationReason
IpAddress
UserAgent
DeviceName
```

Value objects ou tipos de apoio:

```txt
SessionIdentifier
SessionStatus
SessionRevocationReason
DeviceInfo
SecurityStamp
```

Invariantes:

- identificador da sessao e obrigatorio;
- hash do identificador persistido e obrigatorio;
- usuario dono e obrigatorio;
- expiracao deve ser posterior a criacao;
- ultimo uso nao pode ser anterior a criacao;
- revogacao nao pode ser anterior a criacao;
- sessao revogada nao pode emitir access token;
- sessao expirada nao pode emitir access token;
- security stamp divergente invalida a sessao;
- sessao ja revogada pode ser tratada como idempotente em logout.

Factories e metodos esperados:

```txt
Issue ou Start
Restore
EnsureCanIssueAccessToken
Touch ou MarkAsSeen
Revoke
MarkAsSuspicious, se necessario futuramente
```

### 7.4. SessionIdentifier

Representa o identificador opaco entregue ao browser.

Regras:

- gerar valor criptograficamente seguro;
- nao aceitar valor vazio;
- normalizar entrada;
- preferir persistir hash no banco;
- nunca expor dados sensiveis embutidos.

### 7.5. SecurityStamp

Representa a versao de seguranca do usuario.

Regras:

- deve ser gerado com valor imprevisivel;
- deve mudar quando uma acao invalida sessoes/tokens antigos;
- sessao armazena o stamp vigente no login;
- refresh compara stamp da sessao com stamp atual do usuario.

### 7.6. VerificationChallenge

O documento auxiliar propoe `VerificationChallenge`. Para a base atual, a evolucao deve respeitar `EmailVerification` e os repositorios existentes.

Decisao:

- nao criar aggregate novo enquanto a necessidade nao estiver clara;
- usar Redis para codigos temporarios;
- evoluir para `VerificationChallenge` apenas quando houver regras suficientes de estado, tentativas, expiracao e auditoria que justifiquem aggregate proprio.

---

## 8. Contratos de Dominio

### 8.1. Repositorio de sessao

Criar ou evoluir contrato no dominio para persistencia duravel de sessao.

Nome recomendado, preservando a linguagem atual:

```txt
ISessionRepository
```

Operacoes minimas:

```txt
AddAsync(Session session, CancellationToken cancellationToken)
UpdateAsync(Session session, CancellationToken cancellationToken)
GetByIdentifierHashAsync(string identifierHash, CancellationToken cancellationToken)
GetByPublicIdForUserAsync(Guid userId, string sessionId, CancellationToken cancellationToken)
ListByUserIdAsync(Guid userId, CancellationToken cancellationToken)
RevokeActiveByUserIdAsync(Guid userId, SessionRevocationReason reason, DateTime revokedAtUtc, CancellationToken cancellationToken)
```

Se `ISessionStore` permanecer, ele deve representar cache ou compatibilidade temporaria, nao a fonte da verdade.

### 8.2. Hash de identificador de sessao

Adicionar contrato tecnico abstrato, preferencialmente em `Domain.Security` ou em subpasta coerente:

```txt
ISessionIdentifierHasher
```

Responsabilidade:

- calcular hash deterministico do identificador opaco;
- comparar quando necessario;
- esconder algoritmo concreto da aplicacao.

Implementacao concreta fica na infraestrutura.

### 8.3. Emissao de access token

O contrato atual de token deve evoluir para aceitar sessao, ou receber dados suficientes para incluir `sid` e uma versao nao sensivel do stamp quando isso for necessario para consumidores internos.

Regra:

- Domain define contrato de emissao ou modelo de entrada;
- Infrastructure assina JWT;
- Application solicita emissao;
- Api escreve cookie.

---

## 9. Application

### 9.1. Organizacao

Manter vertical slices em:

```txt
UseCases/Authentication/LoginSession
UseCases/Authentication/RefreshSession
UseCases/Authentication/LogoutCurrentSession
UseCases/Authentication/LogoutAllSessions
UseCases/Authentication/GetUserSessions
UseCases/Authentication/RevokeUserSession
UseCases/Users/ChangePassword
```

Nao criar `AuthService` generico.

### 9.2. LoginSessionUseCase

Responsabilidades:

1. normalizar entrada simples;
2. buscar usuario por e-mail;
3. buscar senha;
4. validar credenciais por contrato de criptografia;
5. delegar invariantes ao dominio;
6. criar `Session`;
7. persistir sessao no banco;
8. emitir access token curto;
9. retornar resultado com dados necessarios para a API gravar cookies.

Transacao:

- iniciar transacao quando alterar senha/tentativas e criar sessao no mesmo fluxo;
- `Commit` antes da API escrever cookies;
- `Rollback` em falha.

### 9.3. RefreshSessionUseCase

Responsabilidades:

1. receber identificador de sessao vindo da API;
2. calcular hash;
3. buscar sessao no banco ou cache com fallback;
4. buscar usuario dono;
5. validar `Session.EnsureCanIssueAccessToken(...)`;
6. emitir novo access token;
7. atualizar `LastSeenAtUtc` com intervalo minimo;
8. salvar alteracao quando houve touch;
9. retornar access token e expiracao para a API.

### 9.4. LogoutCurrentSessionUseCase

Responsabilidades:

- buscar sessao atual;
- revogar no dominio com motivo `UserLogout`;
- persistir alteracao;
- invalidar cache opcional por contrato, se existir.

### 9.5. LogoutAllSessionsUseCase

Responsabilidades:

- revogar sessoes ativas do usuario;
- usar motivo `UserLogout` ou `PasswordChanged`, conforme comando;
- manter regra de ownership na aplicacao e dominio;
- transacionar quando participar de troca de senha.

### 9.6. RevokeUserSessionUseCase

Responsabilidades:

- garantir que a sessao pertence ao usuario autenticado;
- revogar com motivo `UserRevokedDevice`;
- retornar sem expor detalhes de sessao alheia.

### 9.7. ChangePasswordUseCase

Evolucao obrigatoria:

- apos senha alterada com sucesso, rotacionar security stamp do usuario;
- revogar sessoes ativas;
- executar em transacao unica quando possivel.

---

## 10. Api

### 10.1. Controllers

Preservar controllers finos.

`SessionAuthController` continua sendo o ponto principal do fluxo browser/session:

```txt
POST api/auth/session/login
POST api/auth/session/refresh
POST api/auth/session/logout
POST api/auth/session/logout-all
GET  api/auth/session/me
GET  api/auth/session/sessions
DELETE api/auth/session/sessions/{sid}
```

Se `refresh` ainda nao existir nesse controller, adiciona-lo sem misturar com `TokenAuthController`.

### 10.2. Requests e responses

Contratos devem seguir:

```txt
Request...Json
Response...Json
```

Login por sessao:

- request contem e-mail e senha;
- IP e user-agent sao derivados do HTTP;
- response nao contem access token.

Refresh por sessao:

- request pode nao ter body;
- session id vem de cookie;
- response deve ser `204 No Content` ou resposta minima, pois o novo access token vai em cookie.

Logout:

- response `204 No Content`;
- cookies removidos pelo response.

### 10.3. Cookies

Cookies recomendados:

```txt
__Host-auth.sid
__Host-auth.access
XSRF-TOKEN
```

Regras:

- cookie `sid` contem identificador opaco, nao hash;
- banco armazena hash do identificador;
- cookie `access` contem JWT curto;
- cookies de autenticacao sao `HttpOnly`;
- `XSRF-TOKEN` pode ser legivel pelo frontend quando for usado em double-submit/header.
- cookies `__Host-*` devem usar `Secure=true`, `Path=/` e nao definir `Domain`;
- se a aplicacao precisar de `Domain`, deve usar outro prefixo de cookie.

O token CSRF deve ser imprevisivel e validado por estado server-side ou por assinatura/HMAC vinculada a sessao. Validacao de `Origin`/`Referer` deve complementar o token em operacoes sensiveis quando aplicavel.

### 10.4. CSRF

Mutacoes autenticadas por cookie devem chamar validador de CSRF:

- logout;
- logout-all;
- revogar sessao;
- refresh;
- troca de senha quando autenticada por cookie;
- qualquer endpoint de alteracao sensivel.

O refresh por cookie e considerado state-changing porque emite novo cookie de access token e pode atualizar `LastSeenAtUtc`. Qualquer excecao futura precisa ser especificada com justificativa explicita, sem prolongamento de sessao e com validacao forte de `Origin`/`Referer`.

### 10.5. Claims

JWT curto deve incluir:

```txt
sub
sid
jti
iss
aud
iat
exp
sst, quando necessario e sem expor o SecurityStamp bruto
roles, se ja for necessario
```

Evitar claims grandes ou sensiveis.

O `SecurityStamp` completo deve permanecer na sessao persistida e no modelo de dominio. Se microservicos precisarem de uma versao no token, usar claim derivada e nao sensivel, como hash curto ou versao opaca.

---

## 11. Infrastructure

### 11.1. Persistencia PostgreSQL

Adicionar tabela versionada de sessoes principais.

Nome sugerido:

```txt
auth_sessions
```

Colunas conceituais:

```txt
id uuid primary key
user_id uuid not null
session_identifier_hash text not null
status smallint not null
security_stamp text null
device_name text null
user_agent text null
ip_address text null
created_at_utc timestamp not null
expires_at_utc timestamp not null
last_seen_at_utc timestamp null
revoked_at_utc timestamp null
revocation_reason smallint null
```

Indices:

```txt
session_identifier_hash unique
user_id, status
expires_at_utc
status, expires_at_utc
user_id, created_at_utc
```

### 11.2. Repositorios

Implementar repositorios com Npgsql e SQL explicito.

Separacao esperada:

- write repository para criar, atualizar e revogar;
- read repository ou metodo especifico para listagem/projecao de sessoes;
- materializacao por `Session.Restore(...)`.

Classes concretas devem ser `internal`.

### 11.3. Cache Redis opcional

Redis pode guardar snapshot minimo de sessao ativa.

Regras:

- cache nao substitui banco;
- revogacao remove cache;
- cache miss consulta banco;
- cache hit so pode ser usado quando contiver dados suficientes e ainda respeitar expiracao.

### 11.4. JWT

Evoluir emissor JWT para:

- access token curto, aproximadamente 5 minutos por configuracao;
- claim `sid`;
- audience por servico;
- validacao forte de configuracao;
- suporte futuro a chave assimetrica.

### 11.5. Cookies

Escrita de cookie pode ficar na API ou em abstracao tecnica injetada, desde que:

- Domain nao conheca cookies;
- Application nao dependa de ASP.NET Core concreto;
- contratos HTTP nao exponham options da infraestrutura.

---

## 12. Gateway e Microservicos

O Gateway deve tratar cookie como detalhe de borda.

Fluxo recomendado:

```txt
Browser
  -> envia __Host-auth.access nas requests comuns
Gateway
  -> valida JWT curto
  -> encaminha Authorization: Bearer internamente, quando necessario
Microservico
  -> valida bearer ou confia na borda conforme politica definida
```

Rotas sensiveis podem exigir validacao ativa da sessao no AuthCore.

Nao espalhar conhecimento de cookies do browser para todos os microservicos.

Endpoints `api/auth/session/*` continuam ancorados no `sid` e na sessao principal. O JWT curto serve para requests comuns e propagacao interna, nao como unica credencial para operacoes de gestao da propria sessao.

---

## 13. Plano de Implementacao

### Fase 1 - Dominio de sessao duravel

Entregas:

- evoluir `Passports.Session`;
- adicionar `SessionStatus`;
- adicionar `SessionRevocationReason`;
- adicionar `SessionIdentifier`, se fizer sentido como value object;
- adicionar `SecurityStamp` em `User` ou value object dedicado;
- adicionar contrato de repositorio duravel de sessao;
- adicionar testes de dominio.

Criterios de aceite:

- dominio valida criacao, restauracao, expiracao, revogacao e emissao de acesso;
- sessao revogada/expirada nao pode emitir access token;
- security stamp divergente invalida sessao;
- testes `AuthCore.Domain.UnitTests` cobrem invariantes principais.

### Fase 2 - Persistencia PostgreSQL

Entregas:

- migracao `Version00000XX` para `auth_sessions`;
- repositorio Npgsql de escrita;
- leitura/listagem por usuario;
- materializacao por `Session.Restore(...)`;
- invalidacao de cache Redis opcional, se mantido.

Criterios de aceite:

- sessao criada e lida do banco preserva estado;
- revogacao persiste motivo e data;
- indices essenciais existem;
- classes concretas da infraestrutura permanecem `internal`.

### Fase 3 - Login por sessao com access token curto

Entregas:

- `LoginSessionUseCase` cria sessao no banco;
- access token inclui `sid`;
- API grava cookie `sid` e cookie `access`;
- response de login nao expoe JWT no body.

Criterios de aceite:

- login valido cria sessao duravel;
- cookies corretos sao emitidos;
- falha de credenciais usa mensagem generica;
- testes de aplicacao cobrem orquestracao.

### Fase 4 - Refresh por sessao

Entregas:

- endpoint de refresh no fluxo session;
- use case valida sessao principal no banco;
- novo access token curto e gravado em cookie;
- `LastSeenAtUtc` atualiza com throttling.

Criterios de aceite:

- refresh de sessao ativa emite novo access token;
- refresh de sessao revogada falha;
- refresh de sessao expirada falha;
- refresh com security stamp divergente falha.

### Fase 5 - Logout e gestao de sessoes

Entregas:

- logout atual revoga sessao no banco;
- logout-all revoga todas as sessoes ativas;
- revogacao de sessao especifica valida ownership;
- listagem usa dados duraveis.

Criterios de aceite:

- logout impede novos refreshes;
- logout-all invalida todas as sessoes;
- usuario nao consegue revogar sessao alheia;
- cookies sao expirados quando aplicavel.

### Fase 6 - Troca de senha e security stamp

Entregas:

- `ChangePasswordUseCase` rotaciona security stamp;
- sessoes ativas sao revogadas;
- operacao e transacional.

Criterios de aceite:

- apos troca de senha, refresh antigo falha;
- usuario precisa autenticar novamente;
- testes de aplicacao validam commit/rollback.

### Fase 7 - Hardening HTTP e Gateway

Entregas:

- CORS com allowlist e credentials;
- CSRF consistente em mutacoes por cookie;
- audiences por servico;
- configuracao JWT com fail-fast;
- plano ou implementacao de assinatura assimetrica.

Criterios de aceite:

- nao existe `AllowAnyOrigin` com credentials;
- mutacoes por cookie validam CSRF;
- segredo/chave invalida falha no bootstrap;
- Gateway preserva separacao entre cookie externo e bearer interno.

---

## 14. Estrategia de Testes

### 14.1. Domain.UnitTests

Cobrir:

- `Session.Issue` com dados validos;
- `Session.Restore` com estado persistido;
- sessao expirada nao emite access token;
- sessao revogada nao emite access token;
- security stamp divergente invalida emissao;
- `Revoke` idempotente quando aplicavel;
- `Touch` respeita data valida;
- `User` rotaciona `SecurityStamp` quando implementado.

Nomes no padrao:

```txt
Issue_WhenDataIsValid_ShouldCreateActiveSession
EnsureCanIssueAccessToken_WhenSessionIsRevoked_ShouldThrowDomainException
Revoke_WhenSessionIsActive_ShouldSetRevocationData
```

### 14.2. Application.UnitTests

Cobrir:

- login cria sessao e emite access token;
- login com credencial invalida nao cria sessao;
- refresh valida sessao e atualiza access token;
- logout revoga sessao;
- logout-all revoga sessoes;
- troca de senha revoga sessoes e commita transacao;
- rollback ocorre em falha transacional.

### 14.3. IntegrationTests

Cobrir:

- contrato HTTP de login por cookie;
- cookies emitidos com flags esperadas;
- refresh por cookie;
- logout remove cookies;
- CSRF em mutacoes;
- CSRF obrigatorio no refresh por cookie;
- persistencia PostgreSQL da sessao;
- composicao de DI.

### 14.4. ArchitectureTests

Se o projeto for ativado, cobrir:

- `Application` nao referencia `Infrastructure`;
- classes concretas de `Infrastructure` sao `internal`, exceto DI;
- controllers nao expoem tipos de infraestrutura.

---

## 15. Criterios Globais de Aceite

A refatoracao so deve ser considerada concluida quando:

1. sessao principal do fluxo browser e persistida no banco;
2. Redis nao e fonte unica de sessao longa;
3. refresh depende de sessao ativa no banco;
4. JWT curto contem `sid`;
5. JWT nao e retornado no body no fluxo browser;
6. cookies de autenticacao sao `HttpOnly` e `Secure`;
7. mutacoes por cookie validam CSRF;
8. refresh por cookie valida CSRF;
9. cookies `__Host-*` usam `Secure=true`, `Path=/` e nao definem `Domain`;
10. logout atual, logout-all e revogacao por dispositivo funcionam com sessao duravel;
11. troca de senha revoga sessoes antigas;
12. fluxo token-based continua separado;
13. regras centrais estao no dominio;
14. Application apenas orquestra;
15. Api permanece fina;
16. Infrastructure usa Npgsql e SQL explicito;
17. testes relevantes de dominio, aplicacao e integracao passam.

---

## 16. Riscos e Mitigacoes

### Risco 1 - Reescrever o dominio como `Account`

Mitigacao:

- manter `User` como aggregate atual;
- tratar `Account` como conceito auxiliar, nao como renomeacao imediata.

### Risco 2 - Redis continuar como fonte da verdade

Mitigacao:

- separar contrato de repositorio duravel e contrato de cache;
- refresh deve conseguir validar pelo banco.

### Risco 3 - Controller absorver regra de negocio

Mitigacao:

- controller apenas le cookies, valida borda HTTP, chama use case e escreve response/cookies;
- regras de sessao ficam em `Session`;
- orquestracao fica em use case.

### Risco 4 - Janela residual do JWT curto ser mal comunicada

Mitigacao:

- documentar explicitamente que JWT ja emitido vale ate `exp`;
- usar expiracao curta;
- validar sessao ativa em rotas sensiveis.

### Risco 5 - Quebra de consumidores existentes

Mitigacao:

- preservar rotas atuais quando possivel;
- manter fluxo token-based separado;
- introduzir mudancas de contrato como evolucao explicita.

---

## 17. Checklist de Implementacao por Camada

### Domain

- `Session` modela status, revogacao, expiracao e security stamp.
- `User` modela capacidade de autenticar e security stamp.
- Value objects protegem identificadores e stamps quando agregam clareza.
- XML docs publicas em portugues.
- Testes de invariantes adicionados.

### Application

- Use cases por vertical slice.
- `ArgumentNullException.ThrowIfNull` aplicado.
- Repositorios e transacao orquestrados na aplicacao.
- Nenhuma regra central reimplementada fora do dominio.

### Api

- Controllers finos.
- Contratos `Request...Json` e `Response...Json`.
- Cookies escritos/removidos na borda.
- CSRF em mutacoes por cookie.
- Swagger/ProducesResponseType atualizados.

### Infrastructure

- Npgsql e SQL explicito.
- Migracoes versionadas.
- Materializacao por factories do dominio.
- Classes concretas `internal`.
- Redis usado como cache/efemero, nao fonte unica.

### Tests

- Domain para invariantes.
- Application para orquestracao.
- Integration para HTTP, DI, cookies e persistencia.

---

## 18. Sequencia Recomendada de Execucao

1. Implementar dominio de sessao duravel e testes.
2. Implementar migracao e repositorio PostgreSQL.
3. Adaptar login por sessao para persistir no banco e emitir access token curto.
4. Adicionar refresh por sessao.
5. Adaptar logout, logout-all, listagem e revogacao de sessoes.
6. Integrar security stamp com troca de senha.
7. Revisar cookies, CSRF, CORS e Gateway.
8. Fortalecer configuracao de JWT e registrar plano de assinatura assimetrica.

Essa ordem reduz risco porque primeiro estabiliza o dominio e a fonte da verdade, depois adapta a borda HTTP e os detalhes tecnicos.

---

## 19. Definicao de Pronto

Uma etapa esta pronta quando:

- os arquivos alterados seguem as regras de camada;
- XML docs publicas estao em portugues;
- testes relevantes da etapa foram adicionados ou atualizados;
- a menor validacao relevante foi executada;
- nao ha dependencia nova violando Clean Architecture;
- a implementacao foi revisada com foco em seguranca, regressao e aderencia ao dominio.

Para a refatoracao completa, executar ao menos:

```bash
dotnet test tests/AuthCore.Domain.UnitTests/AuthCore.Domain.UnitTests.csproj
dotnet test tests/AuthCore.Application.UnitTests/AuthCore.Application.UnitTests.csproj
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
```

Quando PostgreSQL nao estiver disponivel, registrar explicitamente que testes de integracao de persistencia ficaram parcialmente condicionados ao ambiente.
