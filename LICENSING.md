# Licensing checklist for Skin Fitter

This is an engineering compliance checklist, not legal advice. Anything marked **uncertain** needs a human decision
or a check by someone qualified before a public release. Carried over from the original audit (2026-11-11) and
updated for this standalone repository.

## Components

| Component | Source, commit | How the tool uses it | Licence | Obligations / status |
|---|---|---|---|---|
| Our code: `*.py`, `tools/*.py`, `costumebuild/`, `brawlreader/`, `mexcostume/` (stub) | this repository | — | **GPL-3.0-or-later**, Copyright (C) 2026 GD (Gurekam Dhillon) | `LICENSE`; SPDX headers in every source file |
| `mex_hsd.py`, `melee_fighters.py` | vendored from the author's own workspace tools (`mex_hsd.py` whole; `melee_fighters.py` is the `FIGHTERS` and `COMMON` tables trimmed from `build_melee_fighter.py`) | GCM/HSD reading; fighter codes and PlCo common joint names | the project's own code, git author "GD" | **Uncertain** (from the original audit): whether `mex_hsd.py` was written with reference to HSDLib or m-ex sources was not established from its history; it is a plain reader of the documented HSD archive layout |
| `model_export.cs`, `brawl_matdump.cs` | the author's own code (git author "GD") | Brawl model reader | ours | use BrawlLib's API; copy no BrawlLib source |
| HSDLib / HSDRaw | github.com/Ploaj/HSDLib, submodule at upstream `85567e40797de3c820a55476eca54c704921a848` | `costumebuild` project reference | **MIT** (Copyright (c) 2021 Ploaj) | licence text in `licenses/HSDLib-MIT.txt` and `THIRD_PARTY.md`. Note: the original audit named commit `6d45b7f4`; that is the author's local fork commit (two `POBJ_Generator` additions on top of upstream `85567e4`) and cannot be fetched from GitHub. `costumebuild` uses none of those additions; the synthetic test passes against upstream `85567e4` |
| BrawlLib (BrawlCrate) | github.com/soopercool101/BrawlCrate, submodule at `a4c1ee3385010d5c64a88df9c56267c61f213321` | linked dynamically by `model_export` / `brawl_matdump` | **LGPL-3.0** (repository root LICENSE; `BrawlLib/` has no file of its own) | built from source by `build.ps1`; text in `licenses/LGPL-3.0.txt`; users can rebuild or swap `BrawlLib.dll` (`-BrawlLibProject`). **Uncertain:** whether older files carry BrawlBox-era headers with other terms; not audited file by file |
| BrawlLib's NuGet dependencies (OpenTK, System.*) | NuGet, restored at build time | loaded by the reader | MIT | not committed; keep notices if binaries are ever distributed |
| PySide6 | pip | `ui.py`, `frontdoor.py` | LGPL-3.0 / GPL / commercial | installed by the user; a frozen bundle must keep relinking possible |
| NumPy | pip | `skin.py`, `autogroups.py`, `ui.py`, tests | BSD-3-Clause | keep notice if bundled |
| .NET SDK / runtime | user-installed | runs the C# helpers | MIT | none if not bundled |
| DolphinTool | user-installed Dolphin | separate process (`unpack_disc.py`) | GPL-2.0-or-later | not distributed; documented as a prerequisite |
| m-ex, mexTool, MexManager / mexLib | — | **not used** | no licence: all rights reserved | nothing from them may be linked, copied or shipped. The m-ex slot install step is absent until reimplemented clean-room (`mexcostume/` is a stub) |
| Game data (Melee, Brawl) | the user's own discs | read at run time | Nintendo's copyright | never shipped |

## Why GPL-3.0-or-later

BrawlLib is LGPL-3.0, which is compatible with GPL-3.0 but not GPL-2.0-only. MIT (HSDLib, OpenTK), BSD (NumPy) and
LGPL-3.0 (PySide6) are all compatible with GPL-3.0. **Uncertain:** a future GPL-2.0-only dependency would be blocked.

## Release checklist (state in this repository)

1. `LICENSE` (GPL-3.0 text) and SPDX headers: done.
2. Third-party notices: `THIRD_PARTY.md`, `licenses/`: done.
3. Complete corresponding source: `costumebuild`, `model_export`, `brawl_matdump` and their `.csproj` files are
   here; HSDLib and BrawlLib are submodules at recorded commits; `build.ps1` / `build.sh` rebuild everything.
   If binaries are ever published, publish them from a tagged commit of this repo and keep the submodule commits
   reachable (or ship the BrawlLib source alongside, LGPL §4).
4. Rebuild / replace instructions: `README.md` "Build" and "Using your own BrawlLib": done.
5. Tests on synthetic data only (`test_synthetic.py`) and docs: done.

## Must NOT be in the repository or a release

- Anything from m-ex, mexTool or MexManager/mexLib, including the old `mexcostume` implementation from the
  workspace (written after reading mexLib; not clean-room). Only a stub interface is here.
- Any disc-derived file: models, textures, costumes, `.pac`/`.pcs`, `.dat`, disc images, and the
  `<fighter>.bones.json` / `<fighter>.roles.json` tables (owner decision: users generate them on first use;
  `.gitignore` blocks them under `maps/`).
- Prebuilt third-party binaries (`BrawlLib.dll`, `OpenTK.dll`, `System.*.dll`, `HSDRaw.dll`): `.gitignore` blocks
  `*.dll` and `*.exe`.
- Screenshots that show game models or the author's local paths. `docs/` holds four screenshots of the synthetic
  test model only.
