$ErrorActionPreference = 'Stop'

function Assert-Contains {
    param(
        [Parameter(Mandatory)] [string] $Text,
        [Parameter(Mandatory)] [string] $Pattern,
        [Parameter(Mandatory)] [string] $Message
    )

    if ($Text -notmatch $Pattern) {
        throw $Message
    }
}

function Assert-ContainsLiteral {
    param(
        [Parameter(Mandatory)] [string] $Text,
        [Parameter(Mandatory)] [string] $Value,
        [Parameter(Mandatory)] [string] $Message
    )

    if (-not $Text.Contains($Value, [System.StringComparison]::Ordinal)) {
        throw $Message
    }
}

$root = Split-Path -Parent $PSScriptRoot
$formatterPath = Join-Path $root 'LaPrimitiva.App\Exporting\CsvFieldFormatter.cs'
$builderPath = Join-Path $root 'LaPrimitiva.App\Exporting\CsvExportBuilder.cs'
$pagePath = Join-Path $root 'LaPrimitiva.App\Components\Pages\Data.razor'
$testPath = Join-Path $root 'LaPrimitiva.Tests\CsvFieldFormatterTests.cs'
$builderTestPath = Join-Path $root 'LaPrimitiva.Tests\CsvExportBuilderTests.cs'

foreach ($path in @($formatterPath, $builderPath, $pagePath, $testPath, $builderTestPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "No existe el archivo requerido: $path"
    }
}

$formatter = Get-Content -LiteralPath $formatterPath -Raw
$builder = Get-Content -LiteralPath $builderPath -Raw
$page = Get-Content -LiteralPath $pagePath -Raw
$tests = Get-Content -LiteralPath $testPath -Raw
$builderTests = Get-Content -LiteralPath $builderTestPath -Raw

Assert-Contains $formatter "FormulaPrefixes = \['=', '\+', '-', '@'\]" 'No se neutralizan los cuatro prefijos peligrosos.'
Assert-Contains $formatter 'FormulaPrefixes\.Contains\(value\[0\]\)' 'El formateador no comprueba el primer carácter de la celda.'
Assert-Contains $formatter 'value = \$"''\{value\}"' 'El formateador no antepone el apóstrofo neutralizador.'
Assert-ContainsLiteral $formatter 'value.Replace("\"", "\"\"")' 'El formateador no duplica las comillas CSV.'
Assert-Contains $builder 'CsvFieldFormatter\.Encode\(draw\.Notes\)' 'El generador no usa el formateador seguro para las notas.'
Assert-Contains $builder 'value\.ToString\(CultureInfo\.InvariantCulture\)' 'Los importes no usan una cultura invariante.'
Assert-Contains $page 'CsvExportBuilder\.Build\(draws\)' 'La exportación no usa el generador CSV validado.'

foreach ($prefix in @('=', '+', '-', '@')) {
    Assert-ContainsLiteral $tests ('[InlineData("' + $prefix) "Falta una prueba para el prefijo peligroso $prefix."
}

Assert-Contains $tests '\\r\\nsegunda\\ntercera' 'Falta la prueba de saltos de línea.'
Assert-ContainsLiteral $tests 'primera, \"cita\"' 'Falta la prueba combinada de comas y comillas.'
Assert-Contains $builderTests 'CultureInfo\.GetCultureInfo\("es-ES"\)' 'Falta la prueba con cultura española.'
Assert-ContainsLiteral $builderTests '1,Lunes,2026-01-05,1,1.1,2.2,3.3,4.4,11,5.5,6.6,7.7,8.8,28.6,17.6,-3.3' 'La prueba no comprueba los 17 campos con decimales invariantes.'

Write-Output 'M-305 verificado: neutralización, integración y cobertura estática presentes.'
