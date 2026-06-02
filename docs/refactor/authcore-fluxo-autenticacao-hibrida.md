# AuthCore — Análise do Fluxo de Autenticação Híbrida

**Tema:** Sessões Server-Side + JWT de curta duração  
**Contexto:** Boilerplate AuthCore em Clean Architecture + DDD, com Gateway/Ocelot, microserviços e suporte para Browser/PWA, APIs, mobile e integrações.  
**Data:** 2026-05-28

---

## 1. Objetivo da decisão

A ideia analisada é adotar uma autenticação híbrida, combinando:

1. **Sessão server-side de longa duração** como fonte principal de controle.
2. **JWT de curta duração** como token de acesso usado na maioria das requisições.
3. **Cookies HttpOnly** para transportar os identificadores/tokens no navegador, sem expor o JWT diretamente ao JavaScript do frontend.

O objetivo é combinar:

- controle de revogação e gestão de sessões;
- boa performance nas requisições comuns;
- compatibilidade com Gateway e microserviços;
- menor exposição do token no client-side;
- possibilidade de bloquear novas emissões de JWT quando a sessão principal for revogada.

---

## 2. Resumo

Essa abordagem não deve ser vendida como “JWT com revogação imediata absoluta”, porque um JWT já emitido continua válido até expirar. O correto é descrever como:

> Sessão server-side revogável imediatamente, usada para emitir JWTs curtos. Ao revogar a sessão, novos JWTs deixam de ser emitidos, e os JWTs já emitidos expiram rapidamente.

Com JWTs de 5 minutos, o risco residual após revogação é limitado. Para operações muito sensíveis, ainda é possível exigir validação ativa da sessão no Gateway ou no AuthCore.

---

## 3. Desenho conceitual

```txt
Login
  -> valida credenciais
  -> cria sessão server-side
  -> emite JWT curto
  -> grava cookies HttpOnly/Secure/SameSite

Request comum
  -> browser envia cookies automaticamente
  -> Gateway/API valida JWT curto
  -> microserviço recebe identidade autenticada

Refresh/Renovação
  -> backend valida sessão principal
  -> se sessão ativa, emite novo JWT curto
  -> se sessão revogada/expirada, nega renovação

Logout
  -> revoga sessão principal
  -> remove cookies
  -> novos JWTs deixam de ser emitidos
```

---

## 4. Por que essa abordagem é interessante

### 4.1. O que a sessão server-side entrega

A sessão server-side representa a fonte da verdade da autenticação do usuário.

Ela permite:

- logout imediato;
- logout de todos os dispositivos;
- revogação de uma sessão específica;
- bloqueio de usuário sem esperar o token expirar;
- rastreamento de dispositivo, IP, user-agent e última atividade;
- auditoria de sessões;
- invalidação por alteração de senha, MFA ou `security_stamp`;
- gestão de risco e segurança por sessão.

### 4.2. O que o JWT curto entrega

O JWT curto entrega performance e simplicidade operacional nas requisições comuns.

Ele permite:

- validação stateless no Gateway ou na API;
- propagação simples da identidade para microserviços;
- redução de chamadas ao Redis/banco em cada request;
- compatibilidade com o modelo `Authorization: Bearer` internamente;
- menor janela de risco, se tiver expiração curta.

### 4.3. Sessões diferentes para o mesmo usuário com dispositivos diferentes

Essa abordagem permite gerar uma sessão diferente para cada login/dispositivo/navegador.

Ou seja: 
```txt
Usuário Bruno
  -> Chrome no notebook
      SessionId: S1
      JWT curto vinculado ao sid S1

  -> Safari/Chrome no celular
      SessionId: S2
      JWT curto vinculado ao sid S2
```

Mesmo sendo o mesmo usuário, cada contexto autenticado tem sua própria sessão.

### Por que isso é melhor?

Porque você ganha controle granular:

```txt
Logout apenas do Chrome
  -> revoga S1
  -> S2 continua ativa no celular

Logout de todos os dispositivos
  -> revoga S1, S2, S3...

Usuário alterou senha
  -> revoga todas as sessões ativas

Sessão suspeita
  -> revoga apenas aquela sessão
```

Esse é um dos maiores ganhos da sessão server-side.

