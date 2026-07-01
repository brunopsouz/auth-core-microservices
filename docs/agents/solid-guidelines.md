# SOLID Guidelines

## Objetivo

Este repositório deve seguir os princípios SOLID de forma rigorosa, prática e sustentável.

Estas diretrizes devem orientar criação, revisão e refatoração de código C#/.NET em todos os contextos do projeto. A prioridade não é apenas fazer o código funcionar; a prioridade é manter o código simples, testável, extensível, desacoplado e coerente com a arquitetura em camadas do repositório.

Os princípios avaliados são:

- Single Responsibility Principle
- Open/Closed Principle
- Liskov Substitution Principle
- Interface Segregation Principle
- Dependency Inversion Principle

## Documento padrão ou skill

Este conteúdo deve ser tratado como documento padrão do repositório, e não como `SKILL.md`.

Motivos:

- define critérios arquiteturais permanentes, aplicáveis a qualquer mudança;
- complementa `AGENTS.md`, `architecture-overview.md`, `csharp-style.md`, `api-contracts.md`, `testing.md` e os guias de camada;
- deve ser lido como política de engenharia antes de alterar ou revisar código;
- não descreve um workflow especializado e acionável como as skills de `domain-modeling`, `application-use-cases` ou `npgsql-repository`.

Uma skill faria sentido apenas se o objetivo fosse criar um fluxo operacional específico, por exemplo `solid-review`, com passos obrigatórios para auditoria e relatório. Para este projeto, o uso principal é como padrão transversal.

## Checklist geral

Antes de criar, alterar ou revisar código, avalie:

1. Qual responsabilidade este código representa?
2. Qual camada deve conter esta responsabilidade?
3. A mudança aumenta acoplamento?
4. A mudança cria dependência direta de infraestrutura onde deveria existir abstração?
5. A mudança mistura regra de negócio com detalhes técnicos?
6. A mudança facilita ou dificulta testes unitários?
7. A mudança exige modificar código existente estável para adicionar novo comportamento?
8. A mudança cria interfaces grandes, genéricas ou artificiais?
9. A mudança quebra contratos esperados por consumidores existentes?
10. Existe uma alternativa mais simples sem violar SOLID?

Se houver violação clara de SOLID, proponha ou implemente a refatoração adequada antes de expandir a funcionalidade.

## Arquitetura esperada

O projeto deve respeitar a separação entre:

- `Api`
- `Application`
- `Domain`
- `Infrastructure`

### Api

Responsável por:

- controllers;
- requests e responses HTTP;
- middlewares e filters;
- configuração de autenticação e autorização;
- Swagger/OpenAPI;
- versionamento quando existir;
- mapeamento entre HTTP e Application.

Controllers devem ser finos. Eles podem:

- receber request;
- validar entrada superficial quando necessário;
- chamar um use case;
- converter resultado em resposta HTTP.

Controllers não devem:

- acessar banco diretamente;
- usar `DbContext`, `MongoCollection`, `Dapper`, `Npgsql`, `HttpClient` ou SDK externo diretamente;
- implementar regra de negócio;
- enviar e-mail diretamente;
- publicar mensagem diretamente em fila;
- montar queries complexas de domínio;
- decidir fluxo de negócio relevante.

### Application

Responsável por:

- use cases;
- orquestração de fluxo;
- contratos de entrada e saída da aplicação;
- abstrações necessárias para persistência, mensageria, cache, e-mail, storage e integrações;
- validações de aplicação;
- controle de transação quando necessário.

A camada `Application` pode depender do `Domain`, mas não deve depender de `Infrastructure`.

Use cases devem coordenar o fluxo, mas não devem concentrar detalhes técnicos.

Como regra de visibilidade, a abstração consumida por outra camada pode ser `public`, por exemplo `I...UseCase` ou contratos de portas. A implementação concreta do caso de uso deve ser `internal` por padrão, registrada por injeção de dependência e não consumida diretamente pela API ou por outros assemblies.

Um use case pode:

- buscar entidades em repositórios;
- chamar métodos do domínio;
- persistir alterações por contratos;
- publicar eventos por abstração;
- retornar resultado para a API.

Um use case não deve:

