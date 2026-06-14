# Spec Driven Development — Login com Google no AuthCore

> **Status:** Proposta técnica completa  
> **Contexto:** AuthCore Boilerplate — autenticação híbrida, sessão, JWT, Gateway e login social  
> **Provedor inicial:** Google  
> **Tecnologias-alvo:** C#, ASP.NET Core, OAuth 2.0, OpenID Connect, PostgreSQL, Redis, Docker/Swarm, Gateway/Ocelot  
> **Última revisão:** 2026-06-14

---

## 1. Visão geral

O AuthCore deverá suportar autenticação via **Google Login**, utilizando **OAuth 2.0 + OpenID Connect**.

O Google será usado apenas como **provedor externo de identidade**. O AuthCore continuará sendo o responsável por:

- criar e controlar o usuário interno;
- emitir sessão/cookie próprio;
- emitir JWT próprio, quando aplicável;
- controlar refresh token interno;
- aplicar regras de negócio;
- aplicar autorização;
- revogar sessões;
- auditar login;
- controlar onboarding;
- manter vínculo com tenants/clientes/roles/permissões.

O ponto central da arquitetura é:

```text
Google Account != AuthCore User
```

O Google autentica a identidade externa. O AuthCore decide o que fazer com essa identidade dentro do domínio da aplicação.

---

## 2. Objetivo

Permitir que um usuário acesse o AuthCore usando sua conta Google.

Fluxo esperado:

```text
Usuário clica em "Entrar com Google"
AuthCore redireciona para o Google
Google autentica o usuário
Google redireciona para o callback do AuthCore
AuthCore valida o retorno
AuthCore identifica ou cria o usuário interno
AuthCore cria o vínculo externo
AuthCore emite sessão/cookie/JWT próprio
Frontend recebe o usuário autenticado
```

---

## 3. Motivação

O login social reduz fricção no cadastro e no acesso, especialmente em aplicações web, PWA e SaaS.

Para o AuthCore, essa funcionalidade também serve como base para futuros provedores externos, como:

- Meta;
- Microsoft;
- GitHub;
- Apple;
- provedores corporativos OIDC.

A implementação deve nascer genérica o suficiente para suportar múltiplos provedores no futuro, mas sem abstrair demais antes da necessidade real.

---

## 4. Decisão arquitetural principal

### 4.1 Decisão

Modelar Google Login como **External Login Provider**.

O Google não substituirá o AuthCore. O Google apenas provará que o usuário controla determinada conta Google.

O AuthCore continuará dono de:

```text
User
Session
RefreshToken
Roles
Permissions
Tenant/Client
Audit
Onboarding
AccountStatus
SecurityPolicy
```

---

### 4.2 Consequência prática

Depois que o Google autenticar o usuário, o AuthCore deve emitir sua própria autenticação interna.

Não devemos usar o token do Google como token de acesso das APIs internas.

Errado:

```text
Frontend recebe token Google
Frontend chama APIs internas usando token Google
APIs internas confiam no Google token diretamente
```

Correto:

```text
Frontend inicia login Google
Google retorna para AuthCore
AuthCore valida identidade externa
AuthCore emite cookie/JWT interno
Frontend chama APIs internas usando credencial do AuthCore
```

---

## 5. Escopo

### 5.1 Dentro do escopo

- Login com Google.
- Callback do Google.
- Criação de vínculo externo.
- Criação automática de usuário ou início de onboarding.
- Emissão de sessão interna do AuthCore.
- Emissão opcional de JWT interno.
- Suporte a ambiente local.
- Suporte a homologação.
- Suporte a produção.
- Configuração segura de secrets.
- Auditoria.
- Logs.
- Métricas.
- Testes.
- Tratamento de erro.
- Documentação operacional.

### 5.2 Fora do escopo inicial

- Acesso ao Gmail.
- Acesso ao Google Drive.
- Acesso ao Google Calendar.
- Envio de e-mail usando conta Google.
- Sincronização de contatos.
- Revogação do token Google.
- Login com Meta/Microsoft/GitHub.
- Google Cloud Identity Platform.
- Fluxos corporativos SAML/OIDC enterprise.

---

## 6. Custo

### 6.1 Precisa pagar para implementar login com Google?

Para o escopo básico de login com Google usando OAuth 2.0/OpenID Connect, **não é necessário contratar um serviço pago específico**.

Será necessário criar/configurar:

- projeto no Google Cloud;
- OAuth Consent Screen;
- OAuth Client ID;
- Client Secret;
- Redirect URIs;
- domínios autorizados para produção.

### 6.2 O que pode gerar custo?

- domínio próprio;
- hospedagem da API;
- infraestrutura de produção;
- observabilidade/logs gerenciados;
- secret manager, se usar serviço pago;
- Google Cloud Identity Platform, caso seja escolhido no futuro;
- auditorias/verificações adicionais em cenários específicos.

### 6.3 Decisão recomendada

Para o AuthCore, **não começar com Google Cloud Identity Platform**.

Começar com:

```text
OAuth 2.0 + OpenID Connect direto no AuthCore
```

Motivo:

