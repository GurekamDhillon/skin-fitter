// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 GD (Gurekam Dhillon)
//
// Edit operations (spec section 4) with an undo manifest and backups written before anything is touched.
using System.Text.Json;
using System.Text.Json.Nodes;
using HSDRaw;
using HSDRaw.MEX;

namespace MexCostume;

public class Options
{
    public int Internal = -1, External = -1; public string FighterFile;
    public string Costume; public string Name;          // add/replace: source costume file; add: name in files/ (default: its own)
    public int Like = 0;                                // add: costume row whose visibility index and UI images are reused
    public int Vis = -1;                                // add: explicit visibility index (default: Like's)
    public int Row = -1;                                // replace/remove
    public string UndoDir; public bool Force; public bool ReuseImages; public bool Partial; public bool DryRun;
}

public static class Ops
{
    static readonly string[] Archives = { "MxDt.dat", "MnSlChr.usd", "IfAll.usd" };

    public static string UndoRoot(MexBuild b, Options o) => o.UndoDir ?? Path.Combine(b.Root, "skinfitter-undo");

    public static int ResolveFighter(MexBuild b, Options o)
    {
        if (o.Internal >= 0) { if (o.Internal >= b.InternalCount) throw new RefusedException($"internal {o.Internal} out of range"); return o.Internal; }
        if (o.External >= 0) { if (o.External >= b.ExternalCount) throw new RefusedException($"external {o.External} out of range"); return b.InternalOf(o.External); }
        if (o.FighterFile != null)
        {
            for (int i = 0; i < b.InternalCount; i++)
                if (string.Equals(b.GetFighter(i).CharFile, o.FighterFile, StringComparison.OrdinalIgnoreCase)) return i;
            throw new RefusedException($"no fighter uses {o.FighterFile}");
        }
        throw new RefusedException("name the fighter: --internal N, --external N or --fighter-file Pl<code>.dat");
    }

    static void RequireValid(MexBuild b, string when, Options o, int i)
    {
        var bad = b.Verify(o.Partial ? i : -1);
        if (bad.Count > 0) throw new RefusedException($"build fails verification {when}; refusing to edit:\n  " + string.Join("\n  ", bad.Take(20)));
    }

