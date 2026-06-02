# AuthCore — Modelagem DDD para Autenticação Híbrida

**Tema:** Modelagem de domínio para autenticação híbrida com sessão server-side + JWT curto  
**Contexto:** AuthCore em Clean Architecture + DDD  
**Base conceitual:** Domain-Driven Design, Eric Evans; Learning Domain-Driven Design, Vlad Khononov; documentação interna de autenticação híbrida do AuthCore  
**Data:** 2026-06-01

---

## 1. Objetivo

Este documento propõe uma modelagem de classes para o AuthCore usando princípios de Domain-Driven Design.

O foco não é definir endpoints, controllers, cookies, JWT ou detalhes de infraestrutura. O foco é separar corretamente:

- conceitos de domínio;
- aggregates;
- value objects;
- eventos de domínio;
- casos de uso;
- responsabilidades por camada;
- trade-offs de modelagem.

A autenticação híbrida analisada combina:

- sessão server-side como fonte de verdade;
- JWT curto para performance nas requisições comuns;
- cookies HttpOnly para transporte no browser/PWA;
- Redis para dados efêmeros;
- banco de dados como fonte durável das sessões.

---

## 2. Decisão principal de modelagem

A modelagem **não deve girar em torno de uma entidade genérica chamada `Auth`**.

`Auth` pode ser usado como agrupamento externo, por exemplo:

```text
/auth/login
/auth/logout
/auth/refresh
```

Porém, em DDD, endpoint não deve virar modelo de domínio.

O domínio deve ser modelado a partir dos conceitos e invariantes reais:

```text
Conta
Sessão
Credencial
Verificação
Revogação
Política de segurança
Emissão de acesso
```

Portanto, a sugestão é usar um bounded context/módulo com nome mais explícito, como:

```text
IdentityAccess
```

ou:

```text
AccessManagement
```

Dentro dele, os conceitos principais seriam:

```text
IdentityAccess
  Accounts
  Sessions
  Verification
  Authorization
```

---

## 3. Por que não modelar tudo como `Auth`

Uma classe `Auth` tenderia a virar um objeto genérico com responsabilidades demais:

```text
Auth.Login()
Auth.Refresh()
Auth.Logout()
Auth.VerifyEmail()
Auth.RequestPasswordRecovery()
Auth.RevokeSession()
Auth.GenerateJwt()
Auth.WriteCookie()
Auth.ValidatePassword()
```

Esse desenho mistura:

- regra de negócio;
- orquestração de caso de uso;
- detalhe de infraestrutura;
- detalhe HTTP;
- detalhe criptográfico;
- persistência;
- emissão de token;
- escrita de cookie.

Isso gera um modelo anêmico ou um “god service”.

Em DDD, o ideal é que cada conceito relevante tenha nome, responsabilidade e invariantes próprios.

---

## 4. Linguagem ubíqua sugerida

| Termo | Significado no AuthCore |
|---|---|
| Account | Conta autenticável no AuthCore |
| Credential | Meio de autenticação da conta, como senha |
| PasswordHash | Hash da senha armazenada |
| AuthSession | Sessão autenticada de uma conta em um dispositivo/navegador |
| SessionIdentifier | Identificador opaco usado para referenciar a sessão |
| SecurityStamp | Versão de segurança usada para invalidar sessões/tokens antigos |
| VerificationChallenge | Desafio temporário de verificação |
| VerificationCode | Código informado pelo usuário para cumprir um desafio |
| AccessToken | Token curto usado para acessar APIs |
| Refresh/Renew | Renovação do access token a partir de uma sessão válida |
| Revocation | Ato de invalidar uma sessão |
| DeviceInfo | Informações do navegador/dispositivo associado à sessão |

---

## 5. Bounded Context sugerido

### Nome recomendado

```text
IdentityAccess
```

Esse nome é mais expressivo que `Auth`, porque separa dois problemas:

- identidade: quem é a conta;
- acesso: se a conta pode acessar o sistema naquele momento.

### Estrutura conceitual

```text
IdentityAccess
  Accounts
    Account
    Email
    PasswordHash
    SecurityStamp
    AccountStatus

  Sessions
    AuthSession
    SessionIdentifier
    SessionStatus
    SessionRevocationReason
    DeviceInfo
    IpAddress

  Verification
    VerificationChallenge
    VerificationPurpose
    VerificationCodeHash
    VerificationChallengeStatus

  Authorization
    Role
    Permission
    Policy
```

A parte de `Authorization` pode ser deixada para uma evolução futura se o AuthCore inicialmente estiver focado apenas em autenticação.

---

## 6. Aggregates principais

A modelagem sugerida possui três aggregates principais:

```text
Account
AuthSession
VerificationChallenge
```

Cada um representa um conjunto de regras e invariantes que precisa ser protegido.

---

# 7. Aggregate: Account

## 7.1. Responsabilidade

`Account` representa uma conta autenticável dentro do AuthCore.

Ela não deve saber se o usuário é vendedor, administrador comercial, cliente, lead owner ou qualquer papel específico de outro bounded context.

Em outros contextos, o mesmo identificador pode aparecer com outros modelos:

```text
CRM         -> Seller, ClientUser, Owner
Billing     -> CustomerAccount
AuthCore    -> Account
```

## 7.2. Responsabilidades do aggregate

```text
- registrar identidade;
- confirmar e-mail;
- trocar senha;
- bloquear/desbloquear conta;
- rotacionar security stamp;
- informar se a conta pode autenticar;
- publicar eventos de domínio relevantes.
```

## 7.3. O que não deve ficar em Account

```text
- emissão de JWT;
- escrita de cookie;
- validação de CORS;
- CSRF;
- acesso ao Redis;
- acesso direto ao banco;
- lógica HTTP;
- geração de código temporário.
```

## 7.4. Exemplo de classe

