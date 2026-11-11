# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 GD (Gurekam Dhillon)
"""STUB: the m-ex costume-slot install step. NOT IMPLEMENTED in this repository.

Converted costumes are currently installed as a files-only local mod (`skin.py build --install`), either as a
new costume file described in mod.json or as a replacement of an existing colour file (`--slot Gr`). Writing a new
costume row into an m-ex build is being reimplemented from a written specification (clean room) and will land
here. Nothing from m-ex, mexTool or MexManager/mexLib may be copied into this package (see LICENSING.md).

Planned interface (subject to change):

    install_costume(mex_root, fighter, costume_dat, name, *, stock_icon=None, csp=None, dry_run=False) -> SlotResult
        mex_root     an extracted m-ex build folder owned by the user
        fighter      Skin Fitter fighter key ("luigi", ...)
        costume_dat  a costume file produced by `skin.py build`
        name         display name for the new costume
    Returns which costume index was added and which files were written.
"""


class NotImplementedYet(NotImplementedError):
    pass


class SlotResult:
    def __init__(self, costume_index, files_written):
        self.costume_index = costume_index
        self.files_written = list(files_written)


def install_costume(mex_root, fighter, costume_dat, name, *, stock_icon=None, csp=None, dry_run=False):
    raise NotImplementedYet("the m-ex slot install step is being reimplemented; use `skin.py build --install` "
                            "(local mod) or `--slot XX` (replace a colour) for now")
