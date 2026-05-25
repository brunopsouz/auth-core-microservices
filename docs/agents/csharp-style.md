# C# Style

## Objetivo

Este documento oficializa o padrão dominante de escrita C# do projeto `auth_core` com base no estado atual do repositório.

Ele existe para orientar criação, revisão e refatoração incremental de código, sem exigir modernização ampla e não relacionada ao trabalho em andamento.

As regras aqui descritas seguem três premissas:

- `AuthCore.Domain` é a principal fonte de verdade para estilo e modelagem
- quando houver conflito entre padrão recente e legado residual, prevalece o padrão recente nas áreas alteradas
- o objetivo é ampliar consistência e previsibilidade, não impor refactors extensos sem necessidade real

## Fonte de Verdade e Princípios

O padrão de C# do projeto é guiado por alguns princípios recorrentes no código atual:

- modelagem orientada a domínio, com comportamento explícito e responsabilidades bem delimitadas
- clareza estrutural acima de atalhos “espertos” ou abstrações excessivamente implícitas
- consistência entre interface e implementação
- documentação XML pública como parte do contrato e da legibilidade do código
- preservação estrita da responsabilidade de cada camada

Na prática, isso significa:

- o domínio é a referência principal para escrita, organização e nomenclatura
- a aplicação orquestra casos de uso sem roubar regras centrais do domínio
- a API adapta HTTP sem absorver comportamento de negócio
- a infraestrutura implementa detalhes técnicos mantendo o modelo central no domínio

## Convenções Gerais de C#

As convenções abaixo representam o padrão dominante do repositório hoje:

- usar `namespace` no formato file-scoped
- considerar `ImplicitUsings` habilitado como padrão da solução
- considerar `Nullable` habilitado como padrão da solução
- declarar classes concretas preferencialmente como `sealed`
- nomear interfaces com prefixo `I`
- injetar dependências via construtor
- declarar dependências privadas como `private readonly`
- usar `async/await` de forma consistente nas operações assíncronas
- escolher `private set`, `protected set`, `set` ou `init` conforme o papel do tipo e o nível de encapsulamento esperado
- inicializar strings e coleções de forma defensiva quando a natureza do tipo exigir valor padrão seguro
- preferir tipos nomeados, explícitos e previsíveis em vez de construções muito implícitas

Também é importante registrar o que **não** representa o padrão dominante atual:

- `record` não é hoje a escolha principal para DTOs, commands, results ou contracts
- primary constructors não são a forma dominante de construção
- estilos muito funcionais não representam a identidade predominante do repositório
- helpers genéricos que escondem o fluxo da regra não fazem parte do padrão preferido

## Ordem de Membros e Organização Interna

A ordem de membros mais recorrente no repositório é:

1. constantes e campos privados
2. propriedades
3. construtores
4. métodos públicos
5. métodos privados

Esse padrão deve ser tratado como referência para novos arquivos e para trechos refatorados.

O uso de regiões é aceito com moderação quando realmente melhora a leitura. Os nomes mais recorrentes e alinhados ao projeto são:

- `Constructors`
- `Factory`
- `Helpers`
- `Validation`
- `Constants`

Regras de aplicação:

- métodos auxiliares privados devem permanecer no final da classe
- classes pequenas não precisam de regiões artificiais
- regiões devem organizar leitura, não introduzir cerimônia
- a prioridade é legibilidade e previsibilidade estrutural

## Documentação XML

A documentação XML faz parte do padrão de escrita do projeto, especialmente nos tipos de produção.

Os textos devem ser curtos, objetivos e em português. O padrão textual dominante é este:

- classes concretas: `Representa ...`
- interfaces: `Define operação ...` ou `Define operações ...`
- métodos: `Operação para ...`
- construtores controlados e fábricas: `Operação para criar instância da classe.`

Diretrizes de uso:

- documentar membros públicos de tipos de produção
- documentar propriedades públicas quando o significado não for trivial
- documentar métodos privados quando isso já fizer parte do padrão adotado no arquivo ou no módulo
- usar `param` com consistência sempre que o parâmetro carregar informação relevante
- usar `returns` quando isso melhorar a compreensão do contrato
- manter alinhamento semântico entre interface e implementação
- evitar comentários longos, genéricos ou enciclopédicos

Quando uma classe implementa uma interface, a implementação deve seguir a mesma linha semântica do contrato, evitando summaries contraditórios ou muito diferentes.

## Padrões por Tipo de Artefato

### Domínio

Na camada `AuthCore.Domain`, o padrão dominante é:

- agregados, entidades e value objects com comportamento explícito
- construtores privados quando a criação precisa ser controlada
- fábricas estáticas como `Create`, `Register`, `Restore` e `Read` quando fizer sentido
- validação interna de invariantes e consistência de estado
- encapsulamento por meio de propriedades com `private set`
- uso de `null!` apenas em propriedades controladas por factories, restauração ou materialização

Evite mover regra de negócio central para serviços utilitários ou para a aplicação quando ela pertence naturalmente ao tipo de domínio.

### Application

Na camada `AuthCore.Application`, o padrão dominante é:

- organização por caso de uso em formato vertical slice
- interface `I...UseCase`, implementação `...UseCase`, `Command` ou `Query` e `Result` quando necessário
- dependências recebidas pelo construtor
- validação de argumentos com `ArgumentNullException.ThrowIfNull(...)` quando aplicável
- orquestração de repositórios e transação sem reimplementar regra central de negócio
- retorno de DTOs de resultado somente quando agregam clareza ao fluxo

O caso de uso deve ser explícito e legível, com fluxo de execução fácil de seguir.