```csharp
/// <summary>
/// Representa uma conta autenticável dentro do contexto de identidade e acesso.
/// </summary>
public sealed class Account : AggregateRoot
{
    private Account(
        AccountId id,
        Email email,
        PasswordHash passwordHash,
        SecurityStamp securityStamp,
        AccountStatus status,
        DateTime createdAt)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        SecurityStamp = securityStamp;
        Status = status;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Identificador da conta.
    /// </summary>
    public AccountId Id { get; private set; }

    /// <summary>
    /// E-mail principal usado para autenticação.
    /// </summary>
    public Email Email { get; private set; }

    /// <summary>
    /// Hash da senha atual da conta.
    /// </summary>
    public PasswordHash PasswordHash { get; private set; }

    /// <summary>
    /// Versão de segurança usada para invalidar sessões e tokens antigos.
    /// </summary>
    public SecurityStamp SecurityStamp { get; private set; }

    /// <summary>
    /// Estado atual da conta.
    /// </summary>
    public AccountStatus Status { get; private set; }

    /// <summary>
    /// Data de criação da conta.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Cria uma nova conta aguardando confirmação de e-mail.
    /// </summary>
    public static Account Register(
        AccountId id,
        Email email,
        PasswordHash passwordHash,
        DateTime now)
    {
        var account = new Account(
            id,
            email,
            passwordHash,
            SecurityStamp.New(),
            AccountStatus.EmailVerificationPending,
            now);

        account.AddDomainEvent(new AccountRegisteredDomainEvent(id, email));

        return account;
    }

    /// <summary>
    /// Confirma o e-mail da conta e libera a autenticação.
    /// </summary>
    public void ConfirmEmail(DateTime now)
    {
        if (Status != AccountStatus.EmailVerificationPending)
            throw new DomainException("A conta não está aguardando confirmação de e-mail.");

        Status = AccountStatus.Active;

        AddDomainEvent(new AccountEmailConfirmedDomainEvent(Id, Email, now));
    }

    /// <summary>
    /// Altera a senha e rotaciona a versão de segurança da conta.
    /// </summary>
    public void ChangePassword(PasswordHash newPasswordHash, DateTime now)
    {
        if (Status != AccountStatus.Active)
            throw new DomainException("A conta precisa estar ativa para alterar a senha.");

        PasswordHash = newPasswordHash;
        SecurityStamp = SecurityStamp.New();

        AddDomainEvent(new AccountPasswordChangedDomainEvent(Id, now));
    }

    /// <summary>
    /// Verifica se a conta pode iniciar uma nova sessão autenticada.
    /// </summary>
    public void EnsureCanSignIn()
    {
        if (Status != AccountStatus.Active)
            throw new DomainException("A conta não está habilitada para autenticação.");
    }
}
```

## 7.5. Estados possíveis da conta

```csharp
/// <summary>
/// Representa o estado de uma conta autenticável.
/// </summary>
public enum AccountStatus
{
    /// <summary>
    /// Conta aguardando confirmação de e-mail.
    /// </summary>
    EmailVerificationPending = 1,

    /// <summary>
    /// Conta ativa e apta para autenticação.
    /// </summary>
    Active = 2,

    /// <summary>
    /// Conta bloqueada por decisão administrativa ou regra de segurança.
    /// </summary>
    Blocked = 3,

    /// <summary>
    /// Conta encerrada.
    /// </summary>
    Closed = 4
}
```

## 7.6. Trade-offs

### Usar `Account` em vez de `User`

Vantagens:

- reduz ambiguidade com usuários de outros contextos;
- deixa claro que o AuthCore gerencia autenticação;
- evita misturar papel de negócio com identidade técnica.

Desvantagens:

- alguns devs esperam encontrar `User` em sistemas de autenticação;
- pode exigir uma explicação inicial no README/glossário.

### Manter senha dentro de Account

Vantagens:

- modelo simples;
- troca de senha fica junto da conta;
- security stamp fica bem conectado com a credencial.

Desvantagens:

- se houver muitos métodos de login, como senha, Google, Meta, SSO e passkey, pode ser melhor extrair um aggregate ou entidade `Credential`.

---

# 8. Aggregate: AuthSession

## 8.1. Responsabilidade

`AuthSession` representa uma sessão autenticada de uma conta.

No fluxo híbrido, ela é o conceito mais importante, porque é a fonte da verdade para:

```text
- login ativo;
- logout imediato;
- revogação por dispositivo;
- logout de todos os dispositivos;
- expiração longa;
- rastreabilidade;
- auditoria;
- emissão de access token curto;
- invalidação por security stamp.
```

## 8.2. Regra central

A sessão responde à pergunta:

```text
Esta conta ainda possui uma sessão válida para emitir ou renovar acesso?
```

## 8.3. O que pertence à sessão

```text
- AccountId;
- SessionIdentifier;
- Status;
- CreatedAt;
- ExpiresAt;
- LastSeenAt;
- RevokedAt;
- RevocationReason;
- DeviceInfo;
- IpAddress;
- SecurityStamp no momento do login.
```

## 8.4. O que não pertence à sessão

```text
- valor bruto do cookie;
- JWT serializado;
- algoritmo de assinatura;
- configuração SameSite;
- validação de CORS;
- ClaimsPrincipal do ASP.NET Core.
```

## 8.5. Exemplo de classe