- conter SQL inline complexo;
- instanciar providers concretos;
- manipular SDK externo diretamente;
- esconder regra de negócio em código procedural longo;
- virar classe genérica com várias operações diferentes.

### Domain

Responsável por:

- entidades;
- aggregates;
- value objects;
- domain services, quando realmente necessários;
- regras de negócio puras;
- invariantes;
- eventos de domínio;
- exceções de domínio;
- contratos centrais consumidos por Application e Infrastructure.

O domínio deve ser a camada mais estável e independente. Ele não deve depender de:

- `Infrastructure`;
- `Application`;
- `Api`;
- banco de dados;
- framework web;
- filas;
- cache;
- SDKs externos;
- variáveis de ambiente;
- `HttpContext`;
- `ClaimsPrincipal`;
- relógio do sistema diretamente quando tempo afetar regra testável.

O domínio deve expressar comportamento, não apenas dados.

Prefira:

```csharp
user.VerifyEmail(code, nowUtc);
```

Evite:

```csharp
user.EmailVerified = true;
user.EmailVerifiedAt = DateTime.UtcNow;
```

### Infrastructure

Responsável por detalhes técnicos:

- repositórios concretos;
- banco de dados;
- Redis;
- RabbitMQ;
- SMTP;
- HttpClient e SDKs externos;
- providers externos;
- implementações de interfaces do domínio ou da aplicação;
- migrations e configurações técnicas.

`Infrastructure` implementa detalhes e não deve definir regra de negócio.

## 1. Single Responsibility Principle

Cada classe deve ter apenas um motivo claro para mudar.

Uma classe viola SRP quando muda por motivos diferentes, por exemplo:

- regra de negócio;
- persistência;
- formatação HTTP;
- envio de e-mail;
- log;
- cache;
- validação;
- integração externa.

Sinais de violação:

- classe com muitos métodos públicos sem coesão;
- classe chamada `Manager`, `Helper`, `Utils`, `Service` generico ou `Processor`;
- use case que faz validação, regra, query, envio, log e publicação de evento tudo junto;
- controller com lógica de negócio;
- entidade anêmica com regra espalhada fora do domínio;
- método longo com etapas de negócio misturadas com detalhes técnicos.

Regra prática: se for necessário explicar uma classe usando "e", provavelmente ela tem mais de uma responsabilidade.

Ruim:

```text
UserService cadastra usuário e envia e-mail e gera token e salva sessão.
```

Melhor:

```text
RegisterUserUseCase cadastra usuário.
IEmailVerificationSender envia verificação.
ITokenIssuer emite token.
ISessionStore salva sessão.
```

## 2. Open/Closed Principle

O código deve ser aberto para extensão e fechado para modificação.

Adicionar novo comportamento não deve exigir alterar várias classes estáveis.

Sinais de violação:

- muitos `if/else` ou `switch` por tipo de operação;
- enum controlando comportamento complexo;
- toda nova regra exige alterar uma classe central;
- classe conhece todos os tipos concretos possíveis;
- fluxo de negócio depende de strings mágicas;
- provider único com vários branches para SMTP, SES, SendGrid, Brevo etc.

Quando houver variação real de comportamento, prefira abstrações, strategies, policies, factories ou providers.

Ruim:

```csharp
if (provider == "smtp")
{
    // send using smtp
}
else if (provider == "brevo")
{
    // send using brevo
}
```

Melhor:

```csharp
public interface IEmailProvider
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
```

## 3. Liskov Substitution Principle

Implementações de uma abstração devem poder substituir umas as outras sem quebrar o comportamento esperado.

Se uma classe implementa uma interface, ela deve cumprir o contrato completo dessa interface.

Sinais de violação:

- método implementado com `throw new NotSupportedException()`;
- implementação que ignora parâmetros importantes;
- implementação que retorna `null` inesperadamente;
- subclasse que enfraquece validações da classe base;
- subclasse que muda o significado do método herdado;
- interface genérica demais forçando implementações artificiais.

Se uma implementação não consegue cumprir uma interface, a interface provavelmente está errada ou grande demais.

## 4. Interface Segregation Principle

Interfaces devem ser pequenas, específicas e orientadas ao consumidor.

