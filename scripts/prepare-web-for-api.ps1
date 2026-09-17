$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$web = Join-Path $root "src/RelayRoom.Web"
$wwwroot = Join-Path $root "src/RelayRoom.Api/wwwroot"

Write-Host "Building RelayRoom.Web into API wwwroot..."
corepack pnpm --dir $web install --frozen-lockfile
corepack pnpm --dir $web build

if (Test-Path $wwwroot) {
    Remove-Item $wwwroot -Recurse -Force
}
New-Item -ItemType Directory -Path $wwwroot | Out-Null
Copy-Item -Path (Join-Path $web "dist/*") -Destination $wwwroot -Recurse
Write-Host "Copied $web/dist -> $wwwroot"