```csharp
/// <summary>
/// Representa uma sessão autenticada de uma conta em um dispositivo ou navegador.
/// </summary>
public sealed class AuthSession : AggregateRoot
{
    private AuthSession(
        AuthSessionId id,
        AccountId accountId,
        SessionIdentifier identifier,
        SecurityStamp securityStamp,
        DeviceInfo deviceInfo,
        IpAddress ipAddress,
        DateTime createdAt,
        DateTime expiresAt)
    {
        Id = id;
        AccountId = accountId;
        Identifier = identifier;
        SecurityStamp = securityStamp;
        DeviceInfo = deviceInfo;
        IpAddress = ipAddress;
        Status = SessionStatus.Active;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        LastSeenAt = createdAt;
    }

    /// <summary>
    /// Identificador interno da sessão.
    /// </summary>
    public AuthSessionId Id { get; private set; }

    /// <summary>
    /// Conta dona da sessão.
    /// </summary>
    public AccountId AccountId { get; private set; }

    /// <summary>
    /// Identificador opaco usado pelo cliente para referenciar a sessão.
    /// </summary>
    public SessionIdentifier Identifier { get; private set; }

    /// <summary>
    /// Versão de segurança da conta no momento da criação da sessão.
    /// </summary>
    public SecurityStamp SecurityStamp { get; private set; }

    /// <summary>
    /// Informações do dispositivo/navegador associado à sessão.
    /// </summary>
    public DeviceInfo DeviceInfo { get; private set; }

    /// <summary>
    /// Endereço IP usado na criação da sessão.
    /// </summary>
    public IpAddress IpAddress { get; private set; }

    /// <summary>
    /// Estado atual da sessão.
    /// </summary>
    public SessionStatus Status { get; private set; }

    /// <summary>
    /// Data de criação da sessão.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Data de expiração natural da sessão.
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    /// Último momento em que a sessão foi vista.
    /// </summary>
    public DateTime LastSeenAt { get; private set; }

    /// <summary>
    /// Data de revogação da sessão.
    /// </summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>
    /// Motivo da revogação da sessão.
    /// </summary>
    public SessionRevocationReason? RevocationReason { get; private set; }

    /// <summary>
    /// Cria uma nova sessão autenticada para uma conta.
    /// </summary>
    public static AuthSession Start(
        AuthSessionId id,
        Account account,
        SessionIdentifier identifier,
        DeviceInfo deviceInfo,
        IpAddress ipAddress,
        DateTime now,
        DateTime expiresAt)
    {
        account.EnsureCanSignIn();

        var session = new AuthSession(
            id,
            account.Id,
            identifier,
            account.SecurityStamp,
            deviceInfo,
            ipAddress,
            now,
            expiresAt);

        session.AddDomainEvent(new AuthSessionStartedDomainEvent(id, account.Id, now));

        return session;
    }

    /// <summary>
    /// Verifica se a sessão pode emitir um novo token de acesso.
    /// </summary>
    public void EnsureCanIssueAccessToken(SecurityStamp currentSecurityStamp, DateTime now)
    {
        if (Status != SessionStatus.Active)
            throw new DomainException("A sessão não está ativa.");

        if (ExpiresAt <= now)
            throw new DomainException("A sessão está expirada.");

        if (SecurityStamp != currentSecurityStamp)
            throw new DomainException("A sessão foi invalidada por alteração de segurança.");
    }

    /// <summary>
    /// Revoga a sessão.
    /// </summary>
    public void Revoke(SessionRevocationReason reason, DateTime now)
    {
        if (Status == SessionStatus.Revoked)
            return;

        Status = SessionStatus.Revoked;
        RevokedAt = now;
        RevocationReason = reason;

        AddDomainEvent(new AuthSessionRevokedDomainEvent(Id, AccountId, reason, now));
    }

    /// <summary>
    /// Atualiza o último momento de uso da sessão respeitando uma janela mínima.
    /// </summary>
    public void MarkAsSeen(DateTime now, TimeSpan minimumUpdateInterval)
    {
        if (now - LastSeenAt < minimumUpdateInterval)
            return;

        LastSeenAt = now;
    }
}
```

## 8.6. Estados da sessão

```csharp
/// <summary>
/// Representa o estado de uma sessão autenticada.
/// </summary>
public enum SessionStatus
{
    /// <summary>
    /// Sessão ativa.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Sessão expirada naturalmente.
    /// </summary>
    Expired = 2,

    /// <summary>
    /// Sessão revogada explicitamente.
    /// </summary>
    Revoked = 3,

    /// <summary>
    /// Sessão substituída por uma nova sessão.
    /// </summary>
    Replaced = 4,

    /// <summary>
    /// Sessão marcada como suspeita.
    /// </summary>
    Suspicious = 5
}
```

## 8.7. Motivos de revogação

```csharp
/// <summary>
/// Representa o motivo de revogação de uma sessão.
/// </summary>
public enum SessionRevocationReason
{
    /// <summary>
    /// Logout solicitado pelo próprio usuário.
    /// </summary>
    UserLogout = 1,

    /// <summary>
    /// Revogação de uma sessão específica pelo usuário.
    /// </summary>
    UserRevokedDevice = 2,

    /// <summary>
    /// Revogação causada por alteração de senha.
    /// </summary>
    PasswordChanged = 3,

    /// <summary>
    /// Revogação administrativa.
    /// </summary>
    AdministrativeAction = 4,

    /// <summary>
    /// Revogação causada por suspeita de risco.
    /// </summary>
    SecurityRisk = 5
}
```

## 8.8. Trade-offs

### Sessão como aggregate separado

Vantagens:

- controla melhor múltiplos dispositivos;
- permite revogação granular;
- facilita auditoria;
- evita inflar `Account`;
- combina bem com persistência própria e índices por sessão.

Desvantagens:

- exige repositório próprio;
- exige coordenação entre `Account` e `AuthSession`;
- alguns fluxos podem precisar de transação entre conta e sessão.

### Sessão dentro de Account

Vantagens:

- modelo inicial mais simples;
- regras de sessão próximas da conta.

Desvantagens:

- uma conta com muitas sessões pode ficar pesada;
- listar/revogar sessões por dispositivo vira mais custoso;
- aggregate cresce demais;
- dificulta escalar persistência e consultas.

Para o AuthCore, a recomendação é manter `AuthSession` como aggregate separado.

---

# 9. Aggregate: VerificationChallenge

## 9.1. Responsabilidade

`VerificationChallenge` representa um desafio temporário de verificação.

Esse conceito é mais rico que apenas `VerificationCode`.

Um código é só o valor informado pelo usuário. O desafio representa o processo:

```text
Foi solicitado um desafio de verificação para um e-mail, com uma finalidade, uma expiração e um estado.
```

## 9.2. Possíveis finalidades

```text
- confirmação de e-mail;
- primeiro acesso;
- recuperação de senha;
- MFA/OTP;
- confirmação de alteração sensível.
```

## 9.3. Exemplo de classe

