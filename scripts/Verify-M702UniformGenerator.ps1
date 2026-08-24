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
$page = Read-RepositoryFile 'LaPrimitiva.App\Components\Pages\AutomatedCombination.razor'
$appShell = Read-RepositoryFile 'LaPrimitiva.App\Components\App.razor'
$appCss = Read-RepositoryFile 'LaPrimitiva.App\wwwroot\app.css'
$reconnectCss = Read-RepositoryFile 'LaPrimitiva.App\Components\Layout\ReconnectModal.razor.css'
$homePage = Read-RepositoryFile 'LaPrimitiva.App\Components\Pages\Home.razor'
$cloverSvg = Read-RepositoryFile 'LaPrimitiva.App\wwwroot\images\trebol-suerte.svg'
$tests = Read-RepositoryFile 'LaPrimitiva.Tests\AutomatedCombinationServiceTests.cs'
[xml]$resources = Read-RepositoryFile 'LaPrimitiva.Domain\Localization\CombinationResource.es.resx'

$generatorStart = $service.IndexOf('public Task<CombinationResult> GenerateCombinationAsync')
$backtestStart = $service.IndexOf('public async Task<AutomatedCombinationBacktestResult> BacktestAsync')
if ($generatorStart -lt 0 -or $backtestStart -le $generatorStart) {
    throw 'No se pudo aislar el generador de producción del backtest.'
}
$generator = $service.Substring($generatorStart, $backtestStart - $generatorStart)

Assert-Contains $interface 'GenerateCombinationAsync(int variation = 0)' 'El contrato todavía expone parámetros del modelo ponderado.'
Assert-Contains $generator 'GenerateUniformNumbers(random)' 'El generador no usa selección uniforme sin reemplazo.'
Assert-Contains $generator '"strategy", "uniform_without_replacement"' 'Falta identificar la estrategia uniforme en el resultado.'
Assert-Contains $generator '"possible_combinations", 13_983_816' 'Falta documentar el espacio de combinaciones.'
Assert-NotContains $generator '_repository.GetListAsync' 'El generador de producción todavía consulta el histórico.'
Assert-NotContains $generator 'CalculateWeightedProbabilities' 'El generador de producción todavía calcula pesos históricos.'
Assert-NotContains $generator 'PickWeekly' 'El generador de producción todavía usa muestreo ponderado.'

