# Testing

## Objetivo

Este documento oficializa a estratégia de testes do projeto `auth_core` com base no estado atual do repositório.

Ele existe para orientar criação, revisão e evolução de testes de forma pragmática, preservando o foco do projeto em comportamento de negócio, previsibilidade arquitetural e validação incremental por camada.

Os testes também servem como sinal de aderência aos princípios SOLID descritos em `docs/agents/solid-guidelines.md`: se uma regra não pode ser testada sem infraestrutura real, avalie possível violação de SRP ou DIP.

As regras abaixo seguem quatro premissas:

- testar comportamento observável antes de testar detalhe interno
- manter cada camada validada no nível certo de isolamento
- ampliar cobertura quando a mudança introduzir regra, risco ou contrato relevante
- evitar testes cerimoniosos, frágeis ou acoplados demais à implementação

## Visão Atual da Base

O repositório possui hoje quatro frentes de validação automatizada:

- `tests/AuthCore.Domain.UnitTests`: invariantes e comportamento do domínio
- `tests/AuthCore.Application.UnitTests`: orquestração de casos de uso
- `tests/AuthCore.IntegrationTests`: bootstrap, autenticação, exceções HTTP e persistência PostgreSQL
- `tests/AuthCore.ArchitectureTests`: espaço preparado, ainda sem implementação relevante

Os projetos de teste usam hoje:

- `xUnit` como framework principal
- `coverlet.collector` para coleta de cobertura
- dublês manuais como `Fake...` e `Spy...` em vez de bibliotecas de mocking como padrão dominante

O estado atual da base mostra uma estratégia equilibrada:

- o domínio continua sendo o núcleo principal da validação
- a aplicação já possui cobertura relevante para casos de uso
- a integração cobre fluxos importantes de composição e infraestrutura
- testes arquiteturais ainda não fazem parte da prática implementada hoje

## Estratégia por Camada

### Domínio

Os testes de domínio devem ser a primeira linha de defesa quando a mudança altera:

- invariantes
- transições de estado
- factories como `Create`, `Register`, `Restore` e `Read`
- value objects
- regras de validação
- comportamento de agregados e entidades

O foco aqui é validar regra de negócio pura, sem banco, DI, HTTP ou infraestrutura.

Boas práticas observadas no projeto:

- montar o objeto com valores explícitos e legíveis
- afirmar estado final completo quando a transição for importante
- usar `Assert.Throws<DomainException>(...)` para regras inválidas
- testar cenários positivos e de borda mais relevantes

### Application

Os testes da aplicação devem validar a orquestração dos casos de uso, especialmente quando a mudança afeta:

- coordenação entre repositórios
- abertura, commit e rollback de transação
- integração entre domínio e dependências externas por contrato
- montagem de resultados de aplicação
- decisões de fluxo condicionadas por leitura de dados
- consumo de contratos pequenos e coerentes com a responsabilidade exercida

O padrão atual privilegia dublês manuais simples, como:

- `FakeUserRepository`
- `FakePasswordRepository`
- `FakeRefreshTokenRepository`
- `SpyUnitOfWork`

Esses testes devem provar:

- qual dependência foi chamada
- com quais efeitos observáveis
- em qual ordem lógica do fluxo quando isso for relevante
- se a transação foi iniciada, confirmada ou revertida no momento correto

Eles não devem recriar a regra central do domínio dentro do teste.

### Integração

Os testes de integração devem ser usados quando a mudança afeta comportamento atravessando fronteiras reais da aplicação, como:

- registro de dependências
- configuração do pipeline HTTP
- autenticação JWT
- mapeamento de exceções para respostas HTTP
- persistência PostgreSQL e materialização do domínio

Há dois perfis principais já presentes no projeto:

- integração leve de composição, com `WebApplication.CreateBuilder()`, registro real de serviços e validação do bootstrap
- integração com infraestrutura real, como persistência PostgreSQL de refresh tokens

Os testes de persistência usam fixture própria e dependem de PostgreSQL acessível. Quando o banco não está disponível, a fixture marca o cenário como indisponível e o teste retorna sem executar a validação completa.

Referência importante do estado atual:

- `AUTHCORE_TEST_POSTGRES` pode ser usado para apontar a conexão administrativa do PostgreSQL
- na ausência da variável, a fixture tenta usar `Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres;Pooling=false`

### Arquitetura

`AuthCore.ArchitectureTests` existe hoje como diretório preparado, mas ainda sem testes implementados de forma relevante.

