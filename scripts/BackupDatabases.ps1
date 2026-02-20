# ==============================================================================
# SCRIPT DE BACKUP AUTOMATIZADO PARA SQL SERVER LOCALDB
# ==============================================================================
# Autor: Antigravity Assistant
# Descripción: Genera archivos .bak de una lista de BBDD y los copia a Drive.
# ==============================================================================

# --- CONFIGURACIÓN ---

# Lista de bases de datos a procesar
$dbNameList = @(
    "PrimitivaAuditV2",
    "CuentasClarasDB"
)

# Directorio local donde se generarán los Backups (debe existir)
$localBackupDir = "Z:\BBDD\Backups"

# Directorio de Google Drive (Unidad H)
$driveBackupDir = "G:\Mi unidad\BBDD\Backups"

# Días para mantener los backups locales (limpieza)
$daysToKeepLocal = 7

# --- LOGICA DEL SCRIPT ---

# Asegurar que las carpetas existen
if (!(Test-Path $localBackupDir)) { New-Item -ItemType Directory -Path $localBackupDir | Out-Null }
if (!(Test-Path $driveBackupDir)) { 
    Write-Warning "No se pudo encontrar la unidad de Google Drive ($driveBackupDir). El script solo hará backup local."
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
Write-Host "Iniciando proceso de backup a las $(Get-Date)..." -ForegroundColor Cyan

foreach ($dbName in $dbNameList) {
    Write-Host "Procesando: $dbName" -ForegroundColor Yellow
    
    $fileName = "$dbName`_$timestamp.bak"
    $localFile = Join-Path $localBackupDir $fileName
    
    # Ejecutar Backup vía SQLCMD
    $sqlCommand = "BACKUP DATABASE [$dbName] TO DISK = '$localFile' WITH FORMAT, NAME = 'Full Backup of $dbName';"
    
    try {
        $result = sqlcmd -S "(localdb)\MSSQLLocalDB" -Q $sqlCommand -E
        
        if ($LASTEXITCODE -eq 0 -and (Test-Path $localFile)) {
            Write-Host "  [OK] Backup local creado en $localFile" -ForegroundColor Green
            
            # Copiar a Drive si está disponible
            if (Test-Path $driveBackupDir) {
                $driveFile = Join-Path $driveBackupDir $fileName
                Copy-Item -Path $localFile -Destination $driveFile -ErrorAction Stop
                Write-Host "  [OK] Copiado a Google Drive con exito." -ForegroundColor Green
            }
        } else {
            Write-Error "  [ERROR] Fallo el backup de ${dbName}."
            Write-Host "  Detalles del error SQL:" -ForegroundColor Gray
            Write-Host $result -ForegroundColor Red
            Write-Host "  Verifica que la BBDD exista y el LocalDB este encendido." -ForegroundColor Gray
        }
    } catch {
        Write-Error "  [CRITICO] Error al procesar ${dbName}: $_"
    }
}

# --- LIMPIEZA DE ARCHIVOS ANTIGUOS ---
Write-Host "Limpiando backups antiguos (>$daysToKeepLocal días)..." -ForegroundColor Gray
Get-ChildItem $localBackupDir -Filter "*.bak" | Where-Object { $_.CreationTime -lt (Get-Date).AddDays(-$daysToKeepLocal) } | Remove-Item -Force

Write-Host "Proceso finalizado." -ForegroundColor Cyan
