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

$limits = Read-ProjectFile 'LaPrimitiva.Domain\Models\RssFeedLimits.cs'
$client = Read-ProjectFile 'LaPrimitiva.Infrastructure\Services\RssClient.cs'
$parser = Read-ProjectFile 'LaPrimitiva.Application\Services\RssParserService.cs'
$notifications = Read-ProjectFile 'LaPrimitiva.Application\Services\DrawNotificationService.cs'
$clientTests = Read-ProjectFile 'LaPrimitiva.Tests\RssClientTests.cs'
$parserTests = Read-ProjectFile 'LaPrimitiva.Tests\RssParserServiceTests.cs'
$notificationTests = Read-ProjectFile 'LaPrimitiva.Tests\DrawNotificationServiceTests.cs'

Assert-Contains $limits 'MaxBytes = 512 \* 1024' 'Falta el límite explícito de bytes RSS.'
Assert-Contains $limits 'MaxItems = 100' 'Falta el límite explícito de elementos RSS.'
Assert-Contains $limits 'TimeSpan\.FromSeconds\(15\)' 'Falta el límite explícito de tiempo RSS.'

Assert-Contains $client 'HttpCompletionOption\.ResponseHeadersRead' 'La descarga RSS no solicita lectura en streaming.'
Assert-Contains $client 'ContentLength > RssFeedLimits\.MaxBytes' 'No se rechaza Content-Length por encima del límite.'
Assert-Contains $client 'responseStream\.ReadAsync' 'El cuerpo RSS no se lee incrementalmente.'
Assert-Contains $client 'totalBytes > RssFeedLimits\.MaxBytes' 'No se limita un cuerpo sin Content-Length fiable.'
Assert-Contains $client 'ReadAsStreamAsync\(cancellationToken\)' 'La apertura del stream no usa cancelación.'

Assert-Contains $parser 'XmlReader\.Create' 'El parser RSS no usa un lector XML en streaming.'
Assert-Contains $parser 'MaxCharactersInDocument = RssFeedLimits\.MaxBytes' 'El parser XML no limita caracteres.'
Assert-Contains $parser 'itemCount >= RssFeedLimits\.MaxItems' 'El parser RSS no limita elementos.'
Assert-Contains $parser 'ThrowIfCancellationRequested' 'El parser RSS no observa cancelación.'

Assert-Contains $notifications 'static readonly SemaphoreSlim UpdateLock = new\(1, 1\)' 'Falta exclusión mutua global para la actualización RSS.'
Assert-Contains $notifications 'WaitAsync\(0, cancellationToken\)' 'Las actualizaciones RSS concurrentes no se descartan.'
Assert-Contains $notifications 'CreateLinkedTokenSource\(cancellationToken\)' 'El timeout RSS no está enlazado con la cancelación del llamador.'
Assert-Contains $notifications 'CancelAfter\(RssFeedLimits\.Timeout\)' 'El flujo completo RSS no tiene timeout.'
Assert-Contains $notifications 'UpdateLock\.Release\(\)' 'El bloqueo RSS no se libera.'

Assert-Contains $clientTests 'GetRssXmlAsync_WithOversizedContentLength_RejectsFeed' 'Falta la prueba de Content-Length excesivo.'
Assert-Contains $clientTests 'GetRssXmlAsync_WithOversizedChunkedBody_StopsStreamingAtByteLimit' 'Falta la prueba de cuerpo chunked excesivo.'
Assert-Contains $clientTests 'GetRssXmlAsync_WithCancellation_StopsRequest' 'Falta la prueba de cancelación de descarga.'
Assert-Contains $parserTests 'ParseRss_WithTooManyItems_StopsAtConfiguredLimit' 'Falta la prueba del límite de elementos.'
Assert-Contains $parserTests 'ParseRss_WithCancellation_StopsParsing' 'Falta la prueba de cancelación del parser.'
Assert-Contains $notificationTests 'CheckForNewDrawsAsync_WhenUpdateIsRunning_DoesNotStartAnotherDownload' 'Falta la prueba de exclusión mutua.'

Write-Host 'M-304 static verification passed.' -ForegroundColor Green
