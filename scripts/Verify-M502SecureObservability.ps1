[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()

function Read-RepoFile([string]$RelativePath) {
    $path = Join-Path $repoRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $failures.Add("Falta el archivo requerido: $RelativePath")
        return ''
    }

    Get-Content -Raw -LiteralPath $path
}

function Require-Match([string]$Text, [string]$Pattern, [string]$Message) {
    if ($Text -notmatch $Pattern) {
        $failures.Add($Message)
    }
}

$program = Read-RepoFile 'LaPrimitiva.App/Program.cs'
Require-Match $program 'AddJsonConsole' 'La aplicación no configura salida JSON estructurada.'
Require-Match $program 'IncludeScopes = true' 'Los logs JSON no incluyen scopes de correlación.'
Require-Match $program 'SecureJsonFileLoggerProvider' 'La aplicación no conserva una copia local y rotada de los logs JSON.'
Require-Match $program 'AddCheck<DatabaseHealthCheck>' 'No se registró el health check de SQL Server.'
Require-Match $program 'MapHealthChecks\("/health/live"' 'Falta el health check básico de aplicación.'
Require-Match $program 'MapHealthChecks\("/health/ready"' 'Falta el health check de disponibilidad de base de datos.'
Require-Match $program 'status = report.Status.ToString\(\)' 'La respuesta de salud no ofrece un estado agregado.'
if ($program -match 'report\.Entries') {
    $failures.Add('La respuesta de salud expone detalles internos de checks.')
}

$plan = Read-RepoFile 'mejoras/PLAN_DE_MEJORAS.md'
Require-Match $plan '### \[x\] M-502 — Añadir observabilidad segura' 'El plan no marca M-502 como completado.'

$correlation = Read-RepoFile 'LaPrimitiva.App/Observability/CorrelationIdMiddleware.cs'
Require-Match $correlation 'context\.TraceIdentifier' 'La correlación no parte de un identificador generado por el servidor.'
Require-Match $correlation 'BeginScope' 'El identificador de correlación no se añade al scope estructurado.'

$fileLogger = Read-RepoFile 'LaPrimitiva.App/Observability/SecureJsonFileLoggerProvider.cs'
Require-Match $fileLogger 'MaxFileBytes' 'El log local no aplica un límite de tamaño por fichero.'
Require-Match $fileLogger 'RetainedFiles' 'El log local no aplica retención acotada.'
Require-Match $fileLogger 'exception\?\.ToString\(\)' 'El log local no conserva la excepción técnica completa.'

$databaseHealth = Read-RepoFile 'LaPrimitiva.App/Observability/DatabaseHealthCheck.cs'
Require-Match $databaseHealth 'CreateDbContextAsync' 'El health check no usa un DbContext corto creado por factory.'
Require-Match $databaseHealth 'LogError\(\s*exception' 'El fallo técnico del health check no queda registrado con excepción completa.'

$rssClient = Read-RepoFile 'LaPrimitiva.Infrastructure/Services/RssClient.cs'
Require-Match $rssClient 'LogInformation\(' 'La importación RSS no registra sus eventos operativos.'
$rssNotification = Read-RepoFile 'LaPrimitiva.Application/Services/DrawNotificationService.cs'
Require-Match $rssNotification 'errorReporter\.Report\(ex, "RssImport"\)' 'La frontera de importación RSS no registra los fallos técnicos inesperados.'
$errorReporter = Read-RepoFile 'LaPrimitiva.App/Observability/ApplicationErrorReporter.cs'
Require-Match $errorReporter 'LogError\(exception' 'El reporter de aplicación no conserva la excepción técnica completa.'
if ($rssNotification -match 'LastError\s*=\s*\$[^;]*ex\.Message') {
    $failures.Add('La UI RSS vuelve a mostrar directamente el mensaje técnico del proveedor.')
}

foreach ($component in @(
    'LaPrimitiva.App/Components/Pages/AutomatedCombination.razor',
    'LaPrimitiva.App/Components/Pages/Plans.razor',
    'LaPrimitiva.App/Components/Pages/Register.razor'
)) {
    $source = Read-RepoFile $component
    Require-Match $source 'ErrorReporter\.Report\(ex' "$component no usa la frontera transversal para registrar errores inesperados."
}

$errorPage = Read-RepoFile 'LaPrimitiva.App/Components/Pages/Error.razor'
Require-Match $errorPage 'Referencia:' 'La página global de error no muestra una referencia correlacionable.'
if ($errorPage -match 'Development Mode|Exception|StackTrace') {
    $failures.Add('La página global de error vuelve a exponer detalles técnicos.')
}

$operationalLog = Read-RepoFile 'scripts/OperationalLog.ps1'
Require-Match $operationalLog 'ConvertTo-Json -Compress' 'El log operativo no serializa eventos JSON estructurados.'
foreach ($script in @('scripts/Invoke-M401DatabaseMigration.ps1', 'scripts/BackupDatabases.ps1')) {
    $source = Read-RepoFile $script
    Require-Match $source 'Write-OperationalLog' "$script no registra eventos operativos estructurados."
    if ($source -match 'Properties\s+@\{[^}]*ConnectionString') {
        $failures.Add("$script intenta registrar una cadena de conexión.")
    }
}

$temporaryLogDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("laprimitiva-m502-" + [Guid]::NewGuid().ToString('N'))
$temporaryLog = Join-Path $temporaryLogDirectory 'operations.jsonl'
try {
    . (Join-Path $repoRoot 'scripts/OperationalLog.ps1')
    $testException = [InvalidOperationException]::new('detalle técnico de prueba')
    Write-OperationalLog -Path $temporaryLog -Operation 'Verifier' -Status failed -Level Error -CorrelationId 'm502-verifier' -Message 'Evento de prueba.' -Properties @{ safeValue = 1 } -Exception $testException
    $entry = Get-Content -Raw -LiteralPath $temporaryLog | ConvertFrom-Json
    if ($entry.operation -ne 'Verifier' -or $entry.correlationId -ne 'm502-verifier') {
        $failures.Add('El log operativo no conserva operación y correlación estructuradas.')
    }
    if ($entry.exception -notmatch 'detalle técnico de prueba') {
        $failures.Add('El log operativo no conserva el detalle técnico de la excepción.')
    }
}
finally {
    if (Test-Path -LiteralPath $temporaryLogDirectory) {
        Remove-Item -LiteralPath $temporaryLogDirectory -Recurse -Force
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'M-502 static verification passed.'
Write-Host 'Structured logs, safe correlation, RSS/administrative events and local health checks are present.'
