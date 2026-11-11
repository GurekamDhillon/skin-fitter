// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 GD (Gurekam Dhillon)
//
// m-ex costume rows on an extracted build, written from the prose specification
// "m-ex costume rows: on-disc format and edit procedure" (section numbers below refer to it) and HSDLib/HSDRaw's
// MIT-licensed HSDRaw.MEX accessors. Clean-room: no code from m-ex, mexTool or MexManager/mexLib.
using System.Security.Cryptography;
using HSDRaw;
using HSDRaw.Common;
using HSDRaw.Common.Animation;
using HSDRaw.MEX;
using HSDRaw.MEX.Menus;
using HSDRaw.Melee.Pl;
using HSDRaw.Tools;

namespace MexCostume;

public class RefusedException : Exception { public RefusedException(string m) : base(m) { } }

public record Row(string File, string Joint, string MatAnim, int Vis);

public class Fighter
{
    public int Internal; public int External = -1;          // -1: no CSS slot (spec 2: Nana)
    public string CharFile; public string CharSymbol;
    public List<Row> Rows = new();
    public int IdsCount = -1, Red = -1, Blue = -1, Green = -1; // CostumeIDs[e]; -1 without CSS slot
    public int PtrCount; public int PtrBlockSize;
    public bool HasKirbyHatTable;
    public int VisRows = -1;                                   // -1: not readable
}

/// <summary>One texture-animation track set (spec 3): TIMG and TCLT tracks of one HSD_TexAnim.</summary>
public class UiAnim
{
    public string Name; public HSD_TexAnim Tex; public HSD_AOBJ Aobj;
    public UiAnim(string name, HSD_TexAnim tex) { Name = name; Tex = tex; Aobj = tex?.AnimationObject; }
    public IEnumerable<HSD_FOBJDesc> Tracks()
    {
        for (var d = Aobj?.FObjDesc; d != null; d = d.Next)
            if (d.TrackType == (byte)TexTrackType.HSD_A_T_TIMG || d.TrackType == (byte)TexTrackType.HSD_A_T_TCLT) yield return d;
    }
    public int Bound(HSD_FOBJDesc d) => d.TrackType == (byte)TexTrackType.HSD_A_T_TIMG ? Tex.ImageCount : Tex.TlutCount;

    /// <summary>Spec 3: a frame with no key shows the previous key's value. null = no key at or before the frame.</summary>
    public static float? ValueAt(List<FOBJKey> keys, float frame)
    {
        FOBJKey best = null;
        foreach (var k in keys) if (k.Frame <= frame && (best == null || k.Frame >= best.Frame)) best = k;
        return best?.Value;
    }
    public Dictionary<byte, List<FOBJKey>> Keys()
    {
        var o = new Dictionary<byte, List<FOBJKey>>();
        foreach (var d in Tracks()) o[d.TrackType] = d.GetDecodedKeys();
        return o;
    }
    public void SetKeys(byte track, List<FOBJKey> keys)
    {
        foreach (var d in Tracks())
            if (d.TrackType == track) d.SetKeys(keys.OrderBy(k => k.Frame).ToList(), track);
        if (keys.Count > 0 && Aobj.EndFrame < keys.Max(k => k.Frame)) Aobj.EndFrame = keys.Max(k => k.Frame);
    }
}

public class MexBuild
{
    public string Root, Files;
    public HSDRawFile Mx, Css, If;
    public MEX_Data Data; public MEX_FighterData Fd; public MEX_mexSelectChr Sel; public MEX_Stock Stock;
    public UiAnim CspAnim, StockAnim;
    public string MetaHex;

    public static string FilesDir(string path)
    {
        if (File.Exists(Path.Combine(path, "MxDt.dat"))) return path;
        if (File.Exists(Path.Combine(path, "files", "MxDt.dat"))) return Path.Combine(path, "files");
        throw new RefusedException("not an extracted m-ex build: no files/MxDt.dat");
    }

