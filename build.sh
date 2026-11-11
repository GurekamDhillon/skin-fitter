#!/usr/bin/env bash
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 GD (Gurekam Dhillon)
# Fetches deps at the recorded submodule commits and builds costumebuild (net8.0).
# The Brawl reader (net472 + BrawlLib) is Windows-only: on Windows run build.ps1, or pass --brawl here to
# call build.ps1 through PowerShell (Git Bash).
set -euo pipefail
cd "$(dirname "$0")"
git -c core.longpaths=true submodule update --init --depth 1 deps/HSDLib deps/BrawlCrate   # longpaths: BrawlCrate has very long file names
dotnet build costumebuild/costumebuild.csproj -c Release -nologo -m:2
dotnet build mexcostume/cs/mexcostume.csproj -c Release -nologo -m:2
dotnet build mexcostume/tests/mexcostume.tests.csproj -c Release -nologo -m:2
if [ "${1:-}" = "--brawl" ]; then powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1 -SkipFetch; fi
