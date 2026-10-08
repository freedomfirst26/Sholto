"""Writes the harness scenarios behind the marketing-site animations and stills.

Usage: scenarios.py WORKDIR. Expects WORKDIR/library (made by capture.sh from ../DocScreenshots/tracks.tsv);
writes WORKDIR/sc/*.json, whose screenshots land in WORKDIR/raw/<scenario>-NN.png (and encode.py reads them).
Sequences are shot at 1x: back-to-back 2x screenshots alternate (every other frame renders at 1x in a 2x canvas).
Single 2x/3x stills are fine. The decks get a made-up analysis ("analyse" steps) because the harness runs no analysers."""
import json, os, sys
HERE=os.path.dirname(os.path.abspath(__file__))
S=os.path.abspath(sys.argv[1]); LIB=f"{S}/library"
rows=[l.rstrip('\n').split('\t') for l in open(f"{HERE}/../DocScreenshots/tracks.tsv")]
THEMES=["Classic","Serato","Front Line Assembly","Silence Groove","Jeremy Soule","Type O Negative",
        "The Birthday Massacre","Pantera","Dimmu Borgir","Aphex Twin","The Prodigy"]
SITE_THEMES=["The Birthday Massacre","Classic","Aphex Twin","Type O Negative","The Prodigy","Serato"]
HERO="The Birthday Massacre"
def apply_theme(n,rgb=False):
    i=THEMES.index(n)
    return [{"action":"click","target":"LayoutWizard"},{"action":"key","key":"Home"}]+[{"action":"key","key":"Right"}]*i+[
        {"action":"key","key":"Enter"},{"action":"key","key":"D2" if rgb else "D1"},{"action":"key","key":"Enter"}]
def shot(name,scale=1,sec=None):
    d={"action":"screenshot","out":f"{S}/raw/{name}.png","scale":scale}
    if sec is not None: d["seconds"]=sec
    return d
def lib(theme=HERO,rgb=False):
    a=[{"action":"scan","dir":LIB},{"action":"controller","on":True}]+apply_theme(theme,rgb)
    for ar,t,d,b,k in rows: a.append({"action":"row","track":f"{LIB}/{ar} - {t}.mp3","bpm":float(b),"camelot":k})
    return a
def load1(a,wait):
    return a+[{"action":"click","target":"TrackList","index":3},{"action":"key","key":"D1"},
        {"action":"analyse","deck":1,"seconds":360,"camelot":"8A"},{"action":"play","deck":1},{"action":"wait","seconds":wait}]
def decks(a,x=140,y=50):
    a=load1(a,x)
    return a+[{"action":"click","target":"TrackList","index":5},{"action":"key","key":"D2"},
        {"action":"analyse","deck":2,"seconds":360,"camelot":"9A"},{"action":"play","deck":2},{"action":"wait","seconds":y}]
def search_open(a): return load1(a,5)+[{"action":"database"},{"action":"key","key":"Space"},{"action":"wait","seconds":1.0}]
def write(n,a):
    os.makedirs(f"{S}/sc",exist_ok=True); os.makedirs(f"{S}/raw",exist_ok=True)
    json.dump({"actions":a},open(f"{S}/sc/{n}.json","w"),indent=1)

# decks: one bar of both decks playing, waveforms scrolling
a=decks(lib())
for i in range(24): a+=[{"action":"wait","seconds":0.08},shot(f"decks-{i:02d}",1,0.05)]
write("decks",a)
# search: initials typed letter by letter
a=search_open(lib())+[shot("search-00",1,0.6)]
for i,ch in enumerate("mcem"): a+=[{"action":"type","text":ch},shot(f"search-{i+1:02d}",1,0.6)]
write("search",a)
# tracklist: the harness skips startup (ShowTrackList), so Ctrl+L on songs is ignored until a crate is loaded
# first: Tab to the rail, Ctrl+L loads the selected crate, then Tab back and Ctrl+L on two songs.
a=search_open(lib())+[shot("tracklist-00",1,0.4),{"action":"key","key":"Tab"},shot("tracklist-01",1,0.3),
    {"action":"key","key":"L","ctrl":True},{"action":"wait","seconds":0.8},shot("tracklist-02",1,0.3),{"action":"key","key":"Tab"}]
n=3
for k in range(3):
    a+=[{"action":"key","key":"Down"}]*2+[shot(f"tracklist-{n:02d}",1,0.3)]; n+=1
    a+=[{"action":"key","key":"L","ctrl":True},shot(f"tracklist-{n:02d}",1,0.3)]; n+=1
a+=[{"action":"key","key":"Escape"},shot(f"tracklist-{n:02d}",1,0.6)]
write("tracklist",a)
# stems: deck 1 playing; pad mutes VOX, then DRMS, then both come back (two settled shots per state)
a=decks(lib(),x=60,y=20); n=0; s=[]
def SS():
    global n; r=shot(f"stems-{n:02d}",1,0.15); n+=1; return r
s+=[SS(),SS()]
for g in (1,0,0,1): s+=[{"action":"gesture","event":"StemToggle","deck":1,"group":g},SS(),SS()]
write("stems",a+s)
# backspin: deck 1 platter whipped back, then released
b=decks(lib(),x=60,y=20)
for i in range(4): b+=[{"action":"wait","seconds":0.05},shot(f"backspin-{i:02d}",1,0.05)]
b+=[{"action":"gesture","event":"JogTouch","deck":1,"on":True}]
for k in range(6): b+=[{"action":"gesture","event":"JogRotated","deck":1,"delta":-120},{"action":"wait","seconds":0.016}]
b+=[{"action":"gesture","event":"JogTouch","deck":1,"on":False}]
for i in range(4,34): b+=[{"action":"wait","seconds":0.05},shot(f"backspin-{i:02d}",1,0.05)]
write("backspin",b)
# tryon: the Layout Wizard wears each of the 11 themes in turn (frame i = THEMES[i])
a=decks(lib(),x=140,y=50)+[{"action":"click","target":"LayoutWizard"},{"action":"key","key":"Home"}]
for i in range(len(THEMES)): a+=[shot(f"tryon-{i:02d}",1),{"action":"key","key":"Right"}]
write("tryon",a)
# themes: both decks playing, each site theme applied, one 2x still each (single stills, so 2x is safe)
a=decks(lib())
for t in SITE_THEMES: a+=apply_theme(t)+[shot("theme-"+t.replace(' ','_'),2)]
write("themes",a)
# magnet: deck 1 = x+y, deck 2 = y; x = whole bars + 40 ms, so the discs sit on the same downbeat
write("magnet",decks(lib(),x=1.875*40+0.04,y=70)+[shot("magnet",3)])
# waveform styles: 3-BAND then RGB at 3x
a=decks(lib())
write("wavestyles",a+apply_theme(HERO,False)+[shot("wavestyle-3band",3)]+apply_theme(HERO,True)+[shot("wavestyle-rgb",3)])
# faceplate: the on-screen controller guide
write("faceplate",decks(lib(),x=60,y=20)+[{"action":"click","target":"FaceplateButton"},{"action":"wait","seconds":1.0},{"action":"gesture","event":"JogTouch","deck":1,"on":True},{"action":"wait","seconds":1.5},shot("faceplate",2,1.0)])
