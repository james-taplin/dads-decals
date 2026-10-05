# Rebuilds src/DadsDecals/Bundles/dadsdecals from unity/DadsDecalsShaders.
# Needs Unity 2019.4.40f1 (the game's version). Runs with a window because batch mode
# on this editor version doesn't pick up the Unity Hub licence.
param(
    [string]$Unity = "B:\Games\Unity 2019.4.40f1\Editor\Unity.exe"
)
$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "unity\DadsDecalsShaders"
$log = Join-Path $env:TEMP "dadsdecals-bundle.log"
if (Test-Path $log) { Remove-Item $log }

# Don't use -Wait: Unity's licensing client outlives the editor and Start-Process would wait on it.
$p = Start-Process -FilePath $Unity -ArgumentList "-quit", "-projectPath", "`"$project`"", "-executeMethod", "BuildBundle.Build", "-logFile", "`"$log`"" -PassThru
$p.WaitForExit()

$text = Get-Content $log -Raw
if ($text -match "DADSDECALS_BUILD_OK") { Write-Host "Bundle built." }
else {
    Select-String -Path $log -Pattern "error|DADSDECALS" | Select-Object -Last 20 | ForEach-Object { $_.Line }
    throw "Bundle build failed, see $log"
}
