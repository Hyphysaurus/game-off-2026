import os, sys, numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi
S=sys.argv[1]
src=Image.open(os.environ.get('SHIFT_TITLE_SRC','Shift_Title_V2.png')).convert('RGBA')   # the title drawing: set SHIFT_TITLE_SRC to its path
a=np.asarray(src).astype(np.float32)/255
rgb=a[...,:3]*a[...,3:4]+(1-a[...,3:4])          # over white paper
lum=rgb@np.array([0.299,0.587,0.114],np.float32)
mn=rgb.min(axis=2)
H,W=lum.shape
# 1. the paper and everything it fades into: reachable from the border without crossing ink
passable=lum>0.30
lab,n=ndi.label(passable)
border=np.unique(np.concatenate([lab[0],lab[-1],lab[:,0],lab[:,-1]])); border=border[border>0]
R=np.isin(lab,border)
# 2. alpha: opaque for ink and for every enclosed shape (white fills included); outside, distance from white
alpha=np.ones((H,W),np.float32)
a0=np.clip((1-mn)*1.08,0,1)
alpha[R]=a0[R]
alpha[alpha<0.035]=0
# 3. specks: tiny lone marks out on the paper
solid=alpha>0.15
lab2,n2=ndi.label(solid)
sizes=ndi.sum(solid,lab2,index=np.arange(1,n2+1))
objs=ndi.find_objects(lab2)
specks=[]
for i,(sz,sl) in enumerate(zip(sizes,objs),start=1):
    if sz<900:
        cy=(sl[0].start+sl[0].stop)//2; cx=(sl[1].start+sl[1].stop)//2
        specks.append((int(sz),cx,cy))
        alpha[lab2==i]=0
print('components',n2,'removed specks',len(specks),sorted(specks,reverse=True)[:25])
# 4. take the white paper out of the colour where it is see-through
al=np.clip(alpha,1e-3,1)[...,None]
col=np.clip((rgb-(1-al))/al,0,1)
col[alpha==0]=0
out=np.dstack([col,alpha])
ys,xs=np.nonzero(alpha>0.02); pad=24
box=(max(xs.min()-pad,0),max(ys.min()-pad,0),min(xs.max()+pad,W),min(ys.max()+pad,H))
img=Image.fromarray((out*255+0.5).astype(np.uint8),'RGBA').crop(box)
print('art box',box,'size',img.size,'ratio',round(img.size[0]/img.size[1],3))
img.save(S+'/title_full.png')
prot=(mn>0.9)&(~R)
print('white kept inside shapes (px)',int(prot.sum()),' paper removed (px)',int((R&(alpha==0)).sum()))
# previews: on the menu's dark, and the kept-white map
def over(im,bg):
    b=Image.new('RGBA',im.size,bg); b.alpha_composite(im); return b.convert('RGB')
p=over(img,(22,21,34,255)); p.thumbnail((1500,800)); p.save(S+'/title_on_dark.png')
m=Image.fromarray((np.dstack([prot*255,R*90,np.zeros_like(R)]).astype(np.uint8)),'RGB'); m.thumbnail((1200,600)); m.save(S+'/title_masks.png')
