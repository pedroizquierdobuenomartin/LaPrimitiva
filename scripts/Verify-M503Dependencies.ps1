[CmdletBinding()]
param(
    [switch]$SkipOnlineAnalysis
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'LaPrimitiva.sln'
$failures = [System.Collections.Generic.List[string]]::new()

function Get-PackageReferences {
    param([string]$ProjectPath)

    [xml]$projectXml = Get-Content -Raw -LiteralPath $ProjectPath
    foreach ($reference in @($projectXml.Project.ItemGroup.PackageReference)) {
        if ($null -eq $reference) {
            continue
        }

        [pscustomobject]@{
            Project = [System.IO.Path]::GetFileName($ProjectPath)
            Id = [string]$reference.Include
            Version = [string]$reference.Version
            PrivateAssets = [string]$reference.PrivateAssets
        }
    }
}

$projects = Get-ChildItem -Path $repositoryRoot -Recurse -File -Filter '*.csproj' |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|publish|artifacts)\\' } |
    Sort-Object FullName

foreach ($project in $projects) {
    [xml]$projectXml = Get-Content -Raw -LiteralPath $project.FullName
    if ([string]$projectXml.Project.PropertyGroup.TargetFramework -ne 'net10.0') {
        $failures.Add("$($project.Name) debe usar TargetFramework net10.0.")
    }
}

$references = @($projects | ForEach-Object { Get-PackageReferences -ProjectPath $_.FullName })
$expectedVersions = @{
    'Microsoft.AspNetCore.Mvc.Testing' = '10.0.11'
    'Microsoft.EntityFrameworkCore.Design' = '10.0.11'
    'Microsoft.EntityFrameworkCore.InMemory' = '10.0.11'
    'Microsoft.EntityFrameworkCore.SqlServer' = '10.0.11'
    'Microsoft.JSInterop' = '10.0.11'
    'System.Security.Cryptography.Xml' = '10.0.11'
    'xunit.v3.mtp-v2' = '4.0.0'
}

foreach ($reference in $references) {
    if ($reference.Id -eq 'xunit') {
        $failures.Add("$($reference.Project) aún usa el paquete obsoleto xunit; debe usar xunit.v3.")
    }

    if ($expectedVersions.ContainsKey($reference.Id) -and
        $reference.Version -ne $expectedVersions[$reference.Id]) {
        $failures.Add("$($reference.Project): $($reference.Id) debe usar $($expectedVersions[$reference.Id]), no $($reference.Version).")
    }
}

foreach ($vstestPackage in @(
    'Microsoft.NET.Test.Sdk',
    'xunit.runner.visualstudio',
    'coverlet.collector'
)) {
    if ($references.Id -contains $vstestPackage) {
        $failures.Add("$vstestPackage pertenece a la ruta VSTest y no debe coexistir con el runner MTP exclusivo de .NET 10.")
    }
}

foreach ($packageId in $expectedVersions.Keys) {
    if (-not ($references.Id -contains $packageId)) {
        $failures.Add("Falta la referencia esperada a $packageId.")
    }
}

$cryptographyReference = @($references | Where-Object { $_.Id -eq 'System.Security.Cryptography.Xml' })
if ($cryptographyReference.Count -ne 1 -or $cryptographyReference[0].PrivateAssets -ne 'all') {
    $failures.Add('System.Security.Cryptography.Xml debe existir una sola vez y conservar PrivateAssets=all.')
}

foreach ($relativePath in @(
    'LaPrimitiva.Tests\Integration\IntegrationTestBase.cs',
    'LaPrimitiva.Tests\Integration\IntegrationTestFixture.cs'
)) {
    $lifetimePath = Join-Path $repositoryRoot $relativePath
    $lifetimeSource = Get-Content -Raw -LiteralPath $lifetimePath
    foreach ($method in @('InitializeAsync', 'DisposeAsync')) {
        if ($lifetimeSource -notmatch "public\s+(?:async\s+)?ValueTask\s+$method\s*\(") {
            $failures.Add("$relativePath debe implementar $method con ValueTask para xUnit v3.")
        }
    }
}

