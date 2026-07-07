# Guia de Boilerplate CI/CD - Build e Publicação Docker

Este documento descreve, em formato reutilizável, como foi construída a base de CI/CD do AuthCore para validar backend, validar frontend e publicar imagens Docker no GitHub Container Registry (GHCR).

O objetivo deste boilerplate é chegar até a publicação de imagens versionadas. Ele não configura deploy, servidor, staging, produção, SSH, secrets de ambiente ou environments do GitHub.

## 1. Definir o escopo do CI/CD

Antes de criar workflows, defina claramente o que o pipeline deve fazer.

No AuthCore, o escopo foi:

- validar o backend .NET;
- validar o frontend Next.js;
- validar Docker builds;
- publicar imagens Docker no GHCR;
- documentar o processo de release;
- não fazer deploy.

Essa separação evita misturar validação, publicação e entrega em servidor antes de existir infraestrutura real de runtime.

## 2. Organizar os workflows principais

Foram usados três workflows:

| Workflow | Arquivo | Responsabilidade |
|---|---|---|
| Backend CI | `.github/workflows/backend-ci.yml` | Validar backend .NET, testes, artifacts e Docker builds das APIs |
| Web CI | `.github/workflows/web-ci.yml` | Validar frontend Next.js, lint, build e Docker build |
| Docker Publish | `.github/workflows/docker-publish.yml` | Publicar imagens Docker versionadas no GHCR |

Ao replicar em outro repositório, mantenha CI e publish separados. CI deve rodar em pull requests. Publish deve rodar apenas em eventos confiáveis, como push em branches principais, tags ou execução manual.

## 3. Criar o Backend CI

O Backend CI valida a solução .NET antes de qualquer preocupação de publicação.

No AuthCore, ele executa:

- checkout do repositório;
- setup do .NET SDK;
- cache de pacotes NuGet;
- `dotnet restore`;
- `dotnet build`;
- testes unitários;
- testes de integração com serviços auxiliares;
- `dotnet publish` das APIs;
- upload de artifacts das APIs;
- Docker build local das imagens backend;
- GitHub Actions Summary com status da execução.

Pontos para adaptar:

- versão do SDK em `actions/setup-dotnet`;
- nome da solution;
- caminhos dos projetos de teste;
- serviços de integração, como PostgreSQL, Redis ou RabbitMQ;
- caminhos dos Dockerfiles;
- nomes das imagens locais de validação.

Exemplo de comandos centrais:

```bash
dotnet restore AuthCore.sln
dotnet build AuthCore.sln -c Release --no-restore
dotnet test tests/AuthCore.Domain.UnitTests/AuthCore.Domain.UnitTests.csproj -c Release --no-build
docker build -f src/Backend/AuthCore/AuthCore.Api/Dockerfile -t authcore-api-ci src
```

## 4. Criar o Web CI

O Web CI valida o frontend isoladamente.

No AuthCore, ele executa:

- checkout do repositório;
- setup do pnpm;
- setup do Node.js;
- cache baseado no lockfile;
- `pnpm install --frozen-lockfile`;
- `pnpm lint`;
- `pnpm build`;
- Docker build local do frontend;
- GitHub Actions Summary com status da execução.

O frontend usa `packageManager` no `package.json` para fixar o pnpm:

```json
"packageManager": "pnpm@11.7.0"
```

O Next.js usa `output: "standalone"` no `next.config.ts`, permitindo que o Dockerfile copie `.next/standalone` e `.next/static` para uma imagem adequada ao runtime SSR/Node.

Pontos para adaptar:

- versão do Node.js;
- versão do pnpm;
- caminho do frontend;
- nome do lockfile;
- variáveis de ambiente necessárias para build;
- comando de Docker build.

Exemplo de comandos centrais:

```bash
pnpm install --frozen-lockfile
pnpm lint
pnpm build
docker build -t authcore-web-ci src/Frontend/AuthCore.Web
```

## 5. Remover artifacts temporários quando o Docker build assumir a validação

Durante maturação do CI, pode ser útil publicar artifacts temporários, como `.next`, para debug.

No AuthCore, o upload do artifact `.next` foi removido depois que o Dockerfile standalone passou a validar o runtime do frontend.

Regra prática:

- mantenha artifacts quando eles forem úteis para diagnóstico ou entrega real;
- remova artifacts quando eles duplicarem o papel do Docker build;
- não trate `.next` como pacote ideal de deploy para Next.js SSR quando a imagem Docker standalone é o artefato de runtime.

## 6. Criar o workflow Docker Publish

O workflow de publicação deve ser separado do CI comum e não deve rodar em pull requests.

No AuthCore, o workflow roda em:

- push na branch `develop`;
- push na branch `main`;
- tags `v*`;
- `workflow_dispatch`.

Permissões mínimas usadas:

```yaml
permissions:
  contents: read
  packages: write
```

A autenticação no GHCR usa `GITHUB_TOKEN`:

```yaml
- name: Login to GitHub Container Registry
  uses: docker/login-action@v3
  with:
    registry: ghcr.io
    username: ${{ github.actor }}
    password: ${{ secrets.GITHUB_TOKEN }}
```

Não foi usado PAT e não foram criados secrets novos.

## 7. Publicar múltiplas imagens com matrix

Para manter o workflow simples, foi usada uma matrix com:

- nome lógico da imagem;
- imagem completa no GHCR;
- Dockerfile;
- contexto de build.

No AuthCore, as imagens são:

