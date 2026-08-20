[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Read-ProjectFile([string]$relativePath) {
    Get-Content -Raw (Join-Path $root $relativePath)
}

function Assert-Contains([string]$content, [string]$pattern, [string]$message) {
    if ($content -notmatch $pattern) {
        throw $message
    }
}

$drawRecord = Read-ProjectFile 'LaPrimitiva.Domain\Entities\DrawRecord.cs'
$repository = Read-ProjectFile 'LaPrimitiva.Infrastructure\Repositories\DrawRepository.cs'
$seeder = Read-ProjectFile 'LaPrimitiva.Infrastructure\Persistence\Seed\WinningDrawSeeder.cs'
$registration = Read-ProjectFile 'LaPrimitiva.App\Components\Pages\Register.razor'
$summary = Read-ProjectFile 'LaPrimitiva.Application\Services\SummaryService.cs'
$summaryDto = Read-ProjectFile 'LaPrimitiva.Application\DTOs\SummaryDto.cs'
$unitTests = Read-ProjectFile 'LaPrimitiva.Tests\DrawRecordTests.cs'
$summaryTests = Read-ProjectFile 'LaPrimitiva.Tests\SummaryServiceTests.cs'
$repairTests = Read-ProjectFile 'LaPrimitiva.Tests\Integration\FinancialTotalsRepairTests.cs'

Assert-Contains $drawRecord 'CalculatedTotalCost\s*=>\s*CosteFija\s*\+\s*CosteAuto\s*\+\s*CosteJokerFija\s*\+\s*CosteJokerAuto' 'El total de coste de dominio no incluye los cuatro componentes.'
Assert-Contains $drawRecord 'CalculatedTotalPrize\s*=>\s*FixedPrize\s*\+\s*AutoPrize\s*\+\s*JokerFixedPrize\s*\+\s*JokerAutoPrize' 'El total de premios de dominio no incluye los cuatro componentes.'
Assert-Contains $drawRecord 'public void RecalculateFinancials' 'No existe el punto único de recálculo financiero.'
Assert-Contains $repository 'draw\.RecalculateFinancials\(\)' 'Las altas no fuerzan el invariante financiero.'
Assert-Contains $repository 'source\.RecalculateFinancials\(\)' 'Las actualizaciones no fuerzan el invariante financiero.'
Assert-Contains $seeder 'RepairFinancialTotalsAsync' 'No existe reparación idempotente de registros anteriores.'
Assert-Contains $registration 'draw\.RecalculateFinancials\(forceDefaults\)' 'Registro no delega el cálculo al dominio.'
Assert-Contains $registration '@bind="draw\.CosteJokerFija"' 'Registro no muestra el coste Joker de la apuesta fija.'
Assert-Contains $registration '@bind="draw\.JokerAutoPrize"' 'Registro no permite registrar el premio Joker automático.'
Assert-Contains $summary 'summary\.TotalSpent\s*\+=\s*d\.TotalCoste' 'Dashboard no consume el total unificado de coste.'
Assert-Contains $summaryDto 'ROI\s*=>\s*TotalSpent\s*>\s*0\s*\?\s*\(NetResult\s*/\s*TotalSpent\)' 'ROI no usa el total unificado.'
Assert-Contains $unitTests 'WhenEnabledAndAwarded' 'Falta el caso Joker activado y premiado.'
Assert-Contains $unitTests 'WhenEnabledWithoutPrize' 'Falta el caso Joker activado sin premio.'
Assert-Contains $unitTests 'WhenDisabled' 'Falta el caso Joker desactivado.'
Assert-Contains $summaryTests 'GetSummary_UsesUnifiedTotalsForDashboardNetAndRoi' 'Falta la prueba de resumen y ROI.'
Assert-Contains $repairTests 'SeedStartup_RepairsExistingTotalsThatExcludeJoker' 'Falta la prueba de reparación de datos existentes.'

Write-Host 'M-203 verificado estáticamente: regla única, persistencia, reparación, UI, resumen, ROI y casos de prueba presentes.'
