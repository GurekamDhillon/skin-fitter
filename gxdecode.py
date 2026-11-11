# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 GD (Gurekam Dhillon)
"""Decode GameCube/Wii (GX) texture data to RGBA (numpy, h x w x 4 uint8), from the texture records in a
model_export source JSON ({"fmt", "w", "h", "data": base64, "palette_fmt", "palette": base64}).
Formats: I4, I8, IA4, IA8, RGB565, RGB5A3, RGBA8, CI4, CI8 (IA8/RGB565/RGB5A3 palettes), CMPR.
Written from the public GX texture format description (block tiling, big-endian texels)."""
import base64, struct
import numpy as np

BLOCK = {"I4": (8, 8), "I8": (8, 4), "IA4": (8, 4), "IA8": (4, 4), "RGB565": (4, 4), "RGB5A3": (4, 4),
         "RGBA8": (4, 4), "CI4": (8, 8), "CI8": (8, 4), "CMPR": (8, 8)}
FORMATS = set(BLOCK)


def _565(c):
    return [((c >> 11) & 31) * 255 // 31, ((c >> 5) & 63) * 255 // 63, (c & 31) * 255 // 31, 255]


def _5a3(c):
    if c & 0x8000:
        return [((c >> 10) & 31) * 255 // 31, ((c >> 5) & 31) * 255 // 31, (c & 31) * 255 // 31, 255]
    return [((c >> 8) & 15) * 17, ((c >> 4) & 15) * 17, (c & 15) * 17, ((c >> 12) & 7) * 255 // 7]


def _ia8(c):
    return [c & 255, c & 255, c & 255, c >> 8]


def palette(t):
    raw = base64.b64decode(t.get("palette") or "")
    f = {"IA8": _ia8, "RGB565": _565, "RGB5A3": _5a3}.get(t.get("palette_fmt"), _5a3)
    return [f(struct.unpack(">H", raw[i:i + 2])[0]) for i in range(0, len(raw) - 1, 2)]


def decode(t):
    fmt, w, h = t["fmt"], int(t["w"]), int(t["h"])
    if fmt not in FORMATS: raise ValueError("texture format %s not decodable" % fmt)
    d = base64.b64decode(t["data"]); bw, bh = BLOCK[fmt]
    img = np.zeros((h + bh, w + bw, 4), np.uint8); o = 0
    pal = palette(t) if fmt in ("CI4", "CI8") else None
    for by in range(0, h, bh):
        for bx in range(0, w, bw):
            if fmt == "CMPR":
                for sy in (0, 4):
                    for sx in (0, 4):
                        a, b = struct.unpack(">HH", d[o:o + 4]); bits = struct.unpack(">I", d[o + 4:o + 8])[0]; o += 8
                        A, B = np.array(_565(a)), np.array(_565(b))
                        p = [A, B, (2 * A + B) // 3, (A + 2 * B) // 3] if a > b else [A, B, (A + B) // 2, np.array([0, 0, 0, 0])]
                        for i in range(16):
                            img[by + sy + i // 4, bx + sx + i % 4] = p[(bits >> (30 - 2 * i)) & 3]
                continue
            if fmt == "RGBA8":
                ar, gb = d[o:o + 32], d[o + 32:o + 64]; o += 64
                for i in range(16): img[by + i // 4, bx + i % 4] = [ar[2 * i + 1], gb[2 * i], gb[2 * i + 1], ar[2 * i]]
                continue
            for i in range(bw * bh):
                y, x = by + i // bw, bx + i % bw
                if fmt in ("I4", "CI4"):
                    v = (d[o + i // 2] >> (4 if i % 2 == 0 else 0)) & 15
                    img[y, x] = pal[v] if pal and v < len(pal) else [v * 17] * 3 + [255]
                elif fmt in ("I8", "CI8"):
                    v = d[o + i]
                    img[y, x] = pal[v] if pal and v < len(pal) else [v] * 3 + [255]
                elif fmt == "IA4":
                    v = d[o + i]; img[y, x] = [(v & 15) * 17] * 3 + [(v >> 4) * 17]
                else:
                    c = struct.unpack(">H", d[o + 2 * i:o + 2 * i + 2])[0]
                    img[y, x] = {"IA8": _ia8, "RGB565": _565, "RGB5A3": _5a3}[fmt](c)
            o += {"I4": 32, "CI4": 32, "I8": 32, "CI8": 32, "IA4": 32}.get(fmt, 32)
    return img[:h, :w]


def encode_rgba8(img):
    """RGBA (h x w x 4, h and w multiples of 4) -> GX RGBA8 bytes."""
    h, w = img.shape[:2]; o = bytearray()
    for by in range(0, h, 4):
        for bx in range(0, w, 4):
            blk = img[by:by + 4, bx:bx + 4].reshape(16, 4); ar = bytearray(); gb = bytearray()
            for q in blk: ar += bytes([int(q[3]), int(q[0])]); gb += bytes([int(q[1]), int(q[2])])
            o += ar + gb
    return bytes(o)
