# CI build script: pack the libraries that have a .nuspec (EXCEPT the exclude list below) into .\artifacts.
# NO nuget push -- release.yml handles publishing (GitHub Release asset + optional nuget.org).
# Version is set by GitVersion.MsBuild via the SetVersionFromGitVersion target
# (src\ProjectBuildProperties.targets) when packing Release -> nupkg name = <id>.<Major>.<Minor>.<commits>.nupkg.
# Resolves the repo root from this script's location, so it can be invoked from anywhere.
$ErrorActionPreference = 'Stop'
# Quiet the .NET first-run/telemetry banner so it does not contaminate stdout.
$env:DOTNET_NOLOGO = 'true'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 'true'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root

$src = Join-Path $root 'src'
if (-not (Test-Path $src)) { throw "src folder not found: $src" }

# Clean previous output.
$artifacts = Join-Path $root 'artifacts'
Remove-Item -Recurse -Force $artifacts -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

# Packages EXCLUDED from CI (not packed/published). Wildcard matched against the package name (nuspec BaseName).
# 'TqkLibrary.Proxy.Reverse*' covers both the base Reverse package and the Reverse.Transport.* packages.
$excludePatterns = @(
    'TqkLibrary.Proxy.Reverse*',
    'TqkLibrary.Proxy.Vpn.WireProxyCli'
)

# Packable = a project with a sibling .nuspec of the same name (test/demo + WPF have no nuspec -> skipped).
$nuspecs = @(Get-ChildItem -Path $src -Recurse -Filter *.nuspec)
if ($nuspecs.Count -eq 0) { throw "No .nuspec found under $src" }

$count = 0
foreach ($nuspec in $nuspecs) {
    $excluded = $false
    foreach ($p in $excludePatterns) { if ($nuspec.BaseName -like $p) { $excluded = $true; break } }
    if ($excluded) {
        Write-Host "Skip $($nuspec.BaseName) (excluded from CI)"
        continue
    }
    $csproj = Join-Path $nuspec.DirectoryName ($nuspec.BaseName + '.csproj')
    if (-not (Test-Path $csproj)) {
        Write-Host "::warning::Skip $($nuspec.Name) (no sibling $($nuspec.BaseName).csproj)"
        continue
    }
    Write-Host "Packing $($nuspec.BaseName) ..."
    dotnet pack $csproj -c Release -o $artifacts
    if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed ($($nuspec.BaseName))" }
    $count++
}

$packed = @(Get-ChildItem $artifacts -Filter *.nupkg)
Write-Host "Packed $count project(s), $($packed.Count) nupkg -> $artifacts"
if ($packed.Count -eq 0) { throw "No .nupkg produced" }
