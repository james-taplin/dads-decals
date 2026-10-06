# Builds the mod and packages a Unity Mod Manager zip: dist/DadsDecals-<version>.zip
# containing DadsDecals/{DadsDecals.dll, DadsDecals.Multiplayer.dll, info.json, LICENSE, Bundles/dadsdecals, Decals/...}.
$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$src = Join-Path $repo "src\DadsDecals"
# Zip name: the full version from the csproj (e.g. 0.4.0-alpha.1); info.json holds the plain number UMM reads.
$version = [regex]::Match((Get-Content (Join-Path $src "DadsDecals.csproj") -Raw), '<Version>([^<]+)</Version>').Groups[1].Value
if (-not $version) { throw "No <Version> in DadsDecals.csproj" }

# Building the multiplayer project builds DadsDecals too.
& "C:\Program Files\dotnet\dotnet.exe" build (Join-Path $repo "src\DadsDecals.Multiplayer") -c Release | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$stageRoot = Join-Path $repo "dist\stage"
$stage = Join-Path $stageRoot "DadsDecals"
if (Test-Path $stageRoot) { [IO.Directory]::Delete($stageRoot, $true) }
New-Item -ItemType Directory -Force (Join-Path $stage "Bundles") | Out-Null
Copy-Item (Join-Path $src "bin\Release\DadsDecals.dll"), (Join-Path $src "info.json"), (Join-Path $repo "LICENSE") $stage
# Loaded only when the Multiplayer mod is installed; MultiplayerAPI.dll itself is NOT shipped.
Copy-Item (Join-Path $repo "src\DadsDecals.Multiplayer\bin\Release\DadsDecals.Multiplayer.dll") $stage
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