Assert-Contains $page '@L["UniformSelectionNotice"]' 'La interfaz no explica la igualdad de probabilidades.'
Assert-Contains $page '@L["UniformStrategyTitle"]' 'La interfaz no identifica la estrategia adoptada.'
Assert-Contains $page 'from-[var(--brand-primary)] to-[var(--brand-secondary)]' 'El panel principal no usa la paleta oficial de la aplicación.'
Assert-Contains $appCss "family=Poppins:wght@300;400;500;600;700" 'No se cargan todos los pesos Poppins utilizados por la aplicación.'
Assert-Contains $appCss "font-family: 'Poppins', sans-serif;" 'La fuente base global no es Poppins con fallback sans-serif.'
Assert-NotContains $appCss "'Poppins', 'Inter', sans-serif" 'Inter continúa como fallback tipográfico global.'
Assert-Contains $appShell '<body class="h-full text-slate-900 overflow-hidden">' 'El body conserva una clase que puede anular la fuente base.'
Assert-NotContains $appShell 'family=Inter' 'App.razor sigue solicitando Inter de forma redundante.'
Assert-NotContains $appShell "font-['Inter',sans-serif]" 'La clase Tailwind de Inter continúa anulando Poppins.'
Assert-NotContains $reconnectCss "'Inter', sans-serif" 'El modal de reconexión no respeta la tipografía base.'
Assert-NotContains $homePage 'family = "Inter"' 'Las gráficas del dashboard no respetan la tipografía base.'
Assert-NotContains $page 'font-family' 'La página de combinación introduce una familia tipográfica local.'
Assert-Contains $page 'min-h-full' 'El contenedor de página puede volver a comprimir el panel principal.'
Assert-Contains $page 'h-16 w-16 md:h-20 md:w-20 lg:h-24 lg:w-24' 'Las bolas no tienen dimensiones responsivas acotadas.'
Assert-NotContains $page 'aspect-square min-h-[64px]' 'Las bolas todavía crecen con todo el ancho de la rejilla y pueden quedar recortadas.'
Assert-NotContains $page '#062f27' 'Queda un verde fijo ajeno a los tokens de marca.'
Assert-Contains $page 'data-ui="number-ball-clover" src="/images/trebol-suerte.svg"' 'Las bolas principales no usan el trébol SVG seleccionado por el usuario.'
Assert-Contains $page '<span class="relative z-10">@n</span>' 'El número no queda por encima del trébol decorativo.'
Assert-Contains $page 'data-ui="generated-lucky-message"' 'La apuesta generada ha perdido la frase de ánimo aprobada por el usuario.'
Assert-Contains $page '<span>@L["LuckyPrompt"] <span class="font-black text-[var(--brand-accent)]">@L["FortunePrompt"]</span> @L["Today"]</span>' 'La frase de suerte no conserva el texto localizado ni el énfasis de marca.'
if ([regex]::Matches($page, 'data-ui="lucky-message-clover"').Count -ne 2) { throw 'La frase de suerte debe quedar flanqueada exactamente por dos tréboles.' }
if ([regex]::Matches($page, 'src="/images/trebol-suerte.svg"').Count -ne 4) { throw 'El SVG seleccionado debe usarse en las bolas principales, el reintegro y los dos lados del mensaje.' }
Assert-Contains $page 'aria-hidden="true"' 'Los tréboles decorativos deben quedar fuera del árbol de accesibilidad.'
Assert-Contains $page 'data-ui="reintegro-card"' 'El reintegro no dispone de una tarjeta coherente con los números principales.'
Assert-Contains $page 'data-ui="reintegro-header" class="flex items-center justify-between gap-4 mb-5"' 'Reintegro y rango no permanecen en una sola línea.'
Assert-Contains $page 'data-ui="reintegro-ball" class="group/reintegro relative overflow-hidden h-16 w-16 md:h-20 md:w-20 lg:h-24 lg:w-24' 'La bola del reintegro no comparte dimensiones con las bolas principales.'
Assert-Contains $page 'data-ui="reintegro-ball-clover" src="/images/trebol-suerte.svg"' 'La bola del reintegro no usa el trébol SVG aprobado.'
Assert-Contains $cloverSvg 'viewBox="1150 220 850 1030"' 'El SVG del trébol no conserva el recorte optimizado.'
Assert-NotContains $cloverSvg '<rect' 'El SVG optimizado ha recuperado el fondo blanco original.'
Assert-NotContains $cloverSvg 'xmlns:xlink' 'El SVG optimizado conserva dependencias externas innecesarias.'
Assert-Contains $page 'role="status" aria-live="polite"' 'El estado de generación no se anuncia de forma accesible.'
Assert-Contains $page 'class="combination-spinner h-16 w-16' 'El indicador de carga no usa la animación propia y verificable de la aplicación.'
Assert-Contains $page 'combination-loading-overlay' 'El estado de generación no usa el difuminado semitransparente aprobado.'
Assert-Contains $page 'combination-loading-card' 'El spinner no dispone de una superficie de contraste propia.'
Assert-NotContains $page 'border-t-amber-300 animate-spin motion-reduce:animate-none' 'El indicador de carga todavía depende de Tailwind y puede quedar estático con movimiento reducido.'
Assert-Contains $appCss '@keyframes combination-spinner-rotate' 'No existe la animación CSS del indicador de carga.'
Assert-Contains $appCss 'animation: combination-spinner-rotate 0.8s linear infinite;' 'El indicador de carga no tiene una rotación continua definida.'
Assert-Contains $appCss 'background: rgba(0, 112, 65, 0.3);' 'El glass de carga no conserva un respaldo verde semitransparente.'
Assert-Contains $appCss 'color-mix(in srgb, var(--brand-primary) 34%, transparent)' 'El glass de carga no deriva del verde oficial con transparencia.'
Assert-NotContains $appCss 'color-mix(in srgb, var(--brand-accent) 90%, transparent)' 'El glass dorado descartado por el usuario sigue activo.'
Assert-Contains $appCss 'background: color-mix(in srgb, var(--brand-secondary) 94%, transparent);' 'La tarjeta del spinner no usa el verde oscuro oficial para asegurar contraste.'
Assert-Contains $appCss 'border-top-color: var(--brand-accent);' 'El arco móvil del spinner no usa el dorado oficial.'
Assert-Contains $appCss '@media (prefers-reduced-motion: reduce)' 'La animación no adapta su velocidad a la preferencia de movimiento reducido.'
Assert-Contains $page 'focus-visible:ring-4' 'Los controles principales no muestran foco de teclado.'
Assert-Contains $page '<details class="group' 'La evidencia histórica no está agrupada en un panel progresivo.'
Assert-Contains $page '@onclick="Regenerate"' 'El rediseño ha perdido la acción Regenerar.'
Assert-NotContains $page 'DebugInfo["chi2_uniformity"]' 'La interfaz todavía presenta metadatos del modelo ponderado.'
Assert-NotContains $page 'DebugInfo["top10_by_model"]' 'La interfaz todavía presenta números calientes.'

$resourceValues = @{}
foreach ($item in $resources.root.data) { $resourceValues[$item.name] = [string]$item.value }
$localizedKeys = [regex]::Matches($page, '@L\["([^"]+)"\]') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
foreach ($key in $localizedKeys) {
    if (-not $resourceValues.ContainsKey($key)) { throw "Falta el recurso localizado '$key'." }
}
if ($resourceValues['UniformSelection'] -ne 'uniforme sin reemplazo') {
    throw 'El subtítulo continúa describiendo el generador como bayesiano.'
}
Assert-Contains $resourceValues['UniformStrategyDescription'] 'no encontró una ventaja predictiva fiable' 'Falta la cautela estadística en la interfaz.'

Assert-Contains $tests 'GenerateCombinationAsync_ReturnsOneValidUniformTicket' 'Falta la prueba de una apuesta uniforme válida.'
Assert-Contains $tests 'Times.Never' 'Falta comprobar que la generación no consulta el histórico.'
Assert-Contains $tests 'Assert.False(result.DebugInfo.ContainsKey("top10_by_model"))' 'Falta impedir la reintroducción de números calientes.'

Write-Output 'M-702 uniform generator static verification passed.'
