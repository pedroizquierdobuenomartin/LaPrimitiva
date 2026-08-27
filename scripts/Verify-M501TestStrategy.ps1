[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()

function Read-RepoFile([string]$RelativePath) {
    $path = Join-Path $repoRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $failures.Add("Falta el archivo requerido: $RelativePath")
        return ''
    }

    Get-Content -Raw -LiteralPath $path
}

function Require-Match([string]$Text, [string]$Pattern, [string]$Message) {
    if ($Text -notmatch $Pattern) {
        $failures.Add($Message)
    }
}

$strategy = Read-RepoFile 'mejoras/ESTRATEGIA_PRUEBAS_M501.md'
foreach ($requiredHeading in @(
    '## Alcance y niveles',
    '## Matriz de cobertura mínima',
    '## Ejecución reproducible',
    '## Base de datos de integración',
    '## Criterio de cierre'
)) {
    if (-not $strategy.Contains($requiredHeading)) {
        $failures.Add("La estrategia no contiene la sección requerida: $requiredHeading")
    }
}

foreach ($coverageItem in @(
    'Costes, premios, Joker y ROI',
    'Rangos y duplicados de sorteos',
    'Vigencia y solapamiento de planes',
    'Persistencia de ediciones',
    'Parser RSS y límites',
    'Exportación CSV segura',
    'Migraciones desde cero y desde una versión anterior'
)) {
    if (-not $strategy.Contains($coverageItem)) {
        $failures.Add("La matriz no documenta: $coverageItem")
    }
}

Require-Match $strategy 'FullyQualifiedName!~Integration' 'La estrategia no separa la suite rápida de la integración SQL.'
Require-Match $strategy 'FullyQualifiedName~Integration' 'La estrategia no ofrece un comando exclusivo para integración SQL.'
Require-Match $strategy 'dotnet test --project' 'La estrategia debe usar la sintaxis MTP de .NET 10 para seleccionar el proyecto.'
Require-Match $strategy 'dotnet test --solution \./LaPrimitiva\.sln' 'La estrategia debe ofrecer el cierre completo de la solución mediante la sintaxis MTP.'
Require-Match $strategy 'LAPRIMITIVA_INTEGRATION_TEST_CONNECTION' 'La estrategia no documenta la conexión de integración configurable.'
Require-Match $strategy '_IntegrationTests' 'La estrategia no conserva el sufijo de seguridad de la base de pruebas.'

if ($strategy -match '(?m)^dotnet test[^\r\n]*--nologo') {
    $failures.Add('La estrategia no debe reenviar --nologo al host Microsoft.Testing.Platform.')
}
if ($strategy -match '(?m)^dotnet test[^\r\n]*--verbosity') {
    $failures.Add('La estrategia no debe usar la opción VSTest --verbosity con Microsoft.Testing.Platform.')
}

$readme = Read-RepoFile 'README.md'
Require-Match $readme 'ESTRATEGIA_PRUEBAS_M501\.md' 'README no enlaza la estrategia de pruebas vigente.'

$integrationSettings = Read-RepoFile 'LaPrimitiva.Tests/appsettings.IntegrationTests.json'
Require-Match $integrationSettings 'Encrypt=False' 'La conexión local de integración vuelve a exigir TLS sobre el transporte local de LOCALSERVER.'
Require-Match $strategy 'Encrypt=False' 'La estrategia no explica la configuración de cifrado del SQL Server local de pruebas.'

$coverageSources = @{
    'LaPrimitiva.Tests/DrawRecordTests.cs' = @(
        'RecalculateFinancials_IncludesJokerCostsAndPrizes_WhenEnabledAndAwarded'
    )
    'LaPrimitiva.Tests/SummaryServiceTests.cs' = @(
        'GetSummary_UsesUnifiedTotalsForDashboardNetAndRoi'
    )
    'LaPrimitiva.Tests/WinningDrawTests.cs' = @(
        'Validate_WhenMainNumberIsOutsideRange_Throws',
        'Validate_WhenMainNumberIsDuplicated_Throws'
    )
    'LaPrimitiva.Tests/DrawServiceTests.cs' = @(
        'ValidateDrawAsync_ShouldThrowException_WhenDateIsDuplicate',
        'ValidateDrawAsync_ShouldThrowException_WhenDateIsOutsidePlanRange'
    )
    'LaPrimitiva.Tests/PlanTests.cs' = @(
        'Validate_ShouldRejectInvalidBusinessRules'
    )
    'LaPrimitiva.Tests/Integration/PlanIntegrationTests.cs' = @(
        'CreatePlan_ShouldFail_WhenDatesOverlap',
        'UpdatePlan_ShouldPreserveCreatedAt_AndRefreshUpdatedAt'
    )
    'LaPrimitiva.Tests/Integration/DisconnectedDrawPersistenceTests.cs' = @(
        'UpdateAsync_PersistsEditableValuesWithoutChangingStructuralColumns'
    )
    'LaPrimitiva.Tests/RssParserServiceTests.cs' = @(
        'ParseRss_WithMalformedXml_ReturnsEmptyList',
        'ParseRss_WithTooManyItems_StopsAtConfiguredLimit'
    )
    'LaPrimitiva.Tests/RssClientTests.cs' = @(
        'GetRssXmlAsync_WithOversizedChunkedBody_StopsStreamingAtByteLimit'
    )
    'LaPrimitiva.Tests/CsvFieldFormatterTests.cs' = @(
        'Encode_WithFormulaPrefix_NeutralizesFormula'
    )
    'LaPrimitiva.Tests/CsvExportBuilderTests.cs' = @(
        'Build_WithSpanishCurrentCulture_UsesInvariantDecimalsAndValidCsvEscaping'
    )
    'LaPrimitiva.Tests/Integration/M401MigrationTests.cs' = @(
        'Migrations_CreateTheCompleteSchema_FromScratch',
        'Migrations_UpgradeFromPreviousVersion_WithoutLosingData'
    )
}

foreach ($entry in $coverageSources.GetEnumerator()) {
    $source = Read-RepoFile $entry.Key
    foreach ($testName in $entry.Value) {
        if (-not $source.Contains($testName)) {
            $failures.Add("$($entry.Key) no contiene la cobertura requerida: $testName")
        }
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'M-501 static verification passed.'
Write-Host 'The documented test strategy maps every minimum coverage area to focused automated tests.'