| Serviço | Imagem | Dockerfile | Contexto |
|---|---|---|---|
| AuthCore API | `ghcr.io/brunopsouz/authcore-api` | `src/Backend/AuthCore/AuthCore.Api/Dockerfile` | `src` |
| NotificationCore API | `ghcr.io/brunopsouz/notificationcore-api` | `src/Backend/NotificationCore/NotificationCore.Api/Dockerfile` | `src` |
| Gateway API | `ghcr.io/brunopsouz/gateway-api` | `src/Backend/Gateway/Gateway.Api/Dockerfile` | `src` |
| AuthCore Web | `ghcr.io/brunopsouz/authcore-web` | `src/Frontend/AuthCore.Web/Dockerfile` | `src/Frontend/AuthCore.Web` |

Ao adaptar para outro repositório, troque:

- owner do GHCR;
- nomes das imagens;
- caminhos dos Dockerfiles;
- contextos de build.

## 8. Configurar tags de imagem

O workflow usa `docker/metadata-action` para gerar tags consistentes.

Tags configuradas:

- `sha-<short-sha>` para todo push;
- `develop` para branch `develop`;
- `main` para branch `main`;
- `v1.0.0`, `v1.1.0` etc. para tags `v*`.

O AuthCore não publica `latest`.

Configuração base:

```yaml
- name: Extract Docker metadata
  id: meta
  uses: docker/metadata-action@v5
  with:
    images: ${{ matrix.image }}
    flavor: |
      latest=false
    tags: |
      type=sha,prefix=sha-,format=short
      type=ref,event=branch
      type=ref,event=tag
```

Recomendação:

- use `develop` apenas para desenvolvimento;
- use `main` para a linha principal estável;
- use `sha-<short-sha>` para deploy reprodutível;
- use tags `v*` para releases versionadas;
- evite `latest` até existir uma política clara de promoção.

## 9. Adicionar GitHub Actions Summary

Os workflows usam `$GITHUB_STEP_SUMMARY` para registrar informações úteis no final da execução.

No Docker Publish, o summary lista:

- nome da imagem;
- registry;
- Dockerfile;
- contexto;
- ausência de deploy;
- ausência de environment.

Esse summary ajuda a auditar rapidamente o que foi publicado, sem depender apenas dos logs.

## 10. Documentar o processo de release

Foi criado um documento específico para o processo de release:

```text
docs/ci-cd/release-process.md
```

Esse documento explica:

- objetivo da publicação;
- workflows envolvidos;
- imagens publicadas;
- publicação por `develop`;
- publicação por `main`;
- criação de tags `v*`;
- uso recomendado das tags;
- ausência de deploy.

Ao replicar o boilerplate, mantenha a documentação de release separada dos READMEs. Os READMEs devem conter apenas resumos e links para o documento detalhado.

## 11. Atualizar READMEs sem duplicar documentação

No AuthCore, os READMEs foram ajustados assim:

- README geral: visão do monorepo, resumo de CI/CD e link para release process;
- README Backend: detalhes do CI backend, APIs, rotas, testes e imagens backend;
- README Frontend: requisitos, comandos, Web CI, Next.js standalone e imagem web.

Regra prática:

- README geral deve explicar o monorepo;
- README Backend deve explicar detalhes .NET;
- README Frontend deve explicar detalhes web;
- `docs/ci-cd/release-process.md` deve explicar release/publicação.

## 12. Checklist para replicar em outro repositório

Use este checklist ao aplicar o boilerplate:

- Definir branches que publicam imagens.
- Definir se PRs rodam apenas CI, nunca publish.
- Criar ou revisar Dockerfiles de cada serviço.
- Validar Docker build local antes de publicar.
- Criar Backend CI, se houver backend.
- Criar Web CI, se houver frontend.
- Fixar package manager do frontend quando aplicável.
- Criar Docker Publish com `GITHUB_TOKEN`.
- Usar `contents: read` e `packages: write`.
- Desabilitar `latest` se não houver política de promoção.
- Criar tags `sha-<short-sha>`, branch e `v*`.
- Adicionar summary com `$GITHUB_STEP_SUMMARY`.
- Documentar release em `docs/ci-cd/release-process.md`.
- Atualizar READMEs apenas com resumos e links.
- Rodar `git diff --check`.

## 13. O que este boilerplate não cobre

Este boilerplate não configura:

- deploy automático;
- staging;
- produção;
- servidor;
- SSH;
- Kubernetes;
- Docker Compose remoto;
- secrets de produção;
- environments do GitHub;
- aprovação manual de ambiente;
- rollback.

Esses temas devem ser tratados em uma fase posterior, quando existir infraestrutura de execução definida.

## 14. Validações recomendadas

Antes de abrir pull request, execute:

```bash
git diff --check
```

Quando houver mudança no frontend:

```bash
cd src/Frontend/AuthCore.Web
pnpm install --frozen-lockfile
pnpm lint
pnpm build
```

Quando houver mudança no backend:

```bash
dotnet restore AuthCore.sln
dotnet build AuthCore.sln -c Release --no-restore
dotnet test tests/AuthCore.Domain.UnitTests/AuthCore.Domain.UnitTests.csproj -c Release --no-build
dotnet test tests/AuthCore.Application.UnitTests/AuthCore.Application.UnitTests.csproj -c Release --no-build
dotnet test tests/NotificationCore.Domain.UnitTests/NotificationCore.Domain.UnitTests.csproj -c Release --no-build
dotnet test tests/NotificationCore.Application.UnitTests/NotificationCore.Application.UnitTests.csproj -c Release --no-build
```

Para executar testes de integração ou a solution inteira, suba também os serviços e variáveis equivalentes ao CI, como PostgreSQL, Redis e RabbitMQ.

Quando houver mudança em Dockerfiles:

```bash
docker build -f <caminho-do-dockerfile> -t <imagem-local> <contexto>
```

Para publicação real, use push em branch permitida, tag `v*` ou `workflow_dispatch`, conforme a política do repositório.