### Como ficaria no banco?
```txt
UserSession
  Id
  UserId
  SessionTokenHash
  Status
  CreatedAt
  ExpiresAt
  RevokedAt
  LastSeenAt
  IpAddress
  UserAgent
  DeviceName
  SecurityStamp
```
Exemplo
```txt
Id: S1
UserId: 123
DeviceName: Chrome - Windows
Status: Active
ExpiresAt: 2026-06-29

Id: S2
UserId: 123
DeviceName: Chrome - Android
Status: Active
ExpiresAt: 2026-06-29
```
### E o JWT?

Cada JWT curto deveria carregar o sid da sessão que o gerou:

```txt
sub: user_id
sid: session_id
jti: token_id
exp: agora + 5 minutos
```

Assim, quando o JWT expirar e o frontend pedir renovação, o backend sabe qual sessão validar.

```txt
Refresh/Renew
  -> lê cookie sid
  -> busca sessão S1
  -> se ativa, gera novo JWT com sid S1
```

---

## 5. Ponto crítico: revogação

A revogação funciona em dois níveis.

### 5.1. Revogação da sessão principal

Quando a sessão principal é revogada:

- o usuário não deve conseguir renovar o access token;
- novos JWTs não devem ser emitidos;
- refresh, troca de sessão e endpoints autenticados por sessão devem falhar.

### 5.2. JWTs já emitidos

JWTs já emitidos continuam válidos até o `exp`.

Exemplo:

```txt
12:00:00 -> JWT emitido com expiração de 5 minutos
12:02:00 -> sessão revogada
12:05:00 -> JWT expira naturalmente
```

Entre 12:02 e 12:05 ainda existe uma janela residual.

Para a maioria dos sistemas, 5 minutos é um trade-off aceitável. Para operações críticas, pode-se exigir validação adicional da sessão.

---

## 6. Estratégias de validação por criticidade

Nem toda requisição precisa ter o mesmo custo de segurança.

### 6.1. Requisições comuns

Exemplos:

- consultar perfil;
- listar dados comuns;
- acessar telas autenticadas;
- consultar permissões básicas.

Estratégia recomendada:

```txt
Validar apenas o JWT curto.
```

Vantagem:

- alta performance;
- baixa latência;
- menor dependência de Redis/banco em cada request.

### 6.2. Requisições sensíveis

Exemplos:

- alterar senha;
- alterar e-mail;
- remover conta;
- alterar permissões;
- trocar dados de pagamento;
- gerar chaves de integração;
- revogar sessões.

Estratégia recomendada:

```txt
Validar JWT curto + validar sessão ativa.
```

Nesse caso, a API ou o Gateway consulta a sessão principal antes de executar a operação.

---

## 7. Cookies recomendados

Para Browser/PWA, uma sugestão inicial seria:

```txt
__Host-auth.sid       -> identificador opaco da sessão principal
__Host-auth.access    -> JWT curto de acesso
XSRF-TOKEN            -> token anti-CSRF, quando necessário
```

### 7.1. Cookie da sessão principal

```txt
Nome: __Host-auth.sid
Conteúdo: identificador opaco da sessão
HttpOnly: true
Secure: true
SameSite: Lax
TTL: longo, por exemplo 7, 15 ou 30 dias
```

Esse cookie não deve conter dados sensíveis em claro. Ele deve apontar para a sessão persistida no servidor.

### 7.2. Cookie do access token

```txt
Nome: __Host-auth.access
Conteúdo: JWT curto
HttpOnly: true
Secure: true
SameSite: Lax
TTL: curto, por exemplo 5 minutos
```

Esse cookie permite que o browser envie automaticamente o JWT sem que o frontend precise acessar o token via JavaScript.

### 7.3. Cookie anti-CSRF

```txt
Nome: XSRF-TOKEN
Conteúdo: token anti-CSRF
HttpOnly: false, se o frontend precisar ler e reenviar em header
Secure: true
SameSite: Lax
```

Esse cookie é diferente dos cookies de autenticação. Em muitos desenhos, o frontend lê esse valor e o envia em um header, por exemplo:

```txt
X-XSRF-TOKEN: <valor>
```

