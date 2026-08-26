Set-StrictMode -Version Latest

function Write-OperationalLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $Operation,

        [Parameter(Mandatory)]
        [ValidateSet('started', 'succeeded', 'failed', 'warning')]
        [string] $Status,

        [ValidateSet('Information', 'Warning', 'Error')]
        [string] $Level = 'Information',

        [Parameter(Mandatory)]
        [string] $CorrelationId,

        [string] $Message,

        [hashtable] $Properties = @{},

        [System.Exception] $Exception
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $directory = Split-Path -Parent $resolvedPath
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $entry = [ordered]@{
        timestamp = [DateTimeOffset]::Now.ToString('o')
        level = $Level
        operation = $Operation
        status = $Status
        correlationId = $CorrelationId
        message = $Message
        properties = $Properties
    }

    if ($null -ne $Exception) {
        $entry.exceptionType = $Exception.GetType().FullName
        $entry.exception = $Exception.ToString()
    }

    $entry | ConvertTo-Json -Compress -Depth 8 | Add-Content -LiteralPath $resolvedPath -Encoding utf8
}
