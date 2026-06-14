# SOLID Guidelines

## Objetivo

Este repositorio deve seguir os principios SOLID de forma rigorosa, pratica e sustentavel.

Estas diretrizes devem orientar criacao, revisao e refatoracao de codigo C#/.NET em todos os contextos do projeto. A prioridade nao e apenas fazer o codigo funcionar; a prioridade e manter o codigo simples, testavel, extensivel, desacoplado e coerente com a arquitetura em camadas do repositorio.

Os principios avaliados sao:

- Single Responsibility Principle
- Open/Closed Principle
- Liskov Substitution Principle
- Interface Segregation Principle
- Dependency Inversion Principle

## Documento padrao ou skill

Este conteudo deve ser tratado como documento padrao do repositorio, e nao como `SKILL.md`.

Motivos:

- define criterios arquiteturais permanentes, aplicaveis a qualquer mudanca;
- complementa `AGENTS.md`, `architecture-overview.md`, `csharp-style.md`, `api-contracts.md`, `testing.md` e os guias de camada;
- deve ser lido como politica de engenharia antes de alterar ou revisar codigo;
- nao descreve um workflow especializado e acionavel como as skills de `domain-modeling`, `application-use-cases` ou `npgsql-repository`.

Uma skill faria sentido apenas se o objetivo fosse criar um fluxo operacional especifico, por exemplo `solid-review`, com passos obrigatorios para auditoria e relatorio. Para este projeto, o uso principal e como padrao transversal.

## Checklist geral

Antes de criar, alterar ou revisar codigo, avalie:

1. Qual responsabilidade este codigo representa?
2. Qual camada deve conter esta responsabilidade?
3. A mudanca aumenta acoplamento?
4. A mudanca cria dependencia direta de infraestrutura onde deveria existir abstracao?
5. A mudanca mistura regra de negocio com detalhes tecnicos?
6. A mudanca facilita ou dificulta testes unitarios?
7. A mudanca exige modificar codigo existente estavel para adicionar novo comportamento?
8. A mudanca cria interfaces grandes, genericas ou artificiais?
9. A mudanca quebra contratos esperados por consumidores existentes?
10. Existe uma alternativa mais simples sem violar SOLID?

Se houver violacao clara de SOLID, proponha ou implemente a refatoracao adequada antes de expandir a funcionalidade.

## Arquitetura esperada

O projeto deve respeitar a separacao entre:

- `Api`
- `Application`
- `Domain`
- `Infrastructure`

### Api

Responsavel por:

- controllers;
- requests e responses HTTP;
- middlewares e filters;
- configuracao de autenticacao e autorizacao;
- Swagger/OpenAPI;
- versionamento quando existir;
- mapeamento entre HTTP e Application.

Controllers devem ser finos. Eles podem:

- receber request;
- validar entrada superficial quando necessario;
- chamar um use case;
- converter resultado em resposta HTTP.

Controllers nao devem:

- acessar banco diretamente;
- usar `DbContext`, `MongoCollection`, `Dapper`, `Npgsql`, `HttpClient` ou SDK externo diretamente;
- implementar regra de negocio;
- enviar e-mail diretamente;
- publicar mensagem diretamente em fila;
- montar queries complexas de dominio;
- decidir fluxo de negocio relevante.

### Application

Responsavel por:

- use cases;
- orquestracao de fluxo;
- contratos de entrada e saida da aplicacao;
- abstracoes necessarias para persistencia, mensageria, cache, e-mail, storage e integracoes;
- validacoes de aplicacao;
- controle de transacao quando necessario.

A camada `Application` pode depender do `Domain`, mas nao deve depender de `Infrastructure`.

Use cases devem coordenar o fluxo, mas nao devem concentrar detalhes tecnicos.

Como regra de visibilidade, a abstracao consumida por outra camada pode ser `public`, por exemplo `I...UseCase` ou contratos de portas. A implementacao concreta do caso de uso deve ser `internal` por padrao, registrada por injecao de dependencia e nao consumida diretamente pela API ou por outros assemblies.

Um use case pode:

- buscar entidades em repositorios;
- chamar metodos do dominio;
- persistir alteracoes por contratos;
- publicar eventos por abstracao;
- retornar resultado para a API.

Um use case nao deve:

- conter SQL inline complexo;
- instanciar providers concretos;
- manipular SDK externo diretamente;
- esconder regra de negocio em codigo procedural longo;
- virar classe generica com varias operacoes diferentes.

### Domain

Responsavel por:

- entidades;
- aggregates;
- value objects;
- domain services, quando realmente necessarios;
- regras de negocio puras;
- invariantes;
- eventos de dominio;
- excecoes de dominio;
- contratos centrais consumidos por Application e Infrastructure.