O backend compara o valor recebido no header com o valor esperado.

---

## 8. HttpOnly: o que resolve e o que não resolve

`HttpOnly` impede que JavaScript acesse o cookie via `document.cookie`.

Isso reduz bastante o risco de roubo do token por XSS simples, mas não significa que o cookie desaparece do navegador. O cookie ainda:

- aparece nas ferramentas de desenvolvedor;
- é enviado automaticamente em requisições compatíveis;
- pode ser usado pelo navegador sem que o frontend leia seu valor;
- não elimina a necessidade de prevenir XSS;
- não elimina a necessidade de CSRF quando a autenticação é baseada em cookies.

Frase recomendada para documentação interna:

> O JWT não fica acessível ao JavaScript da aplicação, mas continua sendo enviado automaticamente pelo navegador nas requisições permitidas pelo domínio, SameSite, CORS e política de credentials.

---

## 9. CSRF: cuidado obrigatório quando usa cookie

Quando o token de autenticação está em cookie, o navegador tende a enviá-lo automaticamente.

Isso melhora a proteção contra roubo via JavaScript, mas reintroduz o risco de CSRF.

Comparação:

```txt
JWT em Authorization Header
  -> menor risco de CSRF
  -> maior cuidado com armazenamento no frontend

JWT em Cookie HttpOnly
  -> menor exposição ao JavaScript
  -> precisa de proteção anti-CSRF
```

Recomendações:

1. Usar `SameSite` corretamente.
2. Validar `Origin` e/ou `Referer` em operações sensíveis.
3. Usar token anti-CSRF em operações que alteram estado.
4. Considerar Fetch Metadata headers como defesa adicional.
5. Não confiar apenas em CORS como mecanismo de segurança contra CSRF.

---

## 10. SameSite: Lax, Strict ou None

### 10.1. SameSite=Lax

`SameSite=Lax` costuma ser uma boa opção quando frontend e backend estão no mesmo site, por exemplo:

```txt
app.meudominio.com
api.meudominio.com
```

Nesse cenário existe diferença de origem, mas pode continuar sendo o mesmo site.

Vantagens:

- reduz risco de CSRF;
- funciona bem em navegação comum;
- evita envio de cookies em vários cenários cross-site perigosos.

Limitação:

- pode não funcionar para frontend e backend em sites realmente diferentes.

### 10.2. SameSite=None; Secure

Necessário quando o frontend e backend estão em sites diferentes e o cookie precisa ser enviado em chamadas cross-site, por exemplo:

```txt
frontend.vercel.app
api.meudominio.com
```

Nesse caso, normalmente será necessário:

```txt
SameSite=None; Secure
```

Trade-off:

- permite cenário cross-site;
- exige HTTPS;
- aumenta a importância de CSRF token, validação de origem e allowlist de CORS.

### 10.3. SameSite=Strict

Mais restritivo.

Pode ser útil para cookies muito sensíveis, mas costuma gerar fricção em fluxos reais, como abertura de links externos, redirecionamentos e integrações.

---

## 11. CORS com credentials

Quando o frontend precisa enviar cookies para o backend, o frontend precisa habilitar envio de credenciais:

```js
fetch("https://api.meudominio.com/auth/me", {
  credentials: "include"
});
```

Ou, em algumas bibliotecas:

```js
withCredentials = true
```

No backend ASP.NET Core, é necessário permitir credenciais e restringir origens explicitamente.

