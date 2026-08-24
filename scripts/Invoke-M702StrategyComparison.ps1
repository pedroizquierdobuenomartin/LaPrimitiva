param(
    [int]$MinimumTrainingDraws = 104,
    [int]$PortfolioSize = 5,
    [string]$ServerInstance = 'localhost\LOCALSERVER',
    [string]$Database = 'PrimitivaAuditV2',
    [string]$PythonExecutable = 'C:\Program Files\Python313\python.exe'
)

$ErrorActionPreference = 'Stop'
if ($MinimumTrainingDraws -lt 1) { throw 'MinimumTrainingDraws must be positive.' }
if ($PortfolioSize -lt 2) { throw 'PortfolioSize must be at least 2 to evaluate diversification.' }

$sqlcmd = 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\SQLCMD.EXE'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$inputPath = Join-Path ([IO.Path]::GetTempPath()) "M-702-draws-$([guid]::NewGuid().ToString('N')).csv"
$outputPath = Join-Path $repositoryRoot 'mejoras\evidencias\M-702-strategy-comparison-20260824.json'
$pythonScript = Join-Path $PSScriptRoot 'm702_strategy_comparison.py'

try {
    $rows = & $sqlcmd -S $ServerInstance -d $Database -E -No -b -W -h -1 -s ';' -Q @'
SET NOCOUNT ON;
SELECT
    CONVERT(char(10), DrawDate, 23),
    Number1, Number2, Number3, Number4, Number5, Number6,
    Complementario, Reintegro
FROM WinningDraws
ORDER BY DrawDate;
'@
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed with exit code $LASTEXITCODE" }
    $rows | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Set-Content -LiteralPath $inputPath -Encoding utf8

    & $PythonExecutable $pythonScript `
        --input $inputPath `
        --output $outputPath `
        --database $Database `
        --minimum-training-draws $MinimumTrainingDraws `
        --portfolio-size $PortfolioSize
    if ($LASTEXITCODE -ne 0) { throw "strategy comparison failed with exit code $LASTEXITCODE" }
}
finally {
    if (Test-Path -LiteralPath $inputPath) {
        Remove-Item -LiteralPath $inputPath -Force
    }
}
