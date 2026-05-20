$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$luban = Join-Path $root "Tools\Luban\v4.7.0\Luban\Luban.exe"
$conf = Join-Path $PSScriptRoot "luban.conf"
$outputData = Join-Path $PSScriptRoot "Generated\json"
$outputCode = Join-Path $root "Shared\DG.GameCore\Configuration\Generated\LubanTables"
$unityStreamingData = Join-Path $root "Client\DG_Client\Assets\StreamingAssets\GameConfig"

if (!(Test-Path $luban)) {
    throw "Luban executable not found: $luban"
}

New-Item -ItemType Directory -Force $outputData | Out-Null
New-Item -ItemType Directory -Force $outputCode | Out-Null
New-Item -ItemType Directory -Force $unityStreamingData | Out-Null

& $luban -t all -c cs-newtonsoft-json -d json --conf $conf -x "outputCodeDir=$outputCode" -x "outputDataDir=$outputData"
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Copy-Item -Path (Join-Path $outputData "*.json") -Destination $unityStreamingData -Force
