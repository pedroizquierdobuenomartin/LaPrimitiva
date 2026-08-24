$ErrorActionPreference = 'Stop'

function Assert-Contains {
    param(
        [Parameter(Mandatory)] [string] $Content,
        [Parameter(Mandatory)] [string] $Pattern,
        [Parameter(Mandatory)] [string] $Message
    )

    if ($Content -notmatch $Pattern) {
        throw $Message
    }
}

$root = Split-Path -Parent $PSScriptRoot
$domain = Get-Content -LiteralPath (Join-Path $root 'LaPrimitiva.Domain/Entities/WinningDraw.cs') -Raw
$service = Get-Content -LiteralPath (Join-Path $root 'LaPrimitiva.Application/Services/WinningDrawService.cs') -Raw
$generator = Get-Content -LiteralPath (Join-Path $root 'LaPrimitiva.Application/Services/AutomatedCombinationService.cs') -Raw
$repository = Get-Content -LiteralPath (Join-Path $root 'LaPrimitiva.Infrastructure/Repositories/WinningDrawRepository.cs') -Raw
$dbContext = Get-Content -LiteralPath (Join-Path $root 'LaPrimitiva.Infrastructure/PrimitivaDbContext.cs') -Raw
$migration = Get-Content -LiteralPath (Join-Path $root 'LaPrimitiva.Infrastructure/Migrations/20260824150000_ValidateWinningDraws.cs') -Raw
$seeder = Get-Content -LiteralPath (Join-Path $root 'LaPrimitiva.Infrastructure/Persistence/Seed/WinningDrawSeeder.cs') -Raw
$page = Get-Content -LiteralPath (Join-Path $root 'LaPrimitiva.App/Components/Pages/HistoricalDraws.razor') -Raw
$domainTests = Get-Content -LiteralPath (Join-Path $root 'LaPrimitiva.Tests/WinningDrawTests.cs') -Raw
$serviceTests = Get-Content -LiteralPath (Join-Path $root 'LaPrimitiva.Tests/WinningDrawServiceTests.cs') -Raw
$repositoryTests = Get-Content -LiteralPath (Join-Path $root 'LaPrimitiva.Tests/WinningDrawRepositoryTests.cs') -Raw
$generatorTests = Get-Content -LiteralPath (Join-Path $root 'LaPrimitiva.Tests/AutomatedCombinationServiceTests.cs') -Raw

Assert-Contains $domain 'public void Validate\(\)' 'Falta la validación de dominio de WinningDraw.'
Assert-Contains $domain 'number is < MinimumNumber or > MaximumNumber' 'Falta el rango 1..49 para números principales.'
Assert-Contains $domain 'mainNumbers\.Distinct\(\)\.Count\(\) != mainNumbers\.Length' 'Falta impedir números principales duplicados.'
Assert-Contains $domain 'mainNumbers\.Contains\(Complementario\)' 'Falta impedir que el complementario repita un número principal.'
Assert-Contains $domain 'Reintegro is < MinimumReintegro or > MaximumReintegro' 'Falta el rango 0..9 del reintegro.'
Assert-Contains $domain 'Joker\.Length != JokerLength' 'Falta validar la longitud del Joker.'
Assert-Contains $domain 'char\.IsAsciiDigit' 'Falta limitar el Joker a dígitos ASCII.'

Assert-Contains $service 'var validation = Validate\(entity\)' 'Application no valida la entidad antes de crear o actualizar.'
Assert-Contains $service 'ToString\(\$?"D\{WinningDraw\.JokerLength\}"\)' 'RSS no conserva los siete dígitos del Joker.'
Assert-Contains $repository 'draw\.Validate\(\);' 'El repositorio no aplica defensa en profundidad.'
Assert-Contains $generator '\.Where\(draw => draw\.IsValid\(\)\)' 'El consumidor histórico no descarta sorteos corruptos.'

foreach ($constraint in @(
    'CK_WinningDraws_MainNumbersRange',
    'CK_WinningDraws_MainNumbersDistinct',
    'CK_WinningDraws_Complementario',
    'CK_WinningDraws_Reintegro',
    'CK_WinningDraws_Joker'
)) {
    Assert-Contains $dbContext ([regex]::Escape($constraint)) "Falta $constraint en el modelo EF."
    Assert-Contains $migration ([regex]::Escape($constraint)) "Falta $constraint en la migración."
    Assert-Contains $seeder ([regex]::Escape($constraint)) "Falta $constraint en el DDL de arranque."
}

Assert-Contains $migration "THROW 51002, 'No se pueden activar las restricciones: existen sorteos históricos inválidos\.'" 'La migración no bloquea datos históricos inválidos de forma explícita.'
Assert-Contains $migration "RIGHT\(REPLICATE\('0', 7\) \+ \[Joker\], 7\)" 'La migración no recupera ceros iniciales perdidos en Joker numéricos heredados.'
Assert-Contains $seeder 'draw\.Validate\(\);' 'El importador CSV no valida cada sorteo.'
Assert-Contains $page 'type="number" min="1" max="49"' 'La UI no declara el rango de números.'
Assert-Contains $page 'type="number" min="0" max="9"' 'La UI no declara el rango del reintegro.'
Assert-Contains $page 'maxlength="7" pattern="\[0-9\]\{7\}"' 'La UI no limita el formato del Joker.'

Assert-Contains $domainTests 'Validate_WhenMainNumberIsOutsideRange_Throws' 'Falta prueba de rango principal.'
Assert-Contains $domainTests 'Validate_WhenJokerIsNotSevenDigits_Throws' 'Falta prueba de formato Joker.'
Assert-Contains $serviceTests 'CreateAsync_WhenMainNumberIsOutsideRange_ShouldReturnFailure' 'Falta prueba de Application para rango principal.'
Assert-Contains $repositoryTests 'CreateAsync_WhenDrawIsInvalid_RejectsBeforePersistence' 'Falta prueba de defensa del repositorio al crear.'
Assert-Contains $repositoryTests 'UpdateAsync_WhenDrawIsInvalid_RejectsBeforePersistence' 'Falta prueba de defensa del repositorio al actualizar.'
Assert-Contains $generatorTests 'BacktestAsync_IgnoresCorruptHistoricalDraws' 'Falta prueba defensiva del consumidor histórico.'

Write-Host 'M-303 static verification passed.' -ForegroundColor Green