Exemplo conceitual:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("BrowserApp", policy =>
    {
        policy
            .WithOrigins(
                "https://app.meudominio.com",
                "https://admin.meudominio.com")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
```

Evitar:

```csharp
.AllowAnyOrigin()
.AllowCredentials()
```

Essa combinação é insegura e não deve ser usada.

---

## 12. Onde o Gateway entra

Para microserviços, a recomendação é manter os serviços internos trabalhando com o modelo padrão de JWT Bearer.

Fluxo recomendado:

```txt
Browser/PWA
  -> envia cookie HttpOnly com JWT curto
  -> Gateway lê/valida o JWT
  -> Gateway encaminha internamente como Authorization: Bearer <jwt>
  -> microserviço valida JWT ou confia na camada de borda conforme o modelo adotado
```

Isso evita que todos os microserviços precisem conhecer detalhes de cookie de navegador.

O cookie é detalhe de borda. Internamente, o contrato de autenticação pode continuar sendo bearer token.

---

## 13. Sessão longa no Redis ou no banco?

A dúvida principal é: se a sessão principal tem vida longa, vale a pena tirá-la do Redis e persistir em banco?

Minha recomendação para o AuthCore é:

> Persistir a sessão principal no banco como fonte da verdade e usar Redis apenas para dados efêmeros e, se necessário, cache de sessão ativa.

Essa decisão é mais alinhada com segurança, auditoria e consistência de longo prazo.

---

## 14. Redis para sessão principal: vantagens e problemas

### 14.1. Vantagens de manter sessão no Redis

Redis é excelente para:

- leitura muito rápida;
- TTL nativo;
- revogação rápida;
- baixa latência;
- workloads efêmeros;
- dados que podem sumir sem comprometer a integridade histórica do sistema.

Se a sessão for puramente operacional e descartável, Redis é uma opção muito boa.

### 14.2. Problemas de usar Redis como única fonte da sessão longa

Para sessão de longa duração, Redis como única fonte pode ser problemático por alguns motivos:

1. **Durabilidade menor que banco relacional/documental tradicional**  
   Redis pode persistir dados com RDB/AOF, mas o trade-off entre performance e durabilidade precisa ser configurado conscientemente.

2. **Eviction por pressão de memória**  
   Se `maxmemory` e política de eviction estiverem configurados de forma inadequada, chaves podem ser removidas para liberar memória.

3. **Custo de memória**  
   Sessões longas de muitos usuários ocupam RAM por muito tempo.

4. **Auditoria limitada**  
   Sessões são dados de segurança. É comum querer histórico de criação, revogação, IP, user-agent, motivo de revogação, último uso e device.

5. **Risco operacional**  
   Reinício, flush, configuração errada, falha de persistência ou troca de instância podem invalidar sessões indevidamente.

6. **Menos aderente ao conceito de fonte da verdade**  
   A sessão principal é parte relevante do modelo de segurança. Se ela representa login ativo, dispositivo confiável e revogação, tende a merecer persistência durável.

---

## 15. Banco para sessão principal: vantagens e problemas

### 15.1. Vantagens de persistir sessão no banco

Persistir a sessão principal no banco entrega:

- durabilidade;
- rastreabilidade;
- auditoria;
- histórico de sessões;
- facilidade para listar dispositivos conectados;
- revogação por usuário, sessão, tenant ou dispositivo;
- investigação de incidentes;
- consistência com outras regras de segurança;
- menor risco de perda acidental por eviction.

Esse desenho encaixa melhor com DDD, pois a sessão pode ser tratada como um conceito relevante do domínio de autenticação.

### 15.2. Problemas de usar banco para sessão

O banco também tem trade-offs:

- maior latência que Redis;
- mais carga se consultado em toda requisição;
- precisa de índices bem desenhados;
- precisa de limpeza de sessões expiradas;
- refresh em massa pode gerar carga relevante;
- pode exigir cache para alto volume.

Mas no seu desenho híbrido, o banco não precisa ser consultado em toda request comum. Ele pode ser consultado principalmente no refresh e em operações sensíveis.

---

## 16. Modelo recomendado: banco como fonte da verdade + Redis para efêmeros

O desenho mais equilibrado seria:

```txt
Banco de dados
  -> sessão principal de longa duração
  -> histórico e auditoria
  -> estado de revogação
  -> device/session tracking

Redis
  -> códigos de verificação de curta duração
  -> first access code
  -> forgot password code
  -> email verification code
  -> MFA/OTP temporário
  -> rate limit
  -> lock temporário
  -> cache opcional de sessão ativa
```

Esse modelo usa cada tecnologia onde ela é mais forte.

---

## 17. Redis para códigos de verificação

Redis é excelente para códigos que expiram rapidamente, como:

- código de primeiro acesso;
- código de verificação de e-mail;
- código de recuperação de senha;
- OTP de MFA;
- challenge temporário;
- tentativa de login;
- rate limit por IP/e-mail/usuário.

Exemplo:

```txt
verification:first-access:{email} -> TTL 1 minuto
verification:forgot-password:{email} -> TTL 1 minuto
verification:email:{email} -> TTL 1 minuto
mfa:challenge:{userId}:{challengeId} -> TTL 1 a 5 minutos
rate-limit:login:{ip}:{email} -> TTL curto
```

Para esse tipo de dado, a perda após expiração ou falha não é crítica: o usuário pode solicitar um novo código.

---

## 18. Sessão principal no banco: desenho sugerido

Tabela/coleção conceitual:

```txt
AuthSession
  Id
  UserId
  SessionIdentifier
  Status
  CreatedAt
  ExpiresAt
  LastSeenAt
  RevokedAt
  RevokedReason
  IpAddress
  UserAgent
  DeviceName
  SecurityStamp
  TenantId/ClientId, se aplicável
```

Status possíveis:

```txt
Active
Expired
Revoked
Replaced
Suspicious
```

Índices importantes:

```txt
SessionIdentifier unique
UserId + Status
ExpiresAt
UserId + CreatedAt
Status + ExpiresAt
```

---

## 19. Refresh usando sessão no banco

Fluxo recomendado:

```txt
POST /auth/refresh
  -> lê cookie __Host-auth.sid
  -> busca sessão no banco por SessionIdentifier
  -> valida se sessão existe
  -> valida se está ativa
  -> valida se não expirou
  -> valida SecurityStamp, se aplicável
  -> emite novo JWT curto
  -> atualiza LastSeenAt com estratégia controlada
  -> grava novo cookie __Host-auth.access
```

Para evitar escrita excessiva no banco, `LastSeenAt` pode ser atualizado com throttling.

Exemplo:

```txt
Atualizar LastSeenAt apenas se a última atualização tiver mais de 5 ou 10 minutos.
```

---

## 20. Cache opcional de sessão ativa no Redis

Se o volume crescer e o refresh consultar demais o banco, pode-se usar Redis como cache, sem torná-lo a fonte da verdade.

Fluxo:

```txt
Refresh
  -> tenta consultar sessão ativa no Redis
  -> se cache hit, usa dados mínimos
  -> se cache miss, consulta banco
  -> se banco confirmar sessão ativa, reidrata cache
```

Ao revogar sessão:

```txt
Revogar sessão
  -> atualiza banco
  -> remove cache da sessão no Redis
  -> publica evento SessionRevoked, se necessário.
```

Esse modelo entrega performance sem abrir mão da durabilidade.

---

## 21. Quando manter sessão apenas no Redis pode ser aceitável

Manter sessão apenas no Redis pode ser aceitável se:

- o produto aceita logout forçado caso Redis perca dados;
- não há necessidade forte de auditoria histórica;
- Redis está em ambiente gerenciado, com HA e persistência bem configurada;
- eviction está configurado de forma segura;
- sessão é vista como dado operacional, não como registro de segurança durável;
- o sistema é pequeno ou interno;
- o custo de implementação precisa ser reduzido.

Mesmo assim, para o AuthCore como boilerplate reutilizável e base de arquitetura, eu escolheria banco como fonte da verdade.

---

## 22. Comparação direta

| Critério | Sessão no Redis | Sessão no Banco | Banco + Redis Cache |
|---|---|---|---|
| Performance | Excelente | Boa | Excelente |
| Durabilidade | Média, depende da configuração | Alta | Alta |
| Auditoria | Fraca/Média | Forte | Forte |
| Custo de memória | Maior | Menor | Controlado |
| TTL nativo | Sim | Não nativo | Sim no cache |
| Revogação | Muito rápida | Rápida | Muito rápida |
| Risco de eviction | Existe | Não | Só no cache |
| Complexidade | Baixa/Média | Média | Média/Alta |
| Melhor uso | Dados efêmeros | Fonte da verdade | Produção escalável |

---

## 23. Decisão recomendada para o AuthCore

Minha recomendação final:

```txt
Sessão principal de longa duração
  -> Banco de dados

JWT curto
  -> Cookie HttpOnly

Códigos temporários
  -> Redis

Cache de sessão ativa
  -> Redis opcional

Revogação
  -> Banco como fonte da verdade
  -> Redis invalidado quando houver cache
```

Essa decisão é mais robusta para um boilerplate de autenticação reutilizável.

---

## 24. Fluxo final recomendado

```txt
[Browser/PWA]
    |
    | Login com e-mail/senha
    v
[AuthCore API]
    |
    | Cria sessão principal
    v
[Banco de dados]
    |
    | Emite access JWT curto
    v
[Cookies HttpOnly]
    |
    | __Host-auth.sid
    | __Host-auth.access
    v
[Gateway/Ocelot]
    |
    | Valida JWT curto
    | Opcionalmente valida sessão em rotas sensíveis
    v
[Microserviços]
```

Refresh:

```txt
[Browser/PWA]
    |
    | POST /auth/refresh com cookies
    v
[AuthCore]
    |
    | Valida sessão principal no banco
    | Se ativa, emite novo JWT curto
    v
[Cookie __Host-auth.access renovado]
```

Logout:

```txt
[Browser/PWA]
    |
    | POST /auth/logout
    v
[AuthCore]
    |
    | Revoga sessão no banco
    | Remove cache opcional no Redis
    | Expira cookies no response
```

---

## 25. Endpoints sugeridos

### 25.1. Autenticação browser/session

```txt
POST /auth/login
POST /auth/refresh
POST /auth/logout
POST /auth/logout-all
GET  /auth/me
GET  /auth/sessions
DELETE /auth/sessions/{sessionId}
```

### 25.2. Fluxos de código temporário

```txt
POST /auth/first-access/request-code
POST /auth/first-access/confirm
POST /auth/forgot-password/request-code
POST /auth/forgot-password/confirm
POST /auth/email-verification/request-code
POST /auth/email-verification/confirm
```

Esses fluxos são bons candidatos para Redis, porque os códigos são temporários e expiram rapidamente.

### 25.3. API/mobile/integrações

Para APIs externas, mobile ou integrações, ainda pode existir um fluxo explícito com token no response body, desde que separado do fluxo browser.

Exemplo:

```txt
POST /token/login
POST /token/refresh
POST /token/revoke
```

Esse fluxo deve ser separado do fluxo browser para não misturar decisões de segurança diferentes.

---

## 26. Claims recomendadas no JWT curto

O JWT curto deve carregar o mínimo necessário.

Sugestão:

```txt
sub                 -> identificador do usuário
sid                 -> identificador da sessão
jti                 -> identificador único do token
iss                 -> emissor
aud                 -> audiência
exp                 -> expiração curta
iat                 -> emissão
roles               -> papéis mínimos
permissions         -> permissões mínimas, se realmente necessário
security_stamp      -> versão de segurança do usuário/sessão
client_id/tenant_id -> quando houver escopo multi-tenant definido
```

Evitar colocar dados grandes ou sensíveis demais no JWT.

---

## 27. Cuidados de segurança

Checklist recomendado:

- Usar HTTPS sempre.
- Cookies de autenticação com `HttpOnly`.
- Cookies de autenticação com `Secure`.
- Avaliar `SameSite=Lax` quando frontend e API forem same-site.
- Usar `SameSite=None; Secure` apenas quando cross-site for realmente necessário.
- Usar allowlist explícita no CORS.
- Nunca usar `AllowAnyOrigin` com credentials.
- Implementar CSRF token para operações state-changing no fluxo browser.
- Validar `Origin`/`Referer` em operações sensíveis.
- Manter JWT curto, por exemplo 5 minutos.
- Não colocar refresh token acessível ao JavaScript.
- Rotacionar sessão ou security stamp em alteração de senha/MFA.
- Logar criação, renovação, revogação e suspeitas de sessão.
- Considerar rate limiting nos endpoints de login e códigos.
- Padronizar resposta generica no login para evitar enumeracao de conta (nao expor se e-mail existe, se conta esta bloqueada ou pendente).
- Em troca de senha, revogar todas as sessoes ativas de cookie e exigir novo login.
- Definir politica explicita de rotacao/invalidacao de sessao no login para reduzir risco de session fixation e de sessoes antigas ativas.
- Validar `iss` e `aud` com granularidade por servico (evitar audience generica compartilhada).
- Evitar segredos hardcoded/versionados; exigir chave/segredo obrigatorio em ambiente seguro com fail-fast no bootstrap se ausente/invalido.
- Enquanto houver HMAC (HS256), exigir pelo menos 256 bits de entropia aleatoria no segredo.
- Planejar migracao para assinatura assimetrica (`RS256` ou `ES256`) para separar chave de assinatura e validacao.

---

## 28. Impacto na arquitetura do AuthCore

### 28.1. Domain

Conceitos que podem existir no domínio:

```txt
AuthSession
SessionIdentifier
SessionStatus
SessionRevokedDomainEvent
SessionExpiredDomainEvent
SecurityStamp
VerificationCode
```

A sessão pode ser um agregado ou entidade relevante, dependendo da complexidade.

### 28.2. Application

Use cases sugeridos:

```txt
LoginUseCase
RefreshSessionAccessTokenUseCase
LogoutUseCase
LogoutAllSessionsUseCase
ListUserSessionsUseCase
RevokeSessionUseCase
RequestFirstAccessCodeUseCase
ConfirmFirstAccessCodeUseCase
RequestForgotPasswordCodeUseCase
ConfirmForgotPasswordCodeUseCase
```

Contratos/abstrações:

```txt
IAuthSessionRepository
IAccessTokenIssuer
ISessionCookieWriter
IVerificationCodeStore
IClock
IUnitOfWork
```

### 28.3. Infrastructure

Implementações:

```txt
PostgresAuthSessionRepository
RedisVerificationCodeStore
JwtAccessTokenIssuer
AspNetCoreSessionCookieWriter
RedisSessionCache, opcional
```

### 28.4. Api

Responsabilidades:

```txt
Controllers
Contracts Requests/Responses
Authentication Handlers
Authorization Policies
CORS
Cookie Options
CSRF Filter/Middleware
Exception Handling
```

---

## 29. Exemplo conceitual de separação de responsabilidades

```txt
Api
  -> recebe request
  -> lê/grava cookies
  -> aplica CORS/CSRF/Auth policies
  -> chama Application

Application
  -> orquestra caso de uso
  -> valida fluxo de aplicação
  -> chama repositórios e serviços abstratos
  -> controla transação quando necessário

Domain
  -> protege invariantes de sessão, usuário e segurança
  -> expressa eventos de domínio

Infrastructure
  -> persiste sessão no banco
  -> guarda códigos temporários no Redis
  -> emite JWT
  -> integra com mensageria/outbox
```

---

## 30. Migração sugerida em etapas

### Etapa 1 — Definir contratos

- Criar `IAuthSessionRepository`.
- Criar `IVerificationCodeStore`.
- Criar `IAccessTokenIssuer`.
- Criar serviço/abstração para escrita de cookies.

### Etapa 2 — Persistir sessão no banco

- Criar tabela/coleção de sessões.
- Criar índices.
- Implementar criação, busca, revogação e expiração.

### Etapa 3 — Manter Redis para códigos temporários

- First access code.
- Forgot password code.
- Email verification code.
- MFA/OTP, se existir.
- Rate limit.

### Etapa 4 — Ajustar login

- Login cria sessão no banco.
- Login emite JWT curto.
- Login grava cookies HttpOnly.

### Etapa 5 — Ajustar refresh

- Refresh valida sessão no banco.
- Refresh emite novo JWT curto.
- Refresh atualiza cookie do access token.

### Etapa 6 — Ajustar logout/revogação

- Logout revoga sessão no banco.
- Logout remove cookies.
- Logout remove cache opcional.

### Etapa 7 — Gateway

- Gateway lê JWT do cookie ou recebe header interno.
- Gateway encaminha `Authorization: Bearer` para microserviços.
- Rotas sensíveis podem exigir validação ativa da sessão.

### Etapa 8 — CSRF/CORS/SameSite

- Configurar CORS com origens explícitas.
- Definir SameSite conforme ambiente.
- Implementar token anti-CSRF para operações state-changing.


### Etapa 9 — Hardening de autenticacao e chaves

- Padronizar erro publico generico para falhas de login (anti-enumeracao).
- Aplicar politica de rotacao/invalidacao de sessoes antigas no novo login.
- Em troca de senha, revogar todas as sessoes ativas e exigir novo login.
- Remover segredos/chaves hardcoded de arquivos versionados.
- Exigir segredo/chave obrigatoria em ambiente seguro com fail-fast no bootstrap quando ausente/invalido.
- Enquanto houver HS256, garantir segredo com no minimo 256 bits de entropia aleatoria.
- Planejar/executar migracao de JWT para `RS256` ou `ES256`.
- Revisar audience por servico em microservicos.
- Registrar gap e plano de evolucao para OAuth 2.0/OIDC.`r`n`r`n---

## 31. Decisões finais propostas

### Decisão 1

Usar autenticação híbrida para Browser/PWA.

```txt
Sessão server-side + JWT curto em cookie HttpOnly
```

### Decisão 2

Persistir sessão principal no banco.

```txt
Banco = fonte da verdade da sessão
```

### Decisão 3

Usar Redis para dados efêmeros.

```txt
Redis = códigos temporários, OTP, rate limit e cache opcional
```

### Decisão 4

Manter JWT curto.

```txt
Access token com expiração próxima de 5 minutos
```

### Decisão 5

Não expor JWT no response body no fluxo browser.

```txt
Browser recebe JWT apenas via Set-Cookie
```

### Decisão 6

Tratar CSRF explicitamente.

```txt
Cookie auth exige defesa anti-CSRF em operações state-changing
```

### Decisão 7

Separar fluxo browser de fluxo API/mobile/integrações.

```txt
Browser/PWA: cookie HttpOnly
API/mobile/integrações: Authorization Bearer, quando aplicável
```

---


### Decisão 8

Padronizar anti-enumeracao no login.

```txt
Falhas de autenticacao retornam resposta publica generica unica
```

### Decisão 9

Revogar sessoes ativas em troca de senha.

```txt
Password change invalida todas as sessoes de browser e exige novo login
```

### Decisão 10

Definir politica de sessao no login.

```txt
Novo login aplica rotacao/invalidacao de sessoes antigas conforme politica
```

### Decisão 11

Fortalecer assinatura e material criptografico de token.

```txt
Curto prazo: HS256 apenas com segredo forte e seguro
Medio prazo: migracao para RS256/ES256
```

### Decisão 12

Aplicar audience por servico e registrar evolucao de protocolo.

```txt
Cada API com audience propria
Gap OAuth2/OIDC registrado com plano de evolucao
```

## 32. Conclusão

A abordagem híbrida é uma excelente escolha para o AuthCore, especialmente por estar sendo desenhada para microserviços, Gateway, PWA/browser e integrações.

A alteração mais importante na proposta é não tratar Redis como fonte principal de sessões longas. Para um boilerplate de autenticação reutilizável, a sessão principal é um registro relevante de segurança, auditoria e controle. Por isso, o melhor desenho é persistir essa sessão no banco e usar Redis para dados naturalmente temporários.

Modelo final recomendado:

```txt
Banco
  -> sessões longas e revogáveis

Redis
  -> códigos de verificação, OTP, forgot password, first access, rate limit e cache opcional

JWT curto
  -> performance nas requisições comuns

Cookie HttpOnly
  -> transporte seguro no browser, sem expor token ao JavaScript

CSRF + CORS + SameSite
  -> proteção obrigatória para fluxo baseado em cookies
```

Esse desenho entrega controle, escalabilidade e segurança com bons trade-offs para produção.

---

## 33. Referências úteis

- MDN Web Docs — Set-Cookie: https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Set-Cookie
- MDN Web Docs — Using HTTP cookies: https://developer.mozilla.org/en-US/docs/Web/HTTP/Guides/Cookies
- OWASP — Cross-Site Request Forgery Prevention Cheat Sheet: https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html
- Microsoft — CORS no ASP.NET Core: https://learn.microsoft.com/aspnet/core/security/cors
- Microsoft — SameSite cookies no ASP.NET Core: https://learn.microsoft.com/aspnet/core/security/samesite
- Redis Docs — Key eviction: https://redis.io/docs/latest/develop/reference/eviction/
- Redis Docs — Persistence: https://redis.io/docs/latest/operate/oss_and_stack/management/persistence/





