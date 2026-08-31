[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

function Read-RepoFile([string] $RelativePath) {
    $path = Join-Path $repoRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Falta el archivo requerido por M-507: $RelativePath"
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

$program = Read-RepoFile 'LaPrimitiva.App\Program.cs'
$localizationConfiguration = Read-RepoFile 'LaPrimitiva.App\Localization\LocalizationConfiguration.cs'
Require-Match $localizationConfiguration 'SupportedCultureName\s*=\s*"es-ES"' 'La cultura inicial no está fijada explícitamente a es-ES.'
Require-Match ($program + $localizationConfiguration) 'DefaultRequestCulture' 'Falta DefaultRequestCulture.'
Require-Match ($program + $localizationConfiguration) 'SupportedCultures' 'Faltan las culturas soportadas.'
Require-Match ($program + $localizationConfiguration) 'SupportedUICultures' 'Faltan las culturas de interfaz soportadas.'
Require-Match $program 'UseRequestLocalization' 'Falta UseRequestLocalization antes de renderizar componentes.'
Require-Match $program 'RequiredStringLocalizerFactory' 'No está registrada la detección de claves ausentes.'

$app = Read-RepoFile 'LaPrimitiva.App\Components\App.razor'
Require-Match $app 'CultureInfo\.CurrentUICulture\.Name' 'El atributo lang no refleja la cultura activa.'

$neutralSpanishResources = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'LaPrimitiva.Domain\Localization') -Filter '*.es.resx'
if ($neutralSpanishResources) {
    throw "Persisten recursos españoles neutrales: $($neutralSpanishResources.Name -join ', ')."
}

$resourceMarkers = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'LaPrimitiva.Domain\Localization') -Filter '*Resource.cs'
foreach ($marker in $resourceMarkers) {
    $expected = [IO.Path]::ChangeExtension($marker.FullName, '.es-ES.resx')
    if (-not (Test-Path -LiteralPath $expected)) {
        throw "Falta el catálogo es-ES para $($marker.Name)."
    }

    [xml] $catalog = Get-Content -LiteralPath $expected -Raw
    $keys = @($catalog.root.data | ForEach-Object { $_.name })
    if (@($keys | Group-Object | Where-Object Count -gt 1).Count -gt 0) {
        throw "El catálogo $([IO.Path]::GetFileName($expected)) contiene claves duplicadas."
    }
}

$razorFiles = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'LaPrimitiva.App\Components') -Recurse -Filter '*.razor'
$allowedText = @(
    'C', 'R', 'J.', '404', 'Joker', 'La Primitiva', 'La', 'P', 'rimitiva',
    'L', 'LG', 'LL', 'LE'
)
$violations = [Collections.Generic.List[string]]::new()
foreach ($file in $razorFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    Reject-Match $content 'new\s+(?:System\.Globalization\.)?CultureInfo\("es-ES"\)' "$($file.Name) fuerza es-ES localmente en vez de usar la cultura activa."

    $markup = ($content -split '(?m)^\s*@code\s*\{', 2)[0]
    foreach ($match in [regex]::Matches($markup, '>([^<>]+)<')) {
        $text = ($match.Groups[1].Value -replace '\s+', ' ').Trim()
        if ([string]::IsNullOrWhiteSpace($text) -or
            $text.StartsWith('@') -or
            $text.Contains('@') -or
            $text.Contains('"') -or
            $text -match '[{}]' -or
            $text -match '^[-–—:;,.!?¿¡(){}]+$' -or
            $text -match '^\d+(?:[.,]\d+)?$' -or
            $allowedText -contains $text) {
            continue
        }

        if ($text -match '[A-Za-zÁÉÍÓÚáéíóúÑñ]') {
            $violations.Add("$($file.FullName.Replace($repoRoot + '\', '')): texto visible '$text'")
        }
    }

    foreach ($match in [regex]::Matches($markup, '(?:title|aria-label|placeholder|alt)="([^"]+)"')) {
        $text = $match.Groups[1].Value.Trim()
        if ($text.StartsWith('@') -or $text.Contains('@') -or $text -match '^$|^Logo$' -or $allowedText -contains $text) {
            continue
        }

        if ($text -match '[A-Za-zÁÉÍÓÚáéíóúÑñ]' -and $text -notmatch '^(?:[a-z-]+|[A-Z][a-z]+)$') {
            $violations.Add("$($file.FullName.Replace($repoRoot + '\', '')): atributo visible '$text'")
        }
    }

    $localizers = @{}
    foreach ($injection in [regex]::Matches($content, '@inject\s+IStringLocalizer<([^>]+)>\s+(\w+)')) {
        $localizers[$injection.Groups[2].Value] = $injection.Groups[1].Value
    }

    foreach ($localizerName in $localizers.Keys) {
        $resourcePath = Join-Path $repoRoot "LaPrimitiva.Domain\Localization\$($localizers[$localizerName]).es-ES.resx"
        if (-not (Test-Path -LiteralPath $resourcePath)) {
            throw "No existe el catálogo es-ES de $($localizers[$localizerName]) usado por $($file.Name)."
        }

        [xml] $resource = Get-Content -LiteralPath $resourcePath -Raw
        $availableKeys = @($resource.root.data | ForEach-Object { $_.name })
        $keyPattern = '\b' + [regex]::Escape($localizerName) + '\["([^"]+)"\]'
        foreach ($usage in [regex]::Matches($content, $keyPattern)) {
            $key = $usage.Groups[1].Value
            if ($availableKeys -notcontains $key) {
                throw "$($file.Name) usa la clave ausente '$key' de $($localizers[$localizerName])."
            }
        }
    }
}