```csharp
/// <summary>
/// Representa um desafio temporário de verificação, como confirmação de e-mail ou recuperação de senha.
/// </summary>
public sealed class VerificationChallenge : AggregateRoot
{
    private VerificationChallenge(
        VerificationChallengeId id,
        Email email,
        VerificationPurpose purpose,
        VerificationCodeHash codeHash,
        DateTime createdAt,
        DateTime expiresAt)
    {
        Id = id;
        Email = email;
        Purpose = purpose;
        CodeHash = codeHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        Status = VerificationChallengeStatus.Pending;
    }

    /// <summary>
    /// Identificador do desafio.
    /// </summary>
    public VerificationChallengeId Id { get; private set; }

    /// <summary>
    /// E-mail associado ao desafio.
    /// </summary>
    public Email Email { get; private set; }

    /// <summary>
    /// Finalidade do desafio.
    /// </summary>
    public VerificationPurpose Purpose { get; private set; }

    /// <summary>
    /// Hash do código de verificação.
    /// </summary>
    public VerificationCodeHash CodeHash { get; private set; }

    /// <summary>
    /// Estado atual do desafio.
    /// </summary>
    public VerificationChallengeStatus Status { get; private set; }

    /// <summary>
    /// Data de criação.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Data de expiração.
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    /// Cria um novo desafio de verificação.
    /// </summary>
    public static VerificationChallenge Request(
        VerificationChallengeId id,
        Email email,
        VerificationPurpose purpose,
        VerificationCodeHash codeHash,
        DateTime now,
        DateTime expiresAt)
    {
        if (expiresAt <= now)
            throw new DomainException("A expiração do desafio deve ser futura.");

        var challenge = new VerificationChallenge(id, email, purpose, codeHash, now, expiresAt);

        challenge.AddDomainEvent(new VerificationChallengeRequestedDomainEvent(id, email, purpose, now));

        return challenge;
    }

    /// <summary>
    /// Confirma o desafio.
    /// </summary>
    public void Confirm(DateTime now)
    {
        if (Status != VerificationChallengeStatus.Pending)
            throw new DomainException("O desafio de verificação não está pendente.");

        if (ExpiresAt <= now)
            throw new DomainException("O desafio de verificação está expirado.");

        Status = VerificationChallengeStatus.Confirmed;

        AddDomainEvent(new VerificationChallengeConfirmedDomainEvent(Id, Email, Purpose, now));
    }
}
```

## 9.4. Finalidades

```csharp
/// <summary>
/// Representa a finalidade de um desafio de verificação.
/// </summary>
public enum VerificationPurpose
{
    /// <summary>
    /// Confirmação de e-mail.
    /// </summary>
    EmailVerification = 1,

    /// <summary>
    /// Primeiro acesso.
    /// </summary>
    FirstAccess = 2,

    /// <summary>
    /// Recuperação de senha.
    /// </summary>
    PasswordRecovery = 3,

    /// <summary>
    /// Segundo fator de autenticação.
    /// </summary>
    MultiFactorAuthentication = 4
}
```

## 9.5. Trade-offs

### Persistir VerificationChallenge no banco

Vantagens:

- histórico;
- auditoria;
- controle de tentativas;
- análise de risco;
- antifraude;
- suporte a fluxos sensíveis.

Desvantagens:

- mais complexidade;
- exige limpeza de registros antigos;
- pode ser excesso para códigos simples e curtos.

### Manter códigos somente no Redis

Vantagens:

- simples;
- rápido;
- TTL nativo;
- bom para dados efêmeros;
- menor custo de persistência.

Desvantagens:

- menor rastreabilidade;
- auditoria limitada;
- perda de dados caso Redis falhe, dependendo da configuração.

Recomendação pragmática:

```text
Para o MVP:
  - Redis para códigos temporários.
  - Conceito de VerificationChallenge pode existir na Application/Domain sem necessariamente virar tabela durável.

Para evolução:
  - Persistir VerificationChallenge se houver MFA, antifraude, auditoria ou segurança mais avançada.
```

---

# 10. Value Objects sugeridos

## 10.1. Email

```csharp
/// <summary>
/// Representa um endereço de e-mail válido.
/// </summary>
public sealed record Email
{
    /// <summary>
    /// Valor normalizado do e-mail.
    /// </summary>
    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Cria um e-mail válido e normalizado.
    /// </summary>
    public static Email Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("O e-mail é obrigatório.");

        var normalized = value.Trim().ToLowerInvariant();

        if (!normalized.Contains('@'))
            throw new DomainException("O e-mail informado é inválido.");

        return new Email(normalized);
    }

    /// <summary>
    /// Retorna o valor textual do e-mail.
    /// </summary>
    public override string ToString() => Value;
}
```

## 10.2. SecurityStamp

```csharp
/// <summary>
/// Representa a versão de segurança de uma conta.
/// </summary>
public sealed record SecurityStamp
{
    /// <summary>
    /// Valor da versão de segurança.
    /// </summary>
    public string Value { get; }

    private SecurityStamp(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Cria uma nova versão de segurança.
    /// </summary>
    public static SecurityStamp New()
    {
        return new SecurityStamp(Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Restaura uma versão de segurança existente.
    /// </summary>
    public static SecurityStamp Restore(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("O security stamp é obrigatório.");

        return new SecurityStamp(value);
    }
}
```

## 10.3. SessionIdentifier

```csharp
/// <summary>
/// Representa um identificador opaco de sessão.
/// </summary>
public sealed record SessionIdentifier
{
    /// <summary>
    /// Valor do identificador.
    /// </summary>
    public string Value { get; }

    private SessionIdentifier(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Gera um novo identificador opaco de sessão.
    /// </summary>
    public static SessionIdentifier New()
    {
        return new SessionIdentifier(Convert.ToBase64String(Guid.NewGuid().ToByteArray()));
    }

    /// <summary>
    /// Restaura um identificador existente.
    /// </summary>
    public static SessionIdentifier Restore(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("O identificador da sessão é obrigatório.");

        return new SessionIdentifier(value);
    }
}
```

> Observação: em produção, prefira um gerador criptograficamente seguro para o valor da sessão, em vez de `Guid`.

## 10.4. DeviceInfo

```csharp
/// <summary>
/// Representa informações do dispositivo ou navegador associado a uma sessão.
/// </summary>
public sealed record DeviceInfo
{
    /// <summary>
    /// Nome amigável do dispositivo.
    /// </summary>
    public string DeviceName { get; }

    /// <summary>
    /// User-Agent informado pelo cliente.
    /// </summary>
    public string UserAgent { get; }

    private DeviceInfo(string deviceName, string userAgent)
    {
        DeviceName = deviceName;
        UserAgent = userAgent;
    }

    /// <summary>
    /// Cria as informações do dispositivo.
    /// </summary>
    public static DeviceInfo Create(string? deviceName, string? userAgent)
    {
        var safeDeviceName = string.IsNullOrWhiteSpace(deviceName)
            ? "Dispositivo desconhecido"
            : deviceName.Trim();

        var safeUserAgent = string.IsNullOrWhiteSpace(userAgent)
            ? "User-Agent não informado"
            : userAgent.Trim();

        return new DeviceInfo(safeDeviceName, safeUserAgent);
    }
}
```

