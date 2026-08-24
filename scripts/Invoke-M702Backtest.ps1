$ErrorActionPreference = 'Stop'

$sqlcmd = 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\SQLCMD.EXE'
$rows = & $sqlcmd -S 'localhost\LOCALSERVER' -d 'PrimitivaAuditV2' -E -No -b -W -h -1 -s ';' -Q @'
SET NOCOUNT ON;
SELECT CONVERT(char(10), DrawDate, 23), Number1, Number2, Number3, Number4, Number5, Number6
FROM WinningDraws
ORDER BY DrawDate;
'@
if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed with exit code $LASTEXITCODE" }

$draws = foreach ($row in $rows) {
    if ([string]::IsNullOrWhiteSpace($row)) { continue }
    $parts = $row.Split(';')
    [pscustomobject]@{
        Date = [datetime]::ParseExact($parts[0].Trim(), 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
        Numbers = [int[]]($parts[1..6] | ForEach-Object { [int]$_.Trim() })
    }
}

$observedRows = & $sqlcmd -S 'localhost\LOCALSERVER' -d 'PrimitivaAuditV2' -E -No -b -W -h -1 -s ';' -Q @'
SET NOCOUNT ON;
SELECT
    COUNT(*),
    SUM(CASE WHEN FixedPrize > 0 THEN 1 ELSE 0 END),
    SUM(CASE WHEN AutoPrize > 0 THEN 1 ELSE 0 END),
    CAST(SUM(FixedPrize) AS decimal(18,2)),
    CAST(SUM(AutoPrize) AS decimal(18,2)),
    CAST(SUM(CosteFija) AS decimal(18,2)),
    CAST(SUM(CosteAuto) AS decimal(18,2))
FROM DrawRecords
WHERE Played = 1 AND DrawDate >= '2026-02-24' AND DrawDate < '2026-08-25';
'@
if ($LASTEXITCODE -ne 0) { throw "sqlcmd observed comparison failed with exit code $LASTEXITCODE" }
$observedParts = ($observedRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1).Split(';')
$observedComparison = [ordered]@{
    from = '2026-02-24'
    through = '2026-08-24'
    playedDraws = [int]$observedParts[0].Trim()
    fixedWinningDraws = [int]$observedParts[1].Trim()
    automaticWinningDraws = [int]$observedParts[2].Trim()
    fixedPrizes = [decimal]$observedParts[3].Trim()
    automaticPrizes = [decimal]$observedParts[4].Trim()
    fixedCost = [decimal]$observedParts[5].Trim()
    automaticCost = [decimal]$observedParts[6].Trim()
    limitation = 'Only financial outcomes were persisted; the played number combinations cannot be reconstructed.'
}

function Get-WeekSeed([datetime]$date) {
    $day = [int]$date.DayOfWeek
    if ($day -ge 1 -and $day -le 3) { $date = $date.AddDays(3) }
    return [Globalization.ISOWeek]::GetYear($date) * 100 + [Globalization.ISOWeek]::GetWeekOfYear($date)
}

function Get-WeightedNumbers([double[]]$probabilities, [int]$seed) {
    $random = [Random]::new($seed)
    $available = [Collections.Generic.List[int]]::new()
    $weights = [Collections.Generic.List[double]]::new()
    for ($number = 1; $number -le 49; $number++) {
        $available.Add($number)
        $weights.Add($probabilities[$number - 1])
    }

    $picked = [Collections.Generic.List[int]]::new()
    for ($pick = 0; $pick -lt 6; $pick++) {
        $total = 0.0
        foreach ($weight in $weights) { $total += $weight }
        $value = $random.NextDouble() * $total
        $cumulative = 0.0
        $selectedIndex = $weights.Count - 1
        for ($index = 0; $index -lt $weights.Count; $index++) {
            $cumulative += $weights[$index]
            if ($value -lt $cumulative) { $selectedIndex = $index; break }
        }
        $picked.Add($available[$selectedIndex])
        $available.RemoveAt($selectedIndex)
        $weights.RemoveAt($selectedIndex)
    }
    return [int[]]($picked | Sort-Object)
}

function Get-UniformNumbers([int]$seed) {
    $random = [Random]::new($seed)
    $numbers = [int[]](1..49)
    for ($index = $numbers.Length - 1; $index -gt 0; $index--) {
        $swapIndex = $random.Next($index + 1)
        $temporary = $numbers[$index]
        $numbers[$index] = $numbers[$swapIndex]
        $numbers[$swapIndex] = $temporary
    }
    return [int[]]($numbers[0..5] | Sort-Object)
}

function Get-Metrics([Collections.Generic.List[int]]$matches) {
    $distribution = [ordered]@{}
    foreach ($value in 0..6) { $distribution["$value"] = @($matches | Where-Object { $_ -eq $value }).Count }
    return [ordered]@{
        totalMatches = ($matches | Measure-Object -Sum).Sum
        averageMatches = [math]::Round(($matches | Measure-Object -Average).Average, 6)
        maximumMatches = ($matches | Measure-Object -Maximum).Maximum
        drawsWithAtLeastThreeMatches = @($matches | Where-Object { $_ -ge 3 }).Count
        matchDistribution = $distribution
    }
}

$minimumTrainingDraws = 104
if ($draws.Count -le $minimumTrainingDraws) { throw 'Not enough historical draws.' }

$counts = [double[]]::new(49)
$firstTargetDate = $draws[$minimumTrainingDraws].Date
for ($index = 0; $index -lt $minimumTrainingDraws; $index++) {
    $ageDays = [math]::Max(0, ($firstTargetDate - $draws[$index].Date).TotalDays)
    $weight = [math]::Pow(0.5, $ageDays / 365.0)
    foreach ($number in $draws[$index].Numbers) { $counts[$number - 1] += $weight }
}

$weightedMatches = [Collections.Generic.List[int]]::new()
$uniformMatches = [Collections.Generic.List[int]]::new()
for ($index = $minimumTrainingDraws; $index -lt $draws.Count; $index++) {
    $target = $draws[$index]
    if ($index -gt $minimumTrainingDraws) {
        $previous = $draws[$index - 1]
        $decay = [math]::Pow(0.5, ($target.Date - $previous.Date).TotalDays / 365.0)
        for ($numberIndex = 0; $numberIndex -lt 49; $numberIndex++) { $counts[$numberIndex] *= $decay }
        foreach ($number in $previous.Numbers) { $counts[$number - 1] += $decay }
    }

    $totalCount = ($counts | Measure-Object -Sum).Sum
    $probabilities = [double[]]::new(49)
    for ($numberIndex = 0; $numberIndex -lt 49; $numberIndex++) {
        $probabilities[$numberIndex] = ($counts[$numberIndex] + 1.0) / ($totalCount + 49.0)
    }

    $weekSeed = Get-WeekSeed $target.Date
    $weighted = Get-WeightedNumbers $probabilities $weekSeed
    $uniformSeed = $weekSeed -bxor ($target.Date.DayOfYear * 397) -bxor 0x5f3759df
    $uniform = Get-UniformNumbers $uniformSeed
    $weightedMatches.Add(@($weighted | Where-Object { $target.Numbers -contains $_ }).Count)
    $uniformMatches.Add(@($uniform | Where-Object { $target.Numbers -contains $_ }).Count)
}

$evaluatedDraws = $draws.Count - $minimumTrainingDraws
$theoreticalUniformAverage = 36.0 / 49.0
$uniformMatchVariance = 6.0 * (6.0 / 49.0) * (43.0 / 49.0) * (43.0 / 48.0)
$weightedAverage = ($weightedMatches | Measure-Object -Average).Average
$uniformAverage = ($uniformMatches | Measure-Object -Average).Average
$averageZScore = ($weightedAverage - $theoreticalUniformAverage) / [math]::Sqrt($uniformMatchVariance / $evaluatedDraws)
$expectedAtLeastThreeProbability = 0.01863754500202234
$observedAtLeastThree = @($weightedMatches | Where-Object { $_ -ge 3 }).Count

$evidence = [ordered]@{
    milestone = 'M-702'
    generatedAt = [datetime]::UtcNow.ToString('o')
    database = 'PrimitivaAuditV2'
    historicalDraws = $draws.Count
    minimumTrainingDraws = $minimumTrainingDraws
    evaluatedDraws = $evaluatedDraws
    firstHistoricalDate = $draws[0].Date.ToString('yyyy-MM-dd')
    firstEvaluatedDate = $draws[$minimumTrainingDraws].Date.ToString('yyyy-MM-dd')
    lastEvaluatedDate = $draws[-1].Date.ToString('yyyy-MM-dd')
    weightedModel = Get-Metrics $weightedMatches
    uniformBaseline = Get-Metrics $uniformMatches
    theoreticalUniformAverageMatches = [math]::Round($theoreticalUniformAverage, 6)
    statisticalAssessment = [ordered]@{
        weightedMinusDeterministicUniformAverage = [math]::Round($weightedAverage - $uniformAverage, 6)
        weightedMinusTheoreticalUniformAverage = [math]::Round($weightedAverage - $theoreticalUniformAverage, 6)
        approximateAverageZScore = [math]::Round($averageZScore, 6)
        conventionalTwoSidedThreshold = 1.96
        observedDrawsWithAtLeastThreeMatches = $observedAtLeastThree
        theoreticalExpectedDrawsWithAtLeastThreeMatches = [math]::Round($evaluatedDraws * $expectedAtLeastThreeProbability, 3)
        conclusion = 'This initial run does not demonstrate a statistically convincing predictive advantage at the conventional 95% threshold.'
    }
    fixedCombinationAvailable = $false
    observedSixMonthFinancialComparison = $observedComparison
    limitations = @(
        'Walk-forward simulation; only draws before each target were used.',
        'Historical fixed and automatic bet numbers were not persisted.',
        'The deterministic uniform comparator is one reproducible baseline, not the distribution of many random simulations.',
        'Reintegro was not evaluated.'
    )
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $repositoryRoot 'mejoras\evidencias\M-702-backtest-initial-20260824.json'
$evidence | ConvertTo-Json -Depth 6 | Set-Content -Path $outputPath -Encoding utf8
$evidence | ConvertTo-Json -Depth 6