O dominio deve ser a camada mais estavel e independente. Ele nao deve depender de:

- `Infrastructure`;
- `Application`;
- `Api`;
- banco de dados;
- framework web;
- filas;
- cache;
- SDKs externos;
- variaveis de ambiente;
- `HttpContext`;
- `ClaimsPrincipal`;
- relogio do sistema diretamente quando tempo afetar regra testavel.

O dominio deve expressar comportamento, nao apenas dados.

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

Responsavel por detalhes tecnicos:

- repositorios concretos;
- banco de dados;
- Redis;
- RabbitMQ;
- SMTP;
- HttpClient e SDKs externos;
- providers externos;
- implementacoes de interfaces do dominio ou da aplicacao;
- migrations e configuracoes tecnicas.

`Infrastructure` implementa detalhes e nao deve definir regra de negocio.

## 1. Single Responsibility Principle

Cada classe deve ter apenas um motivo claro para mudar.

Uma classe viola SRP quando muda por motivos diferentes, por exemplo:

- regra de negocio;
- persistencia;
- formatacao HTTP;
- envio de e-mail;
- log;
- cache;
- validacao;
- integracao externa.

Sinais de violacao:

- classe com muitos metodos publicos sem coesao;
- classe chamada `Manager`, `Helper`, `Utils`, `Service` generico ou `Processor`;
- use case que faz validacao, regra, query, envio, log e publicacao de evento tudo junto;
- controller com logica de negocio;
- entidade anemica com regra espalhada fora do dominio;
- metodo longo com etapas de negocio misturadas com detalhes tecnicos.

Regra pratica: se for necessario explicar uma classe usando "e", provavelmente ela tem mais de uma responsabilidade.

Ruim:

```text
UserService cadastra usuario e envia e-mail e gera token e salva sessao.
```

Melhor:

```text
RegisterUserUseCase cadastra usuario.
IEmailVerificationSender envia verificacao.
ITokenIssuer emite token.
ISessionStore salva sessao.
```

## 2. Open/Closed Principle

O codigo deve ser aberto para extensao e fechado para modificacao.

Adicionar novo comportamento nao deve exigir alterar varias classes estaveis.

Sinais de violacao:

- muitos `if/else` ou `switch` por tipo de operacao;
- enum controlando comportamento complexo;
- toda nova regra exige alterar uma classe central;
- classe conhece todos os tipos concretos possiveis;
- fluxo de negocio depende de strings magicas;
- provider unico com varios branches para SMTP, SES, SendGrid, Brevo etc.

Quando houver variacao real de comportamento, prefira abstracoes, strategies, policies, factories ou providers.

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

Implementacoes de uma abstracao devem poder substituir umas as outras sem quebrar o comportamento esperado.

Se uma classe implementa uma interface, ela deve cumprir o contrato completo dessa interface.

Sinais de violacao:

- metodo implementado com `throw new NotSupportedException()`;
- implementacao que ignora parametros importantes;
- implementacao que retorna `null` inesperadamente;
- subclasse que enfraquece validacoes da classe base;
- subclasse que muda o significado do metodo herdado;
- interface generica demais forcando implementacoes artificiais.

Se uma implementacao nao consegue cumprir uma interface, a interface provavelmente esta errada ou grande demais.

## 4. Interface Segregation Principle

Interfaces devem ser pequenas, especificas e orientadas ao consumidor.

Nao crie interfaces grandes apenas para representar uma classe concreta.

Sinais de violacao:

- interfaces com muitos metodos;
- interfaces chamadas `IUserService` ou `INotificationService` com varias operacoes sem coesao;
- implementacoes com metodos vazios;
- implementacoes lancando `NotSupportedException`;
- use case recebendo uma interface com metodos que ele nao usa;
- interface criada automaticamente para toda classe sem necessidade real.

A interface deve nascer da necessidade do consumidor, nao da implementacao concreta.

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

Codigo de alto nivel nao deve depender de detalhes de baixo nivel.

`Application` e `Domain` nao devem depender de infraestrutura concreta.

Use cases nao devem depender diretamente de:

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

Use cases devem depender de abstracoes como:

```csharp
IUserRepository
IEmailProvider
IMessagePublisher
IClock
IUnitOfWork
IObjectStorage
ICacheRepository
```

As implementacoes concretas devem ficar em `Infrastructure`.

