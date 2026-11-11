# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 GD (Gurekam Dhillon)
"""Where Skin Fitter keeps things. Nothing here points outside the repository except the per-user data folder.

    repo                    this folder (code, the example slot config, docs)
    data   SKIN_FITTER_DATA %LOCALAPPDATA%/skin-fitter (Windows) or $XDG_DATA_HOME/skin-fitter (~/.local/share)
      maps/                 bone maps and role tables generated from YOUR files on first use (never shipped)
      work/                 scratch folders (SKIN_FITTER_WORK overrides)
      mods/                 default install folder for converted costumes
    tools                   SKIN_COSTUMEBUILD, SKIN_MODEL_EXPORT override the built helper paths
    Melee disc image        SKIN_MELEE_IMAGE (GW_ISO_VANILLA is accepted too). The path is never printed.
"""
import os, subprocess, sys

REPO = os.path.dirname(os.path.abspath(__file__))


def _default_data():
    if os.name == "nt":
        base = os.environ.get("LOCALAPPDATA") or os.path.join(os.path.expanduser("~"), "AppData", "Local")
    else:
        base = os.environ.get("XDG_DATA_HOME") or os.path.join(os.path.expanduser("~"), ".local", "share")
    return os.path.join(base, "skin-fitter")


DATA = os.environ.get("SKIN_FITTER_DATA") or _default_data()
WORK = os.environ.get("SKIN_FITTER_WORK") or os.path.join(DATA, "work")
MAPS = os.path.join(DATA, "maps")            # generated, per user
REPO_MAPS = os.path.join(REPO, "maps")       # shipped: examples only, no disc-derived tables
MODS = os.path.join(DATA, "mods")

_exe = ".exe" if os.name == "nt" else ""
COSTUMEBUILD = os.environ.get("SKIN_COSTUMEBUILD") or os.path.join(REPO, "costumebuild", "bin", "Release", "net8.0", "costumebuild" + _exe)
MODEL_EXPORT = os.environ.get("SKIN_MODEL_EXPORT") or os.path.join(REPO, "brawlreader", "bin", "Release", "net472", "model_export.exe")


def work(name):
    d = os.path.join(WORK, name); os.makedirs(d, exist_ok=True); return d


def melee_image(required=True):
    p = os.environ.get("SKIN_MELEE_IMAGE") or os.environ.get("GW_ISO_VANILLA")
    if not p and required:
        sys.exit("No Melee disc image: set SKIN_MELEE_IMAGE to your own disc image (the path is never printed)")
    return p


def map_file(name):
    """A map/table by file name: the user's generated folder first, then the repo's examples. None if absent."""
    for d in (MAPS, REPO_MAPS):
        p = os.path.join(d, name)
        if os.path.exists(p): return p
    return None


def ensure_maps(brawl_fighter_dir=None, log=print):
    """First-use generation of the disc-derived tables into MAPS (they are not shipped):
    <fighter>.roles.json via `autogroups.py tables` (needs the Melee image), <fighter>.bones.json via
    `make_templates.py --brawl <dir>` (needs the Melee image and an unpacked Brawl fighter/ folder)."""
    os.makedirs(MAPS, exist_ok=True)
    if not melee_image(required=False): return
    have = os.listdir(MAPS)
    if not any(f.endswith(".roles.json") for f in have):
        log("first use: generating role tables from your Melee disc into the user data folder")
        subprocess.run([sys.executable, os.path.join(REPO, "autogroups.py"), "tables"], capture_output=True, text=True)
    if brawl_fighter_dir and os.path.isdir(brawl_fighter_dir) and not any(f.endswith(".bones.json") for f in have):
        log("first use: generating bone-map templates from your Brawl and Melee files into the user data folder")
        subprocess.run([sys.executable, os.path.join(REPO, "make_templates.py"), "--brawl", brawl_fighter_dir], capture_output=True, text=True)
