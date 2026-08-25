[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Assert-Contains {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Expected,

        [Parameter(Mandatory)]
        [string]$Message
    )

    $resolvedPath = Join-Path $repositoryRoot $Path
    if (-not (Test-Path -LiteralPath $resolvedPath)) {
        throw "$Message No existe '$Path'."
    }

    $content = Get-Content -Raw -LiteralPath $resolvedPath
    if (-not $content.Contains($Expected)) {
        throw "$Message Falta '$Expected' en '$Path'."
    }
}

Assert-Contains 'Publish.bat' 'New-DatabaseMigrationBundle.ps1' 'Publish.bat no genera el paquete de migración.'
Assert-Contains 'Publish.bat' '-NoBuild' 'El bundle debe reutilizar la compilación Release del publish.'

Assert-Contains 'scripts\New-DatabaseMigrationBundle.ps1' 'dotnet ef migrations bundle' 'No se genera un EF migration bundle.'
Assert-Contains 'scripts\New-DatabaseMigrationBundle.ps1' '--self-contained' 'El bundle no es autocontenido.'
Assert-Contains 'scripts\New-DatabaseMigrationBundle.ps1' 'win-x64' 'El bundle no está dirigido a Windows x64.'
Assert-Contains 'scripts\New-DatabaseMigrationBundle.ps1' 'ESQUEMA_BD.version' 'No se genera el marcador de versión del esquema.'
Assert-Contains 'scripts\New-DatabaseMigrationBundle.ps1' 'MIGRACIONES_BD.txt' 'No se genera el manifiesto de migraciones.'
Assert-Contains 'scripts\New-DatabaseMigrationBundle.ps1' 'ActualizarBaseDatos.bat' 'No se entrega el lanzador de actualización.'

Assert-Contains 'scripts\ActualizarBaseDatos.bat' 'LaPrimitiva.DatabaseMigration.exe' 'El lanzador no ejecuta el bundle.'
Assert-Contains 'scripts\ActualizarBaseDatos.bat' 'LAPRIMITIVA_MIGRATION_CONNECTION' 'El lanzador no permite una conexión administrativa externa.'
Assert-Contains 'scripts\ActualizarBaseDatos.bat' 'pause' 'El lanzador no mantiene visible el resultado.'

Assert-Contains 'README.md' 'ActualizarBaseDatos.bat' 'El procedimiento portable no está documentado.'
Assert-Contains 'README.md' 'ESQUEMA_BD.version' 'No se documenta cómo reconocer cambios de esquema.'

$scriptsToParse = @(
    'scripts\New-DatabaseMigrationBundle.ps1',
    'scripts\Verify-DatabaseMigrationBundle.ps1'
)

foreach ($relativePath in $scriptsToParse) {
    $resolvedPath = Join-Path $repositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $resolvedPath)) {
        throw "No existe '$relativePath'."
    }

    $tokens = $null
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile(
        $resolvedPath,
        [ref]$tokens,
        [ref]$errors)

    if ($errors.Count -gt 0) {
        throw "'$relativePath' contiene errores de sintaxis: $($errors.Message -join '; ')"
    }
}

Write-Host 'Verificación estática del paquete portable de migraciones: correcta.'
