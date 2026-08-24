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

$repository = Get-Source 'LaPrimitiva.Infrastructure\Repositories\PlanRepository.cs'
$integrationTest = Get-Source 'LaPrimitiva.Tests\Integration\PlanIntegrationTests.cs'

Assert-Contains $repository @(
    'var existing = await _context.Plans.SingleOrDefaultAsync',
    'existing.Name = plan.Name;',
    'existing.FixedCombinationLabel = plan.FixedCombinationLabel;',
    'existing.UpdatedAt = DateTime.UtcNow;'
) 'PlanRepository.UpdateAsync'

if ($repository.Contains('_context.Entry(plan).State = EntityState.Modified;')) {
    throw 'PlanRepository.UpdateAsync vuelve a marcar la entidad desconectada completa como modificada.'
}

if ($repository.Contains('existing.CreatedAt = plan.CreatedAt;')) {
    throw 'PlanRepository.UpdateAsync permite sobrescribir CreatedAt.'
}

Assert-Contains $integrationTest @(
    'UpdatePlan_ShouldPreserveCreatedAt_AndRefreshUpdatedAt',
    'Assert.Equal(originalCreatedAt, persisted.CreatedAt);',
    'Assert.True(persisted.UpdatedAt > originalUpdatedAt);'
) 'PlanIntegrationTests'

Write-Host 'M-206 verificado estaticamente: la actualizacion conserva CreatedAt, refresca UpdatedAt y dispone de regresion explicita.'
