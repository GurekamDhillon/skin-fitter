# Third-party components

Skin Fitter itself is GPL-3.0-or-later (`LICENSE`). Nothing below is committed as a binary; the two .NET libraries
are git submodules built from source by `build.ps1`.

## HSDLib / HSDRaw (MIT)

- Source: https://github.com/Ploaj/HSDLib, submodule `deps/HSDLib`, commit `85567e40797de3c820a55476eca54c704921a848`.
- Used by: `costumebuild` (project reference to `HSDRaw.csproj`).
- Licence text: `licenses/HSDLib-MIT.txt`, reproduced here:

```
MIT License

Copyright (c) 2021 Ploaj

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## BrawlLib, part of BrawlCrate (LGPL-3.0)

- Source: https://github.com/soopercool101/BrawlCrate, submodule `deps/BrawlCrate`, commit
  `a4c1ee3385010d5c64a88df9c56267c61f213321`. BrawlLib copyright: soopercool101, NuclearNecrosis, Simon381; BrawlLib
  (BrawlBox) Bryan Moulton, BlackJax96, libertyernie, Sammi Husky (from `BrawlLib.csproj`).
- Used by: `brawlreader/model_export` and `brawlreader/brawl_matdump`, which link `BrawlLib.dll` dynamically.
- Licence: GNU LGPL v3, `licenses/LGPL-3.0.txt` (the repository's root LICENSE); the GPL v3 it refers to is `LICENSE`.
- Corresponding source: the submodule at that commit. To use a modified BrawlLib, build with
  `.\build.ps1 -BrawlLibProject <your BrawlLib.csproj>` or replace `BrawlLib.dll` next to the built executables.
- BrawlLib's NuGet dependencies (OpenTK, System.Buffers, System.Memory, System.IO.Hashing, ...; MIT) are restored
  from NuGet at build time, not committed.

## Python packages (installed by the user with pip, not bundled)

- **PySide6** (Qt for Python): LGPL-3.0 (or GPL-2.0/GPL-3.0, or commercial). Used unmodified as an installed library
  by `frontdoor.py` and `ui.py`. A frozen/bundled release must keep LGPL relinking possible.
- **NumPy**: BSD-3-Clause. Copyright (c) 2005-2025, NumPy Developers. Keep its notice if NumPy is ever bundled.

## Separate programs

- **DolphinTool** (Dolphin emulator project, GPL-2.0-or-later): optional, installed by the user, run as a separate
  process by `unpack_disc.py`. Not distributed with Skin Fitter.
- **.NET SDK / runtime** (MIT): installed by the user.

## Not used

m-ex, mexTool and MexManager/mexLib carry no licence and are not used, linked, copied or ported here.