---

# 11. Eventos de domínio sugeridos

## 11.1. Account

```text
AccountRegisteredDomainEvent
AccountEmailConfirmedDomainEvent
AccountPasswordChangedDomainEvent
AccountBlockedDomainEvent
```

## 11.2. Session

```text
AuthSessionStartedDomainEvent
AuthSessionRevokedDomainEvent
AuthSessionMarkedAsSuspiciousDomainEvent
```

## 11.3. Verification

```text
VerificationChallengeRequestedDomainEvent
VerificationChallengeConfirmedDomainEvent
VerificationChallengeExpiredDomainEvent
```

## 11.4. Cuidados

Eventos de domínio não devem executar infraestrutura diretamente.

Exemplo: `AccountRegisteredDomainEvent` não envia e-mail diretamente. Ele apenas expressa que uma conta foi registrada.

A Application ou Infrastructure decide como reagir:

```text
AccountRegisteredDomainEvent
  -> Outbox
  -> Message broker
  -> NotificationCore
  -> envio de e-mail
```

---

# 12. Casos de uso por endpoint

Os endpoints são detalhes de API. Eles devem acionar use cases.

## 12.1. Autenticação browser/session

```text
POST /auth/login
  -> LoginUseCase

POST /auth/refresh
  -> RefreshSessionAccessTokenUseCase

POST /auth/logout
  -> LogoutUseCase

POST /auth/logout-all
  -> LogoutAllSessionsUseCase

GET /auth/me
  -> GetCurrentAccountQuery

GET /auth/sessions
  -> ListAccountSessionsQuery

DELETE /auth/sessions/{sessionId}
  -> RevokeSessionUseCase
```

## 12.2. Fluxos de código temporário

```text
POST /auth/first-access/request-code
  -> RequestFirstAccessCodeUseCase

POST /auth/first-access/confirm
  -> ConfirmFirstAccessCodeUseCase

POST /auth/forgot-password/request-code
  -> RequestForgotPasswordCodeUseCase

POST /auth/forgot-password/confirm
  -> ConfirmForgotPasswordCodeUseCase

POST /auth/email-verification/request-code
  -> RequestEmailVerificationCodeUseCase

POST /auth/email-verification/confirm
  -> ConfirmEmailVerificationCodeUseCase
```

## 12.3. API/mobile/integrações

```text
POST /token/login
  -> TokenLoginUseCase

POST /token/refresh
  -> RefreshIntegrationTokenUseCase

POST /token/revoke
  -> RevokeIntegrationTokenUseCase
```

## 12.4. Observação importante

O fato de os endpoints estarem em `/auth` não significa que o domínio deva ter uma entidade `Auth`.

`/auth` é uma convenção HTTP.

A modelagem deve preservar conceitos ricos:

```text
Account
AuthSession
VerificationChallenge
SecurityStamp
SessionIdentifier
```

---

# 13. Fluxo de login modelado

```text
POST /auth/login
  -> LoginUseCase
     -> busca Account por Email
     -> valida senha via IPasswordHasher
     -> Account.EnsureCanSignIn()
     -> cria SessionIdentifier
     -> AuthSession.Start()
     -> salva AuthSession
     -> emite access token via IAccessTokenIssuer
     -> escreve cookies via IAuthCookieWriter
```

## 13.1. Responsabilidade por camada

### Domain

```text
Account.EnsureCanSignIn()
AuthSession.Start()
```

### Application

```text
LoginUseCase orquestra o fluxo.
```

### Infrastructure

```text
PasswordHasher
JwtAccessTokenIssuer
PostgresAuthSessionRepository
AspNetCoreAuthCookieWriter
```

### Api

```text
AuthController recebe request e retorna response.
```

---

# 14. Fluxo de refresh modelado

```text
POST /auth/refresh
  -> RefreshSessionAccessTokenUseCase
     -> lê SessionIdentifier informado pela API
     -> busca AuthSession
     -> busca Account ou SecurityStamp atual
     -> AuthSession.EnsureCanIssueAccessToken()
     -> emite novo access token
     -> atualiza LastSeenAt com throttling
     -> escreve novo cookie de access token
```

## 14.1. Regra de negócio

A sessão só pode emitir um novo access token se:

```text
- existe;
- está ativa;
- não expirou;
- não foi revogada;
- o security stamp ainda é compatível;
- a conta ainda está apta.
```

---

# 15. Fluxo de logout modelado

```text
POST /auth/logout
  -> LogoutUseCase
     -> busca AuthSession
     -> AuthSession.Revoke(UserLogout)
     -> salva alteração
     -> limpa cookies
```

## 15.1. Regras

```text
- logout revoga a sessão principal;
- novos JWTs deixam de ser emitidos;
- JWTs já emitidos continuam válidos até expirar;
- para rotas sensíveis, pode-se exigir validação ativa da sessão.
```

---

# 16. Fluxo de troca de senha

```text
POST /auth/forgot-password/confirm
  -> ConfirmForgotPasswordCodeUseCase
     -> valida VerificationChallenge
     -> busca Account
     -> Account.ChangePassword()
     -> revoga sessões ativas
     -> salva alterações
```

## 16.1. Decisão importante

Ao trocar senha, a recomendação é revogar todas as sessões ativas.

Isso pode ser feito de duas formas:

### Opção A: Application revoga sessões

```text
ConfirmForgotPasswordCodeUseCase
  -> Account.ChangePassword()
  -> AuthSessionRepository.RevokeAllByAccountId()
```

### Opção B: evento de domínio dispara revogação

```text
AccountPasswordChangedDomainEvent
  -> handler de aplicação
  -> revoga sessões
```

Para consistência imediata, prefira a opção A no mesmo fluxo transacional.

