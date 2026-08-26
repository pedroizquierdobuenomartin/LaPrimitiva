[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()

function Read-RepoFile([string]$RelativePath) {
    Get-Content -Raw (Join-Path $repoRoot $RelativePath)
}

$componentFiles = Get-ChildItem (Join-Path $repoRoot 'LaPrimitiva.App/Components') -Recurse -File -Filter '*.razor'
foreach ($file in $componentFiles) {
    $content = Get-Content -Raw $file.FullName
    if ($content -match '\basync\s+void\b') {
        $relative = [System.IO.Path]::GetRelativePath($repoRoot, $file.FullName)
        $failures.Add("$relative conserva un manejador async void.")
    }
}

$mainLayout = Read-RepoFile 'LaPrimitiva.App/Components/Layout/MainLayout.razor'
foreach ($required in @(
    '_feedbackTimer?.Dispose();',
    '_feedbackTimer = null;',
    'GlobalState.OnChange -= HandleStateChange;',
    'NavigationManager.LocationChanged -= HandleLocationChanged;'
)) {
    if (-not $mainLayout.Contains($required)) {
        $failures.Add("MainLayout no contiene la liberación requerida: $required")
    }
}

$breadcrumb = Read-RepoFile 'LaPrimitiva.App/Components/Layout/Breadcrumb.razor'
if ($breadcrumb -notmatch '@implements\s+IDisposable' -or
    -not $breadcrumb.Contains('NavigationManager.LocationChanged += HandleLocationChanged;') -or
    -not $breadcrumb.Contains('NavigationManager.LocationChanged -= HandleLocationChanged;') -or
    $breadcrumb.Contains('LocationChanged += (')) {
    $failures.Add('Breadcrumb no usa una suscripción LocationChanged nominada y liberable.')
}

foreach ($relativePath in @(
    'LaPrimitiva.App/Components/Layout/MainLayout.razor',
    'LaPrimitiva.App/Components/Pages/Home.razor',
    'LaPrimitiva.App/Components/Pages/Plans.razor',
    'LaPrimitiva.App/Components/Pages/Register.razor'
)) {
    $content = Read-RepoFile $relativePath
    foreach ($required in @('private bool _disposed;', 'if (_disposed)', '_disposed = true;', 'Logger.LogError')) {
        if (-not $content.Contains($required)) {
            $failures.Add("$relativePath no protege o registra correctamente callbacks asíncronos: $required")
        }
    }
}

$tests = Read-RepoFile 'LaPrimitiva.Tests/M405ComponentLifetimeTests.cs'
foreach ($testName in @(
    'Components_DoNotDeclareAsyncVoidHandlers',
    'MainLayout_DisposesTimerAndUnsubscribesEveryEvent',
    'Breadcrumb_UsesRemovableLocationChangedSubscription',
    'AsyncEventComponents_GuardQueuedCallbacksAfterDisposal'
)) {
    if (-not $tests.Contains($testName)) {
        $failures.Add("Falta la prueba focalizada $testName.")
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'M-405 static verification passed.'
Write-Host 'Async handlers, timer disposal, event unsubscription and disposed callback guards are present.'