### Api

Na camada `AuthCore.Api`, o padrão dominante é:

- controllers finos, focados em adaptação HTTP
- contratos com sufixo `Request...Json` e `Response...Json`
- mapeamento simples de request para command ou query
- retorno de responses explícitas e previsíveis
- uso de object initializer quando ajuda a montar commands e responses com clareza

Controllers não devem assumir responsabilidade de regra de negócio.

### Infrastructure

Na camada `AuthCore.Infrastructure`, o padrão dominante é:

- classes técnicas explícitas e previsíveis
- classes, interfaces e modelos auxiliares com acessibilidade `internal` por padrão
- persistência com `Npgsql` e SQL explícito
- uso de raw string para SQL quando o arquivo já segue esse padrão
- materialização do domínio por factories como `Restore(...)`
- helpers privados concentrados no final da classe
- integração com o restante da solução via contratos do domínio e abstrações técnicas próprias

A infraestrutura implementa detalhes, mas não redefine o modelo do domínio.

A única classe pública esperada em `Infrastructure` é a classe responsável por registrar dependências no contêiner de injeção de dependência, como `InfrastructureDependencyInjection`. Options, migrações, repositórios, conexões, factories técnicas, implementações de serviços, métricas e modelos auxiliares não devem compor a superfície pública do assembly.

### Configurações e Options

Nas classes de configuração, o padrão mais recorrente é:

- tipo concreto `sealed` e `internal` quando estiver em `Infrastructure`
- constante `SectionName` quando a seção de configuração precisa ser nomeada
- propriedades com `init`
- uso de `string.Empty` para defaults seguros
- atributos de validação quando agregam proteção ao bootstrap da aplicação

### Testes

Nos projetos de teste, o padrão dominante é:

- nomes no formato `Metodo_WhenCondicao_ShouldResultado`
- foco em comportamento e efeito observável
- pouca cerimônia estrutural além do necessário para deixar o teste claro
- documentação XML usada com moderação, sem transformar teste em documentação excessiva

Testes devem privilegiar leitura rápida do cenário e da expectativa.

## Convenções de Escrita Observadas no Projeto

Além das regras estruturais, há convenções recorrentes que aparecem com frequência no código atual:

- uso de `ArgumentNullException.ThrowIfNull(...)` para dependências e argumentos quando aplicável
- uso de `string.Empty` como valor padrão para DTOs, requests, responses e options
- uso de `null!` apenas em propriedades cujo preenchimento é controlado pelo domínio ou pela materialização
- uso parcimonioso de recursos modernos do C# quando eles melhoram a clareza sem reduzir previsibilidade
- uso de raw string para SQL na infraestrutura PostgreSQL
- uso de object initializer em commands, results e responses

Essas convenções ajudam a manter o código uniforme entre camadas diferentes sem esconder o fluxo principal.

## Legado Residual e Regra de Evolução

O repositório possui um padrão dominante claro, mas ainda mantém alguns traços residuais de escrita mais antiga.

Esse legado aparece principalmente em pontos da base comum, com exemplos gerais como `EntityBase` e `DomainException`, onde ainda existem diferenças de:

- estilo textual de summaries
- ordem de membros
- organização física do arquivo
- nível de padronização em relação ao padrão mais recente

Esses trechos devem ser tratados como histórico de evolução, não como referência principal para código novo.

Regra de evolução:

- novo código deve seguir o padrão dominante atual
- trechos alterados devem ser alinhados ao padrão dominante quando isso puder ser feito com segurança
- inconsistências antigas não devem ser replicadas
- refactors amplos e não relacionados devem ser evitados

## Exemplos Canônicos

Os arquivos abaixo representam boas âncoras do estilo dominante do projeto:

- `src/Backend/AuthCore/AuthCore.Domain/Users/Aggregates/User.cs`
- `src/Backend/AuthCore/AuthCore.Application/Authentication/UseCases/Login/LoginUseCase.cs`
- `src/Backend/AuthCore/AuthCore.Infrastructure/Persistences/Write/PostgreSQL/Repositories/UserRepository.cs`

Esses exemplos mostram, em conjunto:

- `User.cs`: encapsulamento, factories estáticas, validação interna, XML docs em português e uso equilibrado de regiões
- `LoginUseCase.cs`: orquestração explícita, dependências no construtor, validação defensiva, helpers privados no final e fluxo transacional legível
- `UserRepository.cs`: classe técnica `sealed`, SQL em raw string, `ArgumentNullException.ThrowIfNull(...)` e helper privado simples para integração com a transação atual

Ao usar esses arquivos como referência, prefira reproduzir a organização e a intenção estrutural, e não copiar blocos de código literalmente.

## Checklist de Uso

Antes de concluir uma alteração C# neste projeto, revise:

- o arquivo usa `namespace` file-scoped
- a classe, interface ou tipo segue a convenção dominante do módulo
- tipos de `Infrastructure` permanecem `internal`, exceto a classe pública de registro de DI
- a ordem de membros está alinhada ao padrão predominante
- regiões foram usadas apenas quando realmente ajudam
- a documentação XML está em português e segue o padrão textual do projeto
- a responsabilidade permaneceu na camada correta
- o código novo foi alinhado ao padrão dominante atual, e não ao legado residual

## Relação com Outras Referências

Este guia complementa o `AGENTS.md` do repositório e deve ser lido junto com ele quando a tarefa exigir alinhamento fino de estilo.

Quando a mudança tocar aspectos de arquitetura, modelagem de domínio, casos de uso, contratos HTTP, persistência ou testes, use também os documentos específicos de `docs/agents/` correspondentes à área alterada.