    static HSDRootNode RootNamed(HSDRawFile f, string name, string file)
        => f.Roots.FirstOrDefault(r => r.Name == name) ?? throw new RefusedException($"{file}: no '{name}' root; not a recognised m-ex build");

    public static MexBuild Open(string path)
    {
        var b = new MexBuild();
        b.Files = FilesDir(Path.GetFullPath(path));
        b.Root = Path.GetFileName(b.Files) == "files" ? Path.GetDirectoryName(b.Files) : b.Files;
        foreach (var f in new[] { "MxDt.dat", "MnSlChr.usd", "IfAll.usd" })
            if (!File.Exists(Path.Combine(b.Files, f))) throw new RefusedException($"missing {f}; not a recognised m-ex build");
        b.Mx = new HSDRawFile(Path.Combine(b.Files, "MxDt.dat"));
        b.Css = new HSDRawFile(Path.Combine(b.Files, "MnSlChr.usd"));
        b.If = new HSDRawFile(Path.Combine(b.Files, "IfAll.usd"));
        var mr = RootNamed(b.Mx, "mexData", "MxDt.dat");
        // typed root: HSDRaw's writer keeps MEX_Data buffers unaligned and un-deduplicated only when it sees the type
        b.Data = new MEX_Data { _s = mr.Data._s }; mr.Data = b.Data;
        var sr = RootNamed(b.Css, "mexSelectChr", "MnSlChr.usd");
        b.Sel = new MEX_mexSelectChr { _s = sr.Data._s }; sr.Data = b.Sel;
        var ir = RootNamed(b.If, "Stc_icns", "IfAll.usd");
        b.Stock = new MEX_Stock { _s = ir.Data._s }; ir.Data = b.Stock;
        b.Fd = b.Data.FighterData ?? throw new RefusedException("mexData has no FighterData");
        // spec 6.6: mexTool-era and other layouts are unsupported; accept only the layout of spec section 2
        if (b.Data._s.Length < 0x38) throw new RefusedException($"mexData is 0x{b.Data._s.Length:X} bytes, expected 0x38: unrecognised m-ex layout (spec 6.6)");
        if (b.Fd._s.Length != 0x90) throw new RefusedException($"FighterData is 0x{b.Fd._s.Length:X} bytes, expected 0x90: unrecognised m-ex layout (spec 6.6)");
        if (b.Fd.CostumeIDs == null || b.Fd.CostumeFileSymbols == null || b.Fd.CostumePointers == null || b.Fd.DefineIDs == null)
            throw new RefusedException("FighterData lacks a costume table; not a recognised m-ex build");
        var meta = b.Data._s.GetReference<HSDAccessor>(0x00);
        b.MetaHex = meta == null ? "none" : Convert.ToHexString(meta._s.GetData().Take(16).ToArray());
        var cspTex = b.Sel.CSPMatAnim?.TextureAnimation;
        var stTex = b.Stock.MatAnimJoint?.MaterialAnimation?.TextureAnimation;
        if (b.Fd.DefineIDs.Length < b.Fd.CostumeIDs.Length) throw new RefusedException("DefineIDs shorter than CostumeIDs: unrecognised layout");
        if (b.Fd.CostumePointers.Length < b.Fd.CostumeFileSymbols.Length) throw new RefusedException("CostumePointers shorter than CostumeFileSymbols: unrecognised layout");
        if (cspTex == null) throw new RefusedException("mexSelectChr has no CSP texture animation");
        if (stTex == null) throw new RefusedException("Stc_icns has no stock texture animation");
        b.CspAnim = new UiAnim("CSP", cspTex); b.StockAnim = new UiAnim("stock", stTex);
        return b;
    }

    public int ExternalCount => Fd.CostumeIDs.Length;
    public int InternalCount => Fd.CostumeFileSymbols.Length;
    public int InternalOf(int e) => Fd.DefineIDs[e].InternalID;
    public int ExternalOf(int i) { for (int e = 0; e < Math.Min(ExternalCount, Fd.DefineIDs.Length); e++) if (InternalOf(e) == i) return e; return -1; }

