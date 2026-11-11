# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 GD (Gurekam Dhillon)
"""Optional helper from the first real conversion: bake Brawl's layered eye textures (White, Iris, Upper, Lower lid)
into one static RGBA8 eye texture per eye, so a Melee slot with a single texture can show them. Eyes do not blink.

    python tools/bake_eyes.py <source.json> <eyeL_uvs.json> <eyeR_uvs.json> <out.json> [--prefix FitLuigiEye]

source.json is model_export output; the *_uvs.json files hold {"fp": [...facepoints...]} for each eye's triangles
(px,py,.. with UV sets at indices 0..7 as used below). Adds textures BakedEyeL / BakedEyeR to out.json.
"""
import json,sys,os,base64,argparse,numpy as np
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)));import cmpr
ap=argparse.ArgumentParser();ap.add_argument("source");ap.add_argument("eyeL");ap.add_argument("eyeR");ap.add_argument("out");ap.add_argument("--prefix",default="FitLuigiEye")
A=ap.parse_args()
s=json.load(open(A.source));T={t['name']:t for t in s['textures']}
L={n:cmpr.any_(T[A.prefix+n]).astype(float)/255 for n in ('White','Iris','Upper','Lower')}
def samp(img,u,v):
    h,w=img.shape[:2];x=np.clip((u*w).astype(int),0,w-1);y=np.clip((v*h).astype(int),0,h-1);return img[y,x]
W,H=64,128
def bake(fp,tag):
    out=np.zeros((H,W,4));cov=np.zeros((H,W),bool)
    fp=np.array(fp)
    for t in range(0,len(fp),3):
        tri=fp[t:t+3];p=tri[:,0:2]*[W,H]
        x0,y0=np.floor(p.min(0)).astype(int);x1,y1=np.ceil(p.max(0)).astype(int)
        xs,ys=np.meshgrid(np.arange(max(x0,0),min(x1+1,W)),np.arange(max(y0,0),min(y1+1,H)))
        P=np.stack([xs.ravel()+.5,ys.ravel()+.5],1)
        a,b,c=p;M=np.array([b-a,c-a]).T
        if abs(np.linalg.det(M))<1e-9: continue
        l=np.linalg.solve(M,(P-a).T).T;l0=1-l.sum(1);ok=(l>=-0.02).all(1)&(l0>=-0.02)
        if not ok.any(): continue
        bw=np.stack([l0,l[:,0],l[:,1]],1)[ok];uv=bw@tri;px=P[ok].astype(int)
        col=samp(L['White'],uv[:,6],uv[:,7])
        for n,k in (('Iris',4),('Upper',0),('Lower',2)):
            c_=samp(L[n],uv[:,k],uv[:,k+1]);al=c_[:,3:4];col[:,:3]=col[:,:3]*(1-al)+c_[:,:3]*al
        col[:,3]=1;out[px[:,1],px[:,0]]=col;cov[px[:,1],px[:,0]]=True
    # dilate so bilinear filtering at edges does not pull in black
    for _ in range(4):
        nb=np.zeros_like(out);cnt=np.zeros((H,W))
        for dy,dx in((1,0),(-1,0),(0,1),(0,-1)):
            sh=np.roll(np.roll(out,dy,0),dx,1);sc=np.roll(np.roll(cov,dy,0),dx,1);nb[sc]+=sh[sc];cnt[sc]+=1
        add=(~cov)&(cnt>0);out[add]=nb[add]/cnt[add][:,None];cov|=add
    img=(np.clip(out,0,1)*255).astype(np.uint8);img[...,3]=255
    o=bytearray()
    for by in range(0,H,4):
        for bx in range(0,W,4):
            blk=img[by:by+4,bx:bx+4].reshape(16,4);ar=bytearray();gb=bytearray()
            for q in blk: ar+=bytes([q[3],q[0]]);gb+=bytes([q[1],q[2]])
            o+=ar+gb
    s['textures'].append({'name':'BakedEye'+tag,'fmt':'RGBA8','w':W,'h':H,'lod':1,'hs':64,'data':base64.b64encode(bytes(o)).decode()})
for tag,f in (('L',A.eyeL),('R',A.eyeR)): bake(json.load(open(f))['fp'],tag)
json.dump(s,open(A.out,'w'));print('ok')
