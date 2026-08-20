[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$backupScript = Join-Path $PSScriptRoot "BackupDatabases.ps1"
$powerShell = (Get-Process -Id $PID).Path
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("LaPrimitiva-M101-" + [guid]::NewGuid().ToString("N"))

function Assert-True {
    param(
        [Parameter(Mandatory)]
        [bool]$Condition,

        [Parameter(Mandatory)]
        [string]$Message
    )

    if (-not $Condition) {
        throw "Fallo de verificación: $Message"
    }
}

function Invoke-BackupScript {
    param(
        [Parameter(Mandatory)]
        [string]$LocalDirectory,

        [Parameter(Mandatory)]
        [string]$DriveDirectory,

        [Parameter(Mandatory)]
        [string]$SqlCmdPath,

        [Parameter(Mandatory)]
        [string]$LogPath
    )

    $env:M101_SQLCMD_LOG = $LogPath
    try {
        $output = & $powerShell -NoLogo -NoProfile -File $backupScript `
            -LocalBackupDir $LocalDirectory `
            -DriveBackupDir $DriveDirectory `
            -DaysToKeepLocal 7 `
            -SqlCmdExecutable $SqlCmdPath 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        Remove-Item Env:M101_SQLCMD_LOG -ErrorAction SilentlyContinue
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = @($output) -join [Environment]::NewLine
    }
}

try {
    $tokens = $null
    $parseErrors = $null
    $syntaxTree = [System.Management.Automation.Language.Parser]::ParseFile($backupScript, [ref]$tokens, [ref]$parseErrors)
    Assert-True ($parseErrors.Count -eq 0) "BackupDatabases.ps1 debe tener sintaxis PowerShell válida"
    Assert-True ($null -ne $syntaxTree.ParamBlock) "BackupDatabases.ps1 debe declarar parámetros explícitos"
    $parameterNames = @($syntaxTree.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath })
    foreach ($requiredParameter in @("ServerInstance", "DatabaseNames", "LocalBackupDir", "DriveBackupDir", "DaysToKeepLocal", "SqlCmdExecutable")) {
        Assert-True ($parameterNames -contains $requiredParameter) "falta el parámetro explícito $requiredParameter"
    }

    New-Item -ItemType Directory -Path $testRoot | Out-Null
    $mockSqlCmd = Join-Path $testRoot "mock-sqlcmd.ps1"
    @'
param(
    [switch]$b,
    [string]$S,
    [switch]$E,
    [string]$Q
)

$capturedArguments = @()
if ($b) { $capturedArguments += "-b" }
$capturedArguments += @("-S", $S)
if ($E) { $capturedArguments += "-E" }
$capturedArguments += @("-Q", $Q)
$capturedArguments | ConvertTo-Json -Compress | Set-Content -LiteralPath $env:M101_SQLCMD_LOG

if ($env:M101_BREAK_DRIVE_PATH) {
    Remove-Item -LiteralPath $env:M101_BREAK_DRIVE_PATH -Recurse -Force
    Set-Content -LiteralPath $env:M101_BREAK_DRIVE_PATH -Value "not-a-directory"
}

if ($env:M101_SQLCMD_EXIT_CODE) {
    Write-Error "Simulated sqlcmd failure"
    exit [int]$env:M101_SQLCMD_EXIT_CODE
}

$match = [regex]::Match($Q, "TO DISK = N?'((?:''|[^'])*)'", "IgnoreCase")
if (-not $match.Success) {
    Write-Error "Backup destination not found in query"
    exit 91
}

$backupPath = $match.Groups[1].Value.Replace("''", "'")
Set-Content -LiteralPath $backupPath -Value "simulated backup"
exit 0
'@ | Set-Content -LiteralPath $mockSqlCmd

    # Caso feliz: servidor/base correctos, copia y retención acotada.
    $successRoot = Join-Path $testRoot "success"
    $localDir = Join-Path $successRoot "local"
    $driveDir = Join-Path $successRoot "drive"
    New-Item -ItemType Directory -Path $localDir, $driveDir | Out-Null

    $managedOldBackup = Join-Path $localDir "PrimitivaAuditV2_LaPrimitiva_20000101_000000.bak"
    $foreignOldBackup = Join-Path $localDir "OtraBase_20000101_000000.bak"
    Set-Content -LiteralPath $managedOldBackup -Value "managed"
    Set-Content -LiteralPath $foreignOldBackup -Value "foreign"
    (Get-Item -LiteralPath $managedOldBackup).LastWriteTime = (Get-Date).AddDays(-30)
    (Get-Item -LiteralPath $foreignOldBackup).LastWriteTime = (Get-Date).AddDays(-30)

    $successLog = Join-Path $successRoot "sqlcmd.json"
    $success = Invoke-BackupScript -LocalDirectory $localDir -DriveDirectory $driveDir -SqlCmdPath $mockSqlCmd -LogPath $successLog
    Assert-True ($success.ExitCode -eq 0) "el caso feliz debe finalizar con código 0. Salida: $($success.Output)"

    $sqlArguments = @(Get-Content -LiteralPath $successLog -Raw | ConvertFrom-Json)
    $serverIndex = [Array]::IndexOf($sqlArguments, "-S")
    Assert-True ($serverIndex -ge 0 -and $sqlArguments[$serverIndex + 1] -eq "localhost\SQLEXPRESS") "sqlcmd debe recibir la instancia de la aplicación"
    Assert-True ($sqlArguments -contains "-b") "sqlcmd debe recibir -b para convertir errores SQL en código de salida"
    $queryIndex = [Array]::IndexOf($sqlArguments, "-Q")
    Assert-True ($queryIndex -ge 0 -and $sqlArguments[$queryIndex + 1] -match 'BACKUP DATABASE \[PrimitivaAuditV2\]') "la consulta debe respaldar PrimitivaAuditV2"
    Assert-True ($sqlArguments[$queryIndex + 1] -notmatch 'CuentasClarasDB') "el backup no debe incluir bases ajenas a LaPrimitiva"

    $localBackups = @(Get-ChildItem -LiteralPath $localDir -Filter "PrimitivaAuditV2_LaPrimitiva_*.bak")
    $driveBackups = @(Get-ChildItem -LiteralPath $driveDir -Filter "PrimitivaAuditV2_LaPrimitiva_*.bak")
    Assert-True ($localBackups.Count -eq 1) "debe conservarse exactamente el nuevo backup gestionado"
    Assert-True ($driveBackups.Count -eq 1) "el nuevo backup debe copiarse al destino remoto"
    Assert-True (-not (Test-Path -LiteralPath $managedOldBackup)) "la retención debe eliminar backups gestionados caducados"
    Assert-True (Test-Path -LiteralPath $foreignOldBackup) "la retención no debe eliminar .bak ajenos"

    # Un error SQL debe propagarse como código distinto de cero.
    $sqlFailureRoot = Join-Path $testRoot "sql-failure"
    $sqlFailureLocal = Join-Path $sqlFailureRoot "local"
    $sqlFailureDrive = Join-Path $sqlFailureRoot "drive"
    New-Item -ItemType Directory -Path $sqlFailureLocal, $sqlFailureDrive | Out-Null
    $env:M101_SQLCMD_EXIT_CODE = "42"
    try {
        $sqlFailure = Invoke-BackupScript -LocalDirectory $sqlFailureLocal -DriveDirectory $sqlFailureDrive -SqlCmdPath $mockSqlCmd -LogPath (Join-Path $sqlFailureRoot "sqlcmd.json")
    }
    finally {
        Remove-Item Env:M101_SQLCMD_EXIT_CODE -ErrorAction SilentlyContinue
    }
    Assert-True ($sqlFailure.ExitCode -ne 0) "un fallo de sqlcmd debe producir código distinto de cero"
    Assert-True ($sqlFailure.Output -notmatch 'Proceso finalizado correctamente') "un fallo SQL no debe anunciar éxito"

    # Un error real de copia también debe propagarse como código distinto de cero.
    $copyFailureRoot = Join-Path $testRoot "copy-failure"
    $copyFailureLocal = Join-Path $copyFailureRoot "local"
    $copyFailureDrive = Join-Path $copyFailureRoot "drive"
    New-Item -ItemType Directory -Path $copyFailureLocal, $copyFailureDrive | Out-Null
    $env:M101_BREAK_DRIVE_PATH = $copyFailureDrive
    try {
        $copyFailure = Invoke-BackupScript -LocalDirectory $copyFailureLocal -DriveDirectory $copyFailureDrive -SqlCmdPath $mockSqlCmd -LogPath (Join-Path $copyFailureRoot "sqlcmd.json")
    }
    finally {
        Remove-Item Env:M101_BREAK_DRIVE_PATH -ErrorAction SilentlyContinue
    }
    Assert-True ($copyFailure.ExitCode -ne 0) "un fallo de copia debe producir código distinto de cero"
    Assert-True ($copyFailure.Output -notmatch 'Proceso finalizado correctamente') "un fallo de copia no debe anunciar éxito"

    Write-Host "[OK] M-101 verificado: instancia/base, errores SQL/copia y retención segura." -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