Não crie interfaces grandes apenas para representar uma classe concreta.

Sinais de violação:

- interfaces com muitos métodos;
- interfaces chamadas `IUserService` ou `INotificationService` com várias operações sem coesão;
- implementações com métodos vazios;
- implementações lançando `NotSupportedException`;
- use case recebendo uma interface com métodos que ele não usa;
- interface criada automaticamente para toda classe sem necessidade real.

A interface deve nascer da necessidade do consumidor, não da implementação concreta.

Ruim:

```csharp
public interface INotificationService
{
    Task SendEmailAsync(...);
    Task SendSmsAsync(...);
    Task SendPushAsync(...);
}
```

Melhor:

```csharp
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public interface ISmsSender
{
    Task SendAsync(SmsMessage message, CancellationToken cancellationToken);
}
```

## 5. Dependency Inversion Principle

Código de alto nível não deve depender de detalhes de baixo nível.

`Application` e `Domain` não devem depender de infraestrutura concreta.

Use cases não devem depender diretamente de:

- `DbContext`;
- `IMongoCollection<T>`;
- `SqlConnection`;
- `NpgsqlConnection`;
- `HttpClient`;
- `SmtpClient`;
- SDK da AWS;
- SDK de pagamento;
- SDK de e-mail;
- Redis client concreto;
- RabbitMQ channel concreto;
- file system diretamente.

Use cases devem depender de abstrações como:

```csharp
IUserRepository
IEmailProvider
IMessagePublisher
IClock
IUnitOfWork
IObjectStorage
ICacheRepository
```

As implementações concretas devem ficar em `Infrastructure`.

A regra de encapsulamento acompanha o DIP: contratos e abstrações que precisam atravessar camadas podem ser `public`; implementações concretas, repositórios, providers, clients, factories técnicas, options e modelos auxiliares devem ser `internal` por padrão. Exceções públicas precisam ser pontuais e justificadas, como classes de registro de DI, controllers/contratos HTTP e tipos exigidos por discovery/reflection de frameworks.

Se uma classe da `Application` tem `new AlgumaClasseDeInfraestrutura()`, provavelmente há violação de DIP.

## Regras para controllers

Controllers devem ser finos.

Permitido:

```csharp
[HttpPost]
public async Task<IActionResult> Register(RegisterUserRequest request, CancellationToken cancellationToken)
{
    var command = request.ToCommand();
    var result = await _useCase.ExecuteAsync(command, cancellationToken);

    return result.ToActionResult();
}
```

Proibido:

```csharp
[HttpPost]
public async Task<IActionResult> Register(RegisterUserRequest request)
{
    var user = new User();
    user.Email = request.Email;

    await _db.Users.InsertOneAsync(user);
    await _smtp.SendAsync(...);

    return Ok();
}
```

Motivo:

- mistura HTTP, domínio, banco e e-mail;
- viola SRP;
- viola DIP;
- dificulta teste;
- acopla controller a infraestrutura.

## Regras para use cases

Cada use case deve representar uma intenção clara do sistema.

Bons nomes:

```text
RegisterUserUseCase
VerifyEmailUseCase
CreateSessionUseCase
RefreshTokenUseCase
DispatchPendingNotificationUseCase
GetDailyOverviewPrioritiesUseCase
```

Evite:

```text
UserService
AuthManager
NotificationHelper
ProcessUserHandler
GenericUseCase
```

Um use case deve ter uma responsabilidade principal. Se o fluxo crescer demais, extraia:

- domain service;
- policy;
- strategy;
- factory;
- provider;
- repository;
- validator;
- mapper.

## Regras para domínio

Entidades devem proteger invariantes.

Ruim:

```csharp
user.Status = UserStatus.Active;
user.EmailVerified = true;
```

Melhor:

```csharp
user.VerifyEmail(code, nowUtc);
user.Activate();
```

Value objects devem validar sua própria consistência.

Use value object quando houver:

- validação;
- normalização;
- comparação por valor;
- regra de formato;
- semântica de domínio importante.

## Regras para infraestrutura

`Infrastructure` implementa detalhes.

Permitido:

