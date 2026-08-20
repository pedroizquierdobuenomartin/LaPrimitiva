[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Read-ProjectFile {
    param([Parameter(Mandatory)][string]$RelativePath)

    $path = Join-Path $repositoryRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "No existe el archivo requerido: $RelativePath"
    }

    return Get-Content -LiteralPath $path -Raw
}

function Assert-Match {
    param(
        [Parameter(Mandatory)][string]$Content,
        [Parameter(Mandatory)][string]$Pattern,
        [Parameter(Mandatory)][string]$Message
    )

    if ($Content -notmatch $Pattern) {
        throw $Message
    }
}

function Assert-NotMatch {
    param(
        [Parameter(Mandatory)][string]$Content,
        [Parameter(Mandatory)][string]$Pattern,
        [Parameter(Mandatory)][string]$Message
    )

    if ($Content -match $Pattern) {
        throw $Message
    }
}

$register = Read-ProjectFile 'LaPrimitiva.App/Components/Pages/Register.razor'
$contract = Read-ProjectFile 'LaPrimitiva.Domain/Repositories/IDrawRepository.cs'
$repository = Read-ProjectFile 'LaPrimitiva.Infrastructure/Repositories/DrawRepository.cs'
$integrationTest = Read-ProjectFile 'LaPrimitiva.Tests/Integration/DisconnectedDrawPersistenceTests.cs'

Assert-Match $register 'private\s+async\s+Task\s+SaveDraw\s*\([^)]*\)[\s\S]*?DrawRepository\.UpdateAsync\(draw\)' `
    'SaveDraw no delega la persistencia desconectada en UpdateAsync.'
Assert-NotMatch $register 'DrawRepository\.SaveChangesAsync\(\)' `
    'Register sigue confiando en SaveChangesAsync para una entidad sin seguimiento.'
Assert-NotMatch $contract 'Task\s+SaveChangesAsync\s*\(' `
    'IDrawRepository todavía expone el guardado genérico que originó el fallo.'
Assert-NotMatch $repository '_context\.DrawRecords\.Update(?:Range)?\s*\(' `
    'El repositorio marca la entidad desconectada completa como modificada.'
Assert-Match $repository 'SingleOrDefaultAsync\(draw\s*=>\s*draw\.Id\s*==\s*id\)' `
    'UpdateAsync no vuelve a cargar una entidad seguida por su identificador.'

$editableProperties = @(
    'Played', 'FixedPrize', 'AutoPrize', 'JokerFixedPrize', 'JokerAutoPrize', 'Notes',
    'CosteFija', 'CosteAuto', 'CosteJokerFija', 'CosteJokerAuto',
    'TotalCoste', 'TotalPremios', 'Neto', 'UpdatedAt'
)

foreach ($property in $editableProperties) {
    Assert-Match $repository "target\.$property\s*=\s*source\.$property\s*;" `
        "La propiedad editable '$property' no se copia explícitamente."
}

$structuralProperties = @('PlanId', 'DrawType', 'DrawDate', 'WeekNumber', 'CreatedAt', 'Acumulado')
foreach ($property in $structuralProperties) {
    Assert-NotMatch $repository "target\.$property\s*=\s*source\.$property\s*;" `
        "La propiedad estructural '$property' se está sobrescribiendo."
}

Assert-Match $integrationTest 'GetListAsync\(draw\s*=>\s*draw\.Id\s*==\s*drawId\)' `
    'La prueba no parte de la consulta AsNoTracking del repositorio.'
Assert-Match $integrationTest 'await\s+repository\.UpdateAsync\(disconnected\)' `
    'La prueba no ejecuta la actualización explícita de la entidad desconectada.'
Assert-Match $integrationTest 'using\s+var\s+assertScope\s*=\s*CreateScope\(\)' `
    'La prueba no crea un contexto nuevo para comprobar la persistencia.'
Assert-Match $integrationTest 'AsNoTracking\(\)[\s\S]*?SingleAsync\(draw\s*=>\s*draw\.Id\s*==\s*drawId\)' `
    'La prueba no recarga el sorteo sin seguimiento desde el contexto nuevo.'

foreach ($property in $structuralProperties | Where-Object { $_ -ne 'Acumulado' }) {
    Assert-Match $integrationTest "Assert\.Equal\([^;]+persisted\.$property\)" `
        "La prueba no protege la columna estructural '$property'."
}

Write-Host 'M-201 verificado: guardado explícito, entidad seguida, columnas acotadas y prueba de persistencia con contexto nuevo.' -ForegroundColor Green