Para desacoplamento maior, pode usar evento + outbox, mas deve avaliar se aceita consistência eventual.

---

# 17. Application Services e contratos

## 17.1. Interfaces recomendadas

```csharp
/// <summary>
/// Repositório de contas autenticáveis.
/// </summary>
public interface IAccountRepository
{
    /// <summary>
    /// Busca uma conta por e-mail.
    /// </summary>
    Task<Account?> FindByEmailAsync(Email email, CancellationToken cancellationToken);

    /// <summary>
    /// Busca uma conta por identificador.
    /// </summary>
    Task<Account?> FindByIdAsync(AccountId accountId, CancellationToken cancellationToken);

    /// <summary>
    /// Adiciona uma nova conta.
    /// </summary>
    Task AddAsync(Account account, CancellationToken cancellationToken);

    /// <summary>
    /// Atualiza uma conta existente.
    /// </summary>
    Task UpdateAsync(Account account, CancellationToken cancellationToken);
}
```

```csharp
/// <summary>
/// Repositório de sessões autenticadas.
/// </summary>
public interface IAuthSessionRepository
{
    /// <summary>
    /// Busca uma sessão pelo identificador opaco.
    /// </summary>
    Task<AuthSession?> FindByIdentifierAsync(
        SessionIdentifier identifier,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adiciona uma nova sessão.
    /// </summary>
    Task AddAsync(AuthSession session, CancellationToken cancellationToken);

    /// <summary>
    /// Atualiza uma sessão existente.
    /// </summary>
    Task UpdateAsync(AuthSession session, CancellationToken cancellationToken);

    /// <summary>
    /// Revoga todas as sessões ativas de uma conta.
    /// </summary>
    Task RevokeActiveSessionsByAccountIdAsync(
        AccountId accountId,
        SessionRevocationReason reason,
        DateTime now,
        CancellationToken cancellationToken);
}
```

```csharp
/// <summary>
/// Emissor de tokens de acesso.
/// </summary>
public interface IAccessTokenIssuer
{
    /// <summary>
    /// Emite um token de acesso curto para uma sessão autenticada.
    /// </summary>
    AccessToken Issue(Account account, AuthSession session, DateTime now);
}
```

```csharp
/// <summary>
/// Serviço responsável pela escrita e remoção de cookies de autenticação.
/// </summary>
public interface IAuthCookieWriter
{
    /// <summary>
    /// Escreve os cookies de autenticação.
    /// </summary>
    void WriteAuthenticationCookies(SessionIdentifier sessionIdentifier, AccessToken accessToken);

    /// <summary>
    /// Escreve apenas o cookie do access token.
    /// </summary>
    void WriteAccessTokenCookie(AccessToken accessToken);

    /// <summary>
    /// Remove os cookies de autenticação.
    /// </summary>
    void ClearAuthenticationCookies();
}
```

```csharp
/// <summary>
/// Serviço de armazenamento de códigos temporários de verificação.
/// </summary>
public interface IVerificationCodeStore
{
    /// <summary>
    /// Armazena um código temporário de verificação.
    /// </summary>
    Task StoreAsync(
        VerificationPurpose purpose,
        Email email,
        VerificationCodeHash codeHash,
        TimeSpan ttl,
        CancellationToken cancellationToken);

    /// <summary>
    /// Valida um código temporário de verificação.
    /// </summary>
    Task<bool> ValidateAsync(
        VerificationPurpose purpose,
        Email email,
        string providedCode,
        CancellationToken cancellationToken);
}
```

---

# 18. O que fica em cada camada

## 18.1. Domain

```text
AuthCore.Domain
└── IdentityAccess
    ├── Accounts
    │   ├── Account.cs
    │   ├── AccountId.cs
    │   ├── AccountStatus.cs
    │   ├── Email.cs
    │   ├── PasswordHash.cs
    │   ├── SecurityStamp.cs
    │   └── Events
    │       ├── AccountRegisteredDomainEvent.cs
    │       ├── AccountEmailConfirmedDomainEvent.cs
    │       └── AccountPasswordChangedDomainEvent.cs
    │
    ├── Sessions
    │   ├── AuthSession.cs
    │   ├── AuthSessionId.cs
    │   ├── SessionIdentifier.cs
    │   ├── SessionStatus.cs
    │   ├── SessionRevocationReason.cs
    │   ├── DeviceInfo.cs
    │   ├── IpAddress.cs
    │   └── Events
    │       ├── AuthSessionStartedDomainEvent.cs
    │       └── AuthSessionRevokedDomainEvent.cs
    │
    ├── Verification
    │   ├── VerificationChallenge.cs
    │   ├── VerificationChallengeId.cs
    │   ├── VerificationPurpose.cs
    │   ├── VerificationCodeHash.cs
    │   └── Events
    │       ├── VerificationChallengeRequestedDomainEvent.cs
    │       └── VerificationChallengeConfirmedDomainEvent.cs
    │
    └── Repositories
        ├── IAccountRepository.cs
        ├── IAuthSessionRepository.cs
        └── IVerificationChallengeRepository.cs
```

## 18.2. Application

```text
AuthCore.Application
└── IdentityAccess
    ├── Authentication
    │   ├── Login
    │   ├── RefreshSessionAccessToken
    │   └── Logout
    │
    ├── Sessions
    │   ├── ListAccountSessions
    │   ├── RevokeSession
    │   └── LogoutAllSessions
    │
    ├── EmailVerification
    │   ├── RequestEmailVerificationCode
    │   └── ConfirmEmailVerificationCode
    │
    ├── PasswordRecovery
    │   ├── RequestForgotPasswordCode
    │   └── ConfirmForgotPasswordCode
    │
    └── Abstractions
        ├── IAccessTokenIssuer.cs
        ├── IAuthCookieWriter.cs
        ├── IVerificationCodeStore.cs
        ├── IPasswordHasher.cs
        └── IClock.cs
```

## 18.3. Infrastructure

```text
AuthCore.Infrastructure
├── Persistence
│   ├── Accounts
│   ├── Sessions
│   └── Verification
│
├── Authentication
│   ├── JwtAccessTokenIssuer.cs
│   ├── EcdsaSigningKeyProvider.cs
│   └── ClaimsFactory.cs
│
├── Cookies
│   └── AspNetCoreAuthCookieWriter.cs
│
├── Caching
│   ├── RedisVerificationCodeStore.cs
│   └── RedisSessionCache.cs
│
└── Security
    └── PasswordHasher.cs
```

