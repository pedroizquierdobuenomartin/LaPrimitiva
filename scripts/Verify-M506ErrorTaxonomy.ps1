[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

function Read-RepoFile([string] $RelativePath) {
    $path = Join-Path $repoRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Falta el archivo requerido por M-506: $RelativePath"
    }

    return Get-Content -LiteralPath $path -Raw
}

function Require-Match([string] $Content, [string] $Pattern, [string] $Message) {
    if ($Content -notmatch $Pattern) {
        throw $Message
    }
}

function Reject-Match([string] $Content, [string] $Pattern, [string] $Message) {
    if ($Content -match $Pattern) {
        throw $Message
    }
}

$taxonomy = Read-RepoFile 'LaPrimitiva.Domain\Errors\ErrorTaxonomy.cs'
foreach ($category in @(
    'BusinessRule',
    'NotFound',
    'Concurrency',
    'Integrity',
    'PersistenceUnavailable',
    'ExternalUnavailable',
    'ExternalInvalidData',
    'Unexpected')) {
    Require-Match $taxonomy "\b$category\b" "El catálogo M-506 no contiene la categoría $category."
}
Require-Match $taxonomy 'ErrorRecoveryAction' 'Falta la política de recuperación de M-506.'
Require-Match $taxonomy 'IsRetryable' 'Falta la política explícita de reintento de M-506.'
Require-Match $taxonomy 'class ExternalDataFormatException : Exception, IErrorException' 'ExternalDataFormatException no hereda de una base válida y transversal.'
Reject-Match $taxonomy 'class ExternalDataFormatException : InvalidDataException' 'InvalidDataException está sellada en .NET 10 y no puede usarse como clase base.'

$domainSources = @(
    'LaPrimitiva.Domain\Entities\Plan.cs',
    'LaPrimitiva.Domain\Entities\DrawRecord.cs',
    'LaPrimitiva.Domain\Entities\WinningDraw.cs'
) | ForEach-Object { Read-RepoFile $_ }
Reject-Match ($domainSources -join "`n") 'throw new InvalidOperationException' 'Las reglas de dominio aún usan InvalidOperationException.'
Require-Match ($domainSources -join "`n") 'BusinessRuleException' 'Las reglas de dominio no usan la taxonomía aprobada.'

$applicationSources = @(
    'LaPrimitiva.Application\Services\PlanService.cs',
    'LaPrimitiva.Application\Services\DrawService.cs',
    'LaPrimitiva.Application\Services\WinningDrawService.cs'
) | ForEach-Object { Read-RepoFile $_ }
Reject-Match ($applicationSources -join "`n") 'throw new InvalidOperationException' 'Application aún usa InvalidOperationException para errores esperados.'
$resultContract = Read-RepoFile 'LaPrimitiva.Application\Services\Result.cs'
Require-Match $resultContract 'ApplicationError' 'Result aún transporta errores como cadenas arbitrarias.'

$rssClient = Read-RepoFile 'LaPrimitiva.Infrastructure\Services\RssClient.cs'
Reject-Match $rssClient 'throw new Exception' 'RssClient aún crea una Exception genérica.'
Reject-Match $rssClient '(?s)Error de red.*\.Message' 'RssClient aún concatena detalles técnicos del proveedor.'
Require-Match $rssClient 'ExternalServiceUnavailableException' 'RssClient no traduce la indisponibilidad externa.'
Require-Match $rssClient 'OperationCanceledException' 'RssClient no conserva la cancelación solicitada.'

$rssParser = Read-RepoFile 'LaPrimitiva.Application\Services\RssParserService.cs'
Reject-Match $rssParser '(?m)^\s*catch\s*$' 'RssParserService aún silencia una captura general.'
Require-Match $rssParser 'ExternalDataFormatException' 'RssParserService no distingue un feed inválido.'

$persistenceTranslator = Read-RepoFile 'LaPrimitiva.Infrastructure\Persistence\PersistenceExceptionTranslator.cs'
Require-Match $persistenceTranslator '2601|2627' 'No se traducen las violaciones de unicidad de SQL Server.'
Require-Match $persistenceTranslator 'DbUpdateConcurrencyException' 'No se conserva la traducción de concurrencia de EF Core.'
Require-Match $persistenceTranslator 'PersistenceUnavailableException' 'No se traduce la indisponibilidad de persistencia.'

