[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Read-ProjectFile {
    param([Parameter(Mandatory)][string]$RelativePath)

    $path = Join-Path $repositoryRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "No existe el archivo requerido: $RelativePath"
    }

    return Get-Content -LiteralPath $path -Raw
}

function Assert-Match {
    param(
        [Parameter(Mandatory)][string]$Content,
        [Parameter(Mandatory)][string]$Pattern,
        [Parameter(Mandatory)][string]$Message
    )

    if ($Content -notmatch $Pattern) {
        throw $Message
    }
}

function Assert-NotMatch {
    param(
        [Parameter(Mandatory)][string]$Content,
        [Parameter(Mandatory)][string]$Pattern,
        [Parameter(Mandatory)][string]$Message
    )

    if ($Content -match $Pattern) {
        throw $Message
    }
}

$routes = Read-ProjectFile 'LaPrimitiva.App/AppRoutes.cs'
$plans = Read-ProjectFile 'LaPrimitiva.App/Components/Pages/Plans.razor'
$register = Read-ProjectFile 'LaPrimitiva.App/Components/Pages/Register.razor'

Assert-Match $routes 'public\s+const\s+string\s+Registration\s*=\s*"/registro"\s*;' `
    'La ruta de Registro no está definida como constante compartida.'
Assert-Match $register '@attribute\s+\[Microsoft\.AspNetCore\.Components\.RouteAttribute\(AppRoutes\.Registration\)\]' `
    'La página Registro no obtiene su plantilla desde la constante compartida.'
Assert-Match $plans 'Nav\.NavigateTo\(AppRoutes\.Registration\)' `
    'Planes no navega mediante la constante compartida de Registro.'
Assert-NotMatch $plans 'Nav\.NavigateTo\("/register"\)' `
    'Planes conserva la ruta inglesa incorrecta /register.'
Assert-NotMatch $register '@page\s+"/registro"' `
    'Register duplica la ruta literal en vez de consumir la constante compartida.'

Write-Host 'M-202 verificado: Planes y Registro comparten la ruta /registro sin literales duplicados.' -ForegroundColor Green
