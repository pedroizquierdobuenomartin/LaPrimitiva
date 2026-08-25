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

function Reject-Match([string]$Text, [string]$Pattern, [string]$Message) {
    if ($Text -match $Pattern) { $failures.Add($Message) }
}

$program = Read-RepoFile 'LaPrimitiva.App/Program.cs'
Require-Match $program 'AddDbContextFactory<PrimitivaDbContext>' 'Program.cs no registra IDbContextFactory.'
Reject-Match $program 'AddDbContext<PrimitivaDbContext>' 'Program.cs conserva el registro scoped de DbContext.'

$operationFiles = @(
    @{ Path = 'LaPrimitiva.Infrastructure/Repositories/DrawRepository.cs'; MinimumContexts = 7 },
    @{ Path = 'LaPrimitiva.Infrastructure/Repositories/PlanRepository.cs'; MinimumContexts = 7 },
    @{ Path = 'LaPrimitiva.Infrastructure/Repositories/WinningDrawRepository.cs'; MinimumContexts = 8 },
    @{ Path = 'LaPrimitiva.Infrastructure/Persistence/Seed/WinningDrawSeeder.cs'; MinimumContexts = 2 }
)

foreach ($entry in $operationFiles) {
    $content = Read-RepoFile $entry.Path
    Require-Match $content 'IDbContextFactory<PrimitivaDbContext>' "$($entry.Path) no depende de la fábrica."
    Reject-Match $content 'private readonly PrimitivaDbContext' "$($entry.Path) conserva un contexto como estado duradero."
    $creationCount = ([regex]::Matches($content, 'CreateDbContextAsync\(')).Count
    if ($creationCount -lt $entry.MinimumContexts) {
        $failures.Add("$($entry.Path) solo crea $creationCount contextos; se esperaban al menos $($entry.MinimumContexts).")
    }
}

$dataPage = Read-RepoFile 'LaPrimitiva.App/Components/Pages/Data.razor'
Require-Match $dataPage '@inject IDbContextFactory<PrimitivaDbContext>' 'Data.razor no inyecta la fábrica.'
Require-Match $dataPage 'await using var db = await DbContextFactory\.CreateDbContextAsync\(\)' 'Data.razor no dispone el contexto de exportación.'
Require-Match $dataPage '\.AsNoTracking\(\)' 'Data.razor conserva tracking en la exportación.'
Reject-Match $dataPage '@inject PrimitivaDbContext' 'Data.razor todavía inyecta un contexto del circuito.'

$productionFiles = Get-ChildItem (Join-Path $repoRoot 'LaPrimitiva.App'), (Join-Path $repoRoot 'LaPrimitiva.Infrastructure') -Recurse -File |
    Where-Object { $_.Extension -in '.cs', '.razor' }
foreach ($file in $productionFiles) {
    $content = Get-Content -Raw $file.FullName
    if ($content -match '(?m)^\s*(?:@inject|private readonly|public\s+\w+\s*\()\s*PrimitivaDbContext\b') {
        $failures.Add("Inyección o almacenamiento directo de PrimitivaDbContext en $($file.FullName).")
    }
}

$tests = Read-RepoFile 'LaPrimitiva.Tests/M402DbContextFactoryTests.cs'
Require-Match $tests 'SimultaneousRepositoryOperations_UseDifferentDisposedContexts' 'Falta la prueba de operaciones simultáneas.'
Require-Match $tests 'ReadOperation_ReturnsDetachedEntities_AndDisposesItsContext' 'Falta la prueba de lectura sin tracking y disposición.'

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'M-402 static verification passed.'
Write-Host 'Factory registration, per-operation contexts, disposal, no-tracking reads and focused tests are present.'
