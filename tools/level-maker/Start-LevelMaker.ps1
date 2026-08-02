$ErrorActionPreference = "Stop"
$tool = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Resolve-Path (Join-Path $tool "..\..")
Start-Process "http://127.0.0.1:4174"
& node (Join-Path $tool "start-server.js") --repo $repo --port 4174