- menor custo;
- maior controle arquitetural;
- melhor aderência ao seu boilerplate;
- melhor aprendizado técnico;
- mantém AuthCore como produto/plataforma de autenticação.

---

## 7. Conceitos importantes

### 7.1 OAuth 2.0

OAuth 2.0 é um protocolo de autorização. Ele permite que uma aplicação obtenha autorização para acessar recursos ou obter informações relacionadas a um usuário.

No caso de login com Google, usamos o OAuth 2.0 junto com OpenID Connect.

### 7.2 OpenID Connect

OpenID Connect adiciona uma camada de identidade sobre OAuth 2.0.

É ele que permite usar o fluxo como autenticação, retornando informações como:

```text
sub
email
email_verified
name
picture
```

### 7.3 Claim `sub`

O `sub` é o identificador único do usuário dentro do provedor OpenID Connect.

Para o AuthCore, ele deve ser tratado como o identificador principal da conta Google.

```text
Provider = Google
ProviderUserId = sub
```

Não usar apenas e-mail como identificador principal do vínculo externo.

Motivo:

- e-mail pode mudar;
- e-mail pode não estar verificado;
- pode haver conflito de conta;
- o `sub` é mais adequado para vínculo com provedor externo.

---

## 8. Escopos necessários

Para login básico, usar somente:

```text
openid
profile
email
```

Não pedir:

```text
Gmail
Drive
Calendar
Contacts
YouTube
Admin SDK
```

### 8.1 Justificativa

O AuthCore precisa apenas autenticar o usuário e obter informações básicas de identidade.

Pedir escopos adicionais aumenta:

- complexidade;
- risco de reprovação/verificação;
- responsabilidade jurídica;
- responsabilidade de segurança;
- exposição de dados do usuário;
- necessidade de armazenar/revogar tokens Google.

---

## 9. Configuração no Google Cloud

### 9.1 Criar projeto

Recomendação para ambientes reais:

```text
authcore-development
authcore-homologation
authcore-production
```

Também é possível usar um único projeto com múltiplos OAuth Clients. Porém, para maturidade operacional, separar produção de desenvolvimento/homologação reduz risco de erro.

### 9.2 Configurar OAuth Consent Screen

Configurações recomendadas:

```text
App name: AuthCore
User support email: e-mail oficial do projeto
Developer contact information: e-mail oficial do projeto
Authorized domains: seudominio.com
Privacy Policy URL: https://seudominio.com/privacy
Terms of Service URL: https://seudominio.com/terms
Scopes: openid, profile, email
```

### 9.3 Criar OAuth Client ID

Tipo de aplicação:

```text
Web application
```

Será gerado:

```text
GOOGLE_CLIENT_ID
GOOGLE_CLIENT_SECRET
```

Esses valores devem ser configurados por ambiente.

---

## 10. Redirect URIs por ambiente

### 10.1 Desenvolvimento local

Se a API AuthCore for chamada diretamente:

```text
https://localhost:7001/auth/external/google/callback
```

Se o Gateway for o ponto de entrada:

```text
https://localhost:7000/auth/external/google/callback
```

### 10.2 Homologação

Direto na API:

```text
https://authcore-api-homolog.seudominio.com/auth/external/google/callback
```

Via Gateway:

```text
https://gateway-homolog.seudominio.com/auth/external/google/callback
```

### 10.3 Produção

Direto na API:

```text
https://api.seudominio.com/auth/external/google/callback
```

Via Gateway:

```text
https://gateway.seudominio.com/auth/external/google/callback
```

### 10.4 Decisão recomendada

Se o Gateway/Ocelot for o ponto público de entrada em produção, o Redirect URI cadastrado no Google deve apontar para o Gateway.

Exemplo:

```text
https://api.seudominio.com/auth/external/google/callback
```

O Gateway roteia internamente para o AuthCore.

---

## 11. Variáveis de ambiente

### 11.1 appsettings.Development.json

```json
{
  "Authentication": {
    "Google": {
      "ClientId": "dev-google-client-id",
      "ClientSecret": "dev-google-client-secret",
      "CallbackPath": "/auth/external/google/callback"
    },
    "AllowedReturnUrls": [
      "https://localhost:5173",
      "https://localhost:3000"
    ]
  }
}
```

### 11.2 appsettings.Homologation.json

```json
{
  "Authentication": {
    "Google": {
      "CallbackPath": "/auth/external/google/callback"
    },
    "AllowedReturnUrls": [
      "https://app-homolog.seudominio.com"
    ]
  }
}
```

### 11.3 appsettings.Production.json

```json
{
  "Authentication": {
    "Google": {
      "CallbackPath": "/auth/external/google/callback"
    },
    "AllowedReturnUrls": [
      "https://app.seudominio.com",
      "https://admin.seudominio.com"
    ]
  }
}
```

### 11.4 Produção via environment variables

```env
AUTHENTICATION__GOOGLE__CLIENTID=xxxxxxxx.apps.googleusercontent.com
AUTHENTICATION__GOOGLE__CLIENTSECRET=GOCSPX-xxxxxxxx
AUTHENTICATION__GOOGLE__CALLBACKPATH=/auth/external/google/callback
```

