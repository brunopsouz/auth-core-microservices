#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION_FILE="$ROOT_DIR/AuthCore.sln"
BACKEND_DIR="$ROOT_DIR/src/Backend"
COMPOSE_FILE="$BACKEND_DIR/docker-compose.yml"
ENV_FILE="$BACKEND_DIR/.env.development"
API_PROJECT="$BACKEND_DIR/AuthCore/AuthCore.Api/AuthCore.Api.csproj"
DEFAULT_COMMAND="${1:-dev}"

GREEN='\033[1;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'
INFRA_STARTED=false

compose() {
    if command -v docker-compose >/dev/null 2>&1; then
        docker-compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" "$@"
        return
    fi

    docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" "$@"
}

ensure_command() {
    local command_name="$1"

    if ! command -v "$command_name" >/dev/null 2>&1; then
        echo -e "${RED}Erro:${NC} comando '$command_name' nao foi encontrado."
        exit 1
    fi
}

ensure_file() {
    local file_path="$1"

    if [ ! -f "$file_path" ]; then
        echo -e "${RED}Erro:${NC} arquivo nao encontrado: $file_path"
        exit 1
    fi
}

load_environment() {
    ensure_file "$ENV_FILE"

    local line
    local variable_name
    local variable_value

    while IFS= read -r line || [ -n "$line" ]; do
        if [[ -z "$line" || "$line" =~ ^[[:space:]]*# ]]; then
            continue
        fi

        if [[ "$line" != *=* ]]; then
            echo -e "${RED}Erro:${NC} linha invalida em $ENV_FILE: $line"
            exit 1
        fi

        variable_name="${line%%=*}"
        variable_value="${line#*=}"

        if [[ ! "$variable_name" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]]; then
            echo -e "${RED}Erro:${NC} nome de variavel invalido em $ENV_FILE: $variable_name"
            exit 1
        fi

        if is_development_environment_variable "$variable_name"; then
            printf -v "$variable_name" '%s' "$variable_value"
            export "$variable_name"
        fi
    done < "$ENV_FILE"
}

is_development_environment_variable() {
    case "$1" in
        API_PORT|ASPNETCORE_ENVIRONMENT|POSTGRES_PORT|POSTGRES_DB|POSTGRES_USER|POSTGRES_PASSWORD|REDIS_PORT|REDIS_PASSWORD|REDIS__KEYPREFIX|RABBITMQ_PORT|RABBITMQ_MANAGEMENT_PORT|RABBITMQ__USERNAME|RABBITMQ__PASSWORD|RABBITMQ__EMAILVERIFICATIONQUEUE|AUTHENTICATION__JWT__ISSUER|AUTHENTICATION__JWT__AUDIENCE|AUTHENTICATION__JWT__SIGNINGKEY|AUTHENTICATION__JWT__ACCESSTOKENLIFETIMEMINUTES)
            return 0
            ;;
        *)
            return 1
            ;;
    esac
}

require_environment_value() {
    local variable_name="$1"

    if [ -z "${!variable_name:-}" ]; then
        echo -e "${RED}Erro:${NC} variavel '$variable_name' nao foi configurada em $ENV_FILE."
        exit 1
    fi
}

require_infrastructure_environment() {
    require_environment_value "POSTGRES_PORT"
    require_environment_value "POSTGRES_DB"
    require_environment_value "POSTGRES_USER"
    require_environment_value "POSTGRES_PASSWORD"
    require_environment_value "REDIS_PORT"
    require_environment_value "REDIS_PASSWORD"
    require_environment_value "REDIS__KEYPREFIX"
    require_environment_value "RABBITMQ_PORT"
    require_environment_value "RABBITMQ_MANAGEMENT_PORT"
    require_environment_value "RABBITMQ__USERNAME"
    require_environment_value "RABBITMQ__PASSWORD"
    require_environment_value "RABBITMQ__EMAILVERIFICATIONQUEUE"
}

require_api_environment() {
    require_infrastructure_environment
    require_environment_value "API_PORT"
    require_environment_value "ASPNETCORE_ENVIRONMENT"
    require_environment_value "AUTHENTICATION__JWT__ISSUER"
    require_environment_value "AUTHENTICATION__JWT__AUDIENCE"
    require_environment_value "AUTHENTICATION__JWT__SIGNINGKEY"
    require_environment_value "AUTHENTICATION__JWT__ACCESSTOKENLIFETIMEMINUTES"
}

