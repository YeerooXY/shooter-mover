param(
    [string]$Repository = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [int]$Port = 4174
)
$server = Join-Path $PSScriptRoot "server.js"
Start-Process "http://127.0.0.1:$Port/"
& node $server --repo $Repository --port $Port
