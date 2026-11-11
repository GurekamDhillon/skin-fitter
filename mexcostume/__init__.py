# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 GD (Gurekam Dhillon)
"""Thin wrapper over the `mexcostume` C# tool (mexcostume/cs): add a converted costume as a new m-ex costume row in an
extracted m-ex build folder (sys/ + files/), and list / verify / replace / remove / undo.

Written clean-room from a prose specification of the on-disc tables and HSDLib's MIT-licensed HSDRaw.MEX accessors;
nothing from m-ex, mexTool or MexManager/mexLib is used. Every edit writes an undo record with backups first
(<build>/skinfitter-undo/) and re-verifies the build afterwards; `undo()` restores the files byte for byte.

    import mexcostume
    mexcostume.add_costume(build_dir, "luigi", "out/PlLgXx.dat")      # -> (ok, output)
"""
import json, os, re, shutil, subprocess, sys, tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
sys.path.insert(0, REPO)
import paths  # noqa: E402

_exe = ".exe" if os.name == "nt" else ""
TOOL = os.environ.get("SKIN_MEXCOSTUME") or os.path.join(HERE, "cs", "bin", "Release", "net8.0", "mexcostume" + _exe)


class MexCostumeError(RuntimeError):
    pass


def run(cmd, build, *args):
    """-> (exit code, combined output). Exit codes: 0 ok, 1 verification failed, 2 refused, 3 usage."""
    if not os.path.exists(TOOL):
        raise MexCostumeError("mexcostume is not built: run build.ps1")
    r = subprocess.run([TOOL, cmd, build] + [str(a) for a in args], capture_output=True, text=True)
    return r.returncode, (r.stdout + r.stderr).strip()


def fighter_file(fighter):
    import melee_fighters
    return "Pl%s.dat" % melee_fighters.FIGHTERS[fighter][1]


def list_costumes(build, fighter=None):
    rc, out = run("list", build, *(["--fighter-file", fighter_file(fighter)] if fighter else []), "--json")
    if rc: raise MexCostumeError(out)
    return json.loads(out)


def verify(build):
    return run("verify", build)


def free_colour(build, fighter):
    """A two-letter costume code whose file name is not taken in files/ (Xa, Xb, ...)."""
    import melee_fighters
    code = melee_fighters.FIGHTERS[fighter][1]
    files = os.path.join(build, "files") if os.path.isdir(os.path.join(build, "files")) else build
    taken = {f.lower() for f in os.listdir(files)}
    for a in "XYZW":
        for b in "abcdefghijklmnopqrstuvwxyz":
            if ("pl%s%s%s.dat" % (code, a, b)).lower() not in taken: return a + b
    raise MexCostumeError("no free costume code")


def add_costume(build, fighter, costume_dat, like=0, reuse_images=False, partial=False):
    """Copy the costume under a free name Pl<code><Xx>.dat with its symbols retagged to match, then add the row."""
    import skin
    col = free_colour(build, fighter)
    name = "Pl%s%s.dat" % (fighter_file(fighter)[2:4], col)
    tmp = tempfile.mkdtemp(prefix="mexcostume-", dir=paths.WORK if os.path.isdir(paths.WORK) else None)
    try:
        staged = os.path.join(tmp, name); shutil.copyfile(costume_dat, staged)
        skin.retag_symbols(staged, fighter, col)
        args = ["--fighter-file", fighter_file(fighter), "--costume", staged, "--name", name, "--like", like]
        if reuse_images: args.append("--reuse-images")
        if partial: args.append("--partial")
        rc, out = run("add", build, *args)
        return rc == 0, out
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def replace_costume(build, fighter, row, costume_dat):
    rc, out = run("replace", build, "--fighter-file", fighter_file(fighter), "--row", row, "--costume", costume_dat)
    return rc == 0, out


def remove_last(build, fighter):
    rc, out = run("remove", build, "--fighter-file", fighter_file(fighter))
    return rc == 0, out


def undo(build, force=False):
    rc, out = run("undo", build, *(["--force"] if force else []))
    return rc == 0, out


# Kept for callers of the old stub interface.
def install_costume(mex_root, fighter, costume_dat, name=None, **kw):
    return add_costume(mex_root, fighter, costume_dat, **kw)
