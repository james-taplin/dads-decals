# Builds the mod and packages a Unity Mod Manager zip: dist/DadsDecals-<version>.zip
# containing DadsDecals/{DadsDecals.dll, info.json, LICENSE, Bundles/dadsdecals, Decals/...}.
$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$src = Join-Path $repo "src\DadsDecals"
$version = (Get-Content (Join-Path $src "info.json") -Raw | ConvertFrom-Json).Version

& "C:\Program Files\dotnet\dotnet.exe" build $src -c Release | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$stageRoot = Join-Path $repo "dist\stage"
$stage = Join-Path $stageRoot "DadsDecals"
if (Test-Path $stageRoot) { [IO.Directory]::Delete($stageRoot, $true) }
New-Item -ItemType Directory -Force (Join-Path $stage "Bundles") | Out-Null
Copy-Item (Join-Path $src "bin\Release\DadsDecals.dll"), (Join-Path $src "info.json"), (Join-Path $repo "LICENSE") $stage
Copy-Item (Join-Path $src "Bundles\dadsdecals") (Join-Path $stage "Bundles")
Copy-Item (Join-Path $repo "examples\Decals") (Join-Path $stage "Decals") -Recurse

$zip = Join-Path $repo "dist\DadsDecals-$version.zip"
if (Test-Path $zip) { [IO.File]::Delete($zip) }
# Built entry by entry: ZipFile.CreateFromDirectory on .NET Framework writes backslash paths,
# which some unzippers and mod installers mishandle.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem $stageRoot -Recurse -File) {
        $entry = $file.FullName.Substring($stageRoot.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }
[IO.Directory]::Delete($stageRoot, $true)
Write-Host "Packaged $zip"
