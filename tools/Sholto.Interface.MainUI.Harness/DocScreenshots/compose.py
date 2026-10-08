"""Crops, labels and encodes the raw harness captures into the WebP images under pictures/.

Usage: compose.py WORKDIR PICTURES_DIR. Needs Pillow (python3-pil) and the Noto Sans font."""
import sys, os
from PIL import Image, ImageDraw, ImageFont
R=f"{os.path.abspath(sys.argv[1])}/raw"; P=sys.argv[2]
F="/usr/share/fonts/truetype/noto/NotoSans-Bold.ttf"
BG=(18,12,26)
def load(n): return Image.open(f"{R}/{n}.png").convert("RGB")
def crop(im, box, s):  # box in window (1x) coordinates
    return im.crop(tuple(int(v*s) for v in box))
def save(im, name, q=90):
    path=f"{P}/{name}.webp"; im.save(path,"WEBP",quality=q,method=6); print(name, im.size, os.path.getsize(path)//1024,"KB")
def label(im, text, xy, size):
    d=ImageDraw.Draw(im); f=ImageFont.truetype(F,size)
    x,y=xy; l,t,r,b=d.textbbox((x,y),text,font=f); pad=int(size*.45)
    d.rounded_rectangle((l-pad,t-pad,r+pad,b+pad),radius=pad,fill=(0,0,0)); d.text((x,y),text,font=f,fill=(255,255,255))

hero=load("main-The_Birthday_Massacre"); save(hero,"sholto-hero")
# montage 2x2
names=[("main-The_Birthday_Massacre","The Birthday Massacre"),("main-Classic","Classic"),("main-Aphex_Twin","Aphex Twin"),("main-Type_O_Negative","Type O Negative")]
g=16; w,h=1200,900; m=Image.new("RGB",(2*w+3*g,2*h+3*g),BG)
for i,(n,t) in enumerate(names):
    im=load(n).resize((w,h),Image.LANCZOS); d0=ImageDraw.Draw(im); tw=d0.textlength(t,font=ImageFont.truetype(F,28)); label(im,t,(w-int(tw)-30,h-56),28)
    m.paste(im,(g+(i%2)*(w+g), g+(i//2)*(h+g)))
save(m,"themes-montage")
# wizard steps, 2x captures, panel crops with a margin
save(crop(load("wiz-theme"),(130,140,1070,760),2),"wizard-theme")
save(crop(load("wiz-wave"),(130,186,1070,713),2),"wizard-waveform")
# waveform style cards close-up. 2x: a modal shot at 3x draws the dialog at 1x in the corner and the rest black.
# Window coordinates: the two style cards, from the top of the cards to the bottom of the legends.
save(crop(load("wiz-wave"),(170,306,1030,596),2),"wizard-waveform-closeup")
# Window rows (1x) of the layout, top down: top bar + hint, column headers, track list, then per deck a
# section map strip above the waveform row. DECK1_WAVE is the deck 1 waveform row, below its section map.
DECK1_WAVE=(402,618)
# 3-BAND vs RGB on a deck, 3x, deck 1 waveform
a=crop(load("main-3band"),(380,DECK1_WAVE[0],1200,DECK1_WAVE[1]),3); b=crop(load("main-rgb"),(380,DECK1_WAVE[0],1200,DECK1_WAVE[1]),3)
c=Image.new("RGB",(a.width,a.height*2+12),BG); c.paste(a,(0,0)); c.paste(b,(0,a.height+12))
label(c,"3-BAND",(36,a.height-90),44); label(c,"RGB",(36,2*a.height+12-90),44)
save(c.resize((c.width*2//3,c.height*2//3),Image.LANCZOS),"waveform-styles")
# magnet: discs, chain-link icon and the green downbeat glow, 3x
mg=crop(load("magnet"),(0,412,860,900),3); save(mg.resize((mg.width*2//3,mg.height*2//3),Image.LANCZOS),"magnet-snap")
# annotated guide from the hero
an=hero.copy(); d=ImageDraw.Draw(an); s=2; f=ImageFont.truetype(F,44); Y=(255,214,0)
# Boxes in window (1x) coordinates, numbered as docs/README.md "What's on screen". Deck 1 is the upper pair.
# Rows: top bar 0-31, hint line to 60, column headers 66-92, track list 93-327, section map 333-401,
# deck 1 row 402-618 (disc and chips left, title and key chip middle, waveform right of x 384).
boxes=[(1,(2,2,1198,31),(300,2)),(2,(2,66,1198,92),(600,64)),(3,(2,95,1198,327),(300,130)),
       (4,(2,333,1198,401),(600,330)),(5,(12,423,168,594),(125,415)),(6,(172,468,384,555),(300,495)),(7,(386,404,1198,618),(400,410))]
for n,b,(tx,ty) in boxes:
    d.rectangle(tuple(v*s for v in b),outline=Y,width=6)
    l,t,r,bb=d.textbbox((tx*s,ty*s),str(n),font=f); d.rectangle((l-10,t-8,r+10,bb+8),fill=(0,0,0)); d.text((tx*s,ty*s),str(n),font=f,fill=Y)
save(an,"sholto-guide-annotated")
# live theme try-on animation
frames=[Image.open(f"{R}/tryon-{i:02d}.png").convert("RGB").resize((960,720),Image.LANCZOS) for i in range(11)]
path=f"{P}/theme-tryon.webp"; frames[0].save(path,"WEBP",save_all=True,append_images=frames[1:],duration=1100,loop=0,quality=80,method=6)
print("theme-tryon", os.path.getsize(path)//1024,"KB")
# Glance deck slots: the header strip, full width, 52 px slots, 2x, one deck playing with the other empty (target), then the playing deck as target
box=(96,112,1104,182)
a=crop(load("slots-one-deck"),box,2); b=crop(load("slots-caution"),box,2)
c=Image.new("RGB",(a.width,a.height*2+8),BG); c.paste(a,(0,0)); c.paste(b,(0,a.height+8))
save(c,"glance-slots")
# Glance chips: the query box with a crate and a tag chip, the rail's active items and the narrowed list, 2x
save(crop(load("glance-chips-hero-both"),(100,180,1100,580),2),"glance-chips")
save(crop(load("glance-initials"),(100,180,1100,580),2),"glance-initials")
save(crop(load("glance-tokens"),(100,180,1100,580),2),"glance-tokens")
# Settings ▸ Platter feel: the panel with the Backspin time and Backspin distance knobs, 2x
save(crop(load("settings-platter"),(280,215,920,685),2),"settings-platter-feel")
