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

$program = Get-Source 'LaPrimitiva.App\Program.cs'
$policy = Get-Source 'LaPrimitiva.App\Security\LocalOnlyPolicy.cs'
$middleware = Get-Source 'LaPrimitiva.App\Security\LocalOnlyMiddleware.cs'
$settings = Get-Source 'LaPrimitiva.App\appsettings.json'
$tests = Get-Source 'LaPrimitiva.Tests\LocalOnlySecurityTests.cs'
$readme = Get-Source 'README.md'

Assert-Contains $program @(
    'LocalOnlyPolicy.ValidateStartupConfiguration(builder.Configuration);',
    'app.UseMiddleware<LocalOnlyMiddleware>();'
) 'Program.cs'

if ($program.IndexOf('LocalOnlyPolicy.ValidateStartupConfiguration(builder.Configuration);') -gt
    $program.IndexOf('var app = builder.Build();')) {
    throw 'La validación local se ejecuta después de construir el servidor.'
}

Assert-Contains $policy @(
    'public static void ValidateStartupConfiguration(IConfiguration configuration)',
    'IPAddress.IsLoopback',
    'http_ports',
    'https_ports',
    'Kestrel:Endpoints'
) 'LocalOnlyPolicy'

Assert-Contains $middleware @(
    'context.Connection.RemoteIpAddress',
    'StatusCodes.Status403Forbidden'
) 'LocalOnlyMiddleware'

$allowedHosts = (($settings | ConvertFrom-Json).AllowedHosts -split ';' | Sort-Object)
$expectedHosts = @('[::1]', '127.0.0.1', 'laprimitiva.local', 'localhost') | Sort-Object

if (Compare-Object $allowedHosts $expectedHosts) {
    throw "AllowedHosts debe contener exclusivamente: $($expectedHosts -join ';')."
}

Assert-Contains $tests @(
    'ValidateStartupConfiguration_ShouldRejectNonLoopbackUrls',
    'ValidateStartupConfiguration_ShouldRejectWildcardPortConfiguration',
    'ValidateStartupConfiguration_ShouldRejectNonLoopbackKestrelEndpoint',
    'LocalOnlyMiddleware_ShouldRejectNonLoopbackClients',
    'LocalOnlyMiddleware_ShouldAllowLoopbackClients'
) 'LocalOnlySecurityTests'

Assert-Contains $readme @(
    'http://laprimitiva.local/',
    '127.0.0.1',
    'Todos sin asignar'
) 'documentacion local de IIS'

Write-Host 'M-301 verificado estaticamente: arranque, clientes, hosts, pruebas y documentacion quedan restringidos al equipo local.'