### 11.5 Regra de segurança

Nunca versionar `ClientSecret`.

Evitar:

```text
appsettings.Production.json com segredo real
.env commitado
README com segredo real
print de tela com segredo
logs contendo segredo
```

---

## 12. Secrets em produção

### 12.1 Opções recomendadas

- Docker Swarm Secrets;
- Kubernetes Secrets;
- AWS Secrets Manager;
- Azure Key Vault;
- GCP Secret Manager;
- HashiCorp Vault;
- variáveis seguras no pipeline CI/CD.

### 12.2 Para Docker Swarm

Exemplo conceitual:

```yaml
services:
  authcore-api:
    image: authcore-api:latest
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      AUTHENTICATION__GOOGLE__CLIENTID: ${GOOGLE_CLIENT_ID}
      AUTHENTICATION__GOOGLE__CALLBACKPATH: /auth/external/google/callback
    secrets:
      - google_client_secret
    ports:
      - "8080:8080"

secrets:
  google_client_secret:
    external: true
```

O segredo poderia ser lido de:

```text
/run/secrets/google_client_secret
```

ou injetado como variável segura pelo pipeline.

---

## 13. Fluxo funcional

## 13.1 Login com Google

### Endpoint

```http
GET /auth/external/google
```

Com returnUrl:

```http
GET /auth/external/google?returnUrl=https://app.seudominio.com/auth/callback
```

### Responsabilidade

Iniciar o desafio de autenticação com o Google.

### Fluxo

```text
1. Frontend chama /auth/external/google
2. AuthCore valida returnUrl
3. AuthCore cria AuthenticationProperties
4. AuthCore cria challenge Google
5. Middleware gera state/correlation
6. Usuário é redirecionado para o Google
7. Google autentica o usuário
8. Google redireciona para o callback
```

---

## 13.2 Callback do Google

### Endpoint

```http
GET /auth/external/google/callback
```

### Responsabilidade

Receber o retorno do Google, validar a autenticação externa e gerar autenticação interna.

### Fluxo

```text
1. AuthCore recebe callback
2. Middleware valida state/correlation
3. AuthCore lê principal externo
4. AuthCore extrai providerUserId/sub
5. AuthCore extrai email
6. AuthCore extrai email_verified
7. AuthCore extrai name/picture, se disponível
8. AuthCore procura ExternalLogin existente
9. Se existir, carrega User interno
10. Se não existir, procura User por e-mail verificado
11. Se User existir, vincula Google ao usuário
12. Se User não existir, cria User ou inicia onboarding
13. AuthCore cria sessão/JWT interno
14. AuthCore limpa cookie temporário externo
15. AuthCore redireciona para frontend
```

---

## 14. Regras de negócio

### 14.1 Identificador principal

O vínculo externo deve usar o `sub` do Google como identificador principal.

```text
Provider: Google
ProviderUserId: claim sub
```

### 14.2 E-mail verificado

Se:

```text
email_verified = true
```

O AuthCore pode considerar o e-mail como verificado pelo provedor externo.

Se:

```text
email_verified = false
```

O AuthCore deve:

- negar criação automática; ou
- exigir verificação interna; ou
- iniciar onboarding restrito.

### 14.3 Usuário já existe com mesmo e-mail

Cenário:

```text
Usuário já tem conta local com e-mail e senha
Depois tenta entrar com Google usando o mesmo e-mail
```

Regra recomendada:

```text
Se email_verified = true:
    Vincular Google ao usuário existente
Se email_verified = false:
    Não vincular automaticamente
```

### 14.4 Usuário não existe

Existem duas estratégias.

#### Estratégia A — Criar usuário automaticamente

```text
Google login cria User interno
Marca e-mail como verificado
Cria vínculo externo
Emite sessão
```

Vantagens:

- melhor experiência;
- menos fricção;
- fluxo rápido.

Desvantagens:

- pode criar usuário incompleto;
- pode exigir onboarding depois;
- precisa tratar aceite de termos.

#### Estratégia B — Criar pré-cadastro/onboarding

```text
Google login cria ExternalLoginPending
Redireciona para completar cadastro
Só depois cria User completo
```

Vantagens:

- mais controle de domínio;
- permite exigir telefone, plano, tenant, aceite de termos;
- evita usuário interno incompleto.

Desvantagens:

- mais fluxo;
- mais fricção;
- mais estados para controlar.

### 14.5 Decisão recomendada para o AuthCore

Criar usuário interno automaticamente se:

```text
email_verified = true
email não está bloqueado
domínio é permitido, se houver regra de tenant
não existe conflito de conta externa
```

Se faltar informação obrigatória:

```text
Criar usuário em PendingOnboarding
Emitir sessão limitada
Redirecionar para /onboarding
```

---

## 15. Modelagem de domínio

### 15.1 Entidade User

