[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$backupScript = Join-Path $PSScriptRoot "BackupDatabases.ps1"
$restoreScript = Join-Path $PSScriptRoot "Test-DatabaseRestore.ps1"
$powerShell = (Get-Process -Id $PID).Path
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("LaPrimitiva-M102-" + [guid]::NewGuid().ToString("N"))

function Assert-True {
    param([Parameter(Mandatory)][bool]$Condition, [Parameter(Mandatory)][string]$Message)
    if (-not $Condition) { throw "Fallo de verificación: $Message" }
}

function Invoke-Backup {
    param([string]$LocalDirectory, [string]$DriveDirectory, [string]$SqlCmdPath, [string]$LogPath)
    $env:M102_SQLCMD_LOG = $LogPath
    try {
        $output = & $powerShell -NoLogo -NoProfile -File $backupScript `
            -LocalBackupDir $LocalDirectory -DriveBackupDir $DriveDirectory `
            -SqlCmdExecutable $SqlCmdPath 2>&1
        return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = @($output) -join [Environment]::NewLine }
    }
    finally { Remove-Item Env:M102_SQLCMD_LOG -ErrorAction SilentlyContinue }
}

try {
    New-Item -ItemType Directory -Path $testRoot | Out-Null
    $mockSqlCmd = Join-Path $testRoot "mock-sqlcmd.ps1"
    @'
param([switch]$b, [string]$S, [switch]$E, [string]$Q)

$Q | Add-Content -LiteralPath $env:M102_SQLCMD_LOG

if ($Q -match '^BACKUP DATABASE') {
    $match = [regex]::Match($Q, "TO DISK = N?'((?:''|[^'])*)'", "IgnoreCase")
    if (-not $match.Success) { exit 91 }
    Set-Content -LiteralPath $match.Groups[1].Value.Replace("''", "'") -Value "simulated backup"
    exit 0
}

if ($Q -match '^RESTORE VERIFYONLY' -and $env:M102_CORRUPT_BACKUP) {
    Write-Error "Simulated corrupt backup"
    exit 45
}

if ($Q -match 'RESTORE FILELISTONLY') {
    Write-Output 'PrimitivaAuditV2|C:\SqlData\PrimitivaAuditV2.mdf|D'
    Write-Output 'PrimitivaAuditV2_log|C:\SqlData\PrimitivaAuditV2_log.ldf|L'
}

exit 0
'@ | Set-Content -LiteralPath $mockSqlCmd

    # El flujo correcto verifica, genera SHA-256 y replica ambos archivos.
    $successRoot = Join-Path $testRoot "success"
    $localDir = Join-Path $successRoot "local"
    $driveDir = Join-Path $successRoot "drive"
    New-Item -ItemType Directory -Path $localDir, $driveDir | Out-Null
    $successLog = Join-Path $successRoot "sqlcmd.log"
    $success = Invoke-Backup -LocalDirectory $localDir -DriveDirectory $driveDir -SqlCmdPath $mockSqlCmd -LogPath $successLog
    Assert-True ($success.ExitCode -eq 0) "el backup verificable debe terminar correctamente. $($success.Output)"

    $backup = @(Get-ChildItem -LiteralPath $localDir -Filter "*_LaPrimitiva_*.bak")[0]
    $hashFile = "$($backup.FullName).sha256"
    Assert-True (Test-Path -LiteralPath $hashFile -PathType Leaf) "debe generarse el fichero SHA-256"
    $expectedHash = (Get-FileHash -LiteralPath $backup.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert-True ((Get-Content -LiteralPath $hashFile -Raw) -match "^$expectedHash \*$([regex]::Escape($backup.Name))") "el SHA-256 debe corresponder al .bak"
    Assert-True (Test-Path -LiteralPath (Join-Path $driveDir $backup.Name) -PathType Leaf) "debe copiarse el .bak"
    Assert-True (Test-Path -LiteralPath (Join-Path $driveDir "$($backup.Name).sha256") -PathType Leaf) "debe copiarse el hash"

    $queries = Get-Content -LiteralPath $successLog -Raw
    Assert-True ($queries -match 'BACKUP DATABASE .* WITH FORMAT, CHECKSUM') "BACKUP DATABASE debe crear checksums de página"
    Assert-True ($queries -match 'RESTORE VERIFYONLY .* WITH CHECKSUM') "cada backup debe ejecutar RESTORE VERIFYONLY con checksum"

    # Un VERIFYONLY fallido debe ser visible y no distribuir el backup.
    $corruptRoot = Join-Path $testRoot "corrupt"
    $corruptLocal = Join-Path $corruptRoot "local"
    $corruptDrive = Join-Path $corruptRoot "drive"
    New-Item -ItemType Directory -Path $corruptLocal, $corruptDrive | Out-Null
    $env:M102_CORRUPT_BACKUP = "1"
    try {
        $corrupt = Invoke-Backup -LocalDirectory $corruptLocal -DriveDirectory $corruptDrive -SqlCmdPath $mockSqlCmd -LogPath (Join-Path $corruptRoot "sqlcmd.log")
    }
    finally { Remove-Item Env:M102_CORRUPT_BACKUP -ErrorAction SilentlyContinue }
    Assert-True ($corrupt.ExitCode -ne 0) "un backup corrupto debe devolver código distinto de cero"
    Assert-True ($corrupt.Output -notmatch 'Proceso finalizado correctamente') "un backup corrupto no debe anunciar éxito"
    Assert-True (@(Get-ChildItem -LiteralPath $corruptDrive -File).Count -eq 0) "un backup no verificado no debe copiarse"
    Assert-True (@(Get-ChildItem -LiteralPath $corruptLocal -Filter '*.sha256' -File).Count -eq 0) "un backup no verificado no debe recibir hash"

    # El simulacro se comprueba estáticamente aquí y se valida contra SQL Server en la prueba operativa de M-102.
    $restoreSource = Get-Content -LiteralPath $restoreScript -Raw
    Assert-True ($restoreSource -match 'RESTORE VERIFYONLY') "el simulacro debe verificar el backup antes de restaurarlo"
    Assert-True ($restoreSource -match 'RESTORE FILELISTONLY') "el simulacro debe descubrir los archivos lógicos"
    Assert-True ($restoreSource -match 'RESTORE DATABASE') "el simulacro debe restaurar una base temporal"
    Assert-True ($restoreSource -match 'DBCC CHECKDB') "el simulacro debe comprobar la consistencia"
    Assert-True ($restoreSource -match 'DROP DATABASE') "el simulacro debe eliminar la base temporal"
    Assert-True ($restoreSource -match "ValidatePattern\('\^PrimitivaRestoreTest_") "el nombre debe estar restringido al prefijo seguro"

    Write-Host "[OK] M-102 verificado: VERIFYONLY, SHA-256, fallo visible y restauración temporal segura." -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}