## 18.4. Api

```text
AuthCore.Api
└── Controllers
    ├── AuthController.cs
    ├── SessionsController.cs
    ├── EmailVerificationController.cs
    └── PasswordRecoveryController.cs
```

---

# 19. Controllers sugeridos

## 19.1. AuthController

Responsável pelos fluxos principais de autenticação browser/session:

```text
POST /auth/login
POST /auth/refresh
POST /auth/logout
POST /auth/logout-all
GET  /auth/me
```

## 19.2. SessionsController

Responsável pela gestão de sessões/dispositivos:

```text
GET    /auth/sessions
DELETE /auth/sessions/{sessionId}
```

## 19.3. EmailVerificationController

Responsável pela confirmação de e-mail:

```text
POST /auth/email-verification/request-code
POST /auth/email-verification/confirm
```

## 19.4. PasswordRecoveryController

Responsável por recuperação de senha:

```text
POST /auth/forgot-password/request-code
POST /auth/forgot-password/confirm
```

## 19.5. TokenController

Opcional para mobile, API externa ou integrações:

```text
POST /token/login
POST /token/refresh
POST /token/revoke
```

Esse controller deve ser separado do fluxo browser para não misturar decisões de segurança diferentes.

---

# 20. JWT, cookies e Redis não são domínio

## 20.1. JWT

JWT é formato técnico de representação do access token.

No domínio, não modele:

```text
JwtToken
JwtClaims
JwtHeader
JwtPayload
JwtSignature
```

Prefira o domínio falar em:

```text
AccessToken
Sessão ativa
Sessão revogada
SecurityStamp inválido
Conta bloqueada
```

A emissão concreta fica na Infrastructure:

```text
JwtAccessTokenIssuer
```

## 20.2. Cookies

Cookie é transporte HTTP.

Não deve estar no Domain:

```text
HttpOnly
SameSite
Secure
CookieOptions
Set-Cookie
```

Esses detalhes ficam na Api ou Infrastructure.

## 20.3. Redis

Redis é mecanismo de armazenamento/cache.

Não deve estar no Domain:

```text
RedisVerificationCode
RedisSessionCache
RedisKey
TTL
```

O Domain pode conhecer expiração de desafio/sessão, mas não deve saber que isso será implementado com Redis.

---

# 21. Modelagem recomendada para dados persistidos

## 21.1. Account

```text
accounts
  id
  email
  password_hash
  security_stamp
  status
  created_at
  updated_at
```

Índices:

```text
email unique
status
```

## 21.2. AuthSession

```text
auth_sessions
  id
  account_id
  session_identifier_hash
  status
  security_stamp
  device_name
  user_agent
  ip_address
  created_at
  expires_at
  last_seen_at
  revoked_at
  revocation_reason
```

Índices:

```text
session_identifier_hash unique
account_id + status
expires_at
status + expires_at
account_id + created_at
```

## 21.3. VerificationChallenge

Se persistido:

```text
verification_challenges
  id
  email
  purpose
  code_hash
  status
  attempts
  created_at
  expires_at
  confirmed_at
```

Índices:

```text
email + purpose + status
expires_at
```

---

# 22. Decisão sobre hash do SessionIdentifier

O cookie pode carregar um identificador opaco, mas o banco não precisa armazenar esse valor em claro.

Recomendação:

```text
Cookie:
  __Host-auth.sid = valor opaco

Banco:
  session_identifier_hash = hash do valor opaco
```

Vantagens:

- reduz impacto se a tabela de sessões vazar;
- aproxima o tratamento de sessão ao tratamento de segredo;
- evita usar session id bruto como credencial persistida.

Trade-off:

- exige hash antes de buscar sessão;
- dificulta debug manual;
- exige padronização do algoritmo.

---

# 23. UseCase exemplo: Login

```csharp
/// <summary>
/// Caso de uso responsável por autenticar uma conta e iniciar uma sessão.
/// </summary>
public sealed class LoginUseCase
{
    private readonly IAccountRepository _accountRepository;
    private readonly IAuthSessionRepository _sessionRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessTokenIssuer _accessTokenIssuer;
    private readonly IAuthCookieWriter _cookieWriter;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Inicializa uma nova instância do caso de uso de login.
    /// </summary>
    public LoginUseCase(
        IAccountRepository accountRepository,
        IAuthSessionRepository sessionRepository,
        IPasswordHasher passwordHasher,
        IAccessTokenIssuer accessTokenIssuer,
        IAuthCookieWriter cookieWriter,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _accountRepository = accountRepository;
        _sessionRepository = sessionRepository;
        _passwordHasher = passwordHasher;
        _accessTokenIssuer = accessTokenIssuer;
        _cookieWriter = cookieWriter;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Executa o login da conta.
    /// </summary>
    public async Task<LoginResult> ExecuteAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var email = Email.Create(command.Email);

        var account = await _accountRepository.FindByEmailAsync(email, cancellationToken);

        if (account is null)
            throw new InvalidCredentialsException();

        var isPasswordValid = _passwordHasher.Verify(command.Password, account.PasswordHash);

        if (!isPasswordValid)
            throw new InvalidCredentialsException();

        account.EnsureCanSignIn();

        var session = AuthSession.Start(
            AuthSessionId.New(),
            account,
            SessionIdentifier.New(),
            DeviceInfo.Create(command.DeviceName, command.UserAgent),
            IpAddress.Create(command.IpAddress),
            now,
            now.AddDays(30));

        var accessToken = _accessTokenIssuer.Issue(account, session, now);

        await _sessionRepository.AddAsync(session, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        _cookieWriter.WriteAuthenticationCookies(session.Identifier, accessToken);

        return new LoginResult
        {
            AccountId = account.Id.Value
        };
    }
}
```

## 23.1. Observações

- `LoginUseCase` orquestra.
- `Account` protege regra de conta.
- `AuthSession` protege regra de sessão.
- `IPasswordHasher` é abstração.
- `IAccessTokenIssuer` é abstração.
- `IAuthCookieWriter` é detalhe de borda.
- JWT e cookie não aparecem no domínio.

