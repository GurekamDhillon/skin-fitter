# Changelog

## 0.1.1

Fixes from the first real conversion.

- **Triangle limit matches the documented 1.6x.** The check used to allow the budget *plus* the retail triangle count
  (about 2.6x retail). The limit is now 1.6x retail over the whole costume: kept and blanked slots count against it,
  and the reduction step gives the replaced meshes what is left. The file-size check was already 1.6x.
- **Fighter detection uses the source's name first.** `FitLuigi00.pac` or a `fighter/luigi/` folder picks Luigi
  (a Brawl-to-Melee name table covers the fighters in both games). Bone-name scoring is the fallback; when two
  fighters score within 0.1, both are shown and the result is marked "Needs review" (Mario and Luigi share a rig).
- **Layered eyes are baked automatically.** `model_export` now writes UV sets 2 to 7 (`uvx`, `uv_sets`) for objects
  that have them. `skin.py build` finds materials that sample a UV set beyond the first two, bakes their layers into
  one static RGBA8 texture in UV-set-0 space, and reports "eyes baked from N layers; static, no blink" in the checks.
  `--no-bake-eyes` turns it off. New `gxdecode.py` decodes the GX texture formats for this.
- **Colour slots are checked against their own retail file.** With `--slot XX`, the installed, retagged file is
  compared with `Pl<code>XX.dat` by `jointdiff` when that file is next to `--retail`; the front door fetches it.
- Tests: detection by name and near-ties, a 4-UV-set layered eye, and the expected file-size failure are asserted.

## 0.1.0-alpha

First public release.

- Brawl -> Melee costume converter: `skin.py` (CLI), the front door wizard, the advanced window, autogroups,
  bone-map templates generated from your own files on first use.
- `costumebuild` (C#, HSDLib) and the Brawl reader (`model_export`, `brawl_matdump`, C#, BrawlLib), built from source
  with `build.ps1`; dependencies as git submodules at recorded commits.
- `mexcostume`: clean-room m-ex costume rows (list, verify, add, replace, remove, undo) with undo records.
- GPL-3.0-or-later; no game data in the repository.