A regra de encapsulamento acompanha o DIP: contratos e abstracoes que precisam atravessar camadas podem ser `public`; implementacoes concretas, repositorios, providers, clients, factories tecnicas, options e modelos auxiliares devem ser `internal` por padrao. Excecoes publicas precisam ser pontuais e justificadas, como classes de registro de DI, controllers/contratos HTTP e tipos exigidos por discovery/reflection de frameworks.

Se uma classe da `Application` tem `new AlgumaClasseDeInfraestrutura()`, provavelmente ha violacao de DIP.

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

- mistura HTTP, dominio, banco e e-mail;
- viola SRP;
- viola DIP;
- dificulta teste;
- acopla controller a infraestrutura.

## Regras para use cases

Cada use case deve representar uma intencao clara do sistema.

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

## Regras para dominio

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

Value objects devem validar sua propria consistencia.

Use value object quando houver:

- validacao;
- normalizacao;
- comparacao por valor;
- regra de formato;
- semantica de dominio importante.

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

Se uma regra de negocio aparece em `Infrastructure`, considere mover para:

- `Domain`, se for regra pura;
- `Application`, se for regra de fluxo;
- policy ou strategy, se for variacao de comportamento.

## Regras para injecao de dependencia

Registre abstracoes no container.

Exemplo:

```csharp
services.AddScoped<IUserRepository, UserRepository>();
services.AddScoped<IEmailProvider, SmtpEmailProvider>();
services.AddSingleton<IClock, SystemClock>();
```

Nao use service locator dentro de use cases.

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

- esconde dependencias;
- dificulta testes;
- viola dependencias explicitas;
- aumenta acoplamento indireto.

## Regras para testes

Toda regra de negocio relevante deve ser testavel sem banco real, fila real ou servico externo real.

Se uma classe nao pode ser testada sem subir infraestrutura, provavelmente ha violacao de DIP ou SRP.

Ao criar codigo novo, considere testes para:

- caminho feliz;
- validacoes de dominio;
- erros esperados;
- contratos de interface;
- comportamento de policies e strategies;
- use cases com fakes de infraestrutura.

## Checklist antes de finalizar

### SRP

- A classe tem apenas um motivo principal para mudar?
- O metodo faz uma coisa clara?
- Existe mistura de regra de negocio com infraestrutura?

### OCP

- Um novo comportamento exigiria alterar essa classe?
- Existe `switch` ou `if/else` por tipo de regra?
- Strategy, policy ou provider deixariam o codigo mais extensivel?

### LSP

- Toda implementacao cumpre o contrato da interface?
- Alguma implementacao lanca `NotSupportedException`?
- Alguma implementacao muda o significado esperado do metodo?

### ISP

- O consumidor usa todos os metodos da interface?
- A interface esta grande demais?
- Faz sentido quebrar a interface por caso de uso?

### DIP

- `Application` depende apenas de abstracoes?
- `Domain` esta livre de infraestrutura?
- Alguma classe instancia dependencia concreta internamente?
- Alguma dependencia tecnica vazou para camada errada?
- A abstracao publica existe apenas quando necessaria e a implementacao concreta permanece `internal` por padrao?

## Como reportar uma revisao SOLID

Ao revisar ou gerar codigo, explique:

1. Qual principio SOLID esta envolvido.
2. Se o codigo atual respeita ou viola o principio.
3. Qual impacto pratico da decisao.
4. Qual refatoracao recomenda.
5. Qual trade-off existe.

Exemplo:

```text
Aqui existe uma violacao de SRP e DIP.

O controller esta fazendo regra de negocio e acessando infraestrutura diretamente.
Isso dificulta teste, aumenta acoplamento e faz o endpoint mudar por motivos diferentes.

Eu moveria o fluxo para um use case e deixaria o controller apenas converter request/response.
O use case dependeria de IUserRepository e IEmailProvider, enquanto UserRepository e SmtpEmailProvider ficariam na Infrastructure.

Trade-off: cria mais classes, mas reduz acoplamento e melhora testabilidade.
```

## Trade-offs permitidos

SOLID deve ser seguido com rigor, mas sem abstracoes artificiais.

Nao crie interface para tudo automaticamente. Crie abstracao quando existir pelo menos um dos motivos:

- dependencia externa;
- infraestrutura;
- necessidade de teste;
- variacao real ou prevista de implementacao;
- regra de negocio intercambiavel;
- protecao entre camadas;
- reducao clara de acoplamento.

Evite overengineering.

Uma classe simples, estavel e interna pode nao precisar de interface.

## Regra final

Se houver conflito entre entregar rapido e preservar SOLID, preserve SOLID e explique o custo.

O codigo deve ser simples, mas nao simplista. O objetivo e crescer o projeto sem transformar a base em codigo acoplado, dificil de testar e dificil de evoluir.
