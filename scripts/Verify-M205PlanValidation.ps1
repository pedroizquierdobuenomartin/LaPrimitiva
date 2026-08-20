[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Assert-Contains {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [string[]] $Expected
    )

    $fullPath = Join-Path $root $Path
    $content = Get-Content -Raw -LiteralPath $fullPath
    foreach ($fragment in $Expected) {
        if (-not $content.Contains($fragment)) {
            throw "Falta '$fragment' en $Path."
        }
    }
}

Assert-Contains 'LaPrimitiva.Domain\Entities\Plan.cs' @(
    'public const int MinBetsPerDraw = 1;',
    'public const int MaxBetsPerDraw = 100;',
    'public void Validate()',
    'EffectiveFrom > EffectiveTo.Value',
    'WeeksToTrackDefault < 0',
    'CostPerBet < 0',
    'JokerCostPerBet < 0',
    '!EnableJoker && JokerCostPerBet != 0'
)

Assert-Contains 'LaPrimitiva.Domain\Entities\DrawRecord.cs' @(
    'Plan.CostPerBet * (Plan.BetsPerDraw - 1)',
    'Plan.JokerCostPerBet * (Plan.BetsPerDraw - 1)',
    'Plan.Validate();'
)

Assert-Contains 'LaPrimitiva.Application\Services\PlanService.cs' @(
    'public async Task CreatePlanAsync(Plan plan)',
    'public async Task UpdatePlanAsync(Plan plan)',
    'plan.Validate();'
)

Assert-Contains 'LaPrimitiva.Infrastructure\Repositories\PlanRepository.cs' @(
    'plan.Validate();',
    'await EnsureNoOverlapAsync(plan);',
    'private async Task EnsureNoOverlapAsync(Plan plan)'
)

Assert-Contains 'LaPrimitiva.Infrastructure\PrimitivaDbContext.cs' @(
    'table.HasTrigger("TR_Plans_PreventOverlap");',
    'table.UseSqlOutputClause(false);'
)

Assert-Contains 'LaPrimitiva.App\Components\Pages\Plans.razor' @(
    'min="@Plan.MinBetsPerDraw"',
    'max="@Plan.MaxBetsPerDraw"',
    'newPlan.Validate();'
)

$sqlMarkers = @(
    'CK_Plans_EffectivePeriod',
    'CK_Plans_Name',
    'CK_Plans_NonNegativeValues',
    'CK_Plans_BetsPerDraw',
    'CK_Plans_DisabledJokerCost',
    'TR_Plans_PreventOverlap'
)
Assert-Contains 'LaPrimitiva.Infrastructure\Persistence\Seed\WinningDrawSeeder.cs' $sqlMarkers
Assert-Contains 'LaPrimitiva.Infrastructure\Migrations\20260820160000_ValidatePlans.cs' $sqlMarkers

Assert-Contains 'LaPrimitiva.Tests\PlanTests.cs' @(
    'Validate_ShouldRejectInvalidBusinessRules',
    'Validate_ShouldAcceptBoundaryValues'
)
Assert-Contains 'LaPrimitiva.Tests\DrawRecordTests.cs' @(
    'RecalculateFinancials_AppliesBetsPerDrawToBaseAndJokerCosts',
    'RecalculateFinancials_WithOneBet_LeavesAutomaticComponentsAtZero'
)
Assert-Contains 'LaPrimitiva.Tests\Integration\PlanIntegrationTests.cs' @(
    'Repository_ShouldRejectInvalidPlan_WhenApplicationServiceIsBypassed',
    'SqlConstraint_ShouldRejectInvalidPlan_WhenEveryServiceIsBypassed',
    'SqlTrigger_ShouldRejectOverlappingPeriods_WhenEveryServiceIsBypassed',
    'UpdatePlan_ShouldSucceed_WhenSqlOverlapTriggerIsEnabled'
)

Write-Host 'M-205 verificado estáticamente: dominio, UI, aplicación, repositorio, SQL, cálculos y pruebas están alineados.'
