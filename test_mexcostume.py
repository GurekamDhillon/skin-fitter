#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 GD (Gurekam Dhillon)
"""Tests for the m-ex costume step on made-up data only: the C# unit tests (mexcostume/tests), then the Python
wrapper (retag + add + list + undo) on a synthetic m-ex build written by the test program.
    python test_mexcostume.py
"""
import hashlib, os, shutil, subprocess, sys, tempfile
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
os.environ.setdefault("SKIN_FITTER_DATA", tempfile.mkdtemp(prefix="skin-fitter-test-"))
import mexcostume, skin

TESTS = os.path.join(HERE, "mexcostume", "tests", "bin", "Release", "net8.0", "mexcostume.tests.dll")


def hashes(d):
    f = os.path.join(d, "files")
    return {n: hashlib.sha256(open(os.path.join(f, n), "rb").read()).hexdigest() for n in os.listdir(f)}


def main():
    work = tempfile.mkdtemp(prefix="mexcostume-py-")
    try:
        r = subprocess.run(["dotnet", TESTS, work], capture_output=True, text=True); print(r.stdout.strip())
        assert r.returncode == 0, r.stdout + r.stderr
        build = os.path.join(work, "build")
        subprocess.run(["dotnet", TESTS, "--make-build", build], check=True, capture_output=True)
        h0 = hashes(build)
        assert mexcostume.verify(build)[0] == 0
        # the synthetic build's internal 1 uses file "PlBb.dat": map a fighter name onto it for the wrapper test
        import melee_fighters
        melee_fighters.FIGHTERS["_synth"] = (99, "Bb", "", "")
        src = os.path.join(work, "PlBbNr_conv.dat"); shutil.copyfile(os.path.join(build, "files", "PlBbNr.dat"), src)
        ok, out = mexcostume.add_costume(build, "_synth", src, like=1)
        assert ok, out
        rows = mexcostume.list_costumes(build, "_synth")["fighters"][0]["rows"]
        assert len(rows) == 4 and rows[3]["file"] == "PlBbXa.dat" and rows[3]["joint"] == "PlyBb5KXa_Share_joint", rows[3]
        ok, out = mexcostume.undo(build); assert ok, out
        assert hashes(build) == h0, "undo not byte-identical"
        print("MEXCOSTUME WRAPPER TESTS PASS")
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main()