Não documente esse projeto como cobertura efetiva. Trate-o como capacidade preparada para evolução futura.

## Convenções Dominantes

As convenções de teste mais estáveis no repositório hoje são:

- nomes no formato `Metodo_WhenCondicao_ShouldResultado`
- uso predominante de `[Fact]`
- cenários curtos, com dados explícitos e pouca abstração
- asserts focados em comportamento observável
- dublês manuais e específicos do contexto

Também há alguns sinais úteis do estilo dominante:

- datas e valores de teste são concretos, não genéricos
- mensagens de exceção importantes são verificadas quando fazem parte do contrato
- testes de aplicação e integração podem usar XML docs de forma moderada, mas isso não é obrigatório para todo teste

## Quando Adicionar Testes

Adicione ou ajuste testes sempre que a mudança:

- alterar regra de negócio no domínio
- mudar fluxo de caso de uso na aplicação
- modificar contrato HTTP, tratamento de exceção ou autenticação
- alterar persistência, SQL, materialização ou comportamento transacional
- corrigir bug já reproduzível por cenário automatizável

Regra prática por tipo de mudança:

- mudança apenas em domínio: atualizar primeiro `AuthCore.Domain.UnitTests`
- mudança em orquestração de caso de uso: atualizar `AuthCore.Application.UnitTests`
- mudança em controller, bootstrap, autenticação ou infraestrutura: complementar com `AuthCore.IntegrationTests`
- mudança puramente estrutural sem efeito observável: avaliar se teste novo realmente agrega valor antes de criar
- refatoração SOLID que altera fronteiras de interface ou DI: atualizar testes de composição e dublês de aplicação para refletir os novos contratos

## O Que Evitar

Evite introduzir testes que:

- validam apenas getters, setters ou mapeamentos triviais sem risco real
- dependem de detalhes internos que podem mudar sem alterar comportamento
- duplicam exaustivamente o que já está coberto em outra camada
- exigem infraestrutura real quando um teste isolado resolveria melhor
- usam dublês genéricos excessivamente inteligentes, escondendo o fluxo principal

Neste projeto, teste bom é teste legível, específico e alinhado à responsabilidade da camada.

## Validação Antes de Concluir

Antes de finalizar uma alteração, rode a menor validação relevante para a área modificada.

Comandos mais úteis no estado atual:

```bash
dotnet test tests/AuthCore.Domain.UnitTests/AuthCore.Domain.UnitTests.csproj
dotnet test tests/AuthCore.Application.UnitTests/AuthCore.Application.UnitTests.csproj
dotnet test tests/AuthCore.IntegrationTests/AuthCore.IntegrationTests.csproj
```

Aplicação recomendada:

- se o domínio mudou, execute pelo menos os testes de domínio
- se o caso de uso mudou, execute os testes da aplicação correspondentes e os de domínio impactados
- se a mudança toca autenticação, bootstrap, exceções HTTP ou persistência, execute os testes de integração relevantes
- se a alteração cruza várias camadas, prefira validar todos os projetos de teste afetados

Quando os testes de integração dependerem de PostgreSQL, confirme a disponibilidade do banco antes de usar o resultado como validação completa da mudança.

## Exemplos Canônicos

Os arquivos abaixo representam boas referências para a estratégia atual de testes:

- `tests/AuthCore.Domain.UnitTests/Aggregates/Users/UserTests.cs`
- `tests/AuthCore.Application.UnitTests/UseCases/Users/ChangePassword/ChangePasswordUseCaseTests.cs`
- `tests/AuthCore.Application.UnitTests/UseCases/Authentication/Support/AuthenticationTestDoubles.cs`
- `tests/AuthCore.IntegrationTests/Authentication/AuthControllerIntegrationTests.cs`
- `tests/AuthCore.IntegrationTests/Exceptions/ApiExceptionHandlerTests.cs`
- `tests/AuthCore.IntegrationTests/Passports/RefreshTokenPersistenceIntegrationTests.cs`

Em conjunto, esses arquivos mostram:

- domínio validando invariantes e transições de estado
- aplicação validando orquestração com `Fake...` e `Spy...`
- integração cobrindo composição real, contrato HTTP, autenticação e persistência

## Relação com Outras Referências

Este guia complementa o `AGENTS.md` do repositório e deve ser lido junto com ele.

Quando a mudança tocar modelagem, casos de uso, contratos HTTP, persistência ou estilo C#, use também os documentos específicos de `docs/agents/` correspondentes à área alterada.