$routes = Read-RepoFile 'LaPrimitiva.App\Components\Routes.razor'
Require-Match $routes '<AppErrorBoundary>' 'Las rutas Blazor no están protegidas por el límite transversal de errores.'
$boundary = Read-RepoFile 'LaPrimitiva.App\Components\Shared\AppErrorBoundary.razor'
Require-Match $boundary 'Referencia:' 'El límite Blazor no muestra una referencia comunicable.'
Require-Match $boundary 'ErrorReporter\.Report' 'El límite Blazor no registra el error inesperado.'
Reject-Match $boundary '@CurrentException\.Message|@context\.Message' 'El límite Blazor expone detalles técnicos.'

$errorPage = Read-RepoFile 'LaPrimitiva.App\Components\Pages\Error.razor'
Require-Match $errorPage 'Referencia:' 'La página global no muestra una referencia.'
Reject-Match $errorPage 'Development Mode|Exception\.Message|StackTrace' 'La página global expone contenido técnico.'

foreach ($componentPath in @(
    'LaPrimitiva.App\Components\Layout\MainLayout.razor',
    'LaPrimitiva.App\Components\Pages\AutomatedCombination.razor',
    'LaPrimitiva.App\Components\Pages\HistoricalDraws.razor',
    'LaPrimitiva.App\Components\Pages\Home.razor',
    'LaPrimitiva.App\Components\Pages\Plans.razor',
    'LaPrimitiva.App\Components\Pages\Register.razor')) {
    $component = Read-RepoFile $componentPath
    if ($component -match 'catch \(Exception' -and $component -notmatch 'ErrorReporter\.Report') {
        throw "$componentPath captura Exception sin usar la frontera transversal de registro."
    }
}

$productionSources = Get-ChildItem -LiteralPath $repoRoot -Directory |
    Where-Object Name -Match '^LaPrimitiva\.(App|Application|Domain|Infrastructure)$' |
    ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Recurse -File -Include '*.cs', '*.razor' } |
    Where-Object FullName -NotMatch '\\(bin|obj)\\' |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
Reject-Match ($productionSources -join "`n") '(?m)^\s*catch\s*$' 'Aún existe una captura general sin tipo en código de producción.'

$documentation = Read-RepoFile 'mejoras\TAXONOMIA_DE_ERRORES.md'
Require-Match $documentation 'Excepciones estándar conservadas' 'No se documentan las excepciones estándar conservadas.'
Require-Match $documentation 'OperationCanceledException' 'No se documenta la semántica de cancelación.'
$roadmap = Read-RepoFile 'mejoras\PLAN_DE_MEJORAS.md'
Require-Match $roadmap '### \[x\] M-506' 'El plan no marca M-506 como completado.'
Require-Match $roadmap '### \[ \] M-507' 'Se avanzó indebidamente al hito M-507.'
foreach ($field in @('Fecha:', 'Commit o referencia:', 'Pruebas realizadas:', 'Resultado:', 'Decisiones:')) {
    Require-Match $roadmap ([regex]::Escape($field)) "El cierre M-506 no documenta $field"
}

$tests = Read-RepoFile 'LaPrimitiva.Tests\M506ErrorTaxonomyTests.cs'
foreach ($scenario in @(
    'Concurrency',
    'UniqueConstraint',
    'EntityNotFound',
    'DatabaseUnavailable',
    'HttpTimeout',
    'HttpUnavailable',
    'InvalidRss',
    'Unexpected')) {
    Require-Match $tests $scenario "Falta la prueba M-506 del escenario $scenario."
}
$integrationTests = Read-RepoFile 'LaPrimitiva.Tests\Integration\M506PersistenceTranslationIntegrationTests.cs'
Require-Match $integrationTests 'DuplicateWinningDrawDate' 'Falta la prueba de integración de unicidad SQL Server.'
Require-Match $integrationTests 'GlobalErrorPage_IsLocalized' 'Falta la prueba de integración de la página global de error.'

Write-Host 'M-506 verificado: taxonomía, traducciones, cancelación, presentación segura y límites Blazor.' -ForegroundColor Green
