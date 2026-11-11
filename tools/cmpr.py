# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 GD (Gurekam Dhillon)
"""GX texture decoders (CMPR, RGBA8, RGB5A3) for base64 texture records in model_export JSON."""
import base64,struct,numpy as np
def c565(c): return np.array([((c>>11)&31)*255//31,((c>>5)&63)*255//63,(c&31)*255//31,255])
def decode(t):
    d=base64.b64decode(t['data']);w,h=t['w'],t['h'];img=np.zeros((h,w,4),np.uint8);o=0
    for by in range(0,h,8):
        for bx in range(0,w,8):
            for sy in (0,4):
                for sx in (0,4):
                    a,b=struct.unpack('>HH',d[o:o+4]);bits=struct.unpack('>I',d[o+4:o+8])[0];o+=8
                    A,Bc=c565(a),c565(b)
                    if a>b: pal=[A,Bc,(2*A+Bc)//3,(A+2*Bc)//3]
                    else: pal=[A,Bc,(A+Bc)//2,np.array([0,0,0,0])]
                    for i in range(16):
                        idx=(bits>>(30-2*i))&3;y,x=by+sy+i//4,bx+sx+i%4
                        if y<h and x<w: img[y,x]=pal[idx]
    return img

def rgba8(t):
    d=base64.b64decode(t['data']);w,h=t['w'],t['h'];img=np.zeros((h,w,4),np.uint8);o=0
    for by in range(0,h,4):
        for bx in range(0,w,4):
            ar=d[o:o+32];gb=d[o+32:o+64];o+=64
            for i in range(16):
                y,x=by+i//4,bx+i%4
                img[y,x]=[ar[2*i+1],gb[2*i],gb[2*i+1],ar[2*i]]
    return img

def rgb5a3(t):
    d=base64.b64decode(t['data']);w,h=t['w'],t['h'];img=np.zeros((h,w,4),np.uint8);o=0
    for by in range(0,h,4):
        for bx in range(0,w,4):
            for i in range(16):
                c=struct.unpack('>H',d[o:o+2])[0];o+=2;y,x=by+i//4,bx+i%4
                if c&0x8000: img[y,x]=[((c>>10)&31)*255//31,((c>>5)&31)*255//31,(c&31)*255//31,255]
                else: img[y,x]=[((c>>8)&15)*17,((c>>4)&15)*17,(c&15)*17,((c>>12)&7)*255//7]
    return img

def any_(t):
    return {'CMPR':decode,'RGBA8':rgba8,'RGB5A3':rgb5a3}[t['fmt']](t)
