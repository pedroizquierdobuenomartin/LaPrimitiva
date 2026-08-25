[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()

function Read-RepoFile([string]$RelativePath) {
    Get-Content -Raw (Join-Path $repoRoot $RelativePath)
}

function Require-Match([string]$Text, [string]$Pattern, [string]$Message) {
    if ($Text -notmatch $Pattern) { $failures.Add($Message) }
}

$entities = @(
    'LaPrimitiva.Domain/Entities/Plan.cs',
    'LaPrimitiva.Domain/Entities/DrawRecord.cs',
    'LaPrimitiva.Domain/Entities/WinningDraw.cs'
)
foreach ($path in $entities) {
    Require-Match (Read-RepoFile $path) 'byte\[\]\s+RowVersion' "$path no expone RowVersion."
}

$dbContext = Read-RepoFile 'LaPrimitiva.Infrastructure/PrimitivaDbContext.cs'
if (([regex]::Matches($dbContext, 'Property\(e => e\.RowVersion\)\.IsRowVersion\(\)')).Count -ne 3) {
    $failures.Add('PrimitivaDbContext no configura exactamente los tres tokens rowversion.')
}

$migration = Read-RepoFile 'LaPrimitiva.Infrastructure/Migrations/20260825120000_AddConcurrencyTokens.cs'
if (([regex]::Matches($migration, 'ADD \[RowVersion\] rowversion NOT NULL')).Count -ne 3) {
    $failures.Add('La migración no crea tres columnas rowversion.')
}
if (([regex]::Matches($migration, "COL_LENGTH\(N'\[[^']+\]', N'RowVersion'\) IS NULL")).Count -ne 3) {
    $failures.Add('La migración no protege la adopción de esquemas que ya contienen RowVersion.')
}

$repositories = @(
    'LaPrimitiva.Infrastructure/Repositories/PlanRepository.cs',
    'LaPrimitiva.Infrastructure/Repositories/DrawRepository.cs',
    'LaPrimitiva.Infrastructure/Repositories/WinningDrawRepository.cs'
)
foreach ($path in $repositories) {
    $content = Read-RepoFile $path
    Require-Match $content 'Property\(entity => entity\.RowVersion\)\.OriginalValue' "$path no compara el token recibido con el persistido."
    Require-Match $content 'DbUpdateConcurrencyException' "$path no traduce el conflicto de EF Core."
    Require-Match $content 'ConcurrencyConflictException' "$path no expone un conflicto comprensible a la UI."
}

$pages = @(
    'LaPrimitiva.App/Components/Pages/Plans.razor',
    'LaPrimitiva.App/Components/Pages/Register.razor',
    'LaPrimitiva.App/Components/Pages/HistoricalDraws.razor'
)
foreach ($path in $pages) {
    $content = Read-RepoFile $path
    Require-Match $content 'catch \(ConcurrencyConflictException' "$path no informa del conflicto de concurrencia."
    Require-Match $content 'Recargar datos actuales' "$path no ofrece recargar el registro vigente."
}

$unitTests = Read-RepoFile 'LaPrimitiva.Tests/M403ConcurrencyTests.cs'
Require-Match $unitTests 'EditableEntities_ConfigureRowVersionAsGeneratedConcurrencyToken' 'Falta la prueba del modelo de concurrencia.'
Require-Match $unitTests 'ReportsConcurrencyConflict' 'Falta la prueba de traducción del conflicto.'

$integrationTests = Read-RepoFile 'LaPrimitiva.Tests/Integration/M403ConcurrencyIntegrationTests.cs'
Require-Match $integrationTests 'RejectsTheStaleCopyWithoutOverwritingTheWinner' 'Falta la prueba real de dos copias concurrentes.'
Require-Match $integrationTests 'Collection\(IntegrationTestCollection\.Name\)' 'La prueba M-403 no pertenece a la colección que proporciona el fixture SQL.'

$integrationSettings = Read-RepoFile 'LaPrimitiva.Tests/appsettings.IntegrationTests.json'
Require-Match $integrationSettings 'localhost\\\\LOCALSERVER' 'Las pruebas de integración no apuntan a la instancia LOCALSERVER disponible.'

$migrationTests = Read-RepoFile 'LaPrimitiva.Tests/Integration/M401MigrationTests.cs'
Require-Match $migrationTests 'GetMigrations\(\)' 'Las pruebas de migración no comparan contra el conjunto de migraciones definido.'
if ($migrationTests -match 'Assert\.Equal\(\d+,\s*(?:applied\.Length|\(await .*GetAppliedMigrationsAsync)') {
    $failures.Add('Las pruebas de migración conservan un contador fijo de migraciones.')
}

$disconnectedPersistenceTests = Read-RepoFile 'LaPrimitiva.Tests/Integration/DisconnectedDrawPersistenceTests.cs'
Require-Match $disconnectedPersistenceTests 'Name = "Plan que no debe aplicarse"[\s\S]*?EffectiveFrom = new DateTime\(2027, 1, 1\)' 'El fixture desconectado vuelve a crear planes solapados y el trigger impedirá preparar la prueba.'

$planIntegrationTests = Read-RepoFile 'LaPrimitiva.Tests/Integration/PlanIntegrationTests.cs'
Require-Match $planIntegrationTests 'RowVersion = loadedPlanDto\.RowVersion\.ToArray\(\)' 'Las ediciones desconectadas de planes no conservan el token leído.'

$trackingTests = Read-RepoFile 'LaPrimitiva.Tests/Integration/DrawRepositoryTrackingTests.cs'
Require-Match $trackingTests 'RowVersion = draw\.RowVersion\.ToArray\(\)' 'La prueba desconectada de registros no conserva el token leído.'

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'M-403 static verification passed.'
Write-Host 'Rowversion mapping, stale-write rejection, UI reload paths and focused tests are present.'
