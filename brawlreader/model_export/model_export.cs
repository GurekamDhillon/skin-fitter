// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 GD (Gurekam Dhillon)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using BrawlLib.Internal;
using BrawlLib.Modeling;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Models;
using BrawlLib.Imaging;

// model_export <FitKirbyNN.pac> <out.json> [modelName]
// Bones (bind / inverse bind, BrawlLib Matrix memory order), objects as triangle-list facepoints in WORLD
// bind space (vertices bound to one bone are stored bone-local in MDL0; they are moved out here),
// materials, textures (raw GX bytes after the TEX0 header, + PLT0 palette bytes).
class ModelExport
{
    static CultureInfo IC = CultureInfo.InvariantCulture;
    static string F(float f) { if (float.IsNaN(f) || float.IsInfinity(f)) return "0"; return f.ToString("R", IC); }
    static string Esc(string s) { return s == null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""; }
    static unsafe string M(Matrix m) { var sb = new StringBuilder("["); float* p = (float*)&m; for (int i = 0; i < 16; i++) { if (i > 0) sb.Append(","); sb.Append(F(p[i])); } return sb.Append("]").ToString(); }
    static void Walk(ResourceNode n, List<ResourceNode> acc, Type t)
    {
        if (t.IsInstanceOfType(n)) acc.Add(n);
        List<ResourceNode> ch = null; try { ch = n.Children; } catch { }
        if (ch == null) return; foreach (var c in ch) Walk(c, acc, t);
    }
    static object GetF(object o, string name) { var f = o.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); return f == null ? null : f.GetValue(o); }

    static unsafe int Main(string[] args)
    {
        ResourceNode root = NodeFactory.FromFile(null, args[0]);
        var mdls = new List<ResourceNode>(); Walk(root, mdls, typeof(MDL0Node));
        MDL0Node mdl = null;
        foreach (var r in mdls) { if (args.Length > 2 ? r.Name == args[2] : !r.Name.EndsWith("Shd")) { mdl = (MDL0Node)r; break; } }
        var w = new StringBuilder();
        w.Append("{\"model\":" + Esc(mdl.Name) + ",\n\"bones\":[");
        var bones = new List<ResourceNode>(); Walk(mdl, bones, typeof(MDL0BoneNode));
        for (int i = 0; i < bones.Count; i++)
        {
            var b = (MDL0BoneNode)bones[i];
            if (i > 0) w.Append(",");
            w.Append("\n{\"name\":" + Esc(b.Name) + ",\"parent\":" + ((b.Parent is MDL0BoneNode) ? Esc(b.Parent.Name) : "null") +
                ",\"bind\":" + M(b.BindMatrix) + ",\"ibind\":" + M(b.InverseBindMatrix) + "}");
        }
        w.Append("],\n\"objects\":[");
        var objs = new List<ResourceNode>(); Walk(mdl, objs, typeof(MDL0ObjectNode));
        for (int oi = 0; oi < objs.Count; oi++)
        {
            var o = (MDL0ObjectNode)objs[oi];
            var pm = o._manager;
            string mat = null, vis = null;
            foreach (DrawCall dc in o._drawCalls) { if (mat == null) { mat = dc.Material; vis = dc.VisibilityBone; } }
            if (oi > 0) w.Append(",");
            w.Append("\n{\"name\":" + Esc(o.Name) + ",\"material\":" + Esc(mat) + ",\"visbone\":" + Esc(vis) + ",\"has\":[");
            for (int k = 0; k < 12; k++) { if (k > 0) w.Append(","); w.Append(pm._faceData[k] != null ? "1" : "0"); }
            w.Append("],\"tris\":[");
            var tri = GetF(pm, "_triangles");
            uint[] idx = tri == null ? new uint[0] : (uint[])GetF(tri, "_indices");
            ushort* vmap = (ushort*)pm._indices.Address;
            for (int t = 0; t < idx.Length; t++)
            {
                int fp = (int)idx[t];
                var v = pm._vertices[vmap[fp]];
                IMatrixNode node = v.GetMatrixNode();
                Vector3 pos = v._position;
                Vector3 nrm = pm._faceData[1] != null ? ((Vector3*)pm._faceData[1].Address)[fp] : new Vector3(0, 1, 0);
                if (node is MDL0BoneNode)
                {
                    Matrix bm = ((MDL0BoneNode)node).BindMatrix;
                    pos = bm * pos;
                    Matrix r = bm.GetRotationMatrix();
                    nrm = r * nrm;
                }
                if (t > 0) w.Append(",");
                w.Append("[" + F(pos._x) + "," + F(pos._y) + "," + F(pos._z) + "," + F(nrm._x) + "," + F(nrm._y) + "," + F(nrm._z));
                for (int u = 4; u < 6; u++)
                {
                    if (pm._faceData[u] != null) { var uv = ((Vector2*)pm._faceData[u].Address)[fp]; w.Append("," + F(uv._x) + "," + F(uv._y)); }
                    else w.Append(",0,0");
                }
                if (pm._faceData[2] != null) { var c = ((RGBAPixel*)pm._faceData[2].Address)[fp]; w.Append("," + c.R + "," + c.G + "," + c.B + "," + c.A); }
                else w.Append(",255,255,255,255");
                w.Append(",[");
                if (node is MDL0BoneNode) w.Append("[" + Esc(((MDL0BoneNode)node).Name) + ",1]");
                else if (node != null)
                {
                    int k = 0;
                    foreach (BoneWeight bw in node.Weights) { if (k++ > 0) w.Append(","); w.Append("[" + Esc(((ResourceNode)bw.Bone).Name) + "," + F(bw.Weight) + "]"); }
                }
                w.Append("]]");
            }
            w.Append("]}");
        }
        w.Append("],\n\"materials\":[");
        var mats = new List<ResourceNode>(); Walk(mdl, mats, typeof(MDL0MaterialNode));
        for (int i = 0; i < mats.Count; i++)
        {
            var m = (MDL0MaterialNode)mats[i];
            if (i > 0) w.Append(",");
            w.Append("\n{\"name\":" + Esc(m.Name) + ",\"cull\":" + Esc(m.CullMode.ToString()) + ",\"xlu\":" + (m.XLUMaterial ? "true" : "false") +
                ",\"stages\":" + m.ActiveShaderStages + ",\"c1mat\":" + Esc(m.C1MaterialColor.ToString()) + ",\"c1amb\":" + Esc(m.C1AmbientColor.ToString()) + ",\"refs\":[");
            int k = 0;
            foreach (var c in m.Children)
            {
                var r = c as MDL0MaterialRefNode; if (r == null) continue;
                if (k++ > 0) w.Append(",");
                w.Append("{\"texture\":" + Esc(r.Texture) + ",\"palette\":" + Esc(r.Palette) + ",\"u\":" + Esc(r.UWrapMode.ToString()) + ",\"v\":" + Esc(r.VWrapMode.ToString()) +
                    ",\"map\":" + Esc(r.MapMode.ToString()) + ",\"coord\":" + Esc(r.Coordinates.ToString()) + "}");
            }
            w.Append("]}");
        }
        w.Append("],\n\"textures\":[");
        var texs = new List<ResourceNode>(); Walk(root, texs, typeof(TEX0Node));
        for (int i = 0; i < texs.Count; i++)
        {
            var t = (TEX0Node)texs[i];
            var src = t.SourceNode;
            var hm = src.GetType().GetMethod("HeaderSize", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            int hs = (int)hm.Invoke(src, null);
            int len = src.WorkingUncompressed.Length - hs;
            var data = new byte[Math.Max(0, len)];
            Marshal.Copy((IntPtr)(void*)(src.WorkingUncompressed.Address + hs), data, 0, data.Length);
            if (i > 0) w.Append(",");
            w.Append("\n{\"name\":" + Esc(t.Name) + ",\"fmt\":" + Esc(t.Format.ToString()) + ",\"w\":" + t.Width + ",\"h\":" + t.Height + ",\"lod\":" + t.LevelOfDetail + ",\"hs\":" + hs + ",\"data\":\"" + Convert.ToBase64String(data) + "\"");
            var plt = t.GetPaletteNode();
            if (plt != null)
            {
                int phs = 0x40; var ps = plt.WorkingUncompressed;
                var pd = new byte[ps.Length - phs]; Marshal.Copy((IntPtr)(void*)(ps.Address + phs), pd, 0, pd.Length);
                w.Append(",\"palette_fmt\":" + Esc(plt.Format.ToString()) + ",\"palette\":\"" + Convert.ToBase64String(pd) + "\"");
            }
            w.Append("}");
        }
        w.Append("]}\n");
        File.WriteAllText(args[1], w.ToString());
        Console.WriteLine("bones " + bones.Count + " objects " + objs.Count + " materials " + mats.Count + " textures " + texs.Count);
        return 0;
    }
}
