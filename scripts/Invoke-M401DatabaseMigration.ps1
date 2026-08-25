[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('Update', 'Script')]
    [string]$Action = 'Update',

    [string]$ConnectionString,

    [string]$OutputPath,

    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$infrastructureProject = Join-Path $repositoryRoot 'LaPrimitiva.Infrastructure\LaPrimitiva.Infrastructure.csproj'
$startupProject = Join-Path $repositoryRoot 'LaPrimitiva.App\LaPrimitiva.App.csproj'

$commonArguments = @(
    '--project', $infrastructureProject,
    '--startup-project', $startupProject,
    '--context', 'PrimitivaDbContext'
)

if ($NoBuild) {
    $commonArguments += '--no-build'
}

if ($Action -eq 'Script') {
    if ([string]::IsNullOrWhiteSpace($OutputPath)) {
        $OutputPath = Join-Path $repositoryRoot 'artifacts\database\LaPrimitiva.Migrations.sql'
    }

    $resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
    $outputDirectory = Split-Path -Parent $resolvedOutput

    if ($PSCmdlet.ShouldProcess($resolvedOutput, 'Generar script idempotente de migraciones EF Core')) {
        New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
        & dotnet ef migrations script --idempotent --output $resolvedOutput @commonArguments
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet ef migrations script terminó con código $LASTEXITCODE."
        }

        Write-Host "Script de migración generado: $resolvedOutput"
    }

    return
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $ConnectionString = $env:LAPRIMITIVA_MIGRATION_CONNECTION
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $settingsPath = Join-Path $repositoryRoot 'LaPrimitiva.App\appsettings.json'
    $settings = Get-Content -Raw -LiteralPath $settingsPath | ConvertFrom-Json
    $ConnectionString = $settings.ConnectionStrings.DefaultConnection
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    throw 'No se encontró la conexión de migración. Use -ConnectionString o LAPRIMITIVA_MIGRATION_CONNECTION.'
}

if ($PSCmdlet.ShouldProcess('la base configurada', 'Aplicar migraciones EF Core pendientes')) {
    & dotnet ef database update @commonArguments --connection $ConnectionString
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet ef database update terminó con código $LASTEXITCODE."
    }

    Write-Host 'Migraciones EF Core aplicadas correctamente.'
}
