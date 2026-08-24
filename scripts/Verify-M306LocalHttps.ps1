[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Get-Source {
    param([Parameter(Mandatory)] [string] $Path)

    return Get-Content -Raw -LiteralPath (Join-Path $root $Path)
}

function Assert-Contains {
    param(
        [Parameter(Mandatory)] [string] $Content,
        [Parameter(Mandatory)] [string[]] $Expected,
        [Parameter(Mandatory)] [string] $Description
    )

    foreach ($fragment in $Expected) {
        if (-not $Content.Contains($fragment)) {
            throw "Falta '$fragment' en $Description."
        }
    }
}

$manager = Get-Source 'scripts\Manage-M306LocalHttps.ps1'
$readme = Get-Source 'README.md'
$guide = Get-Source 'mejoras\GUIA_M306_HTTPS_IIS.md'
$gitignore = Get-Source '.gitignore'
$program = Get-Source 'LaPrimitiva.App\Program.cs'

Assert-Contains $manager @(
    "[ValidateSet('Create', 'Install', 'Verify', 'Remove')]",
    "[string] `$HostName = 'laprimitiva.local'",
    '[string] $OutputDirectory,',
    'if ([string]::IsNullOrWhiteSpace($OutputDirectory))',
    '$repositoryRoot = Split-Path -Parent $PSScriptRoot',
    '$OutputDirectory = Join-Path $repositoryRoot ''artifacts\local-https''',
    '127.0.0.1:443:',
    '-DnsName $HostName',
    "-CertStoreLocation 'Cert:\LocalMachine\Root'",
    '-KeyExportPolicy NonExportable',
    '-KeyExportPolicy Exportable',
    'Export-PfxCertificate',
    'AES256_SHA256',
    'CER de la CA pública (sin clave privada)',
    'CER público del servidor (sin clave privada)',
    'PFX del servidor (certificado público + clave privada, SECRETO)',
    'No vuelvas a importarlos con el asistente de Windows',
    "Where-Object { `$_.Oid.Value -eq '2.5.29.37' }",
    '@($ekuExtension.EnhancedKeyUsages | ForEach-Object { $_.Value })',
    '-SslFlags 1',
    'Remove-WebBinding',
    'Assert-TrustedChain',
    "response.Headers['Strict-Transport-Security']"
) 'gestor HTTPS local'

Assert-Contains $readme @(
    'https://laprimitiva.local/',
    'mejoras/GUIA_M306_HTTPS_IIS.md',
    'artifacts\local-https',
    'LaPrimitiva-Local-Root-CA.cer',
    'laprimitiva.local.pfx',
    "-Action Create",
    "-Action Install",
    "-Action Verify",
    "-Action Remove",
    'Windows PowerShell 5.1',
    '127.0.0.1:443:laprimitiva.local',
    'No se versiona'
) 'documentación HTTPS local'

Assert-Contains $guide @(
    'Windows PowerShell 5.1 como administrador',
    '127.0.0.1 laprimitiva.local',
    "-Action Create",
    "-Action Install",
    "-Action Verify",
    "-Action Remove",
    'LaPrimitiva-Local-Root-CA.cer',
    'laprimitiva.local.pfx',
    'La PFX no es «el certificado privado»',
    'En el primer equipo: no importes nada manualmente',
    'Equipo local',
    'Entidades de certificación raíz de confianza',
    'Personal',
    'Get-WebBinding',
    'Resolve-DnsName laprimitiva.local',
    'ASPNETCORE_ENVIRONMENT=Development',
    'Firefox: no aceptes una excepción permanente como validación',
    'security.enterprise_roots.enabled',
    'LaPrimitiva-Local-Root-CA.cer',
    'No importes la PFX ni `laprimitiva.local.cer` en Firefox',
    'Una excepción manual no sustituye la confianza de cadena',
    'support.mozilla.org/en-US/kb/automatically-trust-third-party-certificates',
    'Evidencia para cerrar M-306'
) 'guía paso a paso de HTTPS en IIS'

Assert-Contains $gitignore @('*.pfx', '*.p12', '*.key', '[Aa]rtifacts/') '.gitignore'
Assert-Contains $program @('app.UseHsts();', 'app.UseHttpsRedirection();') 'Program.cs'

$sensitiveTrackedFiles = @(git -C $root ls-files -- '*.pfx' '*.p12' '*.key')
if ($sensitiveTrackedFiles.Count -gt 0) {
    throw "Hay material privado versionado: $($sensitiveTrackedFiles -join ', ')."
}

Write-Host 'M-306 verificado estaticamente: ciclo de certificado, exportacion segura, binding IIS, retirada y documentacion reproducible.'