    public List<Row> RowsOf(int i)
    {
        var t = Fd.CostumeFileSymbols[i].CostumeSymbols;
        var o = new List<Row>();
        if (t == null) return o;
        int n = Fd.CostumePointers[i].CostumeCount;   // the array accessor has no own count; HSDRaw pads empty arrays to 4 bytes
        int len = t.Length;
        for (int c = 0; c < Math.Max(n, len); c++)
        {
            if (c >= len) break;
            var r = t[c];
            if (r.FileName == null && c >= n) break;
            o.Add(new Row(r.FileName, r.JointSymbol, r.MatAnimSymbol, r.VisibilityLookupIndex));
        }
        return o;
    }

    public Fighter GetFighter(int i)
    {
        var f = new Fighter { Internal = i, External = ExternalOf(i) };
        if (Fd.CharFiles != null && i < Fd.CharFiles.Length)
        {
            var cf = Fd.CharFiles[i];
            try { f.CharFile = cf.FileName; f.CharSymbol = cf.Symbol; } catch { }
        }
        f.Rows = RowsOf(i);
        var p = Fd.CostumePointers[i];
        f.PtrCount = p.CostumeCount; f.PtrBlockSize = p.Pointer?._s.Length ?? 0;
        if (f.External >= 0) { var id = Fd.CostumeIDs[f.External]; f.IdsCount = id.CostumeCount; f.Red = id.RedCostumeIndex; f.Blue = id.BlueCostumeIndex; f.Green = id.GreenCostumeIndex; }
        var kc = Data.KirbyData?.KirbyCostumes;
        f.HasKirbyHatTable = kc != null && i < kc.Length && kc[i] != null;
        f.VisRows = VisRows(f);
        return f;
    }

    public int VisRows(Fighter f)
    {
        if (f.CharFile == null) return -1;
        var p = Path.Combine(Files, f.CharFile);
        if (!File.Exists(p)) return -1;
        try
        {
            var raw = new HSDRawFile(p);
            var r = (f.CharSymbol != null ? raw.Roots.FirstOrDefault(x => x.Name == f.CharSymbol) : null) ?? raw.Roots.FirstOrDefault(x => x.Name.StartsWith("ftData"));
            if (r == null) return -1;
            // HSDRaw SBM_FighterData +0x08 -> SBM_PlayerModelLookupTables, +0x04 CostumeVisibilityLookups
            var lt = new SBM_FighterData { _s = r.Data._s }.ModelLookupTables;
            // spec 5 gives no count field; HSDRaw VisibilityLookupLength is the model count, so the row count is the
            // length of the CostumeVisibilityLookups array (0x10 bytes per costume row)
            return lt?.CostumeVisibilityLookups?.Length ?? -1;
        }
        catch { return -1; }
    }

    public int CspFrame(int e, int c) => e + Sel.CSPStride * c;                          // spec 3
    public int StockFrame(int i, int c) => Stock.Reserved + i + Stock.Stride * c;        // spec 3

    /// <summary>Every frame a valid (fighter, costume) shows: the frames whose values must not change by accident.</summary>
    public List<int> ValidFrames(UiAnim a)
    {
        var o = new List<int>();
        if (a == CspAnim)
            for (int e = 0; e < ExternalCount; e++) { int n = Fd.CostumeIDs[e].CostumeCount; for (int c = 0; c < n; c++) o.Add(CspFrame(e, c)); }
        else
        {
            for (int r = 0; r < Stock.Reserved; r++) o.Add(r);
            for (int i = 0; i < InternalCount; i++) { int n = Fd.CostumePointers[i].CostumeCount; for (int c = 0; c < n; c++) o.Add(StockFrame(i, c)); }
        }
        return o;
    }

    public static IEnumerable<string> SymbolsOf(string datPath)
    {
        var raw = new HSDRawFile(datPath);
        return raw.Roots.Select(r => r.Name).Concat(raw.References.Select(r => r.Name)).ToList();
    }

