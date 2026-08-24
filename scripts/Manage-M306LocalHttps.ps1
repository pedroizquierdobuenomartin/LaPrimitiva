[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Create', 'Install', 'Verify', 'Remove')]
    [string] $Action,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $SiteName,

    [ValidateNotNullOrEmpty()]
    [string] $HostName = 'laprimitiva.local',

    [string] $OutputDirectory,

    [string] $PfxPath,

    [string] $RootCertificatePath,

    [SecureString] $PfxPassword,

    [switch] $RemoveRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $repositoryRoot = Split-Path -Parent $PSScriptRoot
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts\local-https'
}

$rootSubject = 'CN=LaPrimitiva Local Development Root CA'
$leafSubject = "CN=$HostName"
$httpsBindingInformation = "127.0.0.1:443:$HostName"

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)

    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Ejecuta Windows PowerShell como administrador para modificar IIS y los almacenes LocalMachine.'
    }
}

function Import-IisModule {
    if ($PSVersionTable.PSEdition -ne 'Desktop') {
        throw 'Ejecuta este script con Windows PowerShell 5.1 (powershell.exe), no con PowerShell 7.'
    }

    Import-Module WebAdministration
    if (-not (Test-Path "IIS:\Sites\$SiteName")) {
        throw "No existe el sitio IIS '$SiteName'. Usa -SiteName con el nombre exacto mostrado por Get-Website."
    }
}

function Get-SecretPassword {
    if ($null -ne $PfxPassword) {
        return $PfxPassword
    }

    return Read-Host 'Contraseña del PFX (no se guarda en disco ni en el repositorio)' -AsSecureString
}

function Get-LeafCertificateFromPfx {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [SecureString] $Password
    )

    $imported = Import-PfxCertificate -FilePath $Path -Password $Password -CertStoreLocation 'Cert:\LocalMachine\My'
    $leaf = @($imported | Where-Object HasPrivateKey | Select-Object -First 1)[0]
    if ($null -eq $leaf) {
        throw "El PFX '$Path' no contiene un certificado con clave privada."
    }

    return $leaf
}