```csharp
public sealed class User
{
    public Guid Id { get; private set; }
    public Email Email { get; private set; }
    public string? FullName { get; private set; }
    public UserStatus Status { get; private set; }
    public bool EmailVerified { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }

    private User() { }

    public static User CreateFromExternalLogin(
        Email email,
        string? fullName,
        bool emailVerified,
        DateTime nowUtc)
    {
        if (!emailVerified)
            throw new DomainException("Não é possível criar usuário externo com e-mail não verificado.");

        return new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = fullName,
            EmailVerified = true,
            Status = UserStatus.Active,
            CreatedAtUtc = nowUtc,
            LastLoginAtUtc = nowUtc
        };
    }

    public void RegisterLogin(DateTime nowUtc)
    {
        if (Status == UserStatus.Blocked)
            throw new DomainException("Usuário bloqueado não pode autenticar.");

        LastLoginAtUtc = nowUtc;
    }
}
```

### 15.2 Entidade ExternalLogin

```csharp
public sealed class ExternalLogin
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public ExternalLoginProvider Provider { get; private set; }
    public string ProviderUserId { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public bool EmailVerified { get; private set; }
    public DateTime LinkedAtUtc { get; private set; }
    public DateTime? LastUsedAtUtc { get; private set; }

    private ExternalLogin() { }

    public static ExternalLogin LinkGoogle(
        Guid userId,
        string providerUserId,
        string email,
        bool emailVerified,
        DateTime nowUtc)
    {
        if (userId == Guid.Empty)
            throw new DomainException("Usuário é obrigatório para vincular login externo.");

        if (string.IsNullOrWhiteSpace(providerUserId))
            throw new DomainException("Identificador externo do Google é obrigatório.");

        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("E-mail externo é obrigatório.");

        return new ExternalLogin
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = ExternalLoginProvider.Google,
            ProviderUserId = providerUserId,
            Email = email,
            EmailVerified = emailVerified,
            LinkedAtUtc = nowUtc,
            LastUsedAtUtc = nowUtc
        };
    }

    public void RegisterUsage(DateTime nowUtc)
    {
        LastUsedAtUtc = nowUtc;
    }
}
```

### 15.3 Enum ExternalLoginProvider

```csharp
public enum ExternalLoginProvider
{
    Google = 1
}
```

### 15.4 Enum UserStatus

```csharp
public enum UserStatus
{
    PendingOnboarding = 1,
    Active = 2,
    Blocked = 3,
    Deleted = 4
}
```

---

## 16. Persistência

### 16.1 Tabela users

```sql
CREATE TABLE users (
    id UUID PRIMARY KEY,
    email VARCHAR(320) NOT NULL,
    full_name VARCHAR(255) NULL,
    email_verified BOOLEAN NOT NULL DEFAULT FALSE,
    status SMALLINT NOT NULL,
    created_at_utc TIMESTAMP NOT NULL,
    last_login_at_utc TIMESTAMP NULL
);

CREATE UNIQUE INDEX ux_users_email
ON users (LOWER(email));
```

### 16.2 Tabela external_logins

```sql
CREATE TABLE external_logins (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    provider SMALLINT NOT NULL,
    provider_user_id VARCHAR(255) NOT NULL,
    email VARCHAR(320) NOT NULL,
    email_verified BOOLEAN NOT NULL DEFAULT FALSE,
    linked_at_utc TIMESTAMP NOT NULL,
    last_used_at_utc TIMESTAMP NULL,

    CONSTRAINT fk_external_logins_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
);

CREATE UNIQUE INDEX ux_external_logins_provider_provider_user_id
ON external_logins (provider, provider_user_id);

CREATE INDEX ix_external_logins_user_id
ON external_logins (user_id);
```

### 16.3 Justificativa do índice único

O mesmo usuário externo do Google não pode estar vinculado a duas contas internas.

```text
Provider = Google
ProviderUserId = sub
```

Deve ser único.

---

## 17. Contratos de API

### 17.1 Iniciar login

```http
GET /auth/external/google?returnUrl=https://app.seudominio.com/auth/callback
```

### Responsabilidade

Iniciar o fluxo externo com o Google.

### Regras

- `returnUrl` deve ser validado contra allowlist.
- Não aceitar domínio arbitrário.
- Não aceitar `javascript:`.
- Não aceitar `data:`.
- Não aceitar URL protocol-relative, como `//site-malicioso.com`.
- Não aceitar redirect externo não cadastrado.

---

### 17.2 Callback

```http
GET /auth/external/google/callback
```

### Responsabilidade

Receber o retorno do Google e concluir login interno.

---

### 17.3 Vincular Google a usuário logado

```http
POST /auth/external/google/link
Authorization: Bearer <token>
```

ou usando cookie de sessão.

### Regras

- Usuário precisa estar autenticado.
- Conta Google não pode estar vinculada a outro usuário.
- Vínculo deve usar `provider + providerUserId`.

---

### 17.4 Desvincular Google

```http
DELETE /auth/external/google
Authorization: Bearer <token>
```

### Regra importante

Não permitir desvincular o último método de autenticação do usuário.

Exemplo:

```text
Se usuário não tem senha cadastrada
E só possui Google como login
Então não pode remover Google
```

---

