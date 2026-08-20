[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$ServerInstance = "localhost\SQLEXPRESS",

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string[]]$DatabaseNames = @("PrimitivaAuditV2"),

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$LocalBackupDir = "Z:\BBDD\Backups",

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$DriveBackupDir = "G:\Mi unidad\BBDD\Backups",

    [Parameter()]
    [ValidateRange(0, 3650)]
    [int]$DaysToKeepLocal = 7,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$SqlCmdExecutable = "sqlcmd"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$backupMarker = "LaPrimitiva"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

function Get-SafeFileComponent {
    param(
        [Parameter(Mandatory)]
        [string]$Value
    )

    $safeValue = $Value -replace '[^A-Za-z0-9_-]', '_'
    if ([string]::IsNullOrWhiteSpace($safeValue)) {
        throw "El nombre de base de datos '$Value' no permite generar un nombre de archivo seguro."
    }

    return $safeValue
}

try {
    if (-not (Test-Path -LiteralPath $LocalBackupDir -PathType Container)) {
        New-Item -ItemType Directory -Path $LocalBackupDir -ErrorAction Stop | Out-Null
    }

    $copyToDrive = Test-Path -LiteralPath $DriveBackupDir -PathType Container
    if (-not $copyToDrive) {
        Write-Warning "No se encontró el directorio remoto '$DriveBackupDir'. Solo se crearán backups locales."
    }

    Write-Host "Iniciando backup en '$ServerInstance' a las $(Get-Date)..." -ForegroundColor Cyan

    foreach ($databaseName in $DatabaseNames) {
        if ([string]::IsNullOrWhiteSpace($databaseName)) {
            throw "La lista de bases de datos contiene un nombre vacío."
        }

        $safeDatabaseName = Get-SafeFileComponent -Value $databaseName
        $fileName = "${safeDatabaseName}_${backupMarker}_${timestamp}.bak"
        $localFile = Join-Path $LocalBackupDir $fileName
        $escapedDatabaseName = $databaseName.Replace("]", "]]" )
        $escapedLocalFile = $localFile.Replace("'", "''")
        $sqlCommand = "BACKUP DATABASE [$escapedDatabaseName] TO DISK = N'$escapedLocalFile' WITH FORMAT, NAME = N'Full Backup of $escapedDatabaseName';"

        Write-Host "Procesando '$databaseName'..." -ForegroundColor Yellow
        $sqlOutput = & $SqlCmdExecutable -b -S $ServerInstance -E -Q $sqlCommand 2>&1
        $sqlExitCode = $LASTEXITCODE

        if ($sqlExitCode -ne 0) {
            $details = @($sqlOutput) -join [Environment]::NewLine
            throw "sqlcmd falló para '$databaseName' con código $sqlExitCode. $details"
        }

        if (-not (Test-Path -LiteralPath $localFile -PathType Leaf)) {
            throw "sqlcmd terminó sin error, pero no se creó '$localFile'."
        }

        Write-Host "  [OK] Backup local creado en '$localFile'." -ForegroundColor Green

        if ($copyToDrive) {
            $driveFile = Join-Path $DriveBackupDir $fileName
            Copy-Item -LiteralPath $localFile -Destination $driveFile -Force -ErrorAction Stop
            Write-Host "  [OK] Backup copiado a '$driveFile'." -ForegroundColor Green
        }
    }

    $retentionLimit = (Get-Date).AddDays(-$DaysToKeepLocal)
    Write-Host "Limpiando backups gestionados con más de $DaysToKeepLocal días..." -ForegroundColor Gray

    foreach ($databaseName in $DatabaseNames) {
        $safeDatabaseName = Get-SafeFileComponent -Value $databaseName
        $managedPattern = "${safeDatabaseName}_${backupMarker}_*.bak"
        Get-ChildItem -LiteralPath $LocalBackupDir -File -Filter $managedPattern -ErrorAction Stop |
            Where-Object { $_.LastWriteTime -lt $retentionLimit } |
            Remove-Item -Force -ErrorAction Stop
    }

    Write-Host "Proceso finalizado correctamente." -ForegroundColor Cyan
}
catch {
    Write-Error "El proceso de backup ha fallado: $($_.Exception.Message)"
    exit 1
}