```text
PostgreSqlUserRepository : IUserRepository
NpgsqlUserReadRepository : IUserReadRepository
SmtpEmailProvider : IEmailProvider
RabbitMqMessagePublisher : IMessagePublisher
SystemClock : IClock
```

Se uma regra de negócio aparece em `Infrastructure`, considere mover para:

- `Domain`, se for regra pura;
- `Application`, se for regra de fluxo;
- policy ou strategy, se for variação de comportamento.

## Regras para injeção de dependência

Registre abstrações no container.

Exemplo:

```csharp
services.AddScoped<IUserRepository, UserRepository>();
services.AddScoped<IEmailProvider, SmtpEmailProvider>();
services.AddSingleton<IClock, SystemClock>();
```

Não use service locator dentro de use cases.

Proibido:

```csharp
public sealed class CreateUserUseCase
{
    private readonly IServiceProvider _serviceProvider;

    public async Task ExecuteAsync()
    {
        var repository = _serviceProvider.GetRequiredService<IUserRepository>();
    }
}
```

Motivo:

- esconde dependências;
- dificulta testes;
- viola dependências explícitas;
- aumenta acoplamento indireto.

## Regras para testes

Toda regra de negócio relevante deve ser testável sem banco real, fila real ou serviço externo real.

Se uma classe não pode ser testada sem subir infraestrutura, provavelmente há violação de DIP ou SRP.

Ao criar código novo, considere testes para:

- caminho feliz;
- validações de domínio;
- erros esperados;
- contratos de interface;
- comportamento de policies e strategies;
- use cases com fakes de infraestrutura.

## Checklist antes de finalizar

### SRP

- A classe tem apenas um motivo principal para mudar?
- O método faz uma coisa clara?
- Existe mistura de regra de negócio com infraestrutura?

### OCP

- Um novo comportamento exigiria alterar essa classe?
- Existe `switch` ou `if/else` por tipo de regra?
- Strategy, policy ou provider deixariam o código mais extensível?

### LSP

- Toda implementação cumpre o contrato da interface?
- Alguma implementação lança `NotSupportedException`?
- Alguma implementação muda o significado esperado do método?

### ISP

- O consumidor usa todos os métodos da interface?
- A interface está grande demais?
- Faz sentido quebrar a interface por caso de uso?

### DIP

- `Application` depende apenas de abstrações?
- `Domain` está livre de infraestrutura?
- Alguma classe instancia dependência concreta internamente?
- Alguma dependência técnica vazou para camada errada?
- A abstração pública existe apenas quando necessária e a implementação concreta permanece `internal` por padrão?

## Como reportar uma revisão SOLID

Ao revisar ou gerar código, explique:

1. Qual princípio SOLID está envolvido.
2. Se o código atual respeita ou viola o princípio.
3. Qual impacto prático da decisão.
4. Qual refatoração recomenda.
5. Qual trade-off existe.

Exemplo:

```text
Aqui existe uma violação de SRP e DIP.

O controller está fazendo regra de negócio e acessando infraestrutura diretamente.
Isso dificulta teste, aumenta acoplamento e faz o endpoint mudar por motivos diferentes.

Eu moveria o fluxo para um use case e deixaria o controller apenas converter request/response.
O use case dependeria de IUserRepository e IEmailProvider, enquanto UserRepository e SmtpEmailProvider ficariam na Infrastructure.

Trade-off: cria mais classes, mas reduz acoplamento e melhora testabilidade.
```

## Trade-offs permitidos

SOLID deve ser seguido com rigor, mas sem abstrações artificiais.

Não crie interface para tudo automaticamente. Crie abstração quando existir pelo menos um dos motivos:

- dependência externa;
- infraestrutura;
- necessidade de teste;
- variação real ou prevista de implementação;
- regra de negócio intercambiavel;
- proteção entre camadas;
- redução clara de acoplamento.

Evite overengineering.

Uma classe simples, estável e interna pode não precisar de interface.

## Regra final

Se houver conflito entre entregar rápido e preservar SOLID, preserve SOLID e explique o custo.

O código deve ser simples, mas não simplista. O objetivo é crescer o projeto sem transformar a base em código acoplado, difícil de testar e difícil de evoluir.