$testProjectPath = Join-Path $repositoryRoot 'LaPrimitiva.Tests\LaPrimitiva.Tests.csproj'
$testProjectContent = Get-Content -Raw -LiteralPath $testProjectPath
if ($testProjectContent -notmatch '<OutputType>Exe</OutputType>') {
    $failures.Add('LaPrimitiva.Tests debe ser un ejecutable autocontenido para xUnit v3 y Microsoft.Testing.Platform.')
}
if ($testProjectContent -notmatch '<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>') {
    $failures.Add('LaPrimitiva.Tests debe habilitar la integración de Microsoft.Testing.Platform con dotnet test.')
}
if ($testProjectContent -notmatch '<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>') {
    $failures.Add('LaPrimitiva.Tests debe usar el runner MTP de xUnit v3 para que dotnet test de .NET 10 descubra los casos.')
}
if ($testProjectContent -notmatch '<WarningsAsErrors>[^<]*xUnit1051[^<]*</WarningsAsErrors>') {
    $failures.Add('LaPrimitiva.Tests debe tratar xUnit1051 como error para conservar la cancelación cooperativa.')
}
if ($testProjectContent -match '<NoWarn>[^<]*xUnit1051[^<]*</NoWarn>') {
    $failures.Add('xUnit1051 no debe silenciarse mediante NoWarn.')
}

$globalJsonPath = Join-Path $repositoryRoot 'global.json'
if (-not (Test-Path -LiteralPath $globalJsonPath -PathType Leaf)) {
    $failures.Add('Falta global.json para seleccionar Microsoft.Testing.Platform con el SDK .NET 10.')
}
else {
    try {
        $globalJson = Get-Content -Raw -LiteralPath $globalJsonPath | ConvertFrom-Json
        if ($globalJson.test.runner -ne 'Microsoft.Testing.Platform') {
            $failures.Add('global.json debe configurar test.runner como Microsoft.Testing.Platform.')
        }
    }
    catch {
        $failures.Add("global.json no contiene JSON válido: $($_.Exception.Message)")
    }
}

$workflowPath = Join-Path $repositoryRoot '.github\workflows\dependency-audit.yml'
if (-not (Test-Path -LiteralPath $workflowPath -PathType Leaf)) {
    $failures.Add('Falta la automatización periódica .github/workflows/dependency-audit.yml.')
}
else {
    $workflow = Get-Content -Raw -LiteralPath $workflowPath
    foreach ($requiredPattern in @(
        'schedule:',
        'cron:',
        'dotnet restore LaPrimitiva.sln',
        './scripts/Verify-M503Dependencies.ps1'
    )) {
        if (-not $workflow.Contains($requiredPattern)) {
            $failures.Add("El workflow de dependencias no contiene: $requiredPattern")
        }
    }
}

if (-not $SkipOnlineAnalysis) {
    foreach ($analysis in @('outdated', 'vulnerable', 'deprecated')) {
        $output = @(& dotnet package list --project $solutionPath "--$analysis" --include-transitive --no-restore --format json 2>&1)
        $exitCode = $LASTEXITCODE
        $rawOutput = $output -join [Environment]::NewLine

        if ($exitCode -ne 0) {
            $failures.Add("El análisis '$analysis' terminó con código $exitCode`: $rawOutput")
            continue
        }

        $jsonStart = $rawOutput.IndexOf('{')
        if ($jsonStart -lt 0) {
            $failures.Add("El análisis '$analysis' no devolvió JSON reconocible.")
            continue
        }

        try {
            $report = $rawOutput.Substring($jsonStart) | ConvertFrom-Json
        }
        catch {
            $failures.Add("No se pudo interpretar el JSON del análisis '$analysis': $($_.Exception.Message)")
            continue
        }

        $frameworks = @($report.projects | ForEach-Object { @($_.frameworks) })
        $topLevel = @($frameworks | ForEach-Object { @($_.topLevelPackages) }) |
            Where-Object { $null -ne $_ }
        $transitive = @($frameworks | ForEach-Object { @($_.transitivePackages) }) |
            Where-Object { $null -ne $_ }

        switch ($analysis) {
            'outdated' {
                foreach ($package in $topLevel) {
                    $failures.Add("Paquete directo obsoleto: $($package.id) $($package.resolvedVersion) -> $($package.latestVersion).")
                }

                Write-Host "Dependencias transitivas con versión posterior disponible: $($transitive.Count) (informativo; las gobiernan sus paquetes directos)."
            }
            'vulnerable' {
                foreach ($package in @($topLevel) + @($transitive)) {
                    $failures.Add("Paquete vulnerable detectado: $($package.id) $($package.resolvedVersion).")
                }
            }
            'deprecated' {
                foreach ($package in @($topLevel) + @($transitive)) {
                    $failures.Add("Paquete en desuso detectado: $($package.id) $($package.resolvedVersion).")
                }
            }
        }
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'M-503 dependency verification passed.'
Write-Host 'All projects target .NET 10 and direct packages comply with the reviewed dependency policy.'
if ($SkipOnlineAnalysis) {
    Write-Host 'Online outdated, vulnerable and deprecated package analysis was skipped explicitly.'
}
