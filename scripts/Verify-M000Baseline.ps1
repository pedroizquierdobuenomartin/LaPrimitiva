[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$settingsPath = Join-Path $repoRoot 'LaPrimitiva.Tests\appsettings.IntegrationTests.json'
$baselinePath = Join-Path $repoRoot 'mejoras\LINEA_BASE_M000.md'

function Assert-Condition {
    param(
        [Parameter(Mandatory)] [bool] $Condition,
        [Parameter(Mandatory)] [string] $Message
    )

    if (-not $Condition) {
        throw "M-000 FAIL: $Message"
    }
}

$settings = Get-Content -Raw -LiteralPath $settingsPath | ConvertFrom-Json
$connectionString = $settings.ConnectionStrings.DefaultConnection
$databaseMatch = [regex]::Match($connectionString, '(?i)(?:^|;)\s*(?:Database|Initial Catalog)\s*=\s*([^;]+)')

Assert-Condition $databaseMatch.Success 'la conexión de integración no declara una base de datos.'
$databaseName = $databaseMatch.Groups[1].Value.Trim()
Assert-Condition ($databaseName -ne 'PrimitivaAuditV2') 'la conexión de integración apunta a la base de desarrollo.'
Assert-Condition ($databaseName.EndsWith('_IntegrationTests', [StringComparison]::OrdinalIgnoreCase)) 'la base de integración no tiene el sufijo de seguridad requerido.'

$unsafeSqlUsages = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'LaPrimitiva.Tests\Integration') -Filter '*.cs' -File |
    Select-String -Pattern 'UseSqlServer\s*\(\s*"[^"\r\n]*Database=PrimitivaAuditV2(?:;|")'
Assert-Condition ($null -eq $unsafeSqlUsages) 'queda algún UseSqlServer directo contra PrimitivaAuditV2.'

$integrationBase = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'LaPrimitiva.Tests\Integration\IntegrationTestBase.cs')
$seederTests = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'LaPrimitiva.Tests\Integration\WinningDrawSeederTests.cs')
Assert-Condition $integrationBase.Contains('IntegrationTestDatabase.GetConnectionString()', [StringComparison]::Ordinal) 'la factoría web no usa la conexión protegida.'
Assert-Condition $seederTests.Contains('IntegrationTestDatabase.GetConnectionString()', [StringComparison]::Ordinal) 'el seeder de integración no usa la conexión protegida.'

$baseline = Get-Content -Raw -LiteralPath $baselinePath
$requiredFlows = @(
    'FLOW-PLANES', 'FLOW-REGISTRO', 'FLOW-PREMIOS', 'FLOW-JOKER',
    'FLOW-DASHBOARD', 'FLOW-HISTORICO', 'FLOW-RSS', 'FLOW-EXPORTACION',
    'FLOW-GENERACION'
)

foreach ($flow in $requiredFlows) {
    Assert-Condition $baseline.Contains($flow, [StringComparison]::Ordinal) "falta el flujo crítico $flow."
}

Write-Host "M-000 PASS: conexión aislada '$databaseName' y $($requiredFlows.Count) flujos críticos documentados."
