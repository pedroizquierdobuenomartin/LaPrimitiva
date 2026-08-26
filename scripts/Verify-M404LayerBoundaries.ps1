[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()

function Read-RepoFile([string]$RelativePath) {
    Get-Content -Raw (Join-Path $repoRoot $RelativePath)
}

function Require-Match([string]$Text, [string]$Pattern, [string]$Message) {
    if ($Text -notmatch $Pattern) { $failures.Add($Message) }
}

$applicationProject = Read-RepoFile 'LaPrimitiva.Application/LaPrimitiva.Application.csproj'
if ($applicationProject -match 'LaPrimitiva\.Infrastructure|Microsoft\.EntityFrameworkCore') {
    $failures.Add('Application conserva una referencia a Infrastructure o EF Core.')
}
Require-Match $applicationProject 'LaPrimitiva\.Domain' 'Application debe conservar su referencia a Domain.'

$innerLayerFiles = Get-ChildItem @(
    (Join-Path $repoRoot 'LaPrimitiva.Domain'),
    (Join-Path $repoRoot 'LaPrimitiva.Application')
) -Recurse -File -Include '*.cs', '*.csproj' |
    Where-Object { $_.FullName -notmatch '[\\/](?:bin|obj)[\\/]' }

foreach ($file in $innerLayerFiles) {
    $content = Get-Content -Raw $file.FullName
    if ($content -match 'LaPrimitiva\.Infrastructure|Microsoft\.EntityFrameworkCore') {
        $relative = [System.IO.Path]::GetRelativePath($repoRoot, $file.FullName)
        $failures.Add("$relative acopla una capa interior a Infrastructure o EF Core.")
    }
}

$componentFiles = Get-ChildItem (Join-Path $repoRoot 'LaPrimitiva.App/Components') -Recurse -File -Filter '*.razor'
foreach ($file in $componentFiles) {
    $content = Get-Content -Raw $file.FullName
    if ($content -match 'PrimitivaDbContext|IDbContextFactory|Microsoft\.EntityFrameworkCore|LaPrimitiva\.Infrastructure|(?:IPlan|IDraw|IWinningDraw)Repository') {
        $relative = [System.IO.Path]::GetRelativePath($repoRoot, $file.FullName)
        $failures.Add("$relative accede directamente a persistencia o a un repositorio.")
    }
}

$dataPage = Read-RepoFile 'LaPrimitiva.App/Components/Pages/Data.razor'
Require-Match $dataPage 'IDataExportService' 'Data.razor no usa el caso de uso de exportación.'

$homePage = Read-RepoFile 'LaPrimitiva.App/Components/Pages/Home.razor'
Require-Match $homePage 'IDashboardService' 'Home.razor no usa el caso de uso del dashboard.'

$registerPage = Read-RepoFile 'LaPrimitiva.App/Components/Pages/Register.razor'
Require-Match $registerPage 'IDrawService' 'Register.razor no usa el caso de uso de registros.'
if ($registerPage -match 'ValidateDrawAsync|CreateRangeAsync|UpdateRangeAsync') {
    $failures.Add('Register.razor todavía coordina validación o persistencia en vez del caso de uso.')
}

$financialMetrics = Read-RepoFile 'LaPrimitiva.Domain/Services/FinancialMetrics.cs'
Require-Match $financialMetrics 'CalculateNet' 'Falta la regla financiera común de neto.'
Require-Match $financialMetrics 'CalculateRoi' 'Falta la regla financiera común de ROI.'
Require-Match $financialMetrics 'CountWinningBets' 'Falta la regla común de recuento de apuestas premiadas.'

$summary = Read-RepoFile 'LaPrimitiva.Application/DTOs/SummaryDto.cs'
Require-Match $summary 'FinancialMetrics\.CalculateRoi' 'SummaryDto no delega el ROI en la regla común.'

$planService = Read-RepoFile 'LaPrimitiva.Application/Services/PlanService.cs'
Require-Match $planService 'FinancialMetrics\.CountWinningBets' 'PlanService duplica el recuento de premios.'

$plansPage = Read-RepoFile 'LaPrimitiva.App/Components/Pages/Plans.razor'
if ($plansPage -match 'newPlan\.Validate\(\)') {
    $failures.Add('Plans.razor duplica la validación que pertenece al caso de uso y al dominio.')
}

$tests = Read-RepoFile 'LaPrimitiva.Tests/M404LayerBoundaryTests.cs'
Require-Match $tests 'ApplicationProject_DependsOnlyOnDomain' 'Falta la prueba de dependencias del proyecto Application.'
Require-Match $tests 'RazorComponents_DoNotAccessPersistenceOrRepositoriesDirectly' 'Falta la prueba de límites de componentes.'
Require-Match $tests 'FinancialMetrics_UseOneRule' 'Falta la prueba de reglas financieras centralizadas.'

$applicationServiceTests = Read-RepoFile 'LaPrimitiva.Tests/M404ApplicationServiceTests.cs'
Require-Match $applicationServiceTests 'DashboardService_AppliesYearFilterAndBuildsSummaryAndMonthlySeries' 'Falta la prueba de comportamiento del dashboard.'
Require-Match $applicationServiceTests 'DashboardService_WithoutYearRequestsCompleteHistory' 'Falta la prueba de consulta histórica del dashboard.'
Require-Match $applicationServiceTests 'DataExportService_ReturnsEveryDrawOrderedChronologically' 'Falta la prueba de comportamiento de exportación.'

$drawServiceTests = Read-RepoFile 'LaPrimitiva.Tests/DrawServiceTests.cs'
Require-Match $drawServiceTests 'GetDrawsByYearAsync_AppliesPlanSelectionAfterYearQuery' 'Falta la prueba de consulta de registros por plan.'
Require-Match $drawServiceTests 'GetDrawsForWeekAsync_AttachesPlanReturnedByPort' 'Falta la prueba de carga semanal y plan.'
Require-Match $drawServiceTests 'GetCurrentWeekNumber_UsesFirstMondayAsWeekOneBoundary' 'Falta la prueba del límite semanal.'
Require-Match $drawServiceTests 'CreateDrawTemplate_RejectsDayWithoutPrimitivaDraw' 'Falta la prueba de día de sorteo inválido.'
Require-Match $drawServiceTests 'SaveDrawAsync_RecalculatesTotalsBeforeCallingPort' 'Falta la prueba de recálculo antes de persistir.'

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'M-404 static verification passed.'
Write-Host 'Inner-layer dependencies, UI use cases, ports and centralized financial rules are present.'
