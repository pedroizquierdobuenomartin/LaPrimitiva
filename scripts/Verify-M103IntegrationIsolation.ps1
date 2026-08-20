[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$testsRoot = Join-Path $repoRoot 'LaPrimitiva.Tests'
$integrationRoot = Join-Path $testsRoot 'Integration'

function Assert-Condition {
    param(
        [Parameter(Mandatory)]
        [bool]$Condition,

        [Parameter(Mandatory)]
        [string]$Message
    )

    if (-not $Condition) {
        throw "M-103 verification failed: $Message"
    }
}

function Read-RequiredFile {
    param([Parameter(Mandatory)][string]$Path)

    Assert-Condition (Test-Path -LiteralPath $Path -PathType Leaf) "missing file '$Path'."
    return Get-Content -LiteralPath $Path -Raw
}

$databaseSource = Read-RequiredFile (Join-Path $integrationRoot 'IntegrationTestDatabase.cs')
$fixtureSource = Read-RequiredFile (Join-Path $integrationRoot 'IntegrationTestFixture.cs')
$collectionSource = Read-RequiredFile (Join-Path $integrationRoot 'IntegrationTestCollection.cs')
$baseSource = Read-RequiredFile (Join-Path $integrationRoot 'IntegrationTestBase.cs')
$seederTestSource = Read-RequiredFile (Join-Path $integrationRoot 'WinningDrawSeederTests.cs')
$programSource = Read-RequiredFile (Join-Path $repoRoot 'LaPrimitiva.App\Program.cs')
$projectSource = Read-RequiredFile (Join-Path $testsRoot 'LaPrimitiva.Tests.csproj')
$testDataPath = Join-Path $testsRoot 'TestData\winning-draws.csv'

Assert-Condition ($databaseSource -match 'RequiredDatabaseSuffix\s*=\s*"_IntegrationTests"') 'the mandatory test database suffix is absent.'
Assert-Condition ($databaseSource -match 'AttachDBFilename') 'attached database files are not rejected.'
Assert-Condition ($databaseSource -match 'CreateIsolatedConnectionString') 'the per-run database name is not generated.'
Assert-Condition ($databaseSource -match 'Guid\.NewGuid\(\)') 'the per-run database name is not unique.'
Assert-Condition ($fixtureSource -match 'Database\.MigrateAsync\(\)') 'migrations are not applied during fixture initialization.'
Assert-Condition ($fixtureSource -match 'Respawner\.CreateAsync') 'Respawn is not configured for deterministic cleanup.'
Assert-Condition ($fixtureSource -match 'TablesToIgnore\s*=\s*\[new Table\("__EFMigrationsHistory"\)\]') 'migration history is not preserved during reset.'
Assert-Condition ($fixtureSource -match 'Database\.EnsureDeletedAsync\(\)') 'the ephemeral database is not deleted on teardown.'
Assert-Condition ($fixtureSource -match 'IntegrationTestDatabase\.EnsureSafe\(_connectionString\)') 'destructive lifecycle operations are not guarded.'
Assert-Condition ($baseSource -match 'return _fixture\.ResetDatabaseAsync\(\);') 'the integration base does not reset before every test.'
Assert-Condition ($collectionSource -match 'DisableParallelization\s*=\s*true') 'integration tests can run concurrently while sharing lifecycle state.'
Assert-Condition ($programSource -match '!app\.Environment\.IsEnvironment\("IntegrationTests"\)') 'application startup can seed uncontrolled data during integration tests.'
Assert-Condition ($seederTestSource -match 'Path\.Combine\(_fixture\.TestDataDirectory, "winning-draws\.csv"\)') 'the seeder test does not use a portable project resource.'
Assert-Condition ($projectSource -match '<None Update="TestData\\\*\.csv">') 'test resources are not copied to the test output.'
Assert-Condition (Test-Path -LiteralPath $testDataPath -PathType Leaf) 'the portable CSV test resource is absent.'

$unsafePaths = Get-ChildItem -LiteralPath $testsRoot -Recurse -File -Filter '*.cs' |
    Select-String -Pattern '(?i)[a-z]:\\|\\Repositorios\\'
Assert-Condition ($null -eq $unsafePaths) 'absolute Windows paths remain in test sources.'

$directUnsafeConnections = Get-ChildItem -LiteralPath $integrationRoot -Recurse -File -Filter '*.cs' |
    Where-Object Name -ne 'IntegrationTestDatabase.cs' |
    Select-String -Pattern 'UseSqlServer\s*\(\s*IntegrationTestDatabase\.GetConnectionString\(\)'
Assert-Condition ($null -eq $directUnsafeConnections) 'an integration test bypasses the managed fixture connection.'

Write-Output 'M-103 verification passed: isolated name, safety guard, migrations, deterministic reset/delete, portable data and serialized integration collection are present.'
