"""Crops and encodes the raw harness captures into the site's assets.

Usage: encode.py WORKDIR SITE_ASSETS_DIR. Needs ffmpeg and Pillow (python3-pil) and the Noto Sans font.
For each animation writes NAME.mp4 (libx264, yuv420p, even size, +faststart), NAME.webp (animated fallback),
NAME-still.webp (reduced-motion still; decks uses NAME-poster.webp). Also the theme stills and three pictures.
Every scenario must prove its state changes: first/last (and any-frame) pixel difference is checked and the
run fails when frames are identical. Budgets (same as site/check.py): MP4 300 KB, animated WebP 400 KB, still 120 KB."""
import os, subprocess, sys, tempfile
from PIL import Image, ImageChops, ImageDraw, ImageFont, ImageStat
W=os.path.abspath(sys.argv[1]); R=f"{W}/raw"; OUT=os.path.abspath(sys.argv[2])
KB=1024; B_MP4, B_ANIM, B_STILL = 300*KB, 400*KB, 120*KB
F="/usr/share/fonts/truetype/noto/NotoSans-Bold.ttf"; BG=(18,12,26)
report=[]; failed=[]
def have(*names): return all(os.path.exists(f"{R}/{n}.png") for n in names)  # a subset run renders only some scenarios
def load(n): return Image.open(f"{R}/{n}.png").convert("RGB")
def crop(im, box, size):  # box in image pixels, then resized to size
    return im.crop(box).resize(size, Image.LANCZOS)
def diff(a,b):
    """Percent of pixels that differ visibly (>24 in some channel): small UI changes such as a chip going hollow count."""
    d=ImageChops.difference(a,b).convert("L").point(lambda v:255 if v>24 else 0)
    return 100*d.histogram()[255]/(a.width*a.height)
def size(p): return os.path.getsize(p)
def save_still(im, name, q=85):
    p=f"{OUT}/{name}.webp"
    for qq in (q,75,65,55):
        im.save(p,"WEBP",quality=qq,method=6)
        if size(p)<=B_STILL: break
    return p
def save_webp(frames, durs, name):
    p=f"{OUT}/{name}.webp"
    for q in (80,70,60,50,40):
        frames[0].save(p,"WEBP",save_all=True,append_images=frames[1:],duration=durs,loop=0,quality=q,method=6)
        if size(p)<=B_ANIM: break
    return p
def save_mp4(frames, durs, name, fps):
    p=f"{OUT}/{name}.mp4"
    with tempfile.TemporaryDirectory() as t:
        lines=[]
        for i,(f,d) in enumerate(zip(frames,durs)):
            fp=f"{t}/{i:04d}.png"; f.save(fp); lines+= [f"file '{fp}'", f"duration {d/1000:.4f}"]
        lines.append(f"file '{t}/{len(frames)-1:04d}.png'")  # concat demuxer drops the last duration without it
        open(f"{t}/l.txt","w").write("\n".join(lines))
        for crf in (30,33,36,39):
            subprocess.run(["ffmpeg","-nostdin","-loglevel","error","-y","-f","concat","-safe","0","-i",f"{t}/l.txt",
                "-vf",f"fps={fps},scale=trunc(iw/2)*2:trunc(ih/2)*2,format=yuv420p","-c:v","libx264","-crf",str(crf),
                "-preset","veryslow","-movflags","+faststart","-an",p],check=True)
            if size(p)<=B_MP4: break
    return p
def nframes_mp4(p):
    o=subprocess.run(["ffprobe","-v","error","-count_frames","-select_streams","v","-show_entries","stream=nb_read_frames","-of","csv=p=0",p],capture_output=True,text=True).stdout
    return int(o.strip().rstrip(','))
def proof(name, frames, loops=False):
    first=diff(frames[0],frames[-1]); anyd=max(diff(frames[0],f) for f in frames)
    ok=anyd>0.05 and (loops or first>0.05)
    if not ok: failed.append(f"{name}: frames do not change (first/last {first:.2f}, max {anyd:.2f})")
    return first,anyd
def anim(name, raw, box, out, holds, still_idx, still_name=None, mp4=True, fps=20, loops=False, webp_w=None):
    """raw: raw frame names; holds: ms per frame; still_idx: frame used for the still; box crop in 1x px."""
    if not have(*raw): return None  # scenario not rendered in this run
    frames=[crop(load(r),box,out) for r in raw]
    f,a=proof(name,frames,loops)
    row=dict(name=name,frames=len(frames),first_last=round(f,2),max_change=round(a,2))
    wf=frames if not webp_w else [f.resize((webp_w,round(f.height*webp_w/f.width)),Image.LANCZOS) for f in frames]
    row["webp"]=save_webp(wf,holds,name)
    if mp4:
        row["mp4"]=save_mp4(frames,holds,name,fps); row["mp4_frames"]=nframes_mp4(row["mp4"])
    row["still"]=save_still(frames[still_idx], still_name or f"{name}-still")
    report.append(row)
    return frames