    // ------------------------------------------------------------------------------------------------ verify (spec 5)
    public List<string> Verify(int fileScope = -1)   // fileScope >= 0: a partial files/ copy; check row files of that internal id only
    {
        var bad = new List<string>();
        var seenTables = new Dictionary<HSDStruct, int>();
        for (int i = 0; i < InternalCount; i++)
        {
            var f = GetFighter(i);
            var tbl = Fd.CostumeFileSymbols[i].CostumeSymbols;
            if (tbl != null && f.Rows.Count > 0)
            {
                if (seenTables.TryGetValue(tbl._s, out var other)) bad.Add($"internal {i}: costume rows shared with internal {other}");
                else seenTables[tbl._s] = i;
            }
            string who = $"internal {i}" + (f.External >= 0 ? $" (external {f.External})" : "");
            if (f.External >= 0 && f.IdsCount != f.Rows.Count) bad.Add($"{who}: CostumeIDs count {f.IdsCount} != {f.Rows.Count} rows");
            if (f.PtrCount != f.Rows.Count) bad.Add($"{who}: CostumePointers count {f.PtrCount} != {f.Rows.Count} rows");
            if (f.PtrBlockSize < 0x18 * f.PtrCount) bad.Add($"{who}: runtime block 0x{f.PtrBlockSize:X} < 0x18 x {f.PtrCount}");
            if (f.External >= 0)
                foreach (var (n, v) in new[] { ("red", f.Red), ("blue", f.Blue), ("green", f.Green) })
                    if (f.IdsCount > 0 && v >= f.IdsCount) bad.Add($"{who}: {n} team index {v} >= count {f.IdsCount}");
            for (int c = 0; c < f.Rows.Count; c++)
            {
                var r = f.Rows[c];
                if (r.File == null || r.Joint == null) { bad.Add($"{who} row {c}: null file name or joint symbol"); continue; }
                if (r.Vis < 0 || (f.VisRows >= 0 && r.Vis >= f.VisRows)) bad.Add($"{who} row {c}: visibility index {r.Vis} outside 0..{f.VisRows - 1}");
                if (fileScope >= 0 && fileScope != i) continue;
                var p = Path.Combine(Files, r.File);
                if (!File.Exists(p)) { bad.Add($"{who} row {c}: {r.File} missing"); continue; }
                List<string> syms;
                try { syms = SymbolsOf(p).ToList(); } catch (Exception ex) { bad.Add($"{who} row {c}: {r.File} unreadable ({ex.GetType().Name})"); continue; }
                if (!syms.Contains(r.Joint)) bad.Add($"{who} row {c}: {r.File} lacks {r.Joint}");
                if (r.MatAnim != null && !syms.Contains(r.MatAnim)) bad.Add($"{who} row {c}: {r.File} lacks {r.MatAnim}");
            }
        }
        foreach (var a in new[] { CspAnim, StockAnim })
        {
            var keys = a.Keys();
            if (!keys.ContainsKey((byte)TexTrackType.HSD_A_T_TIMG)) { bad.Add($"{a.Name}: no image track"); continue; }
            foreach (var fr in ValidFrames(a))
                foreach (var d in a.Tracks())
                {
                    var v = UiAnim.ValueAt(keys[d.TrackType], fr);
                    if (v == null) bad.Add($"{a.Name} frame {fr}: no key at or before it ({(TexTrackType)d.TrackType})");
                    else if (v < 0 || v >= a.Bound(d)) bad.Add($"{a.Name} frame {fr}: value {v} >= count {a.Bound(d)} ({(TexTrackType)d.TrackType})");
                }
        }
        return bad;
    }

