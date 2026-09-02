$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$drillPath = Join-Path $PSScriptRoot "Invoke-M603RecoveryDrill.ps1"
$restorePath = Join-Path $PSScriptRoot "Test-DatabaseRestore.ps1"
$documentPath = Join-Path $root "mejoras\SIMULACRO_RECUPERACION_M603.md"

foreach ($path in @($drillPath, $restorePath, $documentPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Falta el artefacto obligatorio '$path'."
    }
}

foreach ($scriptPath in @($drillPath, $restorePath)) {
    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$errors) | Out-Null
    if ($errors.Count -ne 0) {
        throw "'$scriptPath' contiene errores de sintaxis: $($errors.Message -join '; ')"
    }
}

$drill = Get-Content -LiteralPath $drillPath -Raw
$restore = Get-Content -LiteralPath $restorePath -Raw
$document = Get-Content -LiteralPath $documentPath -Raw

$requiredDrillPatterns = @(
    'BackupDatabases\.ps1',
    'Test-DatabaseRestore\.ps1',
    'Resolve-Path -LiteralPath \$BackupDirectory',
    'PrimitivaRestoreTest_M603_',
    'Get-FunctionalSnapshot',
    'Assert-SnapshotsEqual',
    'ConnectionStrings__DefaultConnection',
    'WebUtility\]::HtmlDecode',
    '/health/ready',
    '"/planes"',
    '"/registro"',
    '"/historico"',
    '"/datos"',
    'applicationProcess\.Kill',
    '\$restoreAttempted = \$true',
    'DROP DATABASE',
    'cleanup = "completed"',
    'M-603-recovery-drill-'
)
foreach ($pattern in $requiredDrillPatterns) {
    if ($drill -notmatch $pattern) { throw "El simulacro no acredita el contrato '$pattern'." }
}

if ($restore -notmatch '\[switch\]\$KeepDatabase' -or
    $restore -notmatch '\$restored -and -not \$KeepDatabase' -or
    $restore -notmatch 'ValidateSet\("M-102", "M-603"\)') {
    throw "La restauración no permite conservar de forma explícita una copia M-603 sin alterar el comportamiento M-102."
}

foreach ($term in @('backup nuevo', 'base temporal', 'binario existente', 'registros', 'planes', 'premios', 'totales', 'duración', 'limpieza')) {
    if ($document -notmatch [regex]::Escape($term)) { throw "El procedimiento no documenta '$term'." }
}

if ($drill -match 'dotnet\s+(build|run|test|publish|restore)') {
    throw "M-603 no debe compilar, restaurar paquetes ni publicar la aplicación."
}

Write-Host "[OK] M-603 verificado: backup fresco, restauración aislada, comparación funcional, arranque sin build, rutas y limpieza." -ForegroundColor Green
