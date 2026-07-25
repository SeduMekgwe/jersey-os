[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$localDotnet = Join-Path $root '.dotnet\dotnet.exe'
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { 'dotnet' }

& $dotnet restore JerseyOs.slnx --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Backend restore failed.' }

& $dotnet format JerseyOs.slnx --no-restore --verify-no-changes
if ($LASTEXITCODE -ne 0) { throw 'Backend formatting check failed.' }

& $dotnet build JerseyOs.slnx --no-restore --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Backend build failed.' }

& $dotnet test JerseyOs.slnx --no-build --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed.' }

Push-Location 'src/JerseyOs.Web'
try {
    & corepack pnpm install --frozen-lockfile
    if ($LASTEXITCODE -ne 0) { throw 'Frontend restore failed.' }

    & corepack pnpm lint
    if ($LASTEXITCODE -ne 0) { throw 'Frontend lint failed.' }

    & corepack pnpm typecheck
    if ($LASTEXITCODE -ne 0) { throw 'Frontend typecheck failed.' }

    & corepack pnpm test --run
    if ($LASTEXITCODE -ne 0) { throw 'Frontend tests failed.' }

    & corepack pnpm build
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
}
finally {
    Pop-Location
}