    // --------------------------------------------------------------------------------------- transaction
    class Txn
    {
        public MexBuild B; public string Dir; public JsonObject M; public List<string> Touched = new(); public List<string> Added = new();
        public void Write() => File.WriteAllText(Path.Combine(Dir, "manifest.json"), M.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    static Txn Begin(MexBuild b, Options o, string op, int i, IEnumerable<string> willChange, IEnumerable<string> willAdd, JsonObject extra)
    {
        var root = UndoRoot(b, o); Directory.CreateDirectory(root);
        var dir = Path.Combine(root, DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff") + "-" + op);
        Directory.CreateDirectory(Path.Combine(dir, "backup"));
        var t = new Txn { B = b, Dir = dir, M = new JsonObject { ["tool"] = "skin-fitter mexcostume", ["version"] = 1, ["op"] = op, ["internal"] = i, ["state"] = "pending" } };
        foreach (var kv in extra) t.M[kv.Key] = kv.Value?.DeepClone();
        var ch = new JsonArray();
        foreach (var f in willChange.Distinct())
        {
            var p = Path.Combine(b.Files, f);
            File.Copy(p, Path.Combine(dir, "backup", f), false);
            ch.Add(new JsonObject { ["file"] = f, ["sha256_before"] = MexBuild.Sha(p) });
            t.Touched.Add(f);
        }
        var ad = new JsonArray();
        foreach (var f in willAdd) { ad.Add(new JsonObject { ["file"] = f }); t.Added.Add(f); }
        t.M["changed"] = ch; t.M["added"] = ad;
        t.Write();                      // manifest + backups exist before the build is touched
        return t;
    }

    static void Commit(Txn t)
    {
        foreach (JsonObject c in t.M["changed"].AsArray()) c["sha256_after"] = MexBuild.Sha(Path.Combine(t.B.Files, (string)c["file"]));
        foreach (JsonObject c in t.M["added"].AsArray()) c["sha256_after"] = MexBuild.Sha(Path.Combine(t.B.Files, (string)c["file"]));
        t.M["state"] = "done"; t.Write();
    }

    static void Rollback(Txn t)
    {
        foreach (var f in t.Touched) File.Copy(Path.Combine(t.Dir, "backup", f), Path.Combine(t.B.Files, f), true);
        foreach (var f in t.Added) { var p = Path.Combine(t.B.Files, f); if (File.Exists(p)) File.Delete(p); }
        t.M["state"] = "rolled-back"; t.Write();
    }

    static void Run(Txn t, Action edit, string label, Options o, int i)
    {
        try
        {
            edit();
            var after = MexBuild.Open(t.B.Root);           // re-read from disc and re-verify (spec 5)
            var bad = after.Verify(o.Partial ? i : -1);
            if (bad.Count > 0) throw new RefusedException($"{label}: the edited build fails verification; restored:\n  " + string.Join("\n  ", bad.Take(20)));
            Commit(t);
        }
        catch
        {
            Rollback(t); throw;
        }
    }

    static (string joint, string mat) CostumeSymbols(string path)
    {
        List<string> syms;
        try { syms = MexBuild.SymbolsOf(path).ToList(); } catch (Exception e) { throw new RefusedException($"costume file unreadable as an HSD archive ({e.GetType().Name})"); }
        var joints = syms.Where(s => s.EndsWith("_Share_joint")).ToList();
        if (joints.Count != 1) throw new RefusedException($"costume file must have exactly one *_Share_joint root (has {joints.Count})");
        var mats = syms.Where(s => s.EndsWith("_Share_matanim_joint")).ToList();
        return (joints[0], mats.Count == 1 ? mats[0] : null);
    }

    // --------------------------------------------------------------------------------------- add (spec 4)
    public static string Add(MexBuild b, Options o)
    {
        int i = ResolveFighter(b, o); var f = b.GetFighter(i);
        if (f.External < 0) throw new RefusedException($"internal {i} has no CSS slot; add costumes to the CSS fighter instead");
        // spec 6.4: Kirby copy-hat tables are per fighter and are not touched by an add.
        if (o.Costume == null || !File.Exists(o.Costume)) throw new RefusedException("costume file not found");
        RequireValid(b, "before the edit", o, i);
        int n = f.Rows.Count;
        if (n + 1 > 255) throw new RefusedException("costume count is a byte: at most 255");
        if (o.Like < 0 || o.Like >= n) throw new RefusedException($"--like {o.Like} is not an existing costume (0..{n - 1})");
        var name = o.Name ?? Path.GetFileName(o.Costume);
        if (name.IndexOfAny(new[] { '/', '\\' }) >= 0 || !name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)) throw new RefusedException("costume name must be a plain <name>.dat");
        if (Directory.EnumerateFiles(b.Files).Any(p => string.Equals(Path.GetFileName(p), name, StringComparison.OrdinalIgnoreCase)))
            throw new RefusedException($"{name} already exists in files/");
        var (joint, mat) = CostumeSymbols(o.Costume);
        int vis = o.Vis >= 0 ? o.Vis : f.Rows[o.Like].Vis;
        if (f.VisRows >= 0 && vis >= f.VisRows) throw new RefusedException($"visibility index {vis} >= {f.VisRows} rows");
        // spec 3: by default one portrait and one stock image are appended (copies of costume --like's pictures, CI8
        // 136x188 / CI4 24x24 as the build has them); --reuse-images keys the row at --like's existing images instead.
        // TODO: importing new art from an image file (GXImageConverter, CI8 136x188 portrait, CI4 24x24 icon).
        int e = f.External;
        if (o.DryRun) return $"dry run: would add {name} as costume {n} of internal {i} (external {e}), vis {vis}, CSP frame {b.CspFrame(e, n)}, stock frame {b.StockFrame(i, n)}";

        var csBefore = b.ValidFrames(b.CspAnim); var stBefore = b.ValidFrames(b.StockAnim);
        var t = Begin(b, o, "add", i, Archives, new[] { name }, new JsonObject { ["external"] = e, ["row"] = n, ["file"] = name, ["joint"] = joint, ["matanim"] = mat, ["vis"] = vis, ["like"] = o.Like });
        Run(t, () =>
        {
            File.Copy(o.Costume, Path.Combine(b.Files, name), false);
            var tbl = b.Fd.CostumeFileSymbols[i].CostumeSymbols;
            var rows = tbl.Array.Take(n).ToList();
            var r = new MEX_CostumeFileSymbol { _s = new HSDStruct(0x10) };
            r.FileName = name; r.JointSymbol = joint; r.MatAnimSymbol = mat; r.VisibilityLookupIndex = vis;
            rows.Add(r); tbl.Array = rows.ToArray();
            var ids = b.Fd.CostumeIDs[e]; ids.CostumeCount = (byte)(n + 1); b.Fd.CostumeIDs[e] = ids;
            SetRuntime(b, i, n + 1);
            b.CopyUiKey(b.CspAnim, b.CspFrame(e, o.Like), b.CspFrame(e, n), csBefore, !o.ReuseImages);
            b.CopyUiKey(b.StockAnim, b.StockFrame(i, o.Like), b.StockFrame(i, n), stBefore, !o.ReuseImages);
            b.Save();
        }, "add", o, i);
        return $"added {name} as costume {n} of internal {i} (external {e}); undo record {Path.GetFileName(t.Dir)}";
    }

    static void SetRuntime(MexBuild b, int i, int count)
    {
        var p = b.Fd.CostumePointers[i];
        p.Pointer = new HSDAccessor { _s = new HSDStruct(0x18 * Math.Max(count, 1)) };
        p.CostumeCount = (byte)count;
        b.Fd.CostumePointers[i] = p;
    }

    // --------------------------------------------------------------------------------------- replace (spec 4)
    public static string Replace(MexBuild b, Options o)
    {
        int i = ResolveFighter(b, o); var f = b.GetFighter(i);
        if (o.Row < 0 || o.Row >= f.Rows.Count) throw new RefusedException($"--row must be 0..{f.Rows.Count - 1}");
        if (o.Costume == null || !File.Exists(o.Costume)) throw new RefusedException("costume file not found");
        RequireValid(b, "before the edit", o, i);
        var row = f.Rows[o.Row];
        var syms = MexBuild.SymbolsOf(o.Costume).ToList();
        if (!syms.Contains(row.Joint) || (row.MatAnim != null && !syms.Contains(row.MatAnim)))
            throw new RefusedException($"the new file's root names must equal the row's ({row.Joint}{(row.MatAnim != null ? ", " + row.MatAnim : "")}); retag the costume first");
        if (o.DryRun) return $"dry run: would overwrite {row.File}";
        var t = Begin(b, o, "replace", i, new[] { row.File }, Array.Empty<string>(), new JsonObject { ["row"] = o.Row, ["file"] = row.File });
        Run(t, () => File.Copy(o.Costume, Path.Combine(b.Files, row.File), true), "replace", o, i);
        return $"replaced {row.File}; undo record {Path.GetFileName(t.Dir)}";
    }

    // --------------------------------------------------------------------------------------- remove (spec 4: last row, ours only)
    public static List<(string dir, JsonObject m)> Manifests(MexBuild b, Options o)
    {
        var root = UndoRoot(b, o);
        if (!Directory.Exists(root)) return new();
        return Directory.GetDirectories(root).OrderBy(d => d)
            .Select(d => (d, File.Exists(Path.Combine(d, "manifest.json")) ? JsonNode.Parse(File.ReadAllText(Path.Combine(d, "manifest.json")))?.AsObject() : null))
            .Where(x => x.Item2 != null).ToList();
    }

    public static string Remove(MexBuild b, Options o)
    {
        int i = ResolveFighter(b, o); var f = b.GetFighter(i);
        int n = f.Rows.Count;
        if (n == 0) throw new RefusedException("no costumes");
        int c = o.Row < 0 ? n - 1 : o.Row;
        if (c != n - 1) throw new RefusedException("only the last costume row can be removed (later rows would need re-keying); undo later additions first");
        var row = f.Rows[c];
        bool ours = Manifests(b, o).Any(x => (string)x.m["op"] == "add" && (string)x.m["state"] == "done" && (int)x.m["internal"] == i && (string)x.m["file"] == row.File);
        if (!ours) throw new RefusedException($"{row.File} was not added by this tool; refusing to remove it");
        RequireValid(b, "before the edit", o, i);
        if (o.DryRun) return $"dry run: would remove costume {c} ({row.File})";
        int e = f.External;
        var t = Begin(b, o, "remove", i, Archives.Append(row.File), Array.Empty<string>(), new JsonObject { ["external"] = e, ["row"] = c, ["file"] = row.File });
        Run(t, () =>
        {
            var tbl = b.Fd.CostumeFileSymbols[i].CostumeSymbols;
            tbl.Array = tbl.Array.Take(c).ToArray();
            if (e >= 0) { var ids = b.Fd.CostumeIDs[e]; ids.CostumeCount = (byte)c; b.Fd.CostumeIDs[e] = ids; }
            SetRuntime(b, i, c);
            if (e >= 0) b.RemoveUiKey(b.CspAnim, b.CspFrame(e, c), b.ValidFrames(b.CspAnim));
            b.RemoveUiKey(b.StockAnim, b.StockFrame(i, c), b.ValidFrames(b.StockAnim));
            b.Save();
            File.Delete(Path.Combine(b.Files, row.File));
        }, "remove", o, i);
        return $"removed costume {c} ({row.File}); undo record {Path.GetFileName(t.Dir)}";
    }

    // --------------------------------------------------------------------------------------- undo
    public static string Undo(MexBuild b, Options o)
    {
        var last = Manifests(b, o).LastOrDefault(x => (string)x.m["state"] == "done");
        if (last.m == null) throw new RefusedException("nothing to undo");
        var m = last.m;
        foreach (JsonObject c in m["changed"].AsArray())
        {
            var p = Path.Combine(b.Files, (string)c["file"]);
            var want = (string)c["sha256_after"];
            if (MexBuild.Sha(p) != want && !o.Force) throw new RefusedException($"{c["file"]} changed since {m["op"]}; refusing to undo (use --force to restore anyway)");
        }
        foreach (JsonObject c in m["added"].AsArray())
        {
            var p = Path.Combine(b.Files, (string)c["file"]);
            if (File.Exists(p) && MexBuild.Sha(p) != (string)c["sha256_after"] && !o.Force) throw new RefusedException($"{c["file"]} changed since {m["op"]}; refusing (--force)");
        }
        if (o.DryRun) return $"dry run: would undo {Path.GetFileName(last.dir)}";
        foreach (JsonObject c in m["changed"].AsArray())
            File.Copy(Path.Combine(last.dir, "backup", (string)c["file"]), Path.Combine(b.Files, (string)c["file"]), true);
        foreach (JsonObject c in m["added"].AsArray()) { var p = Path.Combine(b.Files, (string)c["file"]); if (File.Exists(p)) File.Delete(p); }
        m["state"] = "undone";
        File.WriteAllText(Path.Combine(last.dir, "manifest.json"), m.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return $"undid {Path.GetFileName(last.dir)}";
    }
}
