// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 GD (Gurekam Dhillon)
//
// Unit tests on a synthetic m-ex-shaped build constructed from the specification's layout (sections 2 and 3).
// No game data: every table, image and costume file is made up here.
using System.Security.Cryptography;
using HSDRaw;
using HSDRaw.Common;
using HSDRaw.Common.Animation;
using HSDRaw.GX;
using HSDRaw.MEX;
using HSDRaw.MEX.Menus;
using HSDRaw.Tools;
using MexCostume;

static class Tests
{
    static int pass, fail;
    static void Check(bool ok, string what) { if (ok) pass++; else { fail++; Console.WriteLine("FAIL " + what); } }
    static void Refuses(Action a, string what, string contains = null)
    {
        try { a(); fail++; Console.WriteLine("FAIL (not refused) " + what); }
        catch (RefusedException e) { Check(contains == null || e.Message.Contains(contains), what + " -> " + e.Message.Split('\n')[0]); }
    }

    // ------------------------------------------------------------------ synthetic build
    // externals: 0 -> internal 0 (2 costumes), 1 -> internal 1 (3 costumes); internal 2 has no CSS slot (1 costume).
    const int CspStride = 4, StockReserved = 1, StockStride = 4;
    static readonly string[] Names = { "Aa", "Bb", "Cc" };
    static readonly int[] Counts = { 2, 3, 1 };
    static readonly int[] VisRows = { 2, 2, 1 };

    static HSD_String S(string v) => new HSD_String { Value = v };

    public static void CostumeFile(string path, string name, string colour, bool matanim = true)
    {
        var f = new HSDRawFile();
        f.Roots.Add(new HSDRootNode { Name = $"Ply{name}5K{colour}_Share_joint", Data = new HSDAccessor { _s = new HSDStruct(new byte[] { 1, 2, 3, 4 }) } });
        if (matanim) f.Roots.Add(new HSDRootNode { Name = $"Ply{name}5K{colour}_Share_matanim_joint", Data = new HSDAccessor { _s = new HSDStruct(new byte[] { 5, 6, 7, 8 }) } });
        f.Save(path);
    }

    static void FighterDataFile(string path, string name, int visRows)
    {
        var lt = new HSDStruct(0x18);
        lt.SetInt32(0x00, 1);
        lt.SetReferenceStruct(0x04, new HSDStruct(0x10 * visRows));
        var ft = new HSDStruct(0x60); ft.SetReferenceStruct(0x08, lt);
        var f = new HSDRawFile(); f.Roots.Add(new HSDRootNode { Name = "ftData" + name, Data = new HSDAccessor { _s = ft } }); f.Save(path);
    }

    static HSD_TexAnim TexAnim(int images, List<(int frame, int img)> keys)
    {
        var t = new HSD_TexAnim { _s = new HSDStruct(0x18) };
        for (int k = 0; k < images; k++)
        {
            var im = new HSD_Image { _s = new HSDStruct(0x18) };
            var px = new byte[8 * 4]; for (int p = 0; p < px.Length; p++) px[p] = (byte)(k * 7 + p);
            im.ImageData = px; im.Width = 8; im.Height = 4; im.Format = GXTexFmt.CI8;
            var tl = new HSD_Tlut { _s = new HSDStruct(0x10) };
            var pal = new byte[16 * 2]; for (int p = 0; p < pal.Length; p++) pal[p] = (byte)(k + p * 3);
            tl.TlutData = pal; tl.Format = GXTlutFmt.RGB565; tl.ColorCount = 16;
            t.AddImage(new HSD_TOBJ { ImageData = im, TlutData = tl }, out _, out _);
        }
        var img = keys.Select(k => new FOBJKey { Frame = k.frame, Value = k.img, InterpolationType = GXInterpolationType.HSD_A_OP_CON }).ToList();
        t.AnimationObject = new HSD_AOBJ { _s = new HSDStruct(0x10) };
        t.AnimationObject.EndFrame = keys.Max(k => k.frame);
        t.AnimationObject.FObjDesc = new HSD_FOBJDesc { _s = new HSDStruct(0x14) };
        t.AnimationObject.FObjDesc.SetKeys(img, (byte)TexTrackType.HSD_A_T_TIMG);
        t.AnimationObject.FObjDesc.Next = new HSD_FOBJDesc { _s = new HSDStruct(0x14) };
        t.AnimationObject.FObjDesc.Next.SetKeys(img.Select(k => new FOBJKey { Frame = k.Frame, Value = k.Value, InterpolationType = k.InterpolationType }).ToList(), (byte)TexTrackType.HSD_A_T_TCLT);
        return t;
    }