## 18. Configuração ASP.NET Core

### 18.1 Pacote esperado

Dependendo da versão do projeto:

```bash
dotnet add package Microsoft.AspNetCore.Authentication.Google
```

### 18.2 Configuração conceitual

```csharp
public static class AuthenticationConfiguration
{
    public static IServiceCollection AddAuthCoreAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddAuthentication()
            .AddCookie("External", options =>
            {
                options.Cookie.Name = "__Host-authcore.external";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
            })
            .AddGoogle("Google", options =>
            {
                options.ClientId = configuration["Authentication:Google:ClientId"]
                    ?? throw new InvalidOperationException("Google ClientId não configurado.");

                options.ClientSecret = configuration["Authentication:Google:ClientSecret"]
                    ?? throw new InvalidOperationException("Google ClientSecret não configurado.");

                options.CallbackPath = configuration["Authentication:Google:CallbackPath"]
                    ?? "/auth/external/google/callback";

                options.SignInScheme = "External";
                options.SaveTokens = false;

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
            });

        return services;
    }
}
```

### 18.3 Por que `SaveTokens = false`?

Para login simples, o AuthCore não precisa persistir `access_token`, `id_token` ou `refresh_token` do Google.

Vantagens:

- menos dados sensíveis;
- menor impacto em caso de vazamento;
- menor responsabilidade de revogação;
- menor complexidade operacional.

Só armazenar tokens Google futuramente se houver necessidade real de consumir APIs do Google.

---

## 19. Use Cases

### 19.1 StartGoogleLoginUseCase

Responsabilidade:

```text
Validar returnUrl
Preparar challenge
Redirecionar para Google
```

Interface conceitual:

```csharp
public interface IStartGoogleLoginUseCase
{
    IActionResult Execute(string? returnUrl);
}
```

### 19.2 CompleteGoogleLoginUseCase

Responsabilidade:

```text
Ler principal externo
Extrair claims
Resolver usuário interno
Criar ou vincular ExternalLogin
Criar sessão/JWT interno
Retornar URL final do frontend
```

Interface conceitual:

```csharp
public interface ICompleteGoogleLoginUseCase
{
    Task<CompleteGoogleLoginResult> ExecuteAsync(
        ExternalLoginCommand command,
        CancellationToken cancellationToken);
}
```

Command:

```csharp
public sealed class ExternalLoginCommand
{
    public string Provider { get; init; } = default!;
    public string ProviderUserId { get; init; } = default!;
    public string Email { get; init; } = default!;
    public bool EmailVerified { get; init; }
    public string? FullName { get; init; }
    public string? PictureUrl { get; init; }
    public string? ReturnUrl { get; init; }
}
```

Result:

```csharp
public sealed class CompleteGoogleLoginResult
{
    public Guid UserId { get; init; }
    public bool RequiresOnboarding { get; init; }
    public string RedirectUrl { get; init; } = default!;
}
```

---

## 20. Controller

Exemplo conceitual:

```csharp
[ApiController]
[Route("auth/external")]
public sealed class ExternalAuthController : ControllerBase
{
    [HttpGet("google")]
    public IActionResult GoogleLogin([FromQuery] string? returnUrl)
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(GoogleCallback), "ExternalAuth"),
            Items =
            {
                ["returnUrl"] = returnUrl ?? "/"
            }
        };

        return Challenge(properties, "Google");
    }

    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback(
        [FromServices] ICompleteGoogleLoginUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await HttpContext.AuthenticateAsync("External");

        if (!result.Succeeded || result.Principal is null)
            return BadRequest("Falha ao autenticar com Google.");

        var providerUserId = result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value;
        var fullName = result.Principal.FindFirst(ClaimTypes.Name)?.Value;

        var emailVerifiedClaim = result.Principal.FindFirst("email_verified")?.Value;
        var emailVerified = string.Equals(
            emailVerifiedClaim,
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(providerUserId) || string.IsNullOrWhiteSpace(email))
            return BadRequest("Google não retornou dados obrigatórios.");

        var command = new ExternalLoginCommand
        {
            Provider = "Google",
            ProviderUserId = providerUserId,
            Email = email,
            EmailVerified = emailVerified,
            FullName = fullName
        };

        var loginResult = await useCase.ExecuteAsync(command, cancellationToken);

        await HttpContext.SignOutAsync("External");

        return Redirect(loginResult.RedirectUrl);
    }
}
```

### Observação arquitetural

A Controller deve continuar fina.

A regra deve ficar na Application.

A Controller apenas:

- recebe request;
- dispara challenge;
- lê resultado externo;
- monta command;
- chama use case;
- retorna redirect/resposta.

---

## 21. Sessão, JWT e Gateway

### 21.1 Web/PWA

Depois do callback do Google, o AuthCore deve criar a mesma sessão usada no login tradicional:

```text
Cookie HttpOnly
Secure = true em produção
SameSite = Lax
TTL no Redis
SessionId opaco
Revogação imediata
```

### 21.2 Mobile/API

Se o cliente for mobile ou API consumer:

