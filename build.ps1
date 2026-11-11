# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 GD (Gurekam Dhillon)
# Builds Skin Fitter's helpers from source:
#   1. fetches HSDLib and BrawlCrate (BrawlLib) at the commits recorded as git submodules in deps/
#   2. costumebuild, mexcostume and its tests (net8.0, HSDLib/HSDRaw, MIT)
#   3. model_export, brawl_matdump (net472, BrawlLib LGPL-3.0) -> brawlreader\bin\Release\net472\
# Usage: .\build.ps1 [-BrawlLibProject C:\path\to\your\BrawlLib.csproj] [-SkipFetch] [-SkipBrawl]
param([string]$BrawlLibProject = "", [switch]$SkipFetch, [switch]$SkipBrawl)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (-not $SkipFetch) {
    git -c core.longpaths=true submodule update --init --depth 1 deps/HSDLib deps/BrawlCrate   # longpaths: BrawlCrate has very long file names
    if ($LASTEXITCODE -ne 0) { throw "submodule fetch failed" }
}

dotnet build costumebuild\costumebuild.csproj -c Release -nologo -m:2
if ($LASTEXITCODE -ne 0) { throw "costumebuild failed" }
dotnet build mexcostume\cs\mexcostume.csproj -c Release -nologo -m:2
if ($LASTEXITCODE -ne 0) { throw "mexcostume failed" }
dotnet build mexcostume\tests\mexcostume.tests.csproj -c Release -nologo -m:2
if ($LASTEXITCODE -ne 0) { throw "mexcostume tests failed to build" }

if (-not $SkipBrawl) {
    # BrawlLib has non-string .resx resources, which need the Visual Studio (full framework) MSBuild,
    # not `dotnet build`. Install "Visual Studio Build Tools" with the .NET desktop workload.
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    $msbuild = $null
    if (Test-Path $vswhere) {
        $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
    }
    if (-not $msbuild) { throw "Visual Studio MSBuild not found (needed for BrawlLib). Use -SkipBrawl to build costumebuild only." }
    $extra = @()
    if ($BrawlLibProject) { $extra += "-p:BrawlLibProject=$BrawlLibProject" }
    foreach ($p in "brawlreader\model_export\model_export.csproj", "brawlreader\brawl_matdump\brawl_matdump.csproj") {
        & $msbuild $p -restore -p:Configuration=Release -nologo -v:m @extra
        if ($LASTEXITCODE -ne 0) { throw "$p failed" }
    }
}
Write-Host "Built. Run: python test_synthetic.py   then   python frontdoor.py"