$presentationSources = $razorFiles | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
Reject-Match ($presentationSources -join "`n") '(?s)(?:ShowFeedback\(|modalErrorMessage\s*=|errorMessage\s*=|_errorMessage\s*=).*?ApplicationError\.FromException\([^\)]*\)\.Message' 'La UI presenta mensajes de excepción en vez de resolver códigos localizados.'
Reject-Match ($presentationSources -join "`n") '(?s)result\.Error\?\.Message|GlobalState\.LastError\s*</' 'La UI presenta mensajes de Application sin localizarlos por código estable.'
Reject-Match ($presentationSources -join "`n") 'string\.IsNullOr(?:Empty|WhiteSpace)\(GlobalState\.LastError\)' 'GlobalState.LastError es ApplicationError y no puede tratarse como una cadena.'
Require-Match ($presentationSources -join "`n") 'GlobalState\.LastError\s+is\s+not\s+null' 'La UI no comprueba el error tipado antes de presentar la notificación.'
Reject-Match ($presentationSources -join "`n") 'ToString\("N2"\)\)?€' 'La UI concatena el euro en vez de usar el formato monetario de la cultura activa.'
Reject-Match ($presentationSources -join "`n") 'ToString\("(?:dd/MM|dd MMM|MMMM yyyy)' 'La UI fuerza patrones españoles en vez de usar patrones de la cultura activa.'
Require-Match ($presentationSources -join "`n") 'ToString\("C2"\)' 'No se ha verificado formato monetario mediante la cultura activa.'

$globalResource = Read-RepoFile 'LaPrimitiva.Domain\Localization\GlobalResource.es-ES.resx'
foreach ($keyMovedToSpecificCatalog in @('OperationFailedTitle', 'Rejoining', 'RecentDraws')) {
    Reject-Match $globalResource ('<data name="' + [regex]::Escape($keyMovedToSpecificCatalog) + '"') "GlobalResource conserva la clave específica $keyMovedToSpecificCatalog."
}
[xml] $errorResource = Read-RepoFile 'LaPrimitiva.Domain\Localization\ErrorResource.es-ES.resx'
$errorResourceKeys = @($errorResource.root.data | ForEach-Object { $_.name })
foreach ($errorCode in @(
    'business.rule',
    'entity.not-found',
    'persistence.concurrency',
    'persistence.integrity',
    'persistence.unavailable',
    'external.unavailable',
    'external.invalid-data',
    'unexpected')) {
    if ($errorResourceKeys -notcontains "Error.$errorCode") {
        throw "ErrorResource no traduce el código estable $errorCode."
    }
}

$csvBuilder = Read-RepoFile 'LaPrimitiva.App\Exporting\CsvExportBuilder.cs'
Require-Match $csvBuilder 'CultureInfo\.InvariantCulture' 'El contrato CSV no conserva cultura invariante.'
Require-Match $csvBuilder '"yyyy-MM-dd"' 'El contrato CSV no conserva fechas ISO.'
$rssParser = Read-RepoFile 'LaPrimitiva.Application\Services\RssParserService.cs'
Require-Match $rssParser 'DateTime\.TryParse\([^\r\n]+CultureInfo\.InvariantCulture' 'El contrato RSS depende indebidamente de la cultura de UI.'
$dbContext = Read-RepoFile 'LaPrimitiva.Infrastructure\PrimitivaDbContext.cs'
Require-Match $dbContext 'HasPrecision\(10, 2\)' 'Los importes no conservan precisión decimal tipada.'
$modelSnapshot = Read-RepoFile 'LaPrimitiva.Infrastructure\Migrations\PrimitivaDbContextModelSnapshot.cs'
Require-Match $modelSnapshot 'decimal\(10,2\)' 'El modelo SQL no conserva columnas decimales.'
Require-Match $modelSnapshot 'datetime2' 'El modelo SQL no conserva columnas temporales tipadas.'
$documentation = Read-RepoFile 'mejoras\LOCALIZACION.md'
foreach ($documentedBoundary in @('RequiredStringLocalizerFactory', 'GlobalResource', 'ErrorResource', 'InvariantCulture', 'yyyy-MM-dd', 'idioma futuro')) {
    Require-Match $documentation ([regex]::Escape($documentedBoundary)) "La estrategia de localización no documenta $documentedBoundary."
}

if ($violations.Count -gt 0) {
    throw "Se detectaron textos visibles hardcoded:`n - $($violations -join "`n - ")"
}

$tests = Read-RepoFile 'LaPrimitiva.Tests\M507LocalizationTests.cs'
foreach ($scenario in @(
    'ConfiguresEsEsForFormattingAndUi',
    'ResolvesSpanishResourceAndParameters',
    'ResolvesEverySpanishCatalogWithoutEmptyValues',
    'ThrowsForMissingResourceKey',
    'RejectsUnsupportedFutureCulture',
    'PreservesTypedValuesAcrossSpanishFormattingRoundTrip',
    'PreservesAmbiguousAndBoundaryDates',
    'PreservesBusinessDateWithoutTimeZoneShift',
    'KeepsCsvContractInvariantUnderSpanishUiCulture',
    'KeepsRssContractInvariantUnderSpanishUiCulture',
    'KeepsPersistenceValuesTypedAndPrecise')) {
    Require-Match $tests $scenario "Falta la prueba M-507 del escenario $scenario."
}

$roadmap = Read-RepoFile 'mejoras\PLAN_DE_MEJORAS.md'
Require-Match $roadmap '### \[x\] M-507' 'El plan no marca M-507 como completado.'

Write-Host 'M-507 verificado: es-ES global, recursos coherentes, claves ausentes detectables y fronteras culturales.' -ForegroundColor Green
