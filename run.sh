#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION_FILE="$ROOT_DIR/AuthCore.sln"
BACKEND_DIR="$ROOT_DIR/src/Backend"
COMPOSE_FILE="$BACKEND_DIR/docker-compose.yml"
ENV_FILE="$BACKEND_DIR/.env.development"
AUTHCORE_API_PROJECT="$BACKEND_DIR/AuthCore/AuthCore.Api/AuthCore.Api.csproj"
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
        AUTHCORE_API_PORT|NOTIFICATIONCORE_API_PORT|GATEWAY_API_PORT|ASPNETCORE_ENVIRONMENT|AUTHCORE_POSTGRES_PORT|AUTHCORE_POSTGRES_DB|AUTHCORE_POSTGRES_USER|AUTHCORE_POSTGRES_PASSWORD|AUTHCORE_DATABASE_MIGRATIONS_AUTOMIGRATEONSTARTUP|AUTHCORE_DATABASE_MIGRATIONS_ENSUREDATABASECREATED|NOTIFICATIONCORE_POSTGRES_PORT|NOTIFICATIONCORE_POSTGRES_DB|NOTIFICATIONCORE_POSTGRES_USER|NOTIFICATIONCORE_POSTGRES_PASSWORD|NOTIFICATIONCORE_DATABASE_MIGRATIONS_AUTOMIGRATEONSTARTUP|NOTIFICATIONCORE_DATABASE_MIGRATIONS_ENSUREDATABASECREATED|REDIS_PORT|REDIS_PASSWORD|AUTHCORE_REDIS_KEYPREFIX|RABBITMQ_PORT|RABBITMQ_MANAGEMENT_PORT|RABBITMQ_USERNAME|RABBITMQ_PASSWORD|RABBITMQ_EXCHANGE|RABBITMQ_ROUTING_KEY|RABBITMQ_QUEUE|RABBITMQ_DEAD_LETTER_QUEUE|SMTP_HOST|SMTP_CONTAINER_PORT|SMTP_PORT|SMTP_WEB_PORT|SMTP_USERNAME|SMTP_PASSWORD|SMTP_USE_TLS|SMTP_SENDER_EMAIL|SMTP_SENDER_NAME|SMTP_TIMEOUT_SECONDS|AUTHENTICATION_JWT_ISSUER|AUTHENTICATION_JWT_AUDIENCE|AUTHENTICATION_JWT_SIGNINGKEY|AUTHENTICATION_JWT_ACCESSTOKENLIFETIMEMINUTES|AUTHENTICATION_JWT_REFRESHTOKENLIFETIMEDAYS|AUTHENTICATION_JWT_CLOCKSKEWSECONDS|AUTHCORE_OUTBOX_ENABLED|AUTHCORE_OUTBOX_BATCH_SIZE|AUTHCORE_OUTBOX_POLLING_INTERVAL_SECONDS|AUTHCORE_OUTBOX_MAX_ATTEMPTS|NOTIFICATIONCORE_RABBITMQ_CONSUMER_ENABLED|NOTIFICATIONCORE_DISPATCHER_ENABLED|NOTIFICATIONCORE_DISPATCHER_BATCH_SIZE|NOTIFICATIONCORE_DISPATCHER_POLLING_INTERVAL_SECONDS|NOTIFICATIONCORE_DISPATCHER_RETRY_DELAY_SECONDS|NOTIFICATIONCORE_DISPATCHER_PROCESSING_TIMEOUT_SECONDS)
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
    require_environment_value "AUTHCORE_POSTGRES_PORT"
    require_environment_value "AUTHCORE_POSTGRES_DB"
    require_environment_value "AUTHCORE_POSTGRES_USER"
    require_environment_value "AUTHCORE_POSTGRES_PASSWORD"
    require_environment_value "NOTIFICATIONCORE_POSTGRES_PORT"
    require_environment_value "NOTIFICATIONCORE_POSTGRES_DB"
    require_environment_value "NOTIFICATIONCORE_POSTGRES_USER"
    require_environment_value "NOTIFICATIONCORE_POSTGRES_PASSWORD"
    require_environment_value "REDIS_PORT"
    require_environment_value "REDIS_PASSWORD"
    require_environment_value "AUTHCORE_REDIS_KEYPREFIX"
    require_environment_value "RABBITMQ_PORT"
    require_environment_value "RABBITMQ_MANAGEMENT_PORT"
    require_environment_value "RABBITMQ_USERNAME"
    require_environment_value "RABBITMQ_PASSWORD"
    require_environment_value "RABBITMQ_EXCHANGE"
    require_environment_value "RABBITMQ_ROUTING_KEY"
    require_environment_value "RABBITMQ_QUEUE"
    require_environment_value "RABBITMQ_DEAD_LETTER_QUEUE"
    require_environment_value "SMTP_PORT"
    require_environment_value "SMTP_CONTAINER_PORT"
    require_environment_value "SMTP_WEB_PORT"
    require_environment_value "SMTP_HOST"
    require_environment_value "SMTP_USE_TLS"
}

