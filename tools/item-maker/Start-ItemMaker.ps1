param(
    [string]$Repository = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [int]$Port = 4173
)
$server = Join-Path $PSScriptRoot "server.js"
Start-Process "http://127.0.0.1:$Port/weapon-folder.html"
& node $server --repo $Repository --port $Port