```text
Emitir access token JWT interno
Emitir refresh token interno
Rotacionar refresh token
Não reutilizar token do Google como token interno
```

### 21.3 Gateway/Ocelot

Se o Gateway for o ponto público:

```text
Google Redirect URI aponta para o Gateway
Gateway roteia /auth/external/google/callback para AuthCore
AuthCore precisa conhecer Forwarded Headers
AuthCore precisa montar URLs públicas corretamente
```

Configuração necessária:

```csharp
app.UseForwardedHeaders();
```

Validar:

```text
X-Forwarded-Proto
X-Forwarded-Host
HTTPS externo
Callback público
```

---

## 22. Segurança

### 22.1 State e correlation

O fluxo OAuth/OIDC precisa proteger contra CSRF no login.

O middleware do ASP.NET Core ajuda nisso usando mecanismos como state/correlation.

Não implementar callback OAuth manualmente sem necessidade.

### 22.2 Redirect seguro

Criar allowlist:

```json
{
  "Authentication": {
    "AllowedReturnUrls": [
      "https://app.seudominio.com",
      "https://admin.seudominio.com",
      "https://app-homolog.seudominio.com"
    ]
  }
}
```

Bloquear:

```text
https://site-malicioso.com
javascript:alert(1)
data:text/html
//site-malicioso.com
```

### 22.3 Cookies em produção

Configuração recomendada:

```text
HttpOnly = true
Secure = true
SameSite = Lax
Domain = .seudominio.com, se precisar compartilhar entre subdomínios
Path = /
```

### 22.4 Não armazenar tokens do Google

Para o escopo inicial:

```text
Não persistir access_token do Google
Não persistir refresh_token do Google
Não usar token do Google para autenticar APIs internas
```

### 22.5 Rate limiting

Aplicar rate limit em:

```text
/auth/external/google
/auth/external/google/callback
/auth/login
/auth/register
/auth/refresh
```

### 22.6 Auditoria

Registrar eventos:

```text
GoogleLoginStarted
GoogleLoginSucceeded
GoogleLoginFailed
ExternalLoginLinked
ExternalLoginUnlinked
ExternalLoginConflictDetected
ExternalLoginBlockedUser
ExternalLoginRequiresOnboarding
```

Não logar:

```text
ClientSecret
Authorization code
Access token do Google
ID token completo
Refresh token
Cookie de sessão
```

---

## 23. Ambiente de produção completo

### 23.1 Domínios sugeridos

```text
Frontend:
https://app.seudominio.com

API/Gateway:
https://api.seudominio.com

Auth callback:
https://api.seudominio.com/auth/external/google/callback

Política:
https://seudominio.com/privacy

Termos:
https://seudominio.com/terms
```

### 23.2 HTTPS obrigatório

Em produção:

```text
Todo tráfego externo via HTTPS
Cookies Secure
HSTS habilitado
Redirect HTTP -> HTTPS
```

### 23.3 Google Cloud produção

OAuth Client:

```text
Name: AuthCore Production
```

Authorized JavaScript origins:

```text
https://app.seudominio.com
https://api.seudominio.com
```

Authorized redirect URIs:

```text
https://api.seudominio.com/auth/external/google/callback
```

### 23.4 Google Cloud homologação

OAuth Client:

```text
Name: AuthCore Homologation
```

Authorized JavaScript origins:

```text
https://app-homolog.seudominio.com
https://api-homolog.seudominio.com
```

Authorized redirect URIs:

```text
https://api-homolog.seudominio.com/auth/external/google/callback
```

### 23.5 Google Cloud desenvolvimento

OAuth Client:

```text
Name: AuthCore Development
```

Authorized JavaScript origins:

```text
https://localhost:7000
https://localhost:7001
```

Authorized redirect URIs:

```text
https://localhost:7001/auth/external/google/callback
```

---

## 24. Reverse Proxy/Nginx

Exemplo conceitual:

```nginx
server {
    listen 443 ssl;
    server_name api.seudominio.com;

    location / {
        proxy_pass http://authcore-api:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Proto https;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }
}
```

### 24.1 ASP.NET Core atrás de proxy

```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;
});

app.UseForwardedHeaders();
```

Importante para o AuthCore gerar corretamente URLs públicas com `https`.

---

## 25. Casos de uso detalhados

### 25.1 Usuário novo com Google

```gherkin
Dado que o usuário não existe no AuthCore
E o Google retorna email_verified = true
Quando ele autentica com Google
Então o AuthCore cria User
E cria ExternalLogin
E cria sessão interna
E redireciona para o frontend
```

### 25.2 Usuário existente com e-mail e senha

```gherkin
Dado que existe User com o mesmo e-mail
E o usuário ainda não possui ExternalLogin Google
E o Google retorna email_verified = true
Quando ele autentica com Google
Então o AuthCore vincula Google ao User existente
E cria sessão interna
```

### 25.3 Google já vinculado

```gherkin
Dado que existe ExternalLogin com Provider Google e ProviderUserId
Quando o usuário autentica com Google
Então o AuthCore carrega o User vinculado
E atualiza LastUsedAtUtc
E cria sessão interna
```