export_api_configuration() {
    load_environment
    require_api_environment

    export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
    export CONNECTIONSTRINGS__POSTGRESQL="Host=localhost;Port=${POSTGRES_PORT};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};Pooling=true"
    export REDIS__CONNECTIONSTRING="localhost:${REDIS_PORT},password=${REDIS_PASSWORD},abortConnect=false"
    export RABBITMQ__HOST="localhost"
    export RABBITMQ__PORT="${RABBITMQ_PORT}"
}

print_usage() {
    cat <<EOF
Uso:
  ./run.sh [comando]

Comandos:
  dev       Sobe postgres, redis e rabbitmq com Docker e executa a API localmente.
  watch     Igual ao dev, mas usa dotnet watch.
  infra     Sobe apenas a infraestrutura em background.
  docker    Sobe toda a aplicacao com Docker Compose.
  build     Compila a solucao.
  test      Executa os testes da solucao.
  down      Encerra os containers do docker compose.
  help      Exibe esta ajuda.

Exemplos:
  ./run.sh
  ./run.sh watch
  ./run.sh docker
  ./run.sh down
EOF
}

cleanup() {
    if [ "$INFRA_STARTED" != true ]; then
        return
    fi

    INFRA_STARTED=false

    echo
    echo -e "${YELLOW}Encerrando infraestrutura...${NC}"
    compose down --remove-orphans
    echo -e "${GREEN}Infraestrutura encerrada.${NC}"
}

start_infra() {
    ensure_command docker
    ensure_file "$COMPOSE_FILE"
    ensure_file "$ENV_FILE"
    load_environment
    require_infrastructure_environment

    echo -e "${YELLOW}Subindo infraestrutura...${NC}"
    compose up -d postgres redis rabbitmq
    INFRA_STARTED=true
    echo -e "${GREEN}Infraestrutura pronta.${NC}"
}

run_api() {
    ensure_command dotnet
    ensure_file "$API_PROJECT"
    export_api_configuration

    echo -e "${YELLOW}Executando AuthCore.Api localmente...${NC}"
    dotnet run --project "$API_PROJECT" --launch-profile http
}

watch_api() {
    ensure_command dotnet
    ensure_file "$API_PROJECT"
    export_api_configuration

    echo -e "${YELLOW}Executando AuthCore.Api com hot reload...${NC}"
    dotnet watch --project "$API_PROJECT" run --launch-profile http
}

run_docker() {
    ensure_command docker
    ensure_file "$COMPOSE_FILE"
    ensure_file "$ENV_FILE"
    load_environment
    require_api_environment

    echo -e "${YELLOW}Subindo aplicacao completa com Docker Compose...${NC}"
    compose up --build
}

build_solution() {
    ensure_command dotnet
    ensure_file "$SOLUTION_FILE"

    echo -e "${YELLOW}Compilando solucao...${NC}"
    dotnet build "$SOLUTION_FILE"
}

test_solution() {
    ensure_command dotnet
    ensure_file "$SOLUTION_FILE"

    echo -e "${YELLOW}Executando testes...${NC}"
    dotnet test "$SOLUTION_FILE"
}

stop_containers() {
    ensure_command docker
    ensure_file "$COMPOSE_FILE"
    ensure_file "$ENV_FILE"
    load_environment
    require_infrastructure_environment

    echo -e "${YELLOW}Encerrando containers...${NC}"
    compose down --remove-orphans
    echo -e "${GREEN}Containers encerrados.${NC}"
}

case "$DEFAULT_COMMAND" in
    dev)
        start_infra
        trap cleanup EXIT INT TERM
        run_api
        ;;
    watch)
        start_infra
        trap cleanup EXIT INT TERM
        watch_api
        ;;
    infra)
        start_infra
        ;;
    docker)
        run_docker
        ;;
    build)
        build_solution
        ;;
    test)
        test_solution
        ;;
    down)
        stop_containers
        ;;
    help|-h|--help)
        print_usage
        ;;
    *)
        echo -e "${RED}Erro:${NC} comando invalido '$DEFAULT_COMMAND'."
        echo
        print_usage
        exit 1
        ;;
esac
