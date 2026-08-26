$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$appPath = Join-Path $root 'LaPrimitiva.App\Components\App.razor'
$programPath = Join-Path $root 'LaPrimitiva.App\Program.cs'
$middlewarePath = Join-Path $root 'LaPrimitiva.App\Security\SecurityHeadersMiddleware.cs'
$appCssPath = Join-Path $root 'LaPrimitiva.App\wwwroot\app.css'
$tailwindPath = Join-Path $root 'LaPrimitiva.App\wwwroot\css\tailwind-3.4.17.min.css'
$chartPath = Join-Path $root 'LaPrimitiva.App\wwwroot\lib\chart.js\4.5.1\chart.umd.min.js'
$interopPath = Join-Path $root 'LaPrimitiva.App\wwwroot\js\app-interop.js'
$packagePath = Join-Path $root 'LaPrimitiva.App\package.json'

function Assert-Contains {
    param([string]$Content, [string]$Pattern, [string]$Message)

    if ($Content -notmatch $Pattern) {
        throw $Message
    }
}

function Assert-NotContains {
    param([string]$Content, [string]$Pattern, [string]$Message)

    if ($Content -match $Pattern) {
        throw $Message
    }
}

$app = Get-Content -Raw $appPath
$program = Get-Content -Raw $programPath
$appCss = Get-Content -Raw $appCssPath

Assert-NotContains $app 'https?://' 'App.razor must not load assets from external origins.'
Assert-NotContains $app '<script(?![^>]*\bsrc=)[^>]*>' 'App.razor must not contain inline script blocks.'
Assert-NotContains $app '<ImportMap\s*/>' 'The generated inline import map is incompatible with a nonce-free CSP.'
Assert-Contains $app 'tailwind-3\.4\.17\.min\.css' 'App.razor must load the pinned local Tailwind stylesheet.'
Assert-Contains $app 'chart\.js/4\.5\.1/chart\.umd\.min\.js' 'App.razor must load pinned local Chart.js.'
Assert-Contains $app 'js/app-interop\.js' 'App.razor must load the local interop script.'
Assert-NotContains $appCss '@import\s+url\s*\(\s*["'']?https?://' 'app.css must not import remote fonts or stylesheets.'

$razorWithInlineStyles = Get-ChildItem (Join-Path $root 'LaPrimitiva.App\Components') -Recurse -File -Filter '*.razor' |
    Select-String -Pattern '\bstyle\s*=|<style\b'
if ($razorWithInlineStyles) {
    throw 'Razor components must not contain inline style attributes or blocks because CSP does not allow unsafe-inline styles.'
}

foreach ($asset in @($tailwindPath, $chartPath, $interopPath)) {
    if (-not (Test-Path -LiteralPath $asset -PathType Leaf)) {
        throw "Required self-hosted asset is missing: $asset"
    }
}

if ((Get-Item $tailwindPath).Length -lt 10000) {
    throw 'The compiled Tailwind stylesheet is unexpectedly small.'
}

if ((Get-Item $chartPath).Length -lt 100000) {
    throw 'The vendored Chart.js bundle is unexpectedly small.'
}

if ((Get-FileHash $tailwindPath -Algorithm SHA256).Hash -ne '1858836721B81C5C72F25EEB1D5DE24CCCC7457D03FF817BEF3F2293F67EE99F') {
    throw 'The compiled Tailwind asset does not match the reviewed M-307 typography output.'
}

if ((Get-FileHash $chartPath -Algorithm SHA256).Hash -ne '48444A82D4EDCB5BEC0F1965FAACDDE18D9C17DB3063D042ABADA2F705C9F54A') {
    throw 'The vendored Chart.js asset does not match version 4.5.1 reviewed for M-302.'
}

$package = Get-Content -Raw $packagePath | ConvertFrom-Json
if ($package.dependencies.'chart.js' -ne '4.5.1' -or $package.devDependencies.tailwindcss -ne '3.4.17') {
    throw 'Web dependencies must remain pinned to exact reviewed versions.'
}

$breakdownBar = Get-Content -Raw (Join-Path $root 'LaPrimitiva.App\Components\Shared\BreakdownBar.razor')
Assert-Contains $breakdownBar 'InvariantCulture' 'Dynamic SVG dimensions must use culture-invariant decimal formatting.'

Assert-Contains $program 'UseMiddleware<SecurityHeadersMiddleware>\(\)' 'Program.cs must enable the security headers middleware.'

if (-not (Test-Path -LiteralPath $middlewarePath -PathType Leaf)) {
    throw 'SecurityHeadersMiddleware.cs is missing.'
}

$middleware = Get-Content -Raw $middlewarePath
Assert-Contains $middleware 'Content-Security-Policy' 'The middleware must emit Content-Security-Policy.'
Assert-Contains $middleware "script-src 'self'" 'CSP must restrict scripts to the same origin.'
Assert-Contains $middleware "style-src 'self'" 'CSP must restrict styles to the same origin.'
Assert-Contains $middleware "connect-src 'self'" 'CSP must restrict connections to the same origin and its exact WebSocket endpoint.'
Assert-Contains $middleware "object-src 'none'" 'CSP must disable object embedding.'
Assert-Contains $middleware "frame-ancestors 'none'" 'CSP must prevent framing.'
Assert-NotContains $middleware "unsafe-inline|unsafe-eval" 'CSP must not allow unsafe inline or eval execution.'
Assert-NotContains $middleware '\*' 'CSP must not contain wildcard sources.'
Assert-Contains $middleware 'X-Content-Type-Options' 'The middleware must emit X-Content-Type-Options.'
Assert-Contains $middleware 'nosniff' 'X-Content-Type-Options must be nosniff.'
Assert-Contains $middleware 'Referrer-Policy' 'The middleware must emit Referrer-Policy.'
Assert-Contains $middleware 'no-referrer' 'Referrer-Policy must be no-referrer.'

Write-Host 'M-302 static verification passed.' -ForegroundColor Green
