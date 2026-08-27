[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Assert-Absent {
    param([Parameter(Mandatory)][string]$RelativePath)

    $path = Join-Path $root $RelativePath
    if (Test-Path -LiteralPath $path) {
        throw "M-504: el artefacto innecesario sigue presente: $RelativePath"
    }
}

function Read-RepoFile {
    param([Parameter(Mandatory)][string]$RelativePath)

    Get-Content -LiteralPath (Join-Path $root $RelativePath) -Raw
}

function Assert-NotContains {
    param(
        [Parameter(Mandatory)][string]$Content,
        [Parameter(Mandatory)][string]$Pattern,
        [Parameter(Mandatory)][string]$Description
    )

    if ($Content -match $Pattern) {
        throw "M-504: $Description"
    }
}

@(
    'LaPrimitiva.App\Components\Pages\Counter.razor',
    'LaPrimitiva.App\Components\Pages\Weather.razor',
    'LaPrimitiva.App\Components\Layout\NavMenu.razor',
    'LaPrimitiva.App\Components\Layout\NavMenu.razor.css',
    'LaPrimitiva.App\wwwroot\lib\bootstrap',
    'LaPrimitiva.Application\Services\DrawGenerationService.cs',
    'LaPrimitiva.Tests\UnitTest1.cs',
    'build_output.txt'
) | ForEach-Object { Assert-Absent $_ }

$program = Read-RepoFile 'LaPrimitiva.App\Program.cs'
$plans = Read-RepoFile 'LaPrimitiva.App\Components\Pages\Plans.razor'
$gitIgnore = Read-RepoFile '.gitignore'

Assert-NotContains $program 'AddScoped<DrawGenerationService>' 'Program.cs aún registra DrawGenerationService, que no tiene consumidor.'
Assert-NotContains $plans '@inject\s+DrawGenerationService\b' 'Plans.razor aún inyecta DrawGenerationService sin utilizarlo.'

if ($gitIgnore -notmatch '(?im)^\[Pp\]ublish/$') {
    throw 'M-504: .gitignore no excluye publish/.'
}

if ($gitIgnore -notmatch '(?im)^build_output\.txt$') {
    throw 'M-504: .gitignore no evita volver a versionar build_output.txt.'
}

Write-Host 'M-504 verificado: plantillas, servicio muerto y artefactos generados eliminados; exclusiones protegidas.' -ForegroundColor Green
