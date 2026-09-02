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

function Assert-NotContains([string]$content, [string]$pattern, [string]$message) {
    if ($content -match $pattern) {
        throw $message
    }
}

$notificationService = Read-ProjectFile 'LaPrimitiva.Application\Services\DrawNotificationService.cs'
$winningDrawService = Read-ProjectFile 'LaPrimitiva.Application\Services\WinningDrawService.cs'
$tests = Read-ProjectFile 'LaPrimitiva.Tests\DrawNotificationServiceTests.cs'
$uiTests = Read-ProjectFile 'LaPrimitiva.Tests\M701RssUiRefreshTests.cs'
$dbContext = Read-ProjectFile 'LaPrimitiva.Infrastructure\PrimitivaDbContext.cs'
$historicalPage = Read-ProjectFile 'LaPrimitiva.App\Components\Pages\HistoricalDraws.razor'

Assert-Contains $notificationService 'GetExistingDrawDatesAsync\(rssDates\)' `
    'La detección RSS no consulta las fechas realmente existentes entre las candidatas.'
Assert-Contains $notificationService '!existingDateSet\.Contains\(draw\.Date\.Date\)' `
    'La detección RSS no calcula los pendientes por diferencia de conjuntos.'
Assert-NotContains $notificationService 'GetLatestDrawDateAsync\(' `
    'La detección RSS todavía utiliza la fecha máxima como marcador.'
Assert-Contains $winningDrawService 'normalizedDates\.Contains\(draw\.DrawDate\.Date\)' `
    'La consulta del histórico no está limitada a las fechas candidatas normalizadas.'
Assert-Contains $tests 'CheckForNewDrawsAsync_WhenNewestDrawExists_KeepsOlderHistoricalGapsPending' `
    'Falta la prueba que conserva huecos anteriores cuando ya existe el sorteo más reciente.'
Assert-Contains $tests 'CheckForNewDrawsAsync_WhenDrawsAreSavedOutOfOrder_RemovesOnlyEachSavedDraw' `
    'Falta la prueba de guardado no cronológico y refresco de pendientes.'
Assert-Contains $notificationService 'NewDrawsCount\s*=\s*rssDraws\.Count' `
    'El contador del icono no representa todos los sorteos pendientes.'
Assert-Contains $tests 'CheckForNewDrawsAsync_WhenMoreThanTenDrawsArePending_CountsAllAndBoundsPopupItems' `
    'Falta la prueba que separa el total pendiente del límite visual del popup.'
Assert-Contains $historicalPage 'OnDataRefreshRequired\s*\+=\s*HandleDataChange' `
    'La página de histórico no escucha los guardados realizados desde el popup.'
Assert-Contains $historicalPage 'LoadDraws\(showLoader:\s*false\)' `
    'El histórico no actualiza la tabla de forma silenciosa tras un guardado externo.'
Assert-Contains $historicalPage 'OnDataRefreshRequired\s*-=\s*HandleDataChange' `
    'La página de histórico no elimina su suscripción al destruirse.'
Assert-Contains $uiTests 'HistoricalDraws_RefreshesItsDataAfterAnExternalSave_AndUnsubscribesOnDispose' `
    'Falta la prueba del refresco localizado y del ciclo de vida de la suscripción.'
Assert-Contains $dbContext 'HasIndex\(e => e\.DrawDate\)\.IsUnique\(\)' `
    'La restricción única de fecha de WinningDraws no está configurada.'

Write-Host 'M-701 verificado estáticamente: pendientes completos, contador total, refresco localizado del histórico y unicidad presentes.' -ForegroundColor Green
