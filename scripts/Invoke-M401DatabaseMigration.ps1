[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('Update', 'Script')]
    [string]$Action = 'Update',

    [string]$ConnectionString,

    [string]$OutputPath,

    [switch]$NoBuild,

    [string]$LogPath
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'OperationalLog.ps1')
if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $repositoryRoot ("artifacts\logs\operations-{0}.jsonl" -f (Get-Date -Format 'yyyyMMdd'))
}
$correlationId = [Guid]::NewGuid().ToString('N')
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
        Write-OperationalLog -Path $LogPath -Operation 'DatabaseMigrationScript' -Status started -CorrelationId $correlationId -Message 'Generando script idempotente de migraciones.'
        New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
        & dotnet ef migrations script --idempotent --output $resolvedOutput @commonArguments
        if ($LASTEXITCODE -ne 0) {
            $exception = [InvalidOperationException]::new("dotnet ef migrations script terminó con código $LASTEXITCODE.")
            Write-OperationalLog -Path $LogPath -Operation 'DatabaseMigrationScript' -Status failed -Level Error -CorrelationId $correlationId -Message 'Falló la generación del script de migraciones.' -Properties @{ exitCode = $LASTEXITCODE } -Exception $exception
            throw $exception
        }

        Write-OperationalLog -Path $LogPath -Operation 'DatabaseMigrationScript' -Status succeeded -CorrelationId $correlationId -Message 'Script idempotente de migraciones generado.' -Properties @{ outputPath = $resolvedOutput }
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
    $exception = [InvalidOperationException]::new('No se encontró la conexión de migración. Use -ConnectionString o LAPRIMITIVA_MIGRATION_CONNECTION.')
    Write-OperationalLog -Path $LogPath -Operation 'DatabaseMigrationUpdate' -Status failed -Level Error -CorrelationId $correlationId -Message 'No se encontró una conexión de migración configurada.' -Exception $exception
    throw $exception
}

if ($PSCmdlet.ShouldProcess('la base configurada', 'Aplicar migraciones EF Core pendientes')) {
    Write-OperationalLog -Path $LogPath -Operation 'DatabaseMigrationUpdate' -Status started -CorrelationId $correlationId -Message 'Aplicando migraciones EF Core pendientes.'
    & dotnet ef database update @commonArguments --connection $ConnectionString
    if ($LASTEXITCODE -ne 0) {
        $exception = [InvalidOperationException]::new("dotnet ef database update terminó con código $LASTEXITCODE.")
        Write-OperationalLog -Path $LogPath -Operation 'DatabaseMigrationUpdate' -Status failed -Level Error -CorrelationId $correlationId -Message 'Falló la aplicación de migraciones.' -Properties @{ exitCode = $LASTEXITCODE } -Exception $exception
        throw $exception
    }

    Write-OperationalLog -Path $LogPath -Operation 'DatabaseMigrationUpdate' -Status succeeded -CorrelationId $correlationId -Message 'Migraciones EF Core aplicadas correctamente.'
    Write-Host 'Migraciones EF Core aplicadas correctamente.'
}
