# CI build script: pack MOI thu vien co .nuspec (9 package TqkLibrary.Proxy.*) vao .\artifacts.
# KHONG nuget push -- release.yml lo phan publish (GitHub Release asset + nuget.org tuy chon).
# Version do GitVersion.MsBuild dat qua target SetVersionFromGitVersion (src\ProjectBuildProperties.targets)
# khi pack Release -> ten nupkg = <id>.<Major>.<Minor>.<commits>.nupkg.
# Resolve repo root tu vi tri script nen goi tu dau cung duoc.
$ErrorActionPreference = 'Stop'
# Quiet .NET first-run/telemetry banner de khong ban stdout.
$env:DOTNET_NOLOGO = 'true'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 'true'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root

$src = Join-Path $root 'src'
if (-not (Test-Path $src)) { throw "src folder not found: $src" }

# Clean output cu.
$artifacts = Join-Path $root 'artifacts'
Remove-Item -Recurse -Force $artifacts -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

# Packable = project co .nuspec cạnh csproj cung ten (test/demo + WPF khong co nuspec -> tu bo qua).
$nuspecs = @(Get-ChildItem -Path $src -Recurse -Filter *.nuspec)
if ($nuspecs.Count -eq 0) { throw "No .nuspec found under $src" }

$count = 0
foreach ($nuspec in $nuspecs) {
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
