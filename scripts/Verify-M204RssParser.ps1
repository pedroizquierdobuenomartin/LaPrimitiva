[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Read-ProjectFile([string]$relativePath) {
    Get-Content -Raw (Join-Path $root $relativePath)
}

function Assert-Contains([string]$content, [string]$pattern, [string]$message) {
    if ($content -notmatch $pattern) {
        throw $message
    }
}

$parser = Read-ProjectFile 'LaPrimitiva.Application\Services\RssParserService.cs'
$tests = Read-ProjectFile 'LaPrimitiva.Tests\RssParserServiceTests.cs'

Assert-Contains $parser "\.Split\('-'\s*,\s*StringSplitOptions\.TrimEntries\s*\|\s*StringSplitOptions\.RemoveEmptyEntries\)" `
    'El parser RSS no separa los números independientemente de los espacios alrededor del guion.'
Assert-Contains $parser 'XElement\.LoadAsync\(subtree, LoadOptions\.None, cancellationToken\)' `
    'Los elementos RSS no se materializan de forma controlada dentro del bloque de captura de errores.'
Assert-Contains $tests 'ParseRss_WithAllowedSeparatorSpacing_ReturnsCorrectNumbers' `
    'Faltan pruebas de separadores válidos con espacios variables.'
Assert-Contains $tests 'ParseRss_WithIncompleteItem_SkipsItem' `
    'Falta la prueba de entrada incompleta.'
Assert-Contains $tests 'ParseRss_WithMalformedDraw_SkipsItemWithoutThrowingDuringMaterialization' `
    'Falta la prueba de sorteo malformado y materialización segura.'
Assert-Contains $tests 'ParseRss_WithMalformedXml_ReturnsEmptyList' `
    'Falta la prueba de XML malformado.'

Write-Host 'M-204 verificado estáticamente: separadores flexibles, materialización protegida y casos válidos, incompletos y malformados presentes.' -ForegroundColor Green
