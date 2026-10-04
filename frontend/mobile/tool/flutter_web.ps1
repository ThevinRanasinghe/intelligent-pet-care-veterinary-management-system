# Runs a Flutter command with the Google Maps JS API key injected as a
# --dart-define, so the web build's clinic picker works without the key ever
# living in web/index.html (which is tracked).
#
# Key resolution order:
#   1. $env:GOOGLE_MAPS_API_KEY
#   2. GOOGLE_MAPS_API_KEY=<key> in android/local.properties (gitignored)
#
# Usage:
#   .\tool\flutter_web.ps1 run -d chrome --web-port=5174
#   .\tool\flutter_web.ps1 build web
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$FlutterArgs)

$projectDir = Split-Path -Parent $PSScriptRoot
$mapsKey = $env:GOOGLE_MAPS_API_KEY

if (-not $mapsKey) {
    $localProps = Join-Path $projectDir 'android\local.properties'
    if (Test-Path $localProps) {
        $match = Select-String -Path $localProps -Pattern '^GOOGLE_MAPS_API_KEY=(.+)$'
        if ($match) { $mapsKey = $match.Matches[0].Groups[1].Value.Trim() }
    }
}

Push-Location $projectDir
try {
    if ($mapsKey) {
        & flutter @FlutterArgs "--dart-define=GOOGLE_MAPS_API_KEY=$mapsKey"
    } else {
        Write-Host 'No GOOGLE_MAPS_API_KEY found — the clinic-picker map will fall back to the clinic list.'
        & flutter @FlutterArgs
    }
    exit $LASTEXITCODE
} finally {
    Pop-Location
}
