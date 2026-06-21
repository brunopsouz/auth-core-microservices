[CmdletBinding()]
param(
    [string[]]$Services,
    [switch]$All,
    [switch]$Infra,
    [switch]$NoCache,
    [switch]$Pull
)

$ErrorActionPreference = "Stop"

$rootDir = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $rootDir "src/Backend/docker-compose.yml"
$envFile = Join-Path $rootDir "src/Backend/.env.development"

if (-not (Test-Path $composeFile)) {
    throw "Arquivo docker-compose nao encontrado em '$composeFile'."
}

if (-not (Test-Path $envFile)) {
    throw "Arquivo de ambiente nao encontrado em '$envFile'."
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker nao foi encontrado no PATH."
}

$defaultServices = @("authcore-api", "notificationcore-api", "gateway-api")
$infraServices = @("authcore-postgres", "notificationcore-postgres", "redis", "rabbitmq")
$allServices = @(
    "authcore-api",
    "notificationcore-api",
    "gateway-api",
    "authcore-postgres",
    "notificationcore-postgres",
    "redis",
    "rabbitmq"
)

if ($All.IsPresent -and $Infra.IsPresent) {
    throw "Use apenas um entre -All e -Infra."
}

if ($All.IsPresent) {
    $targetServices = $allServices
}
elseif ($Infra.IsPresent) {
    $targetServices = $infraServices
}
elseif ($Services -and $Services.Count -gt 0) {
    $targetServices = $Services
}
else {
    $targetServices = $defaultServices
}

$composeArgs = @(
    "compose",
    "--env-file", $envFile,
    "-f", $composeFile
)

if ($Pull.IsPresent) {
    Write-Host "Atualizando imagens base..."
    & docker @composeArgs pull @targetServices
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$upArgs = @("up", "-d", "--build")

if ($NoCache.IsPresent) {
    Write-Host "Executando build sem cache..."
    & docker @composeArgs build "--no-cache" @targetServices
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $upArgs = @("up", "-d")
}

Write-Host ("Atualizando containers: " + ($targetServices -join ", "))
& docker @composeArgs $upArgs @targetServices
exit $LASTEXITCODE
