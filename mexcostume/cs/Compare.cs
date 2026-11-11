// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 GD (Gurekam Dhillon)
using HSDRaw;
using HSDRaw.Common.Animation;

namespace MexCostume;

/// <summary>Structural comparison for tests: layout-independent equality of two HSD archives, and of the images two
/// builds show for every (fighter, costume) portrait and stock frame.</summary>
public static class Compare
{
    public static List<string> Graph(string fileA, string fileB, int max = 20)
    {
        var a = new HSDRawFile(fileA); var b = new HSDRawFile(fileB); var o = new List<string>();
        var ra = a.Roots.Select(r => r.Name).ToList(); var rb = b.Roots.Select(r => r.Name).ToList();
        if (!ra.SequenceEqual(rb)) { o.Add($"root names differ: {string.Join(",", ra)} vs {string.Join(",", rb)}"); return o; }
        var seen = new Dictionary<HSDStruct, HSDStruct>();
        for (int k = 0; k < a.Roots.Count; k++) Walk(a.Roots[k].Data._s, b.Roots[k].Data._s, a.Roots[k].Name, seen, o, max);
        return o;
    }

    static void Walk(HSDStruct x, HSDStruct y, string path, Dictionary<HSDStruct, HSDStruct> seen, List<string> o, int max)
    {
        if (o.Count >= max) return;
        if (seen.TryGetValue(x, out var prev)) { if (prev != y) o.Add($"{path}: shared differently"); return; }
        seen[x] = y;
        var dx = x.GetData(); var dy = y.GetData();
        var px = x.References.Keys.ToHashSet(); var py = y.References.Keys.ToHashSet();
        if (!px.SetEquals(py)) { o.Add($"{path}: pointer slots differ ({px.Count} vs {py.Count})"); return; }
        int n = Math.Min(dx.Length, dy.Length);
        // lengths come from the layout (HSD archives do not store sizes); trailing slack is ignored when it is zero
        if (dx.Skip(n).Any(v => v != 0) || dy.Skip(n).Any(v => v != 0)) o.Add($"{path}: length {dx.Length} vs {dy.Length}");
        for (int i = 0; i < n; i++)
        {
            if (px.Contains(i - i % 4)) continue;   // pointer slot: compared by walking the target
            if (dx[i] != dy[i]) { o.Add($"{path}+0x{i:X}: {dx[i]:X2} vs {dy[i]:X2}"); break; }
        }
        foreach (var k in px.OrderBy(v => v)) Walk(x.References[k], y.References[k], $"{path}->+0x{k:X}", seen, o, max);
    }

    public static List<string> Ui(MexBuild a, MexBuild b, int max = 20)
    {
        var o = new List<string>();
        foreach (var (ua, ub) in new[] { (a.CspAnim, b.CspAnim), (a.StockAnim, b.StockAnim) })
        {
            var fa = a.ValidFrames(ua); var fb = b.ValidFrames(ub);
            if (!fa.SequenceEqual(fb)) { o.Add($"{ua.Name}: valid frame sets differ ({fa.Count} vs {fb.Count})"); continue; }
            var ka = ua.Keys(); var kb = ub.Keys();
            foreach (var f in fa)
            {
                if (o.Count >= max) return o;
                foreach (var t in ka.Keys)
                {
                    var va = UiAnim.ValueAt(ka[t], f); var vb = kb.ContainsKey(t) ? UiAnim.ValueAt(kb[t], f) : null;
                    byte[] ba = Payload(ua, t, va), bb = Payload(ub, t, vb);
                    if (ba == null || bb == null || !ba.SequenceEqual(bb)) o.Add($"{ua.Name} frame {f} ({(TexTrackType)t}): image {va} vs {vb} differ");
                }
            }
        }
        return o;
    }

    static byte[] Payload(UiAnim u, byte track, float? v)
    {
        if (v == null) return null; int i = (int)v;
        if (track == (byte)TexTrackType.HSD_A_T_TIMG)
        {
            var im = u.Tex.ImageBuffers[i].Data; if (im == null) return null;
            return BitConverter.GetBytes(im.Width).Concat(BitConverter.GetBytes(im.Height)).Concat(BitConverter.GetBytes((int)im.Format)).Concat(im.ImageData.Take(HSDRaw.Tools.GXImageConverter.GetImageSize(im.Format, im.Width, im.Height))).ToArray();
        }
        var tl = u.Tex.TlutBuffers[i].Data; if (tl == null) return null;
        return BitConverter.GetBytes((int)tl.Format).Concat(tl.TlutData.Take(tl.ColorCount * 2)).ToArray();
    }
}
