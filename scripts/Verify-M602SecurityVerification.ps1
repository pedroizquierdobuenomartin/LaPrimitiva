[CmdletBinding()]
param(
    [switch] $SkipOnlineAnalysis,
    [switch] $SkipRuntimeAnalysis,
    [string] $AppDllPath,
    [string] $EvidencePath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'LaPrimitiva.sln'
$appRoot = Join-Path $repositoryRoot 'LaPrimitiva.App'

if ([string]::IsNullOrWhiteSpace($AppDllPath)) {
    $AppDllPath = Join-Path $appRoot 'bin\Debug\net10.0\LaPrimitiva.App.dll'
}
elseif (-not [System.IO.Path]::IsPathRooted($AppDllPath)) {
    $AppDllPath = Join-Path $repositoryRoot $AppDllPath
}

if (-not [string]::IsNullOrWhiteSpace($EvidencePath) -and
    -not [System.IO.Path]::IsPathRooted($EvidencePath)) {
    $EvidencePath = Join-Path $repositoryRoot $EvidencePath
}

function Assert-Condition {
    param(
        [Parameter(Mandatory)] [bool] $Condition,
        [Parameter(Mandatory)] [string] $Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Invoke-ExistingVerifier {
    param([Parameter(Mandatory)] [string] $Name)

    Write-Host ">>> $Name"
    & (Join-Path $PSScriptRoot $Name)
    if ($LASTEXITCODE) {
        throw "$Name terminó con código $LASTEXITCODE."
    }
}

function ConvertFrom-CommandJson {
    param(
        [Parameter(Mandatory)] [string[]] $Output,
        [Parameter(Mandatory)] [string] $Description
    )

    $rawOutput = $Output -join [Environment]::NewLine
    $jsonStart = $rawOutput.IndexOf('{')
    if ($jsonStart -lt 0) {
        throw "$Description no devolvió JSON reconocible: $rawOutput"
    }

    try {
        return $rawOutput.Substring($jsonStart) | ConvertFrom-Json
    }
    catch {
        throw "No se pudo interpretar el JSON de $Description`: $($_.Exception.Message)"
    }
}

function Get-FreeLoopbackPort {
    $listener = [System.Net.Sockets.TcpListener]::new(
        [System.Net.IPAddress]::Loopback,
        0)
    try {
        $listener.Start()
        return ([System.Net.IPEndPoint] $listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Start-TestApplication {
    param(
        [Parameter(Mandatory)] [string] $Url,
        [Parameter(Mandatory)] [string] $OutputPath,
        [Parameter(Mandatory)] [string] $ErrorPath
    )

    return Start-Process `
        -FilePath 'dotnet' `
        -ArgumentList @($AppDllPath, '--urls', $Url, '--environment', 'IntegrationTests') `
        -WorkingDirectory $repositoryRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $OutputPath `
        -RedirectStandardError $ErrorPath `
        -PassThru
}

$evidence = [ordered]@{
    milestone = 'M-602'
    checkedAtUtc = [DateTime]::UtcNow.ToString('O')
    repositoryCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    staticAnalysis = [ordered]@{
        passed = $false
        reusedVerifiers = @()
        forbiddenProductionPatterns = @()
        externalMutableScripts = 0
    }
    dependencies = [ordered]@{
        performed = -not $SkipOnlineAnalysis
        nugetVulnerabilities = $null
        npmVulnerabilities = $null
    }
    runtime = [ordered]@{
        performed = -not $SkipRuntimeAnalysis
        appDll = $null
        appDllSha256 = $null
        rejectedNonLoopback = $null
        listeners = @()
        healthStatus = $null
        contentSecurityPolicy = $null
    }
}

foreach ($verifier in @(
    'Verify-M301LocalOnly.ps1',
    'Verify-M302ContentSecurity.ps1',
    'Verify-M304RssLimits.ps1',
    'Verify-M305CsvFormulaNeutralization.ps1'
)) {
    Invoke-ExistingVerifier $verifier
    $evidence.staticAnalysis.reusedVerifiers += $verifier
}

$productionFiles = @(
    Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'LaPrimitiva.App') -Recurse -File -Include '*.cs', '*.razor' |
        Where-Object { $_.FullName -notmatch '\\(bin|obj|node_modules)\\' }
    Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'LaPrimitiva.Application') -Recurse -File -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
    Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'LaPrimitiva.Domain') -Recurse -File -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
    Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'LaPrimitiva.Infrastructure') -Recurse -File -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '\\(bin|obj|Migrations)\\' }
    Get-ChildItem -LiteralPath (Join-Path $appRoot 'wwwroot\js') -Recurse -File -Filter '*.js'
)

$forbiddenPatterns = [ordered]@{
    'SQL dinámico sin parametrizar' = 'ExecuteSqlRaw|FromSqlRaw|SqlQueryRaw'
    'Ejecución de procesos desde producto' = 'Process\.Start|System\.Diagnostics\.Process'
    'Deserialización binaria insegura' = 'BinaryFormatter|NetDataContractSerializer|LosFormatter'
    'XML con DTD habilitado' = 'DtdProcessing\s*=\s*DtdProcessing\.Parse'
    'Ejecución dinámica de JavaScript' = '\beval\s*\(|\bnew\s+Function\s*\(|\.innerHTML\s*='
}

foreach ($entry in $forbiddenPatterns.GetEnumerator()) {
    $matches = @($productionFiles | Select-String -Pattern $entry.Value)
    if ($matches.Count -gt 0) {
        $locations = $matches | ForEach-Object {
            "$($_.Path):$($_.LineNumber)"
        }
        throw "$($entry.Key): $($locations -join ', ')"
    }

    $evidence.staticAnalysis.forbiddenProductionPatterns += $entry.Key
}

$webSourceFiles = @(
    Get-ChildItem -LiteralPath (Join-Path $appRoot 'Components') -Recurse -File -Include '*.razor', '*.html'
    Get-ChildItem -LiteralPath (Join-Path $appRoot 'wwwroot') -Recurse -File -Include '*.js', '*.css' |
        Where-Object { $_.FullName -notmatch '\\(lib|licenses)\\' }
)
$externalAssetPattern = @'
(?is)<script\b[^>]*\bsrc\s*=\s*["']\s*(?:https?:)?//|<link\b[^>]*\bhref\s*=\s*["']\s*(?:https?:)?//|@import\s+(?:url\s*\()?\s*["']?\s*(?:https?:)?//|\bimport\s*\(\s*["']\s*(?:https?:)?//
'@
$externalAssets = @($webSourceFiles | Select-String -Pattern $externalAssetPattern)
Assert-Condition ($externalAssets.Count -eq 0) (
    'Se encontraron scripts, estilos o imports externos mutables: ' +
    (($externalAssets | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join ', '))

$evidence.staticAnalysis.externalMutableScripts = 0
$evidence.staticAnalysis.passed = $true

if (-not $SkipOnlineAnalysis) {
    Write-Host '>>> NuGet vulnerability analysis'
    $nugetOutput = @(& dotnet package list --project $solutionPath --vulnerable --include-transitive --no-restore --format json 2>&1)
    $nugetExitCode = $LASTEXITCODE
    Assert-Condition ($nugetExitCode -eq 0) (
        "La consulta de vulnerabilidades NuGet terminó con código $nugetExitCode`: $($nugetOutput -join [Environment]::NewLine)")
    $nugetReport = ConvertFrom-CommandJson $nugetOutput 'la consulta NuGet'
    $nugetFrameworks = @($nugetReport.projects | ForEach-Object { @($_.frameworks) })
    $nugetVulnerabilities = @(
        $nugetFrameworks | ForEach-Object {
            @($_.topLevelPackages) + @($_.transitivePackages)
        } | Where-Object { $null -ne $_ }
    )
    Assert-Condition ($nugetVulnerabilities.Count -eq 0) (
        "NuGet informó $($nugetVulnerabilities.Count) paquetes vulnerables.")
    $evidence.dependencies.nugetVulnerabilities = 0

    Write-Host '>>> npm vulnerability analysis'
    Push-Location $appRoot
    try {
        $npmOutput = @(& npm audit --json 2>&1)
        $npmExitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
    $npmReport = ConvertFrom-CommandJson $npmOutput 'npm audit'
    $npmVulnerabilityCount = [int] $npmReport.metadata.vulnerabilities.total
    Assert-Condition ($npmExitCode -eq 0 -and $npmVulnerabilityCount -eq 0) (
        "npm audit terminó con código $npmExitCode y $npmVulnerabilityCount vulnerabilidades.")
    $evidence.dependencies.npmVulnerabilities = 0
}

if (-not $SkipRuntimeAnalysis) {
    Assert-Condition (Test-Path -LiteralPath $AppDllPath -PathType Leaf) (
        "No existe el binario para la verificación runtime: $AppDllPath")

    $resolvedAppDll = (Resolve-Path -LiteralPath $AppDllPath).Path
    $evidence.runtime.appDll = $resolvedAppDll
    $evidence.runtime.appDllSha256 = (Get-FileHash -LiteralPath $resolvedAppDll -Algorithm SHA256).Hash

    $nonLoopbackPort = Get-FreeLoopbackPort
    $nonLoopbackOut = Join-Path ([System.IO.Path]::GetTempPath()) "m602-nonloopback-$PID-out.txt"
    $nonLoopbackErr = Join-Path ([System.IO.Path]::GetTempPath()) "m602-nonloopback-$PID-err.txt"
    $nonLoopbackProcess = Start-TestApplication "http://0.0.0.0:$nonLoopbackPort" $nonLoopbackOut $nonLoopbackErr
    try {
        if (-not $nonLoopbackProcess.WaitForExit(10000)) {
            Stop-Process -Id $nonLoopbackProcess.Id -Force
            throw 'La aplicación no rechazó la escucha no-loopback en diez segundos.'
        }

        $nonLoopbackError = Get-Content -LiteralPath $nonLoopbackErr -Raw -ErrorAction SilentlyContinue
        Assert-Condition ($nonLoopbackProcess.ExitCode -ne 0) 'La aplicación aceptó una URL no-loopback.'
        Assert-Condition ($nonLoopbackError -match 'no es de loopback') (
            'El rechazo no-loopback no produjo la causa de seguridad esperada.')
        $evidence.runtime.rejectedNonLoopback = $true
    }
    finally {
        if (-not $nonLoopbackProcess.HasExited) {
            Stop-Process -Id $nonLoopbackProcess.Id -Force
        }
        Remove-Item -LiteralPath $nonLoopbackOut, $nonLoopbackErr -Force -ErrorAction SilentlyContinue
    }

    $loopbackPort = Get-FreeLoopbackPort
    $loopbackOut = Join-Path ([System.IO.Path]::GetTempPath()) "m602-loopback-$PID-out.txt"
    $loopbackErr = Join-Path ([System.IO.Path]::GetTempPath()) "m602-loopback-$PID-err.txt"
    $loopbackProcess = Start-TestApplication "http://127.0.0.1:$loopbackPort" $loopbackOut $loopbackErr
    try {
        $response = $null
        for ($attempt = 0; $attempt -lt 40; $attempt++) {
            Start-Sleep -Milliseconds 250
            try {
                $response = Invoke-WebRequest `
                    -Uri "http://127.0.0.1:$loopbackPort/health/live" `
                    -UseBasicParsing `
                    -TimeoutSec 2
                if ($response.StatusCode -eq 200) {
                    break
                }
            }
            catch {
                if ($loopbackProcess.HasExited) {
                    break
                }
            }
        }

        Assert-Condition ($null -ne $response -and $response.StatusCode -eq 200) (
            'La aplicación no respondió correctamente en el endpoint loopback de salud.')

        $listeners = @(netstat -ano -p tcp |
            Select-String -Pattern ("\s$($loopbackProcess.Id)\s*$") |
            Where-Object { $_.Line -match 'LISTENING' } |
            ForEach-Object { $_.Line.Trim() })
        Assert-Condition ($listeners.Count -gt 0) 'No se encontró el listener TCP de la aplicación.'
        $unexpectedListeners = @($listeners | Where-Object {
            $_ -notmatch "\s127\.0\.0\.1:$loopbackPort\s"
        })
        Assert-Condition ($unexpectedListeners.Count -eq 0) (
            "Se detectó una escucha fuera del loopback esperado: $($unexpectedListeners -join '; ')")

        $csp = [string] $response.Headers['Content-Security-Policy']
        Assert-Condition ($csp -match "script-src 'self'") 'La respuesta runtime no restringe script-src a self.'
        Assert-Condition ($csp -notmatch 'unsafe-inline|unsafe-eval|\*') 'La CSP runtime contiene una fuente insegura.'
        Assert-Condition ([string] $response.Headers['X-Content-Type-Options'] -eq 'nosniff') (
            'La respuesta runtime no contiene X-Content-Type-Options: nosniff.')
        Assert-Condition ([string] $response.Headers['Referrer-Policy'] -eq 'no-referrer') (
            'La respuesta runtime no contiene Referrer-Policy: no-referrer.')

        $evidence.runtime.listeners = $listeners
        $evidence.runtime.healthStatus = [int] $response.StatusCode
        $evidence.runtime.contentSecurityPolicy = $csp
    }
    finally {
        if (-not $loopbackProcess.HasExited) {
            Stop-Process -Id $loopbackProcess.Id -Force
        }
        $loopbackProcess.WaitForExit()
        Remove-Item -LiteralPath $loopbackOut, $loopbackErr -Force -ErrorAction SilentlyContinue
    }
}

if (-not [string]::IsNullOrWhiteSpace($EvidencePath)) {
    $evidenceDirectory = Split-Path -Parent $EvidencePath
    if (-not (Test-Path -LiteralPath $evidenceDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null
    }
    $evidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $EvidencePath -Encoding utf8
    Write-Host "Evidencia escrita en $EvidencePath"
}

Write-Host 'M-602 security verification passed.' -ForegroundColor Green
Write-Host 'Static controls, dependency advisories, loopback runtime, RSS limits, CSV neutralization and CSP are conformant.'
if ($SkipOnlineAnalysis) {
    Write-Warning 'La comprobación online de dependencias se omitió explícitamente.'
}
if ($SkipRuntimeAnalysis) {
    Write-Warning 'La comprobación runtime de escucha y cabeceras se omitió explícitamente.'
}