---

# 24. Quando usar Domain Service

Evite criar um `AuthService` genérico.

Um Domain Service só faz sentido quando a regra de negócio:

- não pertence naturalmente a uma entidade;
- envolve múltiplos aggregates;
- precisa expressar uma política de domínio.

Exemplos aceitáveis:

```text
SessionRevocationPolicy
SignInPolicy
PasswordPolicy
RiskBasedAuthenticationPolicy
```

Exemplo:

```csharp
/// <summary>
/// Política de domínio para determinar se uma conta pode manter sessões anteriores após novo login.
/// </summary>
public sealed class SessionRotationPolicy
{
    /// <summary>
    /// Determina se sessões antigas devem ser revogadas quando uma nova sessão é criada.
    /// </summary>
    public bool ShouldRevokePreviousSessions(Account account, DeviceInfo newDevice)
    {
        return account.Status == AccountStatus.Active;
    }
}
```

Mas cuidado: muitas políticas desse tipo podem ficar melhor na Application enquanto ainda forem simples.

---

# 25. CQRS simples

Para comandos, use aggregates:

```text
Login
Logout
RevokeSession
ConfirmEmail
ChangePassword
```

Para consultas, use read models:

```text
GET /auth/me
GET /auth/sessions
```

Exemplo:

```text
ListAccountSessionsQuery
  -> lê diretamente projeção/tabela de sessões
  -> não precisa carregar aggregate AuthSession inteiro para cada linha
```

Isso evita custo desnecessário.

---

# 26. Decisão sobre consistência

## 26.1. Login

Geralmente precisa salvar sessão e depois escrever cookies.

```text
Salvar sessão -> Commit -> Escrever cookies
```

Se a escrita do cookie falhar, a sessão pode ficar criada sem uso. Isso é aceitável e pode expirar naturalmente ou ser limpa depois.

## 26.2. Troca de senha

Aqui a consistência é mais sensível.

```text
Alterar senha
Rotacionar security stamp
Revogar sessões
Commit
```

Preferencialmente, tudo deve acontecer na mesma unidade transacional.

## 26.3. Envio de e-mail

Não envie e-mail dentro da transação principal.

Use evento + outbox:

```text
AccountRegisteredDomainEvent
  -> OutboxMessage
  -> NotificationCore
```

---

# 27. Decisão sobre assinatura JWT

A assinatura do JWT não pertence ao domínio.

Do ponto de vista da modelagem:

```text
Domain:
  - sessão pode emitir acesso?

Application:
  - solicita emissão de access token

Infrastructure:
  - assina JWT com ECDSA/RSA/HMAC
```

Para microserviços, assinatura assimétrica tende a ser melhor:

```text
AuthCore:
  - assina com chave privada

Gateway/microserviços:
  - validam com chave pública
```

Vantagens:

- menor distribuição de segredo;
- microserviços não conseguem assinar tokens;
- melhor separação de responsabilidade;
- adequado para ambiente distribuído.

Trade-offs:

- rotação de chaves é mais complexa;
- configuração inicial é mais trabalhosa;
- precisa gerenciar `kid` e publicação de chaves públicas futuramente.

---

# 28. Desenho final recomendado

```text
Api
  -> Controllers
  -> Requests/Responses
  -> Cookies
  -> CORS/CSRF
  -> Authentication Handlers
  -> Exception Handling

Application
  -> Use cases
  -> Commands/Results
  -> Orquestração
  -> Interfaces
  -> Transações

Domain
  -> Account
  -> AuthSession
  -> VerificationChallenge
  -> Value Objects
  -> Domain Events
  -> Invariantes

Infrastructure
  -> Repositories
  -> PostgreSQL
  -> Redis
  -> JWT
  -> Cookies
  -> Password Hashing
  -> Outbox
```

---

# 29. Resumo da decisão

## 29.1. Nome do contexto

```text
IdentityAccess
```

## 29.2. Aggregates

```text
Account
AuthSession
VerificationChallenge
```

## 29.3. Value Objects

```text
Email
PasswordHash
SecurityStamp
SessionIdentifier
DeviceInfo
IpAddress
VerificationCodeHash
```

## 29.4. Use cases principais

```text
LoginUseCase
RefreshSessionAccessTokenUseCase
LogoutUseCase
LogoutAllSessionsUseCase
ListAccountSessionsQuery
RevokeSessionUseCase
RequestEmailVerificationCodeUseCase
ConfirmEmailVerificationCodeUseCase
RequestForgotPasswordCodeUseCase
ConfirmForgotPasswordCodeUseCase
```

## 29.5. Serviços técnicos

```text
JwtAccessTokenIssuer
AspNetCoreAuthCookieWriter
PasswordHasher
RedisVerificationCodeStore
RedisSessionCache
PostgresAuthSessionRepository
```

## 29.6. O que evitar

```text
Auth como aggregate genérico
AuthService gigante
JwtToken dentro do Domain
CookieOptions dentro do Domain
Redis dentro do Domain
Controller orquestrando regra de negócio
UseCase com regra de domínio espalhada
```

---

# 30. Conclusão

A autenticação híbrida do AuthCore deve ser modelada como um contexto de identidade e acesso, e não como uma única entidade chamada `Auth`.

A melhor separação inicial é:

```text
Account
  -> quem pode autenticar

AuthSession
  -> login ativo, revogável e rastreável

VerificationChallenge
  -> desafio temporário para confirmar ações sensíveis
```

JWT, cookie, Redis, PostgreSQL, CORS e Gateway são importantes, mas são mecanismos de implementação. Eles devem ficar fora do domínio.

O domínio deve expressar a linguagem e as regras centrais:

```text
Conta ativa pode iniciar sessão.
Sessão ativa pode emitir access token.
Sessão revogada não pode renovar acesso.
Alteração de senha rotaciona security stamp.
Security stamp incompatível invalida a sessão.
Desafio expirado não pode ser confirmado.
```

Esse desenho mantém o AuthCore alinhado com DDD, evita acoplamento técnico no domínio e permite evolução futura para MFA, OAuth/OIDC, passkeys, gestão de dispositivos, políticas de risco e autorização mais granular.