    public static string MakeBuild(string dir, bool badFighterSize = false, bool noStock = false)
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        var files = Path.Combine(dir, "files"); Directory.CreateDirectory(files); Directory.CreateDirectory(Path.Combine(dir, "sys"));
        var colours = new[] { "", "Re", "Bu" };
        var fd = new MEX_FighterData { _s = new HSDStruct(badFighterSize ? 0x88 : 0x90) };
        var charFiles = new HSDArrayAccessor<MEX_CharFileStrings> { _s = new HSDStruct(8 * 3) };
        var define = new HSDStruct(3 * 2); define.SetByte(0, 0); define.SetByte(3, 1);
        var ids = new HSDStruct(4 * 2);
        var tables = new HSDStruct(4 * 3); var ptrs = new HSDStruct(8 * 3);
        for (int i = 0; i < 3; i++)
        {
            var cf = new HSDStruct(8); cf.SetReference(0, S($"Pl{Names[i]}.dat")); cf.SetReference(4, S($"ftData{Names[i]}"));
            charFiles._s.SetEmbededStruct(i * 8, cf);
            FighterDataFile(Path.Combine(files, $"Pl{Names[i]}.dat"), Names[i], VisRows[i]);
            var rows = new HSDStruct(0x10 * Counts[i]);
            for (int c = 0; c < Counts[i]; c++)
            {
                string col = colours[c], file = $"Pl{Names[i]}{(c == 0 ? "Nr" : col)}.dat";
                CostumeFile(Path.Combine(files, file), Names[i], col);
                rows.SetReference(c * 0x10 + 0, S(file));
                rows.SetReference(c * 0x10 + 4, S($"Ply{Names[i]}5K{col}_Share_joint"));
                rows.SetReference(c * 0x10 + 8, S($"Ply{Names[i]}5K{col}_Share_matanim_joint"));
                rows.SetInt32(c * 0x10 + 0xC, c % VisRows[i]);
            }
            tables.SetReferenceStruct(i * 4, rows);
            ptrs.SetReferenceStruct(i * 8, new HSDStruct(0x18 * Counts[i])); ptrs.SetByte(i * 8 + 4, (byte)Counts[i]);
            if (i < 2) { ids.SetByte(i * 4, (byte)Counts[i]); ids.SetByte(i * 4 + 1, 0); ids.SetByte(i * 4 + 2, (byte)(Counts[i] - 1)); }
        }
        fd._s.SetReference(0x04, charFiles);
        fd._s.SetReferenceStruct(0x0C, define); fd._s.SetReferenceStruct(0x10, ids);
        fd._s.SetReferenceStruct(0x14, tables); fd._s.SetReferenceStruct(0x3C, ptrs);
        var data = new MEX_Data { _s = new HSDStruct(0x38) };
        data._s.SetReferenceStruct(0x00, new HSDStruct(new byte[] { 1, 1, 0, 0, 0, 0, 0, 2 }));
        data._s.SetReference(0x08, fd);
        var mx = new HSDRawFile(); mx.Roots.Add(new HSDRootNode { Name = "mexData", Data = data }); mx.Save(Path.Combine(files, "MxDt.dat"));

        // CSP: external e, costume c -> frame e + 4c. (1, 2) = frame 9 deliberately has no key: covered by frame 5.
        var cspKeys = new List<(int, int)> { (0, 0), (1, 1), (4, 2), (5, 3) };
        var sel = new MEX_mexSelectChr { _s = new HSDStruct(0x14) };
        sel.CSPMatAnim = new HSD_MatAnim { _s = new HSDStruct(0x10) };
        sel.CSPMatAnim.TextureAnimation = TexAnim(4, cspKeys);
        sel.CSPStride = CspStride;
        var css = new HSDRawFile();
        css.Roots.Add(new HSDRootNode { Name = "MnSelectChrDataTable", Data = new HSDAccessor { _s = new HSDStruct(new byte[] { 9, 9, 9, 9 }) } });
        css.Roots.Add(new HSDRootNode { Name = "mexSelectChr", Data = sel }); css.Save(Path.Combine(files, "MnSlChr.usd"));

