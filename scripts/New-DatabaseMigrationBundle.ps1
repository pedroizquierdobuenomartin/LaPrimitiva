[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$OutputDirectory,

    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$infrastructureProject = Join-Path $repositoryRoot 'LaPrimitiva.Infrastructure\LaPrimitiva.Infrastructure.csproj'
$startupProject = Join-Path $repositoryRoot 'LaPrimitiva.App\LaPrimitiva.App.csproj'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'publish'
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
$bundlePath = Join-Path $resolvedOutput 'LaPrimitiva.DatabaseMigration.exe'
$settingsPath = Join-Path $resolvedOutput 'appsettings.json'
$schemaVersionPath = Join-Path $resolvedOutput 'ESQUEMA_BD.version'
$manifestPath = Join-Path $resolvedOutput 'MIGRACIONES_BD.txt'

New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null

if (-not (Test-Path -LiteralPath $settingsPath)) {
    throw "No existe '$settingsPath'. Publique la aplicacion antes de generar el bundle."
}

$commonArguments = @(
    '--project', $infrastructureProject,
    '--startup-project', $startupProject,
    '--context', 'PrimitivaDbContext',
    '--configuration', $Configuration
)

if ($NoBuild) {
    $commonArguments += '--no-build'
}

$bundleArguments = @(
    '--output', $bundlePath,
    '--force',
    '--self-contained',
    '--target-runtime', 'win-x64'
)

Write-Host 'Generando EF migration bundle autocontenido para win-x64...'
& dotnet ef migrations bundle @bundleArguments @commonArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet ef migrations bundle termino con codigo $LASTEXITCODE."
}

$previousErrorActionPreference = $ErrorActionPreference
try {
    # Windows PowerShell 5.1 convierte stderr nativo redirigido en NativeCommandError.
    # Los avisos no deben abortar el proceso: la autoridad es el codigo de salida.
    $ErrorActionPreference = 'Continue'
    $listOutput = @(& dotnet ef migrations list --no-connect --json @commonArguments 2>&1)
    $listExitCode = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $previousErrorActionPreference
}

if ($listExitCode -ne 0) {
    throw "dotnet ef migrations list termino con codigo $listExitCode.`n$($listOutput -join [Environment]::NewLine)"
}

$listText = $listOutput -join [Environment]::NewLine
$jsonMatch = [regex]::Match($listText, '(?s)\[\s*\{.*\}\s*\]')
if (-not $jsonMatch.Success) {
    throw 'No se pudo extraer el manifiesto JSON de migraciones de la salida de dotnet ef.'
}

$parsedMigrations = $jsonMatch.Value | ConvertFrom-Json
$migrations = @()
foreach ($migration in $parsedMigrations) {
    $migrations += $migration
}
if ($migrations.Count -eq 0) {
    throw 'El modelo no contiene migraciones; no se puede generar un paquete identificable.'
}

$latestMigration = $migrations[-1].id
Set-Content -LiteralPath $schemaVersionPath -Value $latestMigration -Encoding ascii

$manifest = @(
    'LA PRIMITIVA AUDIT - ESQUEMA DE BASE DE DATOS INCLUIDO',
    "Generado (UTC): $([DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ'))",
    "Ultima migracion: $latestMigration",
    "Total de migraciones: $($migrations.Count)",
    '',
    'Migraciones incluidas:'
)
$manifest += $migrations | ForEach-Object { "- $($_.id)" }
$manifest += @(
    '',
    'REGLA OPERATIVA:',
    'Ejecute ActualizarBaseDatos.bat despues de copiar cada nueva publicacion y antes de iniciar IIS.',
    'El bundle consulta __EFMigrationsHistory y aplica unicamente las migraciones pendientes.',
    'Si la base ya esta actualizada, termina correctamente sin modificar el esquema.'
)
Set-Content -LiteralPath $manifestPath -Value $manifest -Encoding utf8

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ActualizarBaseDatos.bat') -Destination $resolvedOutput -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LEEME_ACTUALIZACION_BD.txt') -Destination $resolvedOutput -Force

Write-Host "Bundle generado: $bundlePath"
Write-Host "Version de esquema incluida: $latestMigration"
Write-Host 'Ejecute ActualizarBaseDatos.bat en el equipo de destino antes de iniciar la aplicacion.'
