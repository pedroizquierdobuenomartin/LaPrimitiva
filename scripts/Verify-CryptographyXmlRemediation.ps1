[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$infrastructureProject = Join-Path $repositoryRoot 'LaPrimitiva.Infrastructure\LaPrimitiva.Infrastructure.csproj'
$bundleScript = Join-Path $repositoryRoot 'scripts\New-DatabaseMigrationBundle.ps1'

$projectContent = Get-Content -Raw -LiteralPath $infrastructureProject
if ($projectContent.Contains('Microsoft.EntityFrameworkCore.Tools')) {
    throw 'Infrastructure aún referencia Microsoft.EntityFrameworkCore.Tools, tooling exclusivo de PMC que introduce la dependencia vulnerable.'
}

[xml]$projectXml = $projectContent
$cryptographyReference = @(
    @($projectXml.Project.ItemGroup.PackageReference) |
        Where-Object { $_.Include -eq 'System.Security.Cryptography.Xml' }
)
if ($cryptographyReference.Count -ne 1 -or $cryptographyReference[0].Version -ne '9.0.19') {
    throw 'Infrastructure debe fijar una única referencia a System.Security.Cryptography.Xml 9.0.19.'
}

if ($cryptographyReference[0].PrivateAssets -ne 'all') {
    throw 'System.Security.Cryptography.Xml debe permanecer como PrivateAssets=all y no propagarse a consumidores.'
}

$assetsFiles = Get-ChildItem -Path $repositoryRoot -Recurse -File -Filter 'project.assets.json' |
    Where-Object { $_.FullName -match '\\obj\\' }
foreach ($assetsFile in $assetsFiles) {
    $assetsContent = Get-Content -Raw -LiteralPath $assetsFile.FullName
    $resolvedVersions = [regex]::Matches(
        $assetsContent,
        '"System\.Security\.Cryptography\.Xml/([^"]+)"') |
        ForEach-Object { $_.Groups[1].Value } |
        Sort-Object -Unique

    $unexpectedVersions = @($resolvedVersions | Where-Object { $_ -ne '9.0.19' })
    if ($unexpectedVersions.Count -gt 0) {
        throw "'$($assetsFile.FullName)' resuelve versiones no autorizadas de Cryptography.Xml: $($unexpectedVersions -join ', ')."
    }
}

$sourceFiles = Get-ChildItem -Path $repositoryRoot -Recurse -File -Include '*.cs', '*.razor' |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|publish|artifacts)\\' }
$cryptographyUsage = $sourceFiles | Select-String -Pattern 'System\.Security\.Cryptography\.Xml|SignedXml|EncryptedXml|XmlDsig|KeyInfo'
if ($cryptographyUsage) {
    throw "Se encontró uso funcional de criptografía XML: $($cryptographyUsage.Path -join ', ')."
}

$bundleContent = Get-Content -Raw -LiteralPath $bundleScript
foreach ($expected in @(
    "`$ErrorActionPreference = 'Continue'",
    '$listExitCode = $LASTEXITCODE',
    'if ($listExitCode -ne 0)'
)) {
    if (-not $bundleContent.Contains($expected)) {
        throw "El wrapper de dotnet-ef no controla correctamente stderr y exit code. Falta: $expected"
    }
}

Write-Host 'Verificación estática de la remediación Cryptography.Xml: correcta.'
