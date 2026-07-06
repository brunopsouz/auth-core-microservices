# Processo de Release - AuthCore

## Objetivo

O processo atual de release do AuthCore publica imagens Docker versionadas no GitHub Container Registry (GHCR) para uso futuro.

Este processo ainda não faz deploy. O projeto ainda não possui servidor, staging ou produção configurados. A etapa atual serve apenas para buildar e publicar imagens Docker rastreáveis.

## Workflows envolvidos

- Backend CI: executa validações do backend, incluindo restore, build, testes e builds Docker das APIs.
- Web CI: executa validações do frontend, incluindo instalação com pnpm, lint, build do Next.js e build Docker da aplicação web.
- Docker Publish: publica imagens Docker no GHCR para branches e tags permitidas.

Os workflows de CI validam o código, mas o workflow Docker Publish não depende tecnicamente deles. A proteção entre validação e publicação deve ser garantida por políticas de branch protection quando isso for necessário.

## Imagens publicadas

| Serviço | Imagem |
|---|---|
| AuthCore API | `ghcr.io/brunopsouz/authcore-api` |
| NotificationCore API | `ghcr.io/brunopsouz/notificationcore-api` |
| Gateway API | `ghcr.io/brunopsouz/gateway-api` |
| AuthCore Web | `ghcr.io/brunopsouz/authcore-web` |

## Como publicar imagens a partir da develop

Um push ou merge na branch `develop` dispara o workflow `.github/workflows/docker-publish.yml`.

Nesse caso, as imagens são publicadas com:

- tag `develop`
- tag `sha-<short-sha>`

A tag `develop` representa o estado mais recente de desenvolvimento.

## Como publicar imagens a partir da main

Um push ou merge na branch `main` dispara o workflow `.github/workflows/docker-publish.yml`.

Nesse caso, as imagens são publicadas com:

- tag `main`
- tag `sha-<short-sha>`

A tag `main` deve representar uma linha mais estável da branch principal.

## Como criar uma versão com tag v*

Para criar uma versão semântica a partir da `main`, use:

```bash
git checkout main
git pull origin main
git tag v1.0.0
git push origin v1.0.0
```

O push da tag `v1.0.0` dispara o workflow `.github/workflows/docker-publish.yml` e publica as imagens com a tag `v1.0.0`.

## Publicação manual

O workflow `.github/workflows/docker-publish.yml` também pode ser executado manualmente por `workflow_dispatch`.

Esse caminho deve ser usado apenas quando fizer sentido republicar imagens de uma referência já conhecida. O processo recomendado continua sendo publicar a partir de `develop`, `main` ou tags `v*`.

## Qual tag usar

- `develop`: testes internos e desenvolvimento.
- `main`: última versão estável da branch principal.
- `sha-<short-sha>`: versão imutável e reprodutível, recomendada para deploy real.
- `v1.0.0`: release versionada.
- `latest`: não usada.

Para produção futura, o ideal é usar `sha-<short-sha>` ou uma tag `v*`, não `develop`.
