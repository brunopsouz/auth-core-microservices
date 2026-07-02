# Proposta para `.github/workflows/docker-publish.yml`

Este arquivo é uma proposta para a próxima fase. Ele não publica imagens hoje.

```yaml
name: Docker Publish

on:
  push:
    branches:
      - develop
      - main
    tags:
      - "v*"
  workflow_dispatch:

permissions:
  contents: read
  packages: write

concurrency:
  group: docker-publish-${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true

env:
  REGISTRY: ghcr.io

jobs:
  publish:
    name: Build and Publish Images
    runs-on: ubuntu-latest
    timeout-minutes: 40

    strategy:
      fail-fast: false
      matrix:
        include:
          - image: authcore-api
            context: src
            file: src/Backend/AuthCore/AuthCore.Api/Dockerfile
          - image: notificationcore-api
            context: src
            file: src/Backend/NotificationCore/NotificationCore.Api/Dockerfile
          - image: gateway-api
            context: src
            file: src/Backend/Gateway/Gateway.Api/Dockerfile
          - image: authcore-web
            context: src/Frontend/AuthCore.Web
            file: src/Frontend/AuthCore.Web/Dockerfile

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Login to GHCR
        uses: docker/login-action@v3
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Normalize image owner
        run: echo "IMAGE_OWNER=${GITHUB_REPOSITORY_OWNER,,}" >> "$GITHUB_ENV"

      - name: Extract metadata
        id: meta
        uses: docker/metadata-action@v5
        with:
          images: ${{ env.REGISTRY }}/${{ env.IMAGE_OWNER }}/${{ matrix.image }}
          tags: |
            type=sha,prefix=sha-
            type=ref,event=branch
            type=semver,pattern={{version}}
            type=semver,pattern={{major}}.{{minor}}

      - name: Build and push image
        uses: docker/build-push-action@v6
        with:
          context: ${{ matrix.context }}
          file: ${{ matrix.file }}
          push: true
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
```

Tags previstas:

- `sha-<commit>`
- nome da branch, como `develop` ou `main`
- versão semântica quando o push for uma tag `v*`

Não há secrets reais nesta proposta. O login usa `GITHUB_TOKEN`, que exige `packages: write`.
