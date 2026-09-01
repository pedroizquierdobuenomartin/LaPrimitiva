[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Read-RepositoryFile([string]$RelativePath) {
    return Get-Content -Raw -LiteralPath (Join-Path $root $RelativePath)
}

function Assert-Contains([string]$Content, [string]$Expected, [string]$Description) {
    if (-not $Content.Contains($Expected, [StringComparison]::Ordinal)) {
        throw "M-601: falta $Description ('$Expected')."
    }
}

$evidence = Read-RepositoryFile 'mejoras\VERIFICACION_FUNCIONAL_M601.md'
$logger = Read-RepositoryFile 'LaPrimitiva.App\Observability\SecureJsonFileLoggerProvider.cs'
$fixture = Read-RepositoryFile 'LaPrimitiva.Tests\Integration\IntegrationTestFixture.cs'
$observabilityTests = Read-RepositoryFile 'LaPrimitiva.Tests\M502ObservabilityTests.cs'
$errorTests = Read-RepositoryFile 'LaPrimitiva.Tests\M506ErrorTaxonomyTests.cs'
$breadcrumb = Read-RepositoryFile 'LaPrimitiva.App\Components\Layout\Breadcrumb.razor'
$persistenceIntegrationTests = Read-RepositoryFile 'LaPrimitiva.Tests\Integration\M506PersistenceTranslationIntegrationTests.cs'
$mainLayout = Read-RepositoryFile 'LaPrimitiva.App\Components\Layout\MainLayout.razor'
$register = Read-RepositoryFile 'LaPrimitiva.App\Components\Pages\Register.razor'
$planService = Read-RepositoryFile 'LaPrimitiva.Application\Services\PlanService.cs'
$dataExportService = Read-RepositoryFile 'LaPrimitiva.Application\Services\DataExportService.cs'
$functionalTests = Read-RepositoryFile 'LaPrimitiva.Tests\M601FunctionalVerificationTests.cs'

foreach ($flow in @(
    'FLOW-PLANES', 'FLOW-REGISTRO', 'FLOW-PREMIOS', 'FLOW-JOKER', 'FLOW-DASHBOARD',
    'FLOW-HISTORICO', 'FLOW-RSS', 'FLOW-EXPORTACION', 'FLOW-GENERACION', 'FLOW-CRUD-LIMPIEZA')) {
    Assert-Contains $evidence $flow "el flujo crítico $flow"
}

foreach ($expected in @(
    '177 pruebas', '145 correctas y 32 fallidas', 'PrimitivaAuditV2_M601Tests',
    'dotnet test --solution .\LaPrimitiva.sln', '5,25', '38,00', '32,75', '623,81 %')) {
    Assert-Contains $evidence $expected 'la evidencia reproducible'
}

foreach ($expected in @('NormalizeValue', 'value is Type type', 'catch (NotSupportedException)')) {
    Assert-Contains $logger $expected 'la protección del sink JSON'
}

Assert-Contains $fixture 'LoopbackConnectionStartupFilter' 'el loopback exclusivo de TestServer'
Assert-Contains $fixture 'IPAddress.Loopback' 'la dirección loopback de integración'
Assert-Contains $observabilityTests 'NormalizesUnsupportedStructuredValuesWithoutThrowing' 'la regresión del logger'
Assert-Contains $observabilityTests 'LG[\"ReferenceLabel\"]' 'el contrato localizado de Error.razor'
Assert-Contains $errorTests 'LG[\"ReferenceLabel\"]' 'el contrato localizado del boundary'
Assert-Contains $breadcrumb '"error" => LE["OperationFailedTitle"]' 'la ruta de error en el breadcrumb'
Assert-Contains $breadcrumb '"404" or "not-found" => LE["NotFoundTitle"]' 'las rutas no encontradas en el breadcrumb'
Assert-Contains $persistenceIntegrationTests 'NotFoundPage_IsLocalizedAndDoesNotFallIntoTheErrorBoundary' 'la regresión de la página no encontrada'
Assert-Contains $persistenceIntegrationTests 'WebUtility.HtmlDecode' 'la decodificación del HTML antes de validar textos localizados'
Assert-Contains $register 'DisplayedModalPlan?.BetsPerDraw' 'los metadatos del plan seleccionado en el alta semanal'
Assert-Contains $register 'DisplayedModalPlan?.EnableJoker' 'el estado Joker del plan seleccionado en el alta semanal'
Assert-Contains $planService 'plan.EffectiveTo?.Year ?? Math.Max(startYear, maxFutureYear)' 'la retención del año inicial en planes futuros'
Assert-Contains $dataExportService 'draw.Acumulado = accumulatedNet' 'el acumulado cronológico exportado'
Assert-Contains $functionalTests 'MainLayout_MobileRegistrationIcon_DoesNotContainInvalidArcFlags' 'la regresión del SVG móvil'

if ($mainLayout.Contains('a2 2 100 4', [StringComparison]::Ordinal)) {
    throw 'M-601: el icono móvil de Registro conserva un arco SVG inválido.'
}

$staleExceptionAssertions = Get-ChildItem -LiteralPath (Join-Path $root 'LaPrimitiva.Tests') -Filter '*.cs' -Recurse |
    Where-Object { $_.FullName -notmatch 'LocalOnlySecurityTests|TestDatabaseSafetyTests' } |
    Select-String -SimpleMatch 'Assert.Throws<InvalidOperationException>', 'Assert.ThrowsAsync<InvalidOperationException>'
if ($staleExceptionAssertions) {
    throw "M-601: quedan expectativas obsoletas de InvalidOperationException: $($staleExceptionAssertions.Path -join ', ')."
}

Write-Host 'M-601: contrato funcional, regresiones y matriz de evidencia verificados correctamente.'
