$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$appRoot = Join-Path $root 'LaPrimitiva.App'
$appCssPath = Join-Path $appRoot 'wwwroot\app.css'
$tailwindConfigPath = Join-Path $appRoot 'tailwind.config.js'
$compiledCssPath = Join-Path $appRoot 'wwwroot\css\tailwind-3.4.17.min.css'
$mainLayoutPath = Join-Path $appRoot 'Components\Layout\MainLayout.razor'
$homePath = Join-Path $appRoot 'Components\Pages\Home.razor'
$interopPath = Join-Path $appRoot 'wwwroot\js\app-interop.js'
$securityHeadersPath = Join-Path $appRoot 'Security\SecurityHeadersMiddleware.cs'
$packageJsonPath = Join-Path $appRoot 'package.json'
$packageLockPath = Join-Path $appRoot 'package-lock.json'
$licensePath = Join-Path $appRoot 'wwwroot\licenses\Poppins-OFL-1.1.txt'
$manifestPath = Join-Path $appRoot 'wwwroot\fonts\poppins\manifest.json'

function Assert-Contains([string]$Content, [string]$Expected, [string]$Message) {
    if (-not $Content.Contains($Expected)) { throw $Message }
}

function Assert-NotMatch([string]$Content, [string]$Pattern, [string]$Message) {
    if ($Content -match $Pattern) { throw $Message }
}

foreach ($path in @(
    $appCssPath, $tailwindConfigPath, $compiledCssPath, $mainLayoutPath, $homePath,
    $interopPath, $securityHeadersPath, $packageJsonPath, $packageLockPath, $licensePath, $manifestPath
)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Falta el archivo requerido por M-307: $path"
    }
}

$appCss = Get-Content -LiteralPath $appCssPath -Raw
$tailwindConfig = Get-Content -LiteralPath $tailwindConfigPath -Raw
$compiledCss = Get-Content -LiteralPath $compiledCssPath -Raw
$mainLayout = Get-Content -LiteralPath $mainLayoutPath -Raw
$homePage = Get-Content -LiteralPath $homePath -Raw
$interop = Get-Content -LiteralPath $interopPath -Raw
$securityHeaders = Get-Content -LiteralPath $securityHeadersPath -Raw
$packageJson = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
$packageLock = Get-Content -LiteralPath $packageLockPath -Raw
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

if ($packageJson.devDependencies.'@fontsource/poppins' -ne '5.3.0') {
    throw 'package.json no fija @fontsource/poppins 5.3.0.'
}
Assert-Contains $packageLock '"node_modules/@fontsource/poppins"' 'package-lock.json no bloquea @fontsource/poppins.'
Assert-Contains $packageLock '"version": "5.3.0"' 'package-lock.json no fija la versión 5.3.0 de Poppins.'

if ($manifest.package -ne '@fontsource/poppins' -or $manifest.version -ne '5.3.0' -or $manifest.license -ne 'OFL-1.1') {
    throw 'El manifiesto de Poppins no identifica paquete, versión y licencia esperados.'
}

$expectedFonts = @(
    @{ weight = 300; style = 'normal' },
    @{ weight = 400; style = 'normal' },
    @{ weight = 500; style = 'normal' },
    @{ weight = 600; style = 'normal' },
    @{ weight = 700; style = 'normal' },
    @{ weight = 800; style = 'normal' },
    @{ weight = 900; style = 'normal' },
    @{ weight = 400; style = 'italic' },
    @{ weight = 500; style = 'italic' },
    @{ weight = 900; style = 'italic' }
)
foreach ($font in $expectedFonts) {
    $weight = $font.weight
    $style = $font.style
    $fileName = "poppins-latin-$weight-$style.woff2"
    $fontPath = Join-Path $appRoot "wwwroot\fonts\poppins\$fileName"
    if (-not (Test-Path -LiteralPath $fontPath -PathType Leaf)) {
        throw "Falta Poppins $weight $style en formato WOFF2."
    }

    $entry = @($manifest.files | Where-Object { $_.name -eq $fileName })
    if ($entry.Count -ne 1 -or $entry[0].sha256 -notmatch '^[a-f0-9]{64}$') {
        throw "El manifiesto no contiene un hash SHA-256 válido para $fileName."
    }

    $actualHash = (Get-FileHash -LiteralPath $fontPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $entry[0].sha256) {
        throw "El hash SHA-256 no coincide para $fileName."
    }

    $signature = [System.Text.Encoding]::ASCII.GetString([System.IO.File]::ReadAllBytes($fontPath), 0, 4)
    if ($signature -ne 'wOF2') {
        throw "$fileName no contiene una firma WOFF2 válida."
    }

    Assert-Contains $appCss "font-weight: $weight;" "app.css no declara el peso Poppins $weight."
    Assert-Contains $appCss "font-style: $style;" "app.css no declara el estilo Poppins $style."
    Assert-Contains $appCss "fonts/poppins/$fileName" "app.css no referencia el activo local $fileName."
}

if (([regex]::Matches($appCss, '@font-face')).Count -ne $expectedFonts.Count) {
    throw 'app.css no declara exactamente las diez variantes Poppins requeridas por la interfaz.'
}
Assert-Contains $appCss 'font-family: "Poppins";' 'Las declaraciones @font-face no usan la familia Poppins.'
Assert-Contains $appCss 'font-display: swap;' 'Poppins no usa font-display: swap.'
Assert-Contains $appCss 'font-family: "Poppins", system-ui, sans-serif;' 'La fuente base global no prioriza Poppins con fallbacks locales.'
Assert-NotMatch $appCss '(?i)@import|https?://|fonts\.googleapis\.com|fonts\.gstatic\.com' 'app.css conserva una importación u origen tipográfico externo.'

Assert-Contains $tailwindConfig 'sans: ["Poppins", "system-ui", "sans-serif"]' 'Tailwind no define Poppins como token font-sans.'
Assert-Contains $compiledCss '.font-sans{font-family:Poppins,system-ui,sans-serif}' 'El CSS compilado no resuelve font-sans a Poppins.'
Assert-Contains $mainLayout 'font-sans' 'MainLayout no usa el token tipográfico semántico de Tailwind.'

Assert-NotMatch $homePage '(?i)family\s*=\s*"system-ui"' 'Chart.js conserva sobrescrituras locales con system-ui.'
Assert-Contains $interop 'Chart.defaults.font.family = "Poppins, system-ui, sans-serif";' 'Chart.js no usa Poppins como familia predeterminada.'
Assert-Contains $securityHeaders 'font-src ''self''' 'La CSP no limita las fuentes al origen propio.'

$license = Get-Content -LiteralPath $licensePath -Raw
Assert-Contains $license 'SIL Open Font License, Version 1.1' 'No se conserva la licencia OFL 1.1 de Poppins.'

Write-Output 'M-307 static verification passed.'