require_api_environment() {
    require_infrastructure_environment
    require_environment_value "AUTHCORE_API_PORT"
    require_environment_value "NOTIFICATIONCORE_API_PORT"
    require_environment_value "GATEWAY_API_PORT"
    require_environment_value "ASPNETCORE_ENVIRONMENT"
    require_environment_value "AUTHENTICATION_JWT_ISSUER"
    require_environment_value "AUTHENTICATION_JWT_AUDIENCE"
    require_environment_value "AUTHENTICATION_JWT_SIGNINGKEY"
    require_environment_value "AUTHENTICATION_JWT_ACCESSTOKENLIFETIMEMINUTES"
    require_environment_value "AUTHENTICATION_JWT_REFRESHTOKENLIFETIMEDAYS"
    require_environment_value "AUTHENTICATION_JWT_CLOCKSKEWSECONDS"
    require_environment_value "AUTHCORE_OUTBOX_ENABLED"
    require_environment_value "AUTHCORE_OUTBOX_BATCH_SIZE"
    require_environment_value "AUTHCORE_OUTBOX_POLLING_INTERVAL_SECONDS"
    require_environment_value "AUTHCORE_OUTBOX_MAX_ATTEMPTS"
    require_environment_value "NOTIFICATIONCORE_RABBITMQ_CONSUMER_ENABLED"
    require_environment_value "NOTIFICATIONCORE_DISPATCHER_ENABLED"
    require_environment_value "NOTIFICATIONCORE_DISPATCHER_BATCH_SIZE"
    require_environment_value "NOTIFICATIONCORE_DISPATCHER_POLLING_INTERVAL_SECONDS"
    require_environment_value "NOTIFICATIONCORE_DISPATCHER_RETRY_DELAY_SECONDS"
    require_environment_value "NOTIFICATIONCORE_DISPATCHER_PROCESSING_TIMEOUT_SECONDS"
    require_environment_value "SMTP_SENDER_EMAIL"
    require_environment_value "SMTP_SENDER_NAME"
    require_environment_value "SMTP_TIMEOUT_SECONDS"
}

export_api_configuration() {
    load_environment
    require_api_environment

    export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
    export CONNECTIONSTRINGS__POSTGRESQL="Host=localhost;Port=${AUTHCORE_POSTGRES_PORT};Database=${AUTHCORE_POSTGRES_DB};Username=${AUTHCORE_POSTGRES_USER};Password=${AUTHCORE_POSTGRES_PASSWORD};Pooling=true"
    export REDIS__CONNECTIONSTRING="localhost:${REDIS_PORT},password=${REDIS_PASSWORD},abortConnect=false"
    export REDIS__KEYPREFIX="${AUTHCORE_REDIS_KEYPREFIX}"
    export AUTHENTICATION__JWT__ISSUER="${AUTHENTICATION_JWT_ISSUER}"
    export AUTHENTICATION__JWT__AUDIENCE="${AUTHENTICATION_JWT_AUDIENCE}"
    export AUTHENTICATION__JWT__SIGNINGKEY="${AUTHENTICATION_JWT_SIGNINGKEY}"
    export AUTHENTICATION__JWT__ACCESSTOKENLIFETIMEMINUTES="${AUTHENTICATION_JWT_ACCESSTOKENLIFETIMEMINUTES}"
    export AUTHENTICATION__JWT__REFRESHTOKENLIFETIMEDAYS="${AUTHENTICATION_JWT_REFRESHTOKENLIFETIMEDAYS}"
    export AUTHENTICATION__JWT__CLOCKSKEWSECONDS="${AUTHENTICATION_JWT_CLOCKSKEWSECONDS}"
    export RABBITMQ__HOST="localhost"
    export RABBITMQ__PORT="${RABBITMQ_PORT}"
    export RABBITMQ__VIRTUALHOST="/"
    export RABBITMQ__USERNAME="${RABBITMQ_USERNAME}"
    export RABBITMQ__PASSWORD="${RABBITMQ_PASSWORD}"
    export RABBITMQ__EXCHANGE="${RABBITMQ_EXCHANGE}"
    export RABBITMQ__ROUTINGKEY="${RABBITMQ_ROUTING_KEY}"
    export RABBITMQ__QUEUE="${RABBITMQ_QUEUE}"
    export RABBITMQ__DEADLETTERQUEUE="${RABBITMQ_DEAD_LETTER_QUEUE}"
    export OUTBOX__ENABLED="${AUTHCORE_OUTBOX_ENABLED}"
    export OUTBOX__BATCHSIZE="${AUTHCORE_OUTBOX_BATCH_SIZE}"
    export OUTBOX__POLLINGINTERVALSECONDS="${AUTHCORE_OUTBOX_POLLING_INTERVAL_SECONDS}"
    export OUTBOX__MAXATTEMPTS="${AUTHCORE_OUTBOX_MAX_ATTEMPTS}"
}

print_usage() {
    cat <<EOF
Uso:
  ./run.sh [comando]

Comandos:
  dev       Sobe infraestrutura com Docker e executa AuthCore.Api localmente.
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
    compose up -d authcore-postgres notificationcore-postgres redis rabbitmq smtp
    INFRA_STARTED=true
    echo -e "${GREEN}Infraestrutura pronta.${NC}"
}

run_api() {
    ensure_command dotnet
    ensure_file "$AUTHCORE_API_PROJECT"
    export_api_configuration

    echo -e "${YELLOW}Executando AuthCore.Api localmente...${NC}"
    dotnet run --project "$AUTHCORE_API_PROJECT" --launch-profile http
}

watch_api() {
    ensure_command dotnet
    ensure_file "$AUTHCORE_API_PROJECT"
    export_api_configuration

    echo -e "${YELLOW}Executando AuthCore.Api com hot reload...${NC}"
    dotnet watch --project "$AUTHCORE_API_PROJECT" run --launch-profile http
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
