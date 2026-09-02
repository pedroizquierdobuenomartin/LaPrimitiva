[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$ServerInstance = "localhost\LOCALSERVER",

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$SourceDatabase = "PrimitivaAuditV2",

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$BackupDirectory = "Z:\BBDD\Backups",

    [Parameter()]
    [ValidatePattern('^PrimitivaRestoreTest_M603_[A-Za-z0-9_]+$')]
    [string]$TemporaryDatabaseName = ("PrimitivaRestoreTest_M603_" + (Get-Date -Format "yyyyMMdd_HHmmss")),

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$AppUrl = "http://127.0.0.1:5063",

    [Parameter()]
    [ValidateRange(5, 300)]
    [int]$StartupTimeoutSeconds = 60,

    [Parameter()]
    [string]$EvidencePath = (Join-Path (Split-Path -Parent $PSScriptRoot) ("mejoras\evidencias\M-603-recovery-drill-{0}.json" -f (Get-Date -Format "yyyyMMdd"))),

    [Parameter()]
    [string]$AppDll = (Join-Path (Split-Path -Parent $PSScriptRoot) "LaPrimitiva.App\bin\Debug\net10.0\LaPrimitiva.App.dll"),

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$SqlCmdExecutable = "sqlcmd",

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$DotNetExecutable = "dotnet"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$backupScript = Join-Path $PSScriptRoot "BackupDatabases.ps1"
$restoreScript = Join-Path $PSScriptRoot "Test-DatabaseRestore.ps1"
$appContentRoot = Join-Path $repositoryRoot "LaPrimitiva.App"
$startedAt = Get-Date
$phaseStartedAt = $startedAt
$timings = [ordered]@{}
$applicationProcess = $null
$restoreAttempted = $false
$backupFile = $null

function ConvertTo-SqlLiteral {
    param([Parameter(Mandatory)][string]$Value)
    return $Value.Replace("'", "''")
}

function Invoke-CheckedSqlCmd {
    param(
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$Query,
        [string[]]$AdditionalArguments = @()
    )

    $output = & $SqlCmdExecutable -b -S $ServerInstance -E -d $Database @AdditionalArguments -Q $Query 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd falló para '$Database'. $(@($output) -join [Environment]::NewLine)"
    }

    return @($output)
}

function Get-FunctionalSnapshot {
    param([Parameter(Mandatory)][string]$Database)

    $query = @"
SET NOCOUNT ON;
SELECT
    (SELECT COUNT_BIG(*) FROM dbo.DrawRecords) AS drawRecords,
    (SELECT COUNT_BIG(*) FROM dbo.Plans) AS plans,
    (SELECT COUNT_BIG(*) FROM dbo.WinningDraws) AS winningDraws,
    (SELECT COUNT_BIG(*) FROM dbo.DrawRecords WHERE Played = 1) AS playedDraws,
    (SELECT COUNT_BIG(*) FROM dbo.DrawRecords WHERE TotalPremios <> 0) AS recordsWithPrizes,
    (SELECT COALESCE(SUM(TotalCoste), 0) FROM dbo.DrawRecords) AS totalSpent,
    (SELECT COALESCE(SUM(TotalPremios), 0) FROM dbo.DrawRecords) AS totalWon,
    (SELECT COALESCE(SUM(FixedPrize), 0) FROM dbo.DrawRecords) AS fixedPrizes,
    (SELECT COALESCE(SUM(AutoPrize), 0) FROM dbo.DrawRecords) AS automaticPrizes,
    (SELECT COALESCE(SUM(JokerFixedPrize + JokerAutoPrize), 0) FROM dbo.DrawRecords) AS jokerPrizes,
    (SELECT COALESCE(SUM(Neto), 0) FROM dbo.DrawRecords) AS netResult
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;
"@
    $json = (@(Invoke-CheckedSqlCmd -Database $Database -Query $query -AdditionalArguments @("-h", "-1", "-W", "-w", "65535")) -join "").Trim()
    if ([string]::IsNullOrWhiteSpace($json)) {
        throw "No se obtuvo el resumen funcional de '$Database'."
    }

    return $json | ConvertFrom-Json
}

