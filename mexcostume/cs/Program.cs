// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 GD (Gurekam Dhillon)
using System.Text.Json;
using MexCostume;

static class Program
{
    const string Usage = @"mexcostume <command> <extracted m-ex build folder> [options]
  list    [--internal N | --external N | --fighter-file PlLg.dat] [--json]
  verify
  add     --costume <file.dat> [--name PlLgXx.dat] [--like C] [--vis V] [--reuse-images]   (+ fighter)
  replace --costume <file.dat> --row C                                                          (+ fighter)
  remove  [--row C]   (last row only, and only rows this tool added)                            (+ fighter)
  undo    [--force]   (restores the newest undo record byte for byte)
common: --partial (files/ holds only this fighter's files: check only its row files), --undo-dir DIR (default <build>/skinfitter-undo), --dry-run
exit: 0 ok, 1 verification failed, 2 refused, 3 usage";

    static int Main(string[] a)
    {
        if (a.Length < 2) { Console.Error.WriteLine(Usage); return 3; }
        var o = new Options(); bool json = false;
        for (int k = a[0] == "compare" ? 3 : 2; k < a.Length; k++)
        {
            string V() => k + 1 < a.Length ? a[++k] : throw new ArgumentException(a[k] + " needs a value");
            switch (a[k])
            {
                case "--internal": o.Internal = int.Parse(V()); break;
                case "--external": o.External = int.Parse(V()); break;
                case "--fighter-file": o.FighterFile = V(); break;
                case "--costume": o.Costume = V(); break;
                case "--name": o.Name = V(); break;
                case "--like": o.Like = int.Parse(V()); break;
                case "--vis": o.Vis = int.Parse(V()); break;
                case "--row": o.Row = int.Parse(V()); break;
                case "--undo-dir": o.UndoDir = V(); break;
                case "--force": o.Force = true; break;
                case "--partial": o.Partial = true; break;
                case "--reuse-images": o.ReuseImages = true; break;
                case "--dry-run": o.DryRun = true; break;
                case "--json": json = true; break;
                default: Console.Error.WriteLine("unknown option " + a[k] + "\n" + Usage); return 3;
            }
        }
        try
        {
            if (a[0] == "compare")      // test aid: compare <buildA> <buildB>: MxDt graph + every shown portrait/stock image
            {
                if (a.Length < 3) { Console.Error.WriteLine("compare <buildA> <buildB>"); return 3; }
                var A = MexBuild.Open(a[1]); var B = MexBuild.Open(a[2]);
                var d = Compare.Graph(Path.Combine(A.Files, "MxDt.dat"), Path.Combine(B.Files, "MxDt.dat")).Select(s => "MxDt " + s).Concat(Compare.Ui(A, B)).ToList();
                foreach (var s in d) Console.WriteLine("DIFF " + s);
                Console.WriteLine(d.Count == 0 ? "EQUAL (MxDt structure; images shown for every valid portrait and stock frame)" : $"{d.Count} differences (first 20 per part)");
                return d.Count == 0 ? 0 : 1;
            }
            var b = MexBuild.Open(a[1]);
            switch (a[0])
            {
                case "list": return List(b, o, json);
                case "verify":
                    var bad = b.Verify(o.Partial ? Ops.ResolveFighter(b, o) : -1);
                    foreach (var s in bad) Console.WriteLine("FAIL " + s);
                    Console.WriteLine(bad.Count == 0 ? $"OK  {b.InternalCount} internal costume tables, {b.ExternalCount} CSS slots, meta {b.MetaHex}" : $"{bad.Count} problems");
                    return bad.Count == 0 ? 0 : 1;
                case "add": Console.WriteLine(Ops.Add(b, o)); return 0;
                case "replace": Console.WriteLine(Ops.Replace(b, o)); return 0;
                case "remove": Console.WriteLine(Ops.Remove(b, o)); return 0;
                case "undo": Console.WriteLine(Ops.Undo(b, o)); return 0;
                default: Console.Error.WriteLine(Usage); return 3;
            }
        }
        catch (RefusedException e) { Console.Error.WriteLine("REFUSED: " + e.Message); return 2; }
    }

    static int List(MexBuild b, Options o, bool json)
    {
        var ids = (o.Internal >= 0 || o.External >= 0 || o.FighterFile != null) ? new[] { Ops.ResolveFighter(b, o) } : Enumerable.Range(0, b.InternalCount).ToArray();
        var csp = b.CspAnim.Keys(); var st = b.StockAnim.Keys();
        const byte TIMG = 1;
        var outp = new List<object>();
        foreach (var i in ids)
        {
            var f = b.GetFighter(i);
            var rows = f.Rows.Select((r, c) => new
            {
                row = c, file = r.File, joint = r.Joint, matanim = r.MatAnim, vis = r.Vis,
                csp_frame = f.External >= 0 ? b.CspFrame(f.External, c) : (int?)null,
                csp_image = f.External >= 0 ? UiAnim.ValueAt(csp[TIMG], b.CspFrame(f.External, c)) : null,
                stock_frame = b.StockFrame(i, c), stock_image = UiAnim.ValueAt(st[TIMG], b.StockFrame(i, c)),
            }).ToList();
            outp.Add(new { @internal = i, external = f.External, char_file = f.CharFile, count = f.IdsCount, red = f.Red, blue = f.Blue, green = f.Green,
                           runtime_count = f.PtrCount, runtime_block = f.PtrBlockSize, vis_rows = f.VisRows, kirby_hat_table = f.HasKirbyHatTable, rows });
            if (!json)
            {
                Console.WriteLine($"internal {i,2}  external {(f.External >= 0 ? f.External.ToString() : "-"),2}  {f.CharFile}  count {f.IdsCount} (red {f.Red} blue {f.Blue} green {f.Green})  runtime count {f.PtrCount} block 0x{f.PtrBlockSize:X}  vis rows {f.VisRows}{(f.HasKirbyHatTable ? "  kirby-hat-table" : "")}");
                foreach (var r in rows)
                    Console.WriteLine($"    {r.row}: {r.file,-14} {r.joint,-34} {r.matanim ?? "-",-42} vis {r.vis}  CSP f{r.csp_frame}->{r.csp_image}  stock f{r.stock_frame}->{r.stock_image}");
            }
        }
        if (json) Console.WriteLine(JsonSerializer.Serialize(new { meta = b.MetaHex, csp_stride = b.Sel.CSPStride, stock_reserved = b.Stock.Reserved, stock_stride = b.Stock.Stride,
                                                                 csp_images = b.CspAnim.Tex.ImageCount, stock_images = b.StockAnim.Tex.ImageCount, fighters = outp }, new JsonSerializerOptions { WriteIndented = true }));
        else Console.WriteLine($"CSP stride {b.Sel.CSPStride}, {b.CspAnim.Tex.ImageCount} images; stock reserved {b.Stock.Reserved} stride {b.Stock.Stride}, {b.StockAnim.Tex.ImageCount} images; meta {b.MetaHex}");
        return 0;
    }
}