    // ------------------------------------------------------------------------------------------------ UI keys (spec 3, 4)
    /// <summary>Set (track-wise) the value at <paramref name="frame"/> to the value shown at <paramref name="fromFrame"/>
    /// (reusing an existing image, which needs no new art), then pin every other valid frame back to its old value.</summary>
    public void CopyUiKey(UiAnim a, int fromFrame, int frame, IEnumerable<int> validBefore, bool appendImage)
    {
        var keys = a.Keys();
        var before = validBefore.Distinct().ToDictionary(f => f, f => keys.ToDictionary(k => k.Key, k => UiAnim.ValueAt(k.Value, f)));
        var newVal = new Dictionary<byte, float>();
        foreach (var (track, list) in keys)
            newVal[track] = UiAnim.ValueAt(list, fromFrame) ?? throw new RefusedException($"{a.Name}: no image at frame {fromFrame} to reuse");
        if (appendImage)
        {
            // spec 3: the community tool appends one image (and palette) per added costume. The new entry points at the
            // same image/palette data as costume --like (a copy of the picture), ready to be swapped for new art.
            const byte TIMG = (byte)TexTrackType.HSD_A_T_TIMG, TCLT = (byte)TexTrackType.HSD_A_T_TCLT;
            var tobj = new HSD_TOBJ { ImageData = a.Tex.ImageBuffers[(int)newVal[TIMG]].Data };
            if (newVal.ContainsKey(TCLT) && a.Tex.TlutBuffers != null) tobj.TlutData = a.Tex.TlutBuffers[(int)newVal[TCLT]].Data;
            a.Tex.AddImage(tobj, out int ii, out int pi);
            newVal[TIMG] = ii;
            if (newVal.ContainsKey(TCLT)) newVal[TCLT] = pi;
        }
        foreach (var (track, list) in keys)
        {
            var v = newVal[track];
            list.RemoveAll(k => k.Frame == frame);
            list.Add(new FOBJKey { Frame = frame, Value = v, InterpolationType = GXInterpolationType.HSD_A_OP_CON });
        }
        Pin(keys, before, frame);
        foreach (var (t, l) in keys) a.SetKeys(t, l);
    }

    public void RemoveUiKey(UiAnim a, int frame, IEnumerable<int> validAfter)
    {
        var keys = a.Keys();
        var before = validAfter.Distinct().ToDictionary(f => f, f => keys.ToDictionary(k => k.Key, k => UiAnim.ValueAt(k.Value, f)));
        var removed = keys.ToDictionary(k => k.Key, k => k.Value.FirstOrDefault(x => x.Frame == frame)?.Value);
        foreach (var l in keys.Values) l.RemoveAll(k => k.Frame == frame);
        Pin(keys, before, -1);
        foreach (var (t, l) in keys) a.SetKeys(t, l);
        // drop the image (and palette) the add appended, when it is the last one and nothing else keys it
        const byte TIMG = (byte)TexTrackType.HSD_A_T_TIMG, TCLT = (byte)TexTrackType.HSD_A_T_TCLT;
        if (removed.TryGetValue(TIMG, out var img) && img == a.Tex.ImageCount - 1 && !keys[TIMG].Any(k => k.Value == img)
            && (!removed.TryGetValue(TCLT, out var pal) || pal == null || (pal == a.Tex.TlutCount - 1 && !keys[TCLT].Any(k => k.Value == pal))))
            a.Tex.RemoveImageAt((int)img);
    }

    static void Pin(Dictionary<byte, List<FOBJKey>> keys, Dictionary<int, Dictionary<byte, float?>> before, int except)
    {
        foreach (var (f, vals) in before.OrderBy(x => x.Key))
        {
            if (f == except) continue;
            foreach (var (t, list) in keys)
            {
                var old = vals[t]; if (old == null) continue;
                if (UiAnim.ValueAt(list, f) != old)
                {
                    list.RemoveAll(k => k.Frame == f);
                    list.Add(new FOBJKey { Frame = f, Value = old.Value, InterpolationType = GXInterpolationType.HSD_A_OP_CON });
                }
            }
        }
    }

    public void Save()
    {
        Mx.Save(Path.Combine(Files, "MxDt.dat"));
        Css.Save(Path.Combine(Files, "MnSlChr.usd"));
        If.Save(Path.Combine(Files, "IfAll.usd"));
    }

    public static string Sha(string p) => File.Exists(p) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant() : null;
}
