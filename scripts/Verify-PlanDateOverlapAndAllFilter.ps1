[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Get-Source {
    param([Parameter(Mandatory)] [string] $Path)

    Get-Content -Raw -LiteralPath (Join-Path $root $Path)
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

$service = Get-Source 'LaPrimitiva.Application\Services\PlanService.cs'
$repository = Get-Source 'LaPrimitiva.Infrastructure\Repositories\PlanRepository.cs'
$migration = Get-Source 'LaPrimitiva.Infrastructure\Migrations\20260820160000_ValidatePlans.cs'
$tests = Get-Source 'LaPrimitiva.Tests\Integration\PlanIntegrationTests.cs'
$globalStateTests = Get-Source 'LaPrimitiva.Tests\GlobalStateTests.cs'
$globalState = Get-Source 'LaPrimitiva.Application\Services\GlobalState.cs'
$layout = Get-Source 'LaPrimitiva.App\Components\Layout\MainLayout.razor'
$plansPage = Get-Source 'LaPrimitiva.App\Components\Pages\Plans.razor'
$dashboard = Get-Source 'LaPrimitiva.App\Components\Pages\Home.razor'
$register = Get-Source 'LaPrimitiva.App\Components\Pages\Register.razor'

Assert-Contains $service @(
    'p.EffectiveFrom <= plan.EffectiveTo',
    'p.EffectiveTo >= plan.EffectiveFrom',
    'p.Id != plan.Id'
) 'PlanService'

Assert-Contains $repository @(
    'existing.EffectiveFrom <= plan.EffectiveTo',
    'existing.EffectiveTo >= plan.EffectiveFrom'
) 'PlanRepository'

Assert-Contains $migration @(
    'existing.[EffectiveFrom] <= candidate.[EffectiveTo]',
    'existing.[EffectiveTo] >= candidate.[EffectiveFrom]'
) 'trigger SQL de planes'

Assert-Contains $tests @(
    'CreatePlan_ShouldFail_WhenStartDateMatchesExistingEndDate',
    'UpdatePlan_ShouldFail_WhenEndDateMatchesAnotherStartDate',
    'Assert.Equal(1, await context.Plans.CountAsync());',
    'Assert.Equal(new DateTime(2033, 12, 31), persisted.EffectiveTo);'
) 'PlanIntegrationTests'

Assert-Contains $globalState @(
    'public bool ShowAllPlans',
    '_showAllPlans = false;'
) 'GlobalState'

Assert-Contains $globalStateTests @(
    'ShowAllPlans_ShouldPreserveSelectedYear',
    'SelectingYear_ShouldExitShowAllPlansMode_AndNotifyOnce',
    'Assert.False(state.ShowAllPlans);'
) 'GlobalStateTests'

Assert-Contains $layout @(
    '@if (_isPlansPage)',
    '<option value="all">@L["All"]</option>',
    'GlobalState.ShowAllPlans = true;',
    'GlobalState.SelectedYear = year;'
) 'selector anual de MainLayout'

Assert-Contains $plansPage @(
    'GlobalState.ShowAllPlans',
    '@PlanPageDescription',
    'private string PlanPageDescription',
    'await PlanService.GetPlansAsync()',
    'await PlanService.GetPlansByYearAsync(GlobalState.SelectedYear)'
) 'pagina de planes'

if ($plansPage.Contains('@L["ConfigurePlanRules"] <span')) {
    throw 'Plans.razor conserva la expresion Razor ambigua que produjo CS1513.'
}

if ($dashboard.Contains('ShowAllPlans') -or $register.Contains('ShowAllPlans')) {
    throw 'El filtro exclusivo de todos los planes no debe alterar Dashboard ni Registro.'
}

Write-Host 'Filtro de planes y solapamientos verificados estaticamente: limites inclusivos en todas las capas y opcion Todos aislada de las demas paginas.'
