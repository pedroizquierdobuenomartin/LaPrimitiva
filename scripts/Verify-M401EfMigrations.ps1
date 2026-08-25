[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()

function Assert-Contains {
    param([string]$Path, [string]$Pattern, [string]$Message)

    $content = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot $Path)
    if ($content -notmatch $Pattern) {
        $failures.Add($Message)
    }
}

function Assert-NotContains {
    param([string]$Path, [string]$Pattern, [string]$Message)

    $content = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot $Path)
    if ($content -match $Pattern) {
        $failures.Add($Message)
    }
}

$seederPath = 'LaPrimitiva.Infrastructure\Persistence\Seed\WinningDrawSeeder.cs'
$programPath = 'LaPrimitiva.App\Program.cs'
$initialMigration = 'LaPrimitiva.Infrastructure\Migrations\20260113083854_InitialCreate.cs'
$winningDrawMigration = 'LaPrimitiva.Infrastructure\Migrations\20260204135951_AddWinningDraws.cs'
$planValidationMigration = 'LaPrimitiva.Infrastructure\Migrations\20260820160000_ValidatePlans.cs'
$drawValidationMigration = 'LaPrimitiva.Infrastructure\Migrations\20260824150000_ValidateWinningDraws.cs'

Assert-NotContains $seederPath 'IF\s+OBJECT_ID|CREATE\s+TABLE|ALTER\s+TABLE|ExecuteSqlRaw|EnsureAllTablesExist' 'El seeder todavía contiene DDL manual.'
Assert-NotContains $programPath 'Database\.(Migrate|EnsureCreated)' 'El arranque normal intenta modificar el esquema.'

Assert-Contains $initialMigration "OBJECT_ID\(N?'\[Plans\]'" 'La migración inicial no contempla Plans.'
Assert-Contains $initialMigration "OBJECT_ID\(N?'\[DrawRecords\]'" 'La migración inicial no contempla DrawRecords.'
Assert-Contains $winningDrawMigration "OBJECT_ID\(N?'\[WinningDraws\]'" 'La migración de históricos no contempla WinningDraws.'
Assert-Contains $winningDrawMigration 'COL_LENGTH' 'La migración no puede adoptar las columnas financieras de un esquema anterior.'
Assert-Contains $planValidationMigration 'TR_Plans_PreventOverlap' 'El trigger de solapamiento no está gobernado por migraciones.'
Assert-Contains $planValidationMigration 'sys\.check_constraints' 'Las restricciones de planes no son compatibles con el esquema legado.'
Assert-Contains $drawValidationMigration 'sys\.check_constraints' 'Las restricciones de sorteos no son compatibles con el esquema legado.'

Assert-Contains 'scripts\Invoke-M401DatabaseMigration.ps1' 'dotnet ef database update' 'No existe una vía administrativa para aplicar las migraciones.'
Assert-Contains 'scripts\Invoke-M401DatabaseMigration.ps1' 'migrations script --idempotent' 'No existe una vía para generar el script EF idempotente.'
Assert-Contains 'LaPrimitiva.Tests\Integration\M401MigrationTests.cs' 'Migrations_CreateTheCompleteSchema_FromScratch' 'Falta la prueba de creación desde cero.'
Assert-Contains 'LaPrimitiva.Tests\Integration\M401MigrationTests.cs' 'Migrations_AdoptLegacySchema_WithoutLosingData' 'Falta la prueba de adopción sin pérdida de datos.'

$obsoleteScripts = @(
    'LaPrimitiva.Infrastructure\Migrations\Scripts\CreateWinningDrawsTable.sql',
    'LaPrimitiva.App\RepositoriosLaPrimitivaLaPrimitiva.InfrastructureMigrationsScriptsApply_AddWinningDraws.sql'
)
foreach ($obsoleteScript in $obsoleteScripts) {
    if (Test-Path -LiteralPath (Join-Path $repositoryRoot $obsoleteScript)) {
        $failures.Add("Permanece el DDL manual obsoleto: $obsoleteScript")
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'M-401 verificado: esquema gobernado por migraciones, adopción legado cubierta y arranque sin DDL.'
