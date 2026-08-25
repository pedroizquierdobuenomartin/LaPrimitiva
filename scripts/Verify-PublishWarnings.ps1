[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

$notificationService = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'LaPrimitiva.Application\Services\DrawNotificationService.cs')
if ($notificationService.Contains('ILocalStorageService localStorage')) {
    throw 'DrawNotificationService conserva el parámetro localStorage no utilizado que produce CS9113.'
}

$notificationTests = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'LaPrimitiva.Tests\DrawNotificationServiceTests.cs')
if ($notificationTests.Contains('Mock.Of<ILocalStorageService>()')) {
    throw 'Los tests aún pasan la dependencia localStorage retirada del servicio.'
}

if (-not $notificationTests.Contains('using LaPrimitiva.Application.Interfaces;')) {
    throw 'DrawNotificationServiceTests necesita Application.Interfaces para resolver IWinningDrawService.'
}

$toolManifestPath = Join-Path $repositoryRoot '.config\dotnet-tools.json'
if (-not (Test-Path -LiteralPath $toolManifestPath)) {
    throw 'No existe un manifiesto local que fije la versión de dotnet-ef.'
}

$toolManifest = Get-Content -Raw -LiteralPath $toolManifestPath | ConvertFrom-Json
$dotnetEf = $toolManifest.tools.'dotnet-ef'
if ($null -eq $dotnetEf -or $dotnetEf.version -ne '10.0.11') {
    throw 'dotnet-ef debe estar fijado en la versión estable 10.0.11, compatible con EF Core 10.'
}

$publishScript = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'Publish.bat')
if (-not $publishScript.Contains('dotnet tool restore')) {
    throw 'Publish.bat no restaura la herramienta local dotnet-ef antes de usarla.'
}

$bundleScript = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'scripts\New-DatabaseMigrationBundle.ps1')
foreach ($expected in @(
    '$parsedMigrations = $jsonMatch.Value | ConvertFrom-Json',
    'foreach ($migration in $parsedMigrations)',
    '$latestMigration = $migrations[-1].id'
)) {
    if (-not $bundleScript.Contains($expected)) {
        throw "La normalización de migraciones para Windows PowerShell 5.1 está incompleta. Falta: $expected"
    }
}

if ($bundleScript -match '[^\x00-\x7F]') {
    throw 'New-DatabaseMigrationBundle.ps1 contiene texto no ASCII que Windows PowerShell 5.1 puede interpretar incorrectamente sin BOM.'
}

Write-Host 'Verificación estática de advertencias del publish: correcta.'