function Assert-LeafCertificate {
    param([Parameter(Mandatory)] [System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate)

    if (-not $Certificate.HasPrivateKey) {
        throw 'El certificado HTTPS no contiene una clave privada.'
    }

    if ($Certificate.NotBefore -gt (Get-Date) -or $Certificate.NotAfter -le (Get-Date)) {
        throw 'El certificado HTTPS todavía no es válido o ha caducado.'
    }

    $san = @($Certificate.DnsNameList | ForEach-Object Unicode)
    if ($san -notcontains $HostName) {
        throw "El certificado no contiene '$HostName' en el SAN."
    }

    $serverAuthenticationOid = '1.3.6.1.5.5.7.3.1'
    $ekuExtension = $Certificate.Extensions |
        Where-Object { $_.Oid.Value -eq '2.5.29.37' } |
        Select-Object -First 1
    $ekuOids = if ($null -eq $ekuExtension) {
        @()
    }
    else {
        @($ekuExtension.EnhancedKeyUsages | ForEach-Object { $_.Value })
    }

    if ($ekuOids -notcontains $serverAuthenticationOid) {
        throw 'El certificado no permite autenticación de servidor TLS.'
    }
}

function Assert-TrustedChain {
    param([Parameter(Mandatory)] [System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate)

    $chain = [Security.Cryptography.X509Certificates.X509Chain]::new()
    $chain.ChainPolicy.RevocationMode = [Security.Cryptography.X509Certificates.X509RevocationMode]::NoCheck
    if (-not $chain.Build($Certificate)) {
        $errors = $chain.ChainStatus.StatusInformation.Trim() -join '; '
        throw "La cadena del certificado HTTPS no es de confianza: $errors"
    }
}

function Set-HttpsBinding {
    param([Parameter(Mandatory)] [System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate)

    $existing = @(Get-WebBinding -Name $SiteName -Protocol 'https' |
        Where-Object bindingInformation -eq $httpsBindingInformation)

    if ($existing.Count -eq 0) {
        New-WebBinding -Name $SiteName -Protocol 'https' -IPAddress '127.0.0.1' -Port 443 -HostHeader $HostName -SslFlags 1
    }

    $binding = Get-WebBinding -Name $SiteName -Protocol 'https' |
        Where-Object bindingInformation -eq $httpsBindingInformation |
        Select-Object -First 1
    $binding.AddSslCertificate($Certificate.Thumbprint, 'My')

    Get-WebBinding -Name $SiteName -Protocol 'http' |
        Where-Object bindingInformation -match ":80:$([regex]::Escape($HostName))$" |
        ForEach-Object {
            Remove-WebBinding -Name $SiteName -Protocol 'http' -BindingInformation $_.bindingInformation
        }
}

function New-CertificateBundle {
    $password = Get-SecretPassword
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

    $root = Get-ChildItem 'Cert:\LocalMachine\My' |
        Where-Object {
            $_.Subject -eq $rootSubject -and
            $_.HasPrivateKey -and
            $_.NotBefore -le (Get-Date) -and
            $_.NotAfter -gt (Get-Date).AddMonths(14)
        } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1

    if ($null -eq $root) {
        $root = New-SelfSignedCertificate `
            -Type Custom `
            -Subject $rootSubject `
            -CertStoreLocation 'Cert:\LocalMachine\My' `
            -HashAlgorithm 'SHA256' `
            -KeyAlgorithm 'RSA' `
            -KeyLength 3072 `
            -KeyExportPolicy NonExportable `
            -KeyUsage CertSign, CRLSign, DigitalSignature `
            -TextExtension @('2.5.29.19={critical}{text}ca=1&pathlength=0') `
            -NotAfter (Get-Date).AddYears(5)
    }

    $rootCerPath = Join-Path $OutputDirectory 'LaPrimitiva-Local-Root-CA.cer'
    Export-Certificate -Cert $root -FilePath $rootCerPath -Force | Out-Null
    Import-Certificate -FilePath $rootCerPath -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null

    $leaf = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $leafSubject `
        -DnsName $HostName `
        -Signer $root `
        -CertStoreLocation 'Cert:\LocalMachine\My' `
        -HashAlgorithm 'SHA256' `
        -KeyAlgorithm 'RSA' `
        -KeyLength 2048 `
        -KeyExportPolicy Exportable `
        -KeyUsage DigitalSignature, KeyEncipherment `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.1') `
        -NotAfter (Get-Date).AddMonths(13)

    $leafCerPath = Join-Path $OutputDirectory "$HostName.cer"
    $leafPfxPath = Join-Path $OutputDirectory "$HostName.pfx"
    Export-Certificate -Cert $leaf -FilePath $leafCerPath -Force | Out-Null
    Export-PfxCertificate -Cert $leaf -FilePath $leafPfxPath -Password $password -CryptoAlgorithmOption AES256_SHA256 -Force | Out-Null

    Assert-LeafCertificate $leaf
    Assert-TrustedChain $leaf
    Set-HttpsBinding $leaf

    Get-ChildItem 'Cert:\LocalMachine\My' |
        Where-Object { $_.Subject -eq $leafSubject -and $_.Thumbprint -ne $leaf.Thumbprint } |
        Remove-Item -Force

    Write-Host "HTTPS configurado para $httpsBindingInformation."
    Write-Host "CER de la CA pública (sin clave privada): $rootCerPath"
    Write-Host "CER público del servidor (sin clave privada): $leafCerPath"
    Write-Host "PFX del servidor (certificado público + clave privada, SECRETO): $leafPfxPath"
    Write-Host 'Este equipo ya tiene los certificados instalados. No vuelvas a importarlos con el asistente de Windows.'
    Write-Warning 'Transfiere la PFX por un canal seguro y comunica su contraseña por otro canal. Nunca la añadas a Git.'
}

function Install-CertificateBundle {
    if ([string]::IsNullOrWhiteSpace($PfxPath) -or [string]::IsNullOrWhiteSpace($RootCertificatePath)) {
        throw 'Install requiere -PfxPath y -RootCertificatePath.'
    }

    $password = Get-SecretPassword
    Import-Certificate -FilePath $RootCertificatePath -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
    $leaf = Get-LeafCertificateFromPfx -Path $PfxPath -Password $password
    Assert-LeafCertificate $leaf
    Assert-TrustedChain $leaf
    Set-HttpsBinding $leaf
    Write-Host "Certificado importado y HTTPS configurado para $httpsBindingInformation."
}

function Test-LocalHttps {
    $binding = Get-WebBinding -Name $SiteName -Protocol 'https' |
        Where-Object bindingInformation -eq $httpsBindingInformation |
        Select-Object -First 1
    if ($null -eq $binding) {
        throw "No existe el binding HTTPS '$httpsBindingInformation' en '$SiteName'."
    }

    if ([int]$binding.sslFlags -ne 1) {
        throw 'El binding HTTPS no tiene SNI habilitado.'
    }

    $thumbprint = if ($binding.certificateHash -is [byte[]]) {
        ([BitConverter]::ToString($binding.certificateHash)).Replace('-', '')
    }
    else {
        ([string]$binding.certificateHash).Replace(' ', '')
    }
    $leaf = Get-Item "Cert:\LocalMachine\My\$thumbprint"
    Assert-LeafCertificate $leaf
    Assert-TrustedChain $leaf

    $httpBindings = @(Get-WebBinding -Name $SiteName -Protocol 'http' |
        Where-Object bindingInformation -match ":80:$([regex]::Escape($HostName))$")
    if ($httpBindings.Count -gt 0) {
        throw "Sigue habilitado un binding HTTP para '$HostName' en el puerto 80."
    }

    $addresses = @(Resolve-DnsName -Name $HostName -Type A | ForEach-Object IPAddress)
    if ($addresses -notcontains '127.0.0.1') {
        throw "$HostName no resuelve a 127.0.0.1."
    }

    $response = Invoke-WebRequest -Uri "https://$HostName/" -UseBasicParsing -TimeoutSec 15
    if ([int]$response.StatusCode -ne 200) {
        throw "La petición HTTPS devolvió $([int]$response.StatusCode)."
    }

    $hsts = [string]$response.Headers['Strict-Transport-Security']
    if ([string]::IsNullOrWhiteSpace($hsts)) {
        throw 'La respuesta HTTPS no contiene Strict-Transport-Security.'
    }

    Write-Host "M-306 operativo: HTTPS confiable, SAN correcto, SNI activo, loopback y HSTS verificados para $HostName."
}

function Remove-LocalHttps {
    Get-WebBinding -Name $SiteName -Protocol 'https' |
        Where-Object bindingInformation -eq $httpsBindingInformation |
        ForEach-Object { Remove-WebBinding -Name $SiteName -Protocol 'https' -BindingInformation $httpsBindingInformation }

    Get-ChildItem 'Cert:\LocalMachine\My' |
        Where-Object Subject -eq $leafSubject |
        Remove-Item -Force

    if ($RemoveRoot) {
        Get-ChildItem 'Cert:\LocalMachine\Root', 'Cert:\LocalMachine\My' |
            Where-Object Subject -eq $rootSubject |
            Remove-Item -Force
    }

    Write-Host 'Binding HTTPS y certificados de servidor retirados. Usa -RemoveRoot para retirar también la CA local.'
}

Assert-Administrator
Import-IisModule

if (-not $PSCmdlet.ShouldProcess("IIS '$SiteName' y almacenes LocalMachine", "$Action HTTPS local para $HostName")) {
    return
}

switch ($Action) {
    'Create' { New-CertificateBundle }
    'Install' { Install-CertificateBundle }
    'Verify' { Test-LocalHttps }
    'Remove' { Remove-LocalHttps }
}