### 25.4 Usuário bloqueado

```gherkin
Dado que o ExternalLogin existe
E o User está Blocked
Quando o usuário autentica com Google
Então o AuthCore nega login
E registra auditoria
```

### 25.5 E-mail não verificado

```gherkin
Dado que o Google retorna email_verified = false
Quando o usuário autentica
Então o AuthCore não cria conta automaticamente
E retorna erro ou inicia verificação interna
```

### 25.6 Conflito de vínculo

```gherkin
Dado que uma conta Google já está vinculada ao User A
Quando o User B tenta vincular a mesma conta Google
Então o AuthCore nega a operação
E registra ExternalLoginConflictDetected
```

---

## 26. Erros esperados

### 26.1 Usuário cancela no Google

Resposta sugerida:

```text
/auth/error?reason=google_cancelled
```

### 26.2 Callback inválido

Possíveis causas:

```text
State inválido
Correlation cookie ausente
Redirect URI incorreta
ClientId incorreto
ClientSecret incorreto
Clock skew
Proxy sem forwarded headers
Cookie bloqueado pelo navegador
```

Resposta sugerida:

```text
/auth/error?reason=external_callback_failed
```

### 26.3 Redirect não permitido

```text
/auth/error?reason=invalid_return_url
```

### 26.4 Usuário bloqueado

```text
/auth/error?reason=user_blocked
```

### 26.5 Onboarding obrigatório

```text
/onboarding?provider=google
```

---

## 27. Observabilidade

### 27.1 Logs estruturados

Campos recomendados:

```text
event_name
provider
user_id
external_login_id
email_hash
success
failure_reason
correlation_id
ip
user_agent
created_at_utc
```

Evitar logar e-mail puro quando possível. Usar hash para correlação.

### 27.2 Métricas

```text
auth_google_login_started_total
auth_google_login_succeeded_total
auth_google_login_failed_total
auth_google_login_cancelled_total
auth_external_login_linked_total
auth_external_login_conflict_total
auth_google_callback_duration_ms
```

### 27.3 Alertas

Criar alertas para:

```text
Aumento súbito de falha no callback
Aumento de invalid_state
Aumento de invalid_return_url
Aumento de login bloqueado
Falha de configuração do ClientSecret
```

---

## 28. Testes

### 28.1 Testes unitários

- Deve criar ExternalLogin com providerUserId válido.
- Deve bloquear criação sem providerUserId.
- Deve bloquear criação com e-mail vazio.
- Deve vincular Google a usuário existente.
- Deve impedir duplicidade provider + providerUserId.
- Deve impedir login de usuário bloqueado.
- Deve exigir onboarding quando dados obrigatórios faltarem.
- Deve validar returnUrl permitido.
- Deve bloquear returnUrl externo.

### 28.2 Testes de integração

- `GET /auth/external/google` deve gerar challenge.
- Callback válido deve criar sessão.
- Callback sem principal externo deve retornar erro.
- Callback com e-mail não verificado deve negar ou iniciar onboarding.
- Usuário existente deve vincular ExternalLogin.
- Usuário já vinculado deve apenas autenticar.

### 28.3 Testes manuais em homologação/produção

Checklist:

- Login Google com usuário novo.
- Login Google com usuário já existente.
- Login Google cancelado.
- Login Google com returnUrl válido.
- Login Google com returnUrl malicioso.
- Logout após login Google.
- Revogação de sessão.
- Conta bloqueada não consegue entrar.
- Callback via Gateway.
- Callback direto na API, se permitido.
- Cookie Secure presente.
- SameSite correto.
- Domínio do cookie correto.

---

## 29. Critérios de aceite

### 29.1 Funcionais

- Usuário consegue iniciar login com Google.
- Usuário consegue concluir login com Google.
- AuthCore cria vínculo externo.
- AuthCore reutiliza vínculo externo existente.
- AuthCore cria sessão interna.
- AuthCore não usa token Google como token interno.
- AuthCore bloqueia returnUrl inválido.
- AuthCore não permite duplicidade de vínculo externo.
- AuthCore respeita status do usuário.
- AuthCore redireciona para onboarding quando necessário.

### 29.2 Segurança

- ClientSecret não é versionado.
- Callback usa HTTPS em produção.
- Cookies usam Secure em produção.
- Tokens do Google não são persistidos no escopo inicial.
- State/correlation são validados.
- ReturnUrl usa allowlist.
- Logs não expõem tokens/secrets.
- Conta Google já vinculada não pode ser tomada por outro usuário.

### 29.3 Produção

- OAuth Consent Screen configurada.
- Domínio verificado.
- Privacy Policy disponível.
- Terms of Service disponível.
- Redirect URI de produção cadastrado.
- Secrets injetados por pipeline/secret manager.
- Forwarded Headers configurado.
- Logs e métricas ativos.
- Alertas mínimos configurados.

---

## 30. Plano de implementação por fases

### Fase 1 — Infraestrutura Google

