param(
    [string]$Root = "D:\SteamLibrary\steamapps\common\Nuclear Option"
)
$ErrorActionPreference = "Stop"

dotnet build -c Debug
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$scriptsDir = Join-Path $Root "BepInEx\scripts"
$pluginFile = Join-Path $Root "BepInEx\plugins\NOVor.dll"

Remove-Item -LiteralPath $pluginFile -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Deployed to: $scriptsDir\NOVor.dll"
Write-Host "Removed old plugins/ copy so ScriptEngine is the single loader."
Write-Host ""
Write-Host "Hot reload: edit code, run this script, then press Insert in-game (ScriptEngine watcher also auto-reloads after ~3s)."
