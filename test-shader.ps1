# Renders the decal shader offline (unity/DadsDecalsShaders, ShaderTestRender) under deferred and
# forward cameras. Output: unity/DadsDecalsShaders/TestRenders/<Tag>deferred.png / <Tag>forward.png
#   .\test-shader.ps1 -Tag grime- -Props "_Grime=1"
param(
    [string]$Tag = "",
    [string]$Props = "",
    [string]$Unity = "B:\Games\Unity 2019.4.40f1\Editor\Unity.exe"
)
$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "unity\DadsDecalsShaders"
$log = Join-Path $env:TEMP "dadsdecals-test.log"
$env:DADSDECALS_TEST_TAG = $Tag
$env:DADSDECALS_TEST_PROPS = $Props
$p = Start-Process -FilePath $Unity -ArgumentList "-quit", "-projectPath", "`"$project`"", "-executeMethod", "ShaderTestRender.Run", "-logFile", "`"$log`"" -PassThru
$p.WaitForExit()
Remove-Item Env:DADSDECALS_TEST_TAG, Env:DADSDECALS_TEST_PROPS -ErrorAction SilentlyContinue
if (-not (Select-String -CaseSensitive -Path $log -Pattern "DADSDECALS_TEST_OK" -Quiet)) {
    Select-String -CaseSensitive -Path $log -Pattern "Shader error|error CS|Exception" | Select-Object -First 10 | ForEach-Object { $_.Line }
    throw "Shader test failed, see $log"
}