# decks: one bar, 24 frames at 11 fps, poster = first frame
anim("decks",[f"decks-{i:02d}" for i in range(24)],(0,203,1200,900),(720,418),[91]*24,0,"decks-poster",fps=11,loops=True,webp_w=560)
# search: m, c, e, m typed; still = the narrowed list. WebP only (no MP4).
anim("search",[f"search-{i:02d}" for i in range(5)],(100,110,1100,521),(720,296),[600,600,600,600,1800],4,mp4=False)
# tracklist: crate + two songs added, then Esc shows the strip. holds from the 10 captured states
anim("tracklist",[f"tracklist-{i:02d}" for i in range(10)],(0,0,1200,900),(800,600),[1100,900,1300,700,1100,700,1100,700,1100,2600],9)
# stems: pairs of settled frames per state: all on, VOX off, VOX+DRMS off, VOX off (DRMS back), all on again
anim("stems",[f"stems-{i:02d}" for i in (1,3,5,7,9)],(0,330,1200,625),(960,236),[1300]*5,1,loops=True)
# backspin: 34 frames at 20 fps, last held 0.9 s
anim("backspin",[f"backspin-{i:02d}" for i in range(34)],(0,330,1200,625),(960,236),[50]*33+[950],0,webp_w=720)
# tryon: 11 themes; WebP only; still = The Birthday Massacre
anim("tryon",[f"tryon-{i:02d}" for i in range(11)],(0,0,1200,900),(640,480),[900]*11,6,mp4=False)

# theme stills: 2x capture, the area under the Track List, 1440x852
names={"The_Birthday_Massacre":"the-birthday-massacre","Classic":"classic","Aphex_Twin":"aphex-twin",
       "Type_O_Negative":"type-o-negative","The_Prodigy":"the-prodigy","Serato":"serato"}
imgs=[]
for raw,slug in names.items():
    if not have(f"theme-{raw}"): continue
    im=crop(load(f"theme-{raw}"),(0,380,2400,1800),(1440,852)); imgs.append(im)
    p=save_still(im,f"theme-{slug}"); report.append(dict(name=f"theme-{slug}",still=p))
for im in imgs[1:]:
    if diff(imgs[0],im)<0.5: failed.append("theme stills are identical to the first")

# three pictures the page also shows (made here from fresh captures, not copied from pictures/)
def label(im, text, xy, sz):
    d=ImageDraw.Draw(im); f=ImageFont.truetype(F,sz); x,y=xy; l,t,r,b=d.textbbox((x,y),text,font=f); pad=int(sz*.45)
    d.rounded_rectangle((l-pad,t-pad,r+pad,b+pad),radius=pad,fill=(0,0,0)); d.text((x,y),text,font=f,fill=(255,255,255))
if have("wavestyle-3band","wavestyle-rgb"):
  a=load("wavestyle-3band").crop((1140,1200,3600,1848)); b=load("wavestyle-rgb").crop((1140,1200,3600,1848))
  c=Image.new("RGB",(a.width,a.height*2+12),BG); c.paste(a,(0,0)); c.paste(b,(0,a.height+12))
  label(c,"3-BAND",(36,a.height-90),44); label(c,"RGB",(36,2*a.height+12-90),44)
  if diff(a,b)<0.5: failed.append("waveform-styles: 3-BAND and RGB frames are identical")
  p=save_still(c.resize((760,round(760*c.height/c.width)),Image.LANCZOS),"waveform-styles"); report.append(dict(name="waveform-styles",still=p))
if have("magnet"):
  m=load("magnet").crop((0,1236,2580,2700)); p=save_still(m.resize((760,round(760*m.height/m.width)),Image.LANCZOS),"magnet-snap"); report.append(dict(name="magnet-snap",still=p))
# the whole guide: board plus the side panel from its title down (2400x1500 -> 1000x625)
if have("faceplate"):
  fp=load("faceplate").crop((0,0,2400,1500)); p=save_still(fp.resize((1000,625),Image.LANCZOS),"sholto-faceplate"); report.append(dict(name="sholto-faceplate",still=p))

print(f"{'asset':18}{'frames':>7}{'mp4 fr':>7}{'MP4 KB':>8}{'WebP KB':>9}{'still KB':>9}  first/last  max")
for r in report:
    k=lambda key: f"{size(r[key])/KB:.0f}" if key in r else "-"
    print(f"{r['name']:18}{r.get('frames','-'):>7}{r.get('mp4_frames','-'):>7}{k('mp4'):>8}{k('webp'):>9}{k('still'):>9}  {r.get('first_last','-')}  {r.get('max_change','-')}")
    for key,b in (("mp4",B_MP4),("webp",B_ANIM),("still",B_STILL)):
        if key in r and size(r[key])>b: failed.append(f"{r['name']} {key} over budget: {size(r[key])//KB} KB")
if failed:
    print("FAILED:"); [print(" ",f) for f in failed]; sys.exit(1)