function Assert-SnapshotsEqual {
    param($Source, $Restored)

    foreach ($property in $Source.PSObject.Properties.Name) {
        if ([string]$Source.$property -ne [string]$Restored.$property) {
            throw "La copia restaurada difiere en '$property': origen=$($Source.$property), restaurada=$($Restored.$property)."
        }
    }
}

function Complete-Phase {
    param([Parameter(Mandatory)][string]$Name)
    $now = Get-Date
    $timings[$Name] = [math]::Round(($now - $phaseStartedAt).TotalSeconds, 3)
    $script:phaseStartedAt = $now
}

try {
    if (-not (Test-Path -LiteralPath $AppDll -PathType Leaf)) {
        throw "No existe el binario '$AppDll'. M-603 no compila: debe proporcionarse un binario ya validado."
    }
    if (-not (Test-Path -LiteralPath $BackupDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $BackupDirectory -Force | Out-Null
    }
    $BackupDirectory = (Resolve-Path -LiteralPath $BackupDirectory).Path

    $sourceSnapshot = Get-FunctionalSnapshot -Database $SourceDatabase
    Complete-Phase -Name "sourceSnapshotSeconds"

    $existingBackups = @(Get-ChildItem -LiteralPath $BackupDirectory -Filter "${SourceDatabase}_LaPrimitiva_*.bak" -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
    & pwsh -NoProfile -File $backupScript -ServerInstance $ServerInstance -DatabaseNames $SourceDatabase -LocalBackupDir $BackupDirectory -DriveBackupDir (Join-Path $BackupDirectory "M603-no-secondary-copy") -DaysToKeepLocal 7 -SqlCmdExecutable $SqlCmdExecutable
    if ($LASTEXITCODE -ne 0) {
        throw "La creación del backup nuevo terminó con código $LASTEXITCODE."
    }

    $backupFile = Get-ChildItem -LiteralPath $BackupDirectory -Filter "${SourceDatabase}_LaPrimitiva_*.bak" -File |
        Where-Object FullName -notin $existingBackups |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -eq $backupFile) {
        throw "No se pudo identificar un backup nuevo de '$SourceDatabase'."
    }
    Complete-Phase -Name "backupSeconds"

    $restoreAttempted = $true
    & $restoreScript -ServerInstance $ServerInstance -BackupFile $backupFile.FullName -TemporaryDatabaseName $TemporaryDatabaseName -SqlCmdExecutable $SqlCmdExecutable -KeepDatabase -Milestone "M-603" | Out-Null
    Complete-Phase -Name "restoreAndCheckDbSeconds"

    $restoredSnapshot = Get-FunctionalSnapshot -Database $TemporaryDatabaseName
    Assert-SnapshotsEqual -Source $sourceSnapshot -Restored $restoredSnapshot
    Complete-Phase -Name "functionalComparisonSeconds"

    $connectionString = "Server=$ServerInstance;Database=$TemporaryDatabaseName;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
    $processInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $processInfo.FileName = $DotNetExecutable
    $processInfo.WorkingDirectory = $appContentRoot
    $processInfo.UseShellExecute = $false
    $processInfo.CreateNoWindow = $true
    $processInfo.ArgumentList.Add((Resolve-Path -LiteralPath $AppDll).Path)
    $processInfo.ArgumentList.Add("--urls")
    $processInfo.ArgumentList.Add($AppUrl)
    $processInfo.Environment["ConnectionStrings__DefaultConnection"] = $connectionString
    $processInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"
    $applicationProcess = [System.Diagnostics.Process]::Start($processInfo)

    $deadline = (Get-Date).AddSeconds($StartupTimeoutSeconds)
    $readyResponse = $null
    while ((Get-Date) -lt $deadline -and -not $applicationProcess.HasExited) {
        try {
            $readyResponse = Invoke-WebRequest -Uri "$AppUrl/health/ready" -UseBasicParsing -TimeoutSec 5
            if ($readyResponse.StatusCode -eq 200 -and $readyResponse.Content -match 'Healthy') { break }
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    }
    if ($null -eq $readyResponse -or $readyResponse.StatusCode -ne 200 -or $readyResponse.Content -notmatch 'Healthy') {
        throw "La aplicación no alcanzó readiness contra la copia restaurada en $StartupTimeoutSeconds segundos."
    }
    Complete-Phase -Name "applicationStartupSeconds"

    $routeChecks = [ordered]@{}
    $routes = [ordered]@{
        "/" = @("Total Gastado", "Total Ganado")
        "/planes" = @("Planes", "Premios")
        "/registro" = @("Registro", "Listado de sorteos para el año")
        "/historico" = @("Histórico")
        "/datos" = @("Datos")
    }
    foreach ($route in $routes.GetEnumerator()) {
        $response = Invoke-WebRequest -Uri ($AppUrl + $route.Key) -UseBasicParsing -TimeoutSec 15
        if ($response.StatusCode -ne 200) {
            throw "La ruta '$($route.Key)' devolvió HTTP $($response.StatusCode)."
        }
        $decodedContent = [System.Net.WebUtility]::HtmlDecode($response.Content)
        foreach ($marker in $route.Value) {
            if ($decodedContent -notmatch [regex]::Escape($marker)) {
                throw "La ruta '$($route.Key)' no contiene el marcador funcional '$marker'."
            }
        }
        $routeChecks[$route.Key] = [ordered]@{ statusCode = $response.StatusCode; markers = @($route.Value) }
    }
    Complete-Phase -Name "applicationRoutesSeconds"

    $completedAt = Get-Date
    $evidence = [ordered]@{
        milestone = "M-603"
        result = "validation-successful-pending-cleanup"
        serverInstance = $ServerInstance
        sourceDatabase = $SourceDatabase
        temporaryDatabase = $TemporaryDatabaseName
        backupFile = $backupFile.FullName
        backupBytes = $backupFile.Length
        backupSha256 = (Get-FileHash -LiteralPath $backupFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        appDll = (Resolve-Path -LiteralPath $AppDll).Path
        appDllSha256 = (Get-FileHash -LiteralPath $AppDll -Algorithm SHA256).Hash.ToLowerInvariant()
        appUrl = $AppUrl
        readiness = "Healthy"
        sourceSnapshot = $sourceSnapshot
        restoredSnapshot = $restoredSnapshot
        routeChecks = $routeChecks
        timingsSeconds = $timings
        totalDurationSeconds = [math]::Round(($completedAt - $startedAt).TotalSeconds, 3)
        startedAt = $startedAt.ToString("o")
        completedAt = $completedAt.ToString("o")
        cleanup = "pending"
    }

    $evidenceDirectory = Split-Path -Parent $EvidencePath
    if ($evidenceDirectory -and -not (Test-Path -LiteralPath $evidenceDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null
    }
    $evidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $EvidencePath -Encoding utf8
    Write-Host "[OK] M-603 completado en $($evidence.totalDurationSeconds) s. Evidencia: '$EvidencePath'." -ForegroundColor Green
}
finally {
    if ($null -ne $applicationProcess -and -not $applicationProcess.HasExited) {
        $applicationProcess.Kill($true)
        $applicationProcess.WaitForExit(10000) | Out-Null
    }

    if ($restoreAttempted) {
        $escapedTemporaryDatabase = $TemporaryDatabaseName.Replace("]", "]]" )
        $literalTemporaryDatabase = ConvertTo-SqlLiteral -Value $TemporaryDatabaseName
        $dropQuery = "IF DB_ID(N'$literalTemporaryDatabase') IS NOT NULL BEGIN ALTER DATABASE [$escapedTemporaryDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$escapedTemporaryDatabase]; END;"
        Invoke-CheckedSqlCmd -Database "master" -Query $dropQuery | Out-Null
        Write-Host "[OK] Base temporal '$TemporaryDatabaseName' eliminada." -ForegroundColor Green
        if (Test-Path -LiteralPath $EvidencePath -PathType Leaf) {
            $savedEvidence = Get-Content -LiteralPath $EvidencePath -Raw | ConvertFrom-Json
            $savedEvidence.result = "successful"
            $savedEvidence.cleanup = "completed"
            $savedEvidence | Add-Member -NotePropertyName cleanupCompletedAt -NotePropertyValue ((Get-Date).ToString("o")) -Force
            $savedEvidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $EvidencePath -Encoding utf8
        }
    }
}
