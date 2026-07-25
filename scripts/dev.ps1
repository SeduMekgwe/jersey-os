[CmdletBinding()]
param(
    [switch]$WithObservability,
    [switch]$Detached
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker Desktop with Compose v2 is required.'
}

if (-not (Test-Path '.env')) {
    throw 'Create .env from .env.example and replace every change-me value before starting Jersey OS.'
}

$arguments = @('compose')
if ($WithObservability) {
    $arguments += @('--profile', 'observability')
}

$arguments += @('up', '--build', '--wait')
if ($Detached) {
    $arguments += '--detach'
}

& docker @arguments
if ($LASTEXITCODE -ne 0) {
    throw "docker compose failed with exit code $LASTEXITCODE."
}
