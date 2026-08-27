param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Read-RepositoryFile([string]$relativePath) {
    Get-Content -LiteralPath (Join-Path $repositoryRoot $relativePath) -Raw
}

function Assert-Contains([string]$text, [string]$value, [string]$message) {
    if (-not $text.Contains($value)) { throw $message }
}

function Assert-NotContains([string]$text, [string]$value, [string]$message) {
    if ($text.Contains($value)) { throw $message }
}

$service = Read-RepositoryFile 'LaPrimitiva.Application\Services\AutomatedCombinationService.cs'
$interface = Read-RepositoryFile 'LaPrimitiva.Application\Interfaces\IAutomatedCombinationService.cs'
$backtestDto = Read-RepositoryFile 'LaPrimitiva.Application\DTOs\AutomatedCombinationBacktestResult.cs'
$page = Read-RepositoryFile 'LaPrimitiva.App\Components\Pages\AutomatedCombination.razor'
$tests = Read-RepositoryFile 'LaPrimitiva.Tests\AutomatedCombinationServiceTests.cs'
[xml]$resources = Read-RepositoryFile 'LaPrimitiva.Domain\Localization\CombinationResource.es.resx'

$runtimeStatisticalSurface = $service + $interface + $backtestDto + $page
Assert-NotContains $runtimeStatisticalSurface 'pValue' 'El marcador pValue continúa en la superficie de ejecución.'
Assert-NotContains $runtimeStatisticalSurface 'PValue' 'El marcador PValue continúa en la superficie de ejecución.'
Assert-NotContains $runtimeStatisticalSurface 'ApproximateAverageZScore' 'La UI conserva una inferencia aproximada sin un contrato estadístico suficiente.'
Assert-NotContains $runtimeStatisticalSurface 'HasConventionalStatisticalAdvantage' 'La UI sigue clasificando una simulación retrospectiva como ventaja estadística.'

Assert-Contains $page '@L["StatisticalScopeTitle"]' 'La interfaz no muestra el alcance no predictivo del generador.'
Assert-Contains $page '@L["StatisticalScopeDescription"]' 'La interfaz no explica la independencia de los sorteos.'
Assert-Contains $page '@L["BacktestInterpretation"]' 'La evaluación retrospectiva no muestra una interpretación cauta y estable.'

$resourceValues = @{}
foreach ($item in $resources.root.data) { $resourceValues[$item.name] = [string]$item.value }

foreach ($requiredKey in @('StatisticalScopeTitle', 'StatisticalScopeDescription', 'BacktestInterpretation')) {
    if (-not $resourceValues.ContainsKey($requiredKey)) { throw "Falta el recurso localizado '$requiredKey'." }
}

Assert-Contains $resourceValues['StatisticalScopeDescription'] 'sorteos son independientes' 'La UI no declara que los sorteos son independientes.'
Assert-Contains $resourceValues['StatisticalScopeDescription'] 'histórico no aumenta la probabilidad matemática' 'La UI no aclara que el histórico no mejora la probabilidad.'
Assert-Contains $resourceValues['BacktestDescription'] 'simulación retrospectiva' 'El backtest no se identifica explícitamente como simulación.'
Assert-Contains $resourceValues['BacktestInterpretation'] 'no predice sorteos futuros' 'La interpretación del backtest todavía puede confundirse con una predicción.'
Assert-NotContains $resourceValues['ValidatedMethod'] 'validado' 'La cabecera sigue presentando el generador como un método validado.'
Assert-NotContains $resourceValues['EvidenceSubtitle'] 'sustenta' 'La evidencia retrospectiva todavía se presenta como respaldo predictivo.'

Assert-Contains $tests 'Assert.False(result.DebugInfo.ContainsKey("pValue"))' 'Falta la regresión que impide reintroducir el marcador pValue.'

Write-Output 'M-505 statistical scope static verification passed.'