- Criar projeto Google Cloud.
- Configurar OAuth Consent Screen.
- Criar OAuth Client para development.
- Criar OAuth Client para homolog.
- Criar OAuth Client para production.
- Cadastrar redirect URIs.
- Cadastrar authorized origins.
- Criar variáveis de ambiente.
- Adicionar secrets ao pipeline.

### Fase 2 — Domínio e persistência

- Criar `ExternalLoginProvider`.
- Criar entidade `ExternalLogin`.
- Criar repositório de `ExternalLogin`.
- Criar migration/tabela `external_logins`.
- Criar índice único `provider + provider_user_id`.
- Adicionar métodos de vínculo ao domínio.

### Fase 3 — Application

- Criar `CompleteGoogleLoginUseCase`.
- Criar `LinkExternalLoginUseCase`.
- Criar `UnlinkExternalLoginUseCase`.
- Criar validação de `returnUrl`.
- Criar política para usuário novo.
- Criar política para usuário existente.
- Criar política para onboarding.

### Fase 4 — API

- Adicionar `AddGoogle` no Authentication.
- Criar `ExternalAuthController`.
- Criar endpoint `/auth/external/google`.
- Criar endpoint `/auth/external/google/callback`.
- Integrar emissão de sessão/JWT interno.
- Integrar logout externo temporário.

### Fase 5 — Segurança

- Configurar cookies.
- Configurar forwarded headers.
- Configurar allowlist de returnUrl.
- Adicionar rate limit.
- Sanitizar logs.
- Adicionar auditoria.

### Fase 6 — Produção

- Configurar domínio.
- Configurar HTTPS.
- Configurar reverse proxy.
- Configurar secrets.
- Configurar OAuth Client produção.
- Validar OAuth Consent Screen.
- Testar callback público.
- Validar cookies.
- Validar observabilidade.

---

## 31. Estrutura sugerida no projeto

```text
AuthCore.Api
 ├── Controllers
 │    └── ExternalAuthController.cs
 ├── Configuration
 │    └── AuthenticationConfiguration.cs
 ├── Contracts
 │    └── Auth
 │         └── ExternalLoginResponseJson.cs

AuthCore.Application
 ├── Auth
 │    └── ExternalLogin
 │         ├── CompleteGoogleLoginUseCase.cs
 │         ├── CompleteGoogleLoginCommand.cs
 │         ├── CompleteGoogleLoginResult.cs
 │         ├── LinkExternalLoginUseCase.cs
 │         └── UnlinkExternalLoginUseCase.cs
 ├── Abstractions
 │    └── Repositories
 │         └── IExternalLoginRepository.cs

AuthCore.Domain
 ├── Users
 │    ├── User.cs
 │    ├── UserStatus.cs
 │    └── ExternalLogin.cs
 ├── Auth
 │    └── ExternalLoginProvider.cs

AuthCore.Infrastructure
 ├── Persistence
 │    └── PostgreSql
 │         └── Repositories
 │              └── ExternalLoginRepository.cs
 ├── Migrations
 │    └── CreateExternalLoginsTable.sql
```

---

## 32. Definition of Done

A entrega estará pronta quando:

- Login com Google funcionar em development.
- Login com Google funcionar em homolog.
- Configuração de produção estiver documentada.
- `ExternalLogin` estiver persistido.
- Usuário interno for criado/vinculado corretamente.
- Sessão/JWT interno for emitido.
- `ClientSecret` estiver fora do repositório.
- `returnUrl` estiver protegido.
- Callback funcionar atrás do Gateway/proxy.
- Logs não expuserem tokens/secrets.
- Testes unitários forem criados.
- Testes de integração forem criados.
- Checklist manual for validado.
- Documentação for atualizada.

---

## 33. Decisão final recomendada

Para o AuthCore, implementar:

```text
Google como ExternalLoginProvider
Escopos mínimos: openid, profile, email
Sem armazenar token Google
Sem Google Cloud Identity Platform inicialmente
AuthCore continua emitindo sessão/JWT próprio
Usuário novo com email_verified=true pode ser criado automaticamente
Usuário incompleto vai para PendingOnboarding
Produção com domínio, HTTPS, privacy policy, terms, redirect URI fixo e secrets seguros
```

Essa abordagem mantém o boilerplate maduro, extensível e coerente com Clean Architecture/DDD.

O Google fica na borda como provedor externo. O domínio do AuthCore continua dono das decisões importantes.

---

## 34. Referências oficiais e materiais consultados

- Google — OpenID Connect: https://developers.google.com/identity/openid-connect/openid-connect
- Google — OAuth 2.0 Scopes for Google APIs: https://developers.google.com/identity/protocols/oauth2/scopes
- Google — OAuth 2.0 for Web Server Applications: https://developers.google.com/identity/protocols/oauth2/web-server
- Google — OAuth app brand verification: https://developers.google.com/identity/protocols/oauth2/production-readiness/brand-verification
- Microsoft — Google external login setup in ASP.NET Core: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/google-logins?view=aspnetcore-10.0
- Microsoft — ASP.NET Core Identity overview: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0
- Paper — Mitigating CSRF attacks on OAuth 2.0 and OpenID Connect: https://arxiv.org/abs/1801.07983
