[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$ServerInstance = "localhost\SQLEXPRESS",

    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$BackupFile,

    [Parameter()]
    [ValidatePattern('^PrimitivaRestoreTest_[A-Za-z0-9_]+$')]
    [string]$TemporaryDatabaseName = ("PrimitivaRestoreTest_" + (Get-Date -Format "yyyyMMdd_HHmmss")),

    [Parameter()]
    [string]$RestoreDirectory,

    [Parameter()]
    [string]$EvidencePath,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$SqlCmdExecutable = "sqlcmd",

    [Parameter()]
    [switch]$KeepDatabase,

    [Parameter()]
    [ValidateSet("M-102", "M-603")]
    [string]$Milestone = "M-102"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function ConvertTo-SqlLiteral {
    param([Parameter(Mandatory)][string]$Value)
    return $Value.Replace("'", "''")
}

function Invoke-CheckedSqlCmd {
    param(
        [Parameter(Mandatory)][string]$Query,
        [string[]]$AdditionalArguments = @()
    )

    $output = & $SqlCmdExecutable -b -S $ServerInstance -E @AdditionalArguments -Q $Query 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        $details = @($output) -join [Environment]::NewLine
        throw "sqlcmd falló con código $exitCode. $details"
    }

    return @($output)
}

$resolvedBackupFile = (Resolve-Path -LiteralPath $BackupFile).Path
$escapedBackupFile = ConvertTo-SqlLiteral -Value $resolvedBackupFile
$escapedDatabaseName = $TemporaryDatabaseName.Replace("]", "]]" )
$restored = $false
$startedAt = Get-Date

try {
    $verifyQuery = "RESTORE VERIFYONLY FROM DISK = N'$escapedBackupFile' WITH CHECKSUM;"
    Invoke-CheckedSqlCmd -Query $verifyQuery | Out-Null

    $fileListQuery = "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK = N'$escapedBackupFile';"
    $fileListOutput = Invoke-CheckedSqlCmd -Query $fileListQuery -AdditionalArguments @("-h", "-1", "-W", "-s", "|")
    $databaseFiles = @(
        foreach ($line in $fileListOutput) {
            $parts = @($line -split '\|' | ForEach-Object { $_.Trim() })
            if ($parts.Count -ge 3 -and $parts[2] -in @("D", "L")) {
                [pscustomobject]@{ LogicalName = $parts[0]; Type = $parts[2] }
            }
        }
    )

    if ($databaseFiles.Count -eq 0) {
        throw "RESTORE FILELISTONLY no devolvió archivos de datos o log analizables."
    }

    if ([string]::IsNullOrWhiteSpace($RestoreDirectory)) {
        $pathQuery = "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000));"
        $RestoreDirectory = (@(Invoke-CheckedSqlCmd -Query $pathQuery -AdditionalArguments @("-h", "-1", "-W")) |
            ForEach-Object { $_.Trim() } | Where-Object { $_ } | Select-Object -First 1)
    }

    if ([string]::IsNullOrWhiteSpace($RestoreDirectory)) {
        throw "No se pudo determinar el directorio de datos de SQL Server."
    }

    $moveClauses = @()
    $dataIndex = 0
    $logIndex = 0
    foreach ($databaseFile in $databaseFiles) {
        if ($databaseFile.Type -eq "L") {
            $logIndex++
            $extension = ".ldf"
            $suffix = "log_$logIndex"
        }
        else {
            $dataIndex++
            $extension = if ($dataIndex -eq 1) { ".mdf" } else { ".ndf" }
            $suffix = "data_$dataIndex"
        }

        $physicalPath = Join-Path $RestoreDirectory "${TemporaryDatabaseName}_${suffix}${extension}"
        $escapedLogicalName = ConvertTo-SqlLiteral -Value $databaseFile.LogicalName
        $escapedPhysicalPath = ConvertTo-SqlLiteral -Value $physicalPath
        $moveClauses += "MOVE N'$escapedLogicalName' TO N'$escapedPhysicalPath'"
    }

    $cleanupQuery = "IF DB_ID(N'$(ConvertTo-SqlLiteral -Value $TemporaryDatabaseName)') IS NOT NULL BEGIN ALTER DATABASE [$escapedDatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$escapedDatabaseName]; END;"
    Invoke-CheckedSqlCmd -Query $cleanupQuery | Out-Null

    $restoreQuery = @"
RESTORE DATABASE [$escapedDatabaseName]
FROM DISK = N'$escapedBackupFile'
WITH CHECKSUM, RECOVERY, REPLACE, $($moveClauses -join ', ');
DBCC CHECKDB([$escapedDatabaseName]) WITH NO_INFOMSGS;
"@
    Invoke-CheckedSqlCmd -Query $restoreQuery | Out-Null
    $restored = $true

    $hash = (Get-FileHash -LiteralPath $resolvedBackupFile -Algorithm SHA256).Hash.ToLowerInvariant()
    $evidence = [ordered]@{
        milestone = $Milestone
        result = "successful"
        serverInstance = $ServerInstance
        sourceBackup = $resolvedBackupFile
        sha256 = $hash
        temporaryDatabase = $TemporaryDatabaseName
        restoreVerifyOnly = "successful"
        restoreDatabase = "successful"
        dbccCheckDb = "successful"
        cleanup = if ($KeepDatabase) { "retained-for-functional-validation" } else { "scheduled" }
        startedAt = $startedAt.ToString("o")
        completedAt = (Get-Date).ToString("o")
    }

    if (-not [string]::IsNullOrWhiteSpace($EvidencePath)) {
        $evidenceDirectory = Split-Path -Parent $EvidencePath
        if ($evidenceDirectory -and -not (Test-Path -LiteralPath $evidenceDirectory -PathType Container)) {
            New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null
        }
        $evidence | ConvertTo-Json | Set-Content -LiteralPath $EvidencePath -Encoding utf8
    }

    Write-Host "[OK] Restauración temporal y DBCC CHECKDB correctos para '$TemporaryDatabaseName'." -ForegroundColor Green
    $evidence
}
finally {
    if ($restored -and -not $KeepDatabase) {
        $dropQuery = "IF DB_ID(N'$(ConvertTo-SqlLiteral -Value $TemporaryDatabaseName)') IS NOT NULL BEGIN ALTER DATABASE [$escapedDatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$escapedDatabaseName]; END;"
        Invoke-CheckedSqlCmd -Query $dropQuery | Out-Null
        Write-Host "[OK] Base temporal '$TemporaryDatabaseName' eliminada." -ForegroundColor Green
    }
    elseif ($restored) {
        Write-Host "[OK] Base temporal '$TemporaryDatabaseName' conservada para validación funcional." -ForegroundColor Green
    }
}