        // stock: reserved 1 + internal i + 4c
        var stKeys = new List<(int, int)> { (0, 0), (1, 1), (2, 2), (3, 3), (5, 4), (6, 5), (10, 6) };
        var st = new MEX_Stock { _s = new HSDStruct(0x10) }; st.Reserved = StockReserved; st.Stride = StockStride;
        st.MatAnimJoint = new HSD_MatAnimJoint { _s = new HSDStruct(0xC) };
        st.MatAnimJoint.MaterialAnimation = new HSD_MatAnim { _s = new HSDStruct(0x10) };
        st.MatAnimJoint.MaterialAnimation.TextureAnimation = TexAnim(7, stKeys);
        var ifall = new HSDRawFile();
        ifall.Roots.Add(new HSDRootNode { Name = "DmgMrk_scene_models", Data = new HSDAccessor { _s = new HSDStruct(new byte[] { 7, 7, 7, 7 }) } });
        if (!noStock) ifall.Roots.Add(new HSDRootNode { Name = "Stc_icns", Data = st });
        ifall.Save(Path.Combine(files, "IfAll.usd"));
        return dir;
    }

    static Dictionary<string, string> Hashes(string dir) => Directory.GetFiles(Path.Combine(dir, "files")).ToDictionary(Path.GetFileName, p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
    static bool SameHashes(Dictionary<string, string> a, Dictionary<string, string> b) => a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && v == kv.Value);

    static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--make-build") { MakeBuild(args[1]); Console.WriteLine("synthetic m-ex build written"); return 0; }
        var tmp = Path.Combine(args.Length > 0 ? args[0] : Path.GetTempPath(), "mexcostume-tests");
        Directory.CreateDirectory(tmp);
        var newCostume = Path.Combine(tmp, "PlBbXx.dat"); CostumeFile(newCostume, "Bb", "Xx");
        var noJoint = Path.Combine(tmp, "nojoint.dat");
        { var f = new HSDRawFile(); f.Roots.Add(new HSDRootNode { Name = "something", Data = new HSDAccessor { _s = new HSDStruct(4) } }); f.Save(noJoint); }

        // 1. the synthetic build verifies
        var dir = MakeBuild(Path.Combine(tmp, "b1"));
        var b = MexBuild.Open(dir);
        var v0 = b.Verify();
        Check(v0.Count == 0, "synthetic build verifies: " + string.Join("; ", v0));
        Check(b.InternalCount == 3 && b.ExternalCount == 2, "table lengths");
        Check(b.ExternalOf(2) == -1 && b.InternalOf(1) == 1, "external/internal ids");
        Check(b.GetFighter(1).VisRows == 2, "visibility rows read from Pl<code>.dat");
        var h0 = Hashes(dir);

        // 2. add: tables, frames, preserved neighbours, re-verify, undo byte-identical
        var msg = Ops.Add(b, new Options { Internal = 1, Costume = newCostume, Like = 1 });
        var a = MexBuild.Open(dir); var f1 = a.GetFighter(1);
        Check(f1.Rows.Count == 4 && f1.IdsCount == 4 && f1.PtrCount == 4 && f1.PtrBlockSize == 0x18 * 4, "add: counts and runtime block");
        Check(f1.Rows[3] == new Row("PlBbXx.dat", "PlyBb5KXx_Share_joint", "PlyBb5KXx_Share_matanim_joint", 1), "add: row contents, vis from --like");
        Check(f1.Red == 0 && f1.Blue == 2, "add: team indices unchanged");
        Check(File.Exists(Path.Combine(dir, "files", "PlBbXx.dat")), "add: file copied");
        Check(a.Verify().Count == 0, "add: re-verifies");
        Check(a.CspAnim.Tex.ImageCount == 5 && a.StockAnim.Tex.ImageCount == 8, "add: one portrait and one stock image appended");
        var ck = a.CspAnim.Keys()[(byte)TexTrackType.HSD_A_T_TIMG];
        Check(UiAnim.ValueAt(ck, 1 + CspStride * 3) == 4, "add: CSP key at e + stride*n -> new image");
        Check(UiAnim.ValueAt(ck, 1 + CspStride * 2) == 3, "add: covered neighbour frame keeps its image (pinned)");
        var sk = a.StockAnim.Keys()[(byte)TexTrackType.HSD_A_T_TIMG];
        Check(UiAnim.ValueAt(sk, StockReserved + 1 + StockStride * 3) == 7, "add: stock key at reserved + i + stride*n");
        var orig = MexBuild.Open(MakeBuild(Path.Combine(tmp, "ref")));
        Check(Compare.Graph(Path.Combine(orig.Files, "IfAll.usd"), Path.Combine(orig.Files, "IfAll.usd")).Count == 0, "compare: identity");
        var imgA = a.CspAnim.Tex.ImageBuffers[4].Data.ImageData.Take(32).ToArray(); var imgL = a.CspAnim.Tex.ImageBuffers[(int)UiAnim.ValueAt(ck, 1 + CspStride)].Data.ImageData.Take(32).ToArray();
        Check(imgA.SequenceEqual(imgL), "add: new portrait is a copy of --like's");
        Ops.Undo(a, new Options());
        Check(SameHashes(h0, Hashes(dir)), "undo after add: byte-identical, added file removed");

        // 3. --reuse-images keys the existing images
        b = MexBuild.Open(dir);
        Ops.Add(b, new Options { Internal = 1, Costume = newCostume, Like = 0, ReuseImages = true });
        a = MexBuild.Open(dir);
        Check(a.CspAnim.Tex.ImageCount == 4 && UiAnim.ValueAt(a.CspAnim.Keys()[1], 1 + CspStride * 3) == 1, "reuse-images: no new image, --like's index");
        Ops.Undo(a, new Options());
        Check(SameHashes(h0, Hashes(dir)), "undo after reuse-images add");

        // 4. add then remove: structurally back to the original; refusals on remove
        b = MexBuild.Open(dir);
        Ops.Add(b, new Options { Internal = 1, Costume = newCostume, Like = 1 });
        a = MexBuild.Open(dir);
        Refuses(() => Ops.Remove(a, new Options { Internal = 1, Row = 1 }), "remove: not the last row", "last");
        Ops.Remove(a, new Options { Internal = 1 });
        a = MexBuild.Open(dir);
        Check(Compare.Graph(Path.Combine(orig.Files, "MxDt.dat"), Path.Combine(a.Files, "MxDt.dat")).Count == 0, "remove: MxDt structurally equal to the original");
        Check(Compare.Ui(orig, a).Count == 0, "remove: every portrait/stock frame shows the original image");
        Check(a.CspAnim.Tex.ImageCount == 4 && a.StockAnim.Tex.ImageCount == 7, "remove: appended images dropped");
        Check(!File.Exists(Path.Combine(dir, "files", "PlBbXx.dat")), "remove: file deleted");
        Refuses(() => Ops.Remove(a, new Options { Internal = 1 }), "remove: retail row refused", "not added by this tool");
        Ops.Undo(a, new Options()); Ops.Undo(MexBuild.Open(dir), new Options());
        Check(SameHashes(h0, Hashes(dir)), "undo remove then undo add: byte-identical");

        // 5. replace
        b = MexBuild.Open(dir);
        Refuses(() => Ops.Replace(b, new Options { Internal = 1, Row = 1, Costume = newCostume }), "replace: mismatched symbols refused", "retag");
        var re = Path.Combine(tmp, "PlBbRe2.dat"); CostumeFile(re, "Bb", "Re"); File.AppendAllText(re, "");
        { var f = new HSDRawFile(re); f.Roots.Add(new HSDRootNode { Name = "extra", Data = new HSDAccessor { _s = new HSDStruct(4) } }); f.Save(re); }
        Ops.Replace(b, new Options { Internal = 1, Row = 1, Costume = re });
        Check(File.ReadAllBytes(Path.Combine(dir, "files", "PlBbRe.dat")).SequenceEqual(File.ReadAllBytes(re)), "replace: file overwritten");
        var hRep = Hashes(dir);
        Check(hRep["MxDt.dat"] == h0["MxDt.dat"] && hRep["MnSlChr.usd"] == h0["MnSlChr.usd"], "replace: tables untouched");
        Ops.Undo(MexBuild.Open(dir), new Options());
        Check(SameHashes(h0, Hashes(dir)), "undo replace");

        // 6. add refusals
        b = MexBuild.Open(dir);
        Refuses(() => Ops.Add(b, new Options { Internal = 2, Costume = newCostume }), "add: fighter without CSS slot", "no CSS slot");
        Refuses(() => Ops.Add(b, new Options { Internal = 1, Costume = noJoint }), "add: no *_Share_joint root", "_Share_joint");
        Refuses(() => Ops.Add(b, new Options { Internal = 1, Costume = newCostume, Name = "PlBbRe.dat" }), "add: name clash", "already exists");
        Refuses(() => Ops.Add(b, new Options { Internal = 1, Costume = newCostume, Vis = 5 }), "add: visibility index out of range", "visibility");
        Refuses(() => Ops.Add(b, new Options { Internal = 1, Costume = newCostume, Like = 7 }), "add: --like out of range", "--like");
        Refuses(() => Ops.Add(b, new Options { Internal = 9, Costume = newCostume }), "add: unknown fighter", "out of range");
        Check(SameHashes(h0, Hashes(dir)), "refusals touched nothing");
        Check(!Directory.Exists(Path.Combine(dir, "skinfitter-undo")) || Ops.Manifests(b, new Options()).All(m => (string)m.m["state"] != "pending"), "no pending undo records");

        // 7. invariant failures refuse before editing
        var bad = MexBuild.Open(dir);
        var idsBad = bad.Fd.CostumeIDs[1]; idsBad.CostumeCount = 2; bad.Fd.CostumeIDs[1] = idsBad; bad.Mx.Save(Path.Combine(bad.Files, "MxDt.dat"));
        var hb = Hashes(dir);
        b = MexBuild.Open(dir);
        Check(b.Verify().Any(s => s.Contains("CostumeIDs count")), "verify: count mismatch found");
        Refuses(() => Ops.Add(b, new Options { Internal = 1, Costume = newCostume }), "add: refuses a build that fails verification", "fails verification");
        Check(SameHashes(hb, Hashes(dir)), "failed-invariant refusal touched nothing");
        MakeBuild(dir); File.Delete(Path.Combine(dir, "files", "PlAaRe.dat"));
        Check(MexBuild.Open(dir).Verify().Any(s => s.Contains("missing")), "verify: missing costume file found");
        MakeBuild(dir); CostumeFile(Path.Combine(dir, "files", "PlAaRe.dat"), "Aa", "Zz");
        Check(MexBuild.Open(dir).Verify().Any(s => s.Contains("lacks")), "verify: wrong joint symbol found");

        // 8. unrecognised builds
        MakeBuild(dir, badFighterSize: true);
        Refuses(() => MexBuild.Open(dir), "open: FighterData size not 0x90", "unrecognised");
        MakeBuild(dir, noStock: true);
        Refuses(() => MexBuild.Open(dir), "open: no Stc_icns root", "Stc_icns");
        Directory.CreateDirectory(Path.Combine(tmp, "empty"));
        Refuses(() => MexBuild.Open(Path.Combine(tmp, "empty")), "open: not an m-ex build", "MxDt");

        // 9. undo refuses when a file changed after the edit; --force restores anyway
        MakeBuild(dir); h0 = Hashes(dir);
        Ops.Add(MexBuild.Open(dir), new Options { Internal = 0, Costume = newCostume, Name = "PlAaXx.dat" });
        File.AppendAllText(Path.Combine(dir, "files", "MxDt.dat"), "x");
        Refuses(() => Ops.Undo(MexBuild.Open(Path.Combine(dir)), new Options()), "undo: changed file refused", "changed since");
        Ops.Undo(MexBuild.Open(dir), new Options { Force = true });
        Check(SameHashes(h0, Hashes(dir)), "undo --force restores byte-identical");

        // 10. a costume file without a matanim root gives a null matanim symbol
        MakeBuild(dir); h0 = Hashes(dir);
        var nomat = Path.Combine(tmp, "nomat.dat"); CostumeFile(nomat, "Aa", "Yy", matanim: false);
        Ops.Add(MexBuild.Open(dir), new Options { Internal = 0, Costume = nomat, Name = "PlAaYy.dat" });
        Check(MexBuild.Open(dir).GetFighter(0).Rows[2].MatAnim == null, "add: matanim null when the file has none");

        Directory.Delete(tmp, true);
        Console.WriteLine($"mexcostume tests: {pass} passed, {fail} failed");
        return fail == 0 ? 0 : 1;
    }
}
