"""Writes the harness scenarios behind the README/docs screenshots.

Usage: scenarios.py WORKDIR [HERO_THEME]. Expects WORKDIR/library (made by capture.sh); writes
WORKDIR/sc/*.json, whose screenshots land in WORKDIR/raw/.
The decks get a made-up analysis ("analyse" steps) because the harness runs no analysers."""
import json, sys, os
HERE=os.path.dirname(os.path.abspath(__file__))
S=os.path.abspath(sys.argv[1])
LIB=f"{S}/library"
rows=[l.rstrip('\n').split('\t') for l in open(f"{HERE}/tracks.tsv")]
THEMES=["Classic","Serato","Front Line Assembly","Silence Groove","Jeremy Soule","Type O Negative",
        "The Birthday Massacre","Pantera","Dimmu Borgir","Aphex Twin","The Prodigy"]
def base(x_sec=140, y_sec=50, theme=None):
    a=[{"action":"scan","dir":LIB},{"action":"controller","on":True}]
    if theme: a+=apply_theme(theme)
    for ar,t,d,b,k in rows:
        a.append({"action":"row","track":f"{LIB}/{ar} - {t}.mp3","bpm":float(b),"camelot":k})
    a+=[{"action":"click","target":"TrackList","index":3},{"action":"key","key":"D1"},
        {"action":"analyse","deck":1,"seconds":360,"camelot":"8A"},{"action":"play","deck":1},
        {"action":"wait","seconds":x_sec},
        {"action":"click","target":"TrackList","index":5},{"action":"key","key":"D2"},
        {"action":"analyse","deck":2,"seconds":360,"camelot":"9A"},{"action":"play","deck":2},
        {"action":"wait","seconds":y_sec},
        {"action":"click","target":"TrackList","index":7}]
    return a
def pick_theme(name):
    i=THEMES.index(name)
    return [{"action":"key","key":"Home"}]+[{"action":"key","key":"Right"}]*i
def apply_theme(name, rgb=False):
    a=[{"action":"click","target":"LayoutWizard"}]+pick_theme(name)+[{"action":"key","key":"Enter"}]
    a+=[{"action":"key","key":"D2" if rgb else "D1"},{"action":"key","key":"Enter"}]
    return a
def shot(out, scale=2): return {"action":"screenshot","out":f"{S}/raw/{out}.png","scale":scale}
def write(name, actions):
    os.makedirs(f"{S}/raw",exist_ok=True)
    json.dump({"actions":actions},open(f"{S}/sc/{name}.json","w"),indent=1)
os.makedirs(f"{S}/sc",exist_ok=True)
hero=sys.argv[2] if len(sys.argv)>2 else "The Birthday Massacre"
# hero + montage + style closeups
a=base()
for t in ["The Birthday Massacre","Classic","Aphex Twin","Type O Negative",hero]:
    a+=apply_theme(t)+[shot(f"main-{t.replace(' ','_')}")]
a+=apply_theme(hero, rgb=True)+[shot("main-rgb",3)]
a+=apply_theme(hero, rgb=False)+[shot("main-3band",3)]
write("main",a)
# wizard
# Apply the theme first: the waveform step's legend swatches are built when the wizard opens, from the
# theme worn then, and do not follow a try-on.
# The theme step is shot while trying the theme on (the CURRENT tag then sits on the short
# "Silence Groove" card instead of truncating the long name); Esc puts it back.
a=base()+[{"action":"click","target":"LayoutWizard"}]+pick_theme(hero)+[shot("wiz-theme"),{"action":"key","key":"Escape"}]
a+=apply_theme(hero)+[{"action":"click","target":"LayoutWizard"}]
a+=[{"action":"key","key":"Enter"},shot("wiz-wave")]
write("wizard",a)
# try-on animation frames
a=base()+[{"action":"click","target":"LayoutWizard"},{"action":"key","key":"Home"}]
for i in range(11):
    a+=[shot(f"tryon-{i:02d}",1),{"action":"key","key":"Right"}]
write("tryon",a)
# magnet: deck1 = x+y, deck2 = y; x = whole bars + 40 ms
a=base(x_sec=1.875*40+0.04, y_sec=70, theme=hero)+[shot("magnet",3)]
write("magnet",a)
# Glance LOAD TO slots: one deck playing with the other empty (the target), then the playing deck as the
# target (caution), 2x. Cropped by compose.py.
a=[{"action":"scan","dir":LIB},{"action":"controller","on":True}]+apply_theme(hero)
for ar,t,d,b,k in rows:
    a.append({"action":"row","track":f"{LIB}/{ar} - {t}.mp3","bpm":float(b),"camelot":k})
a+=[{"action":"click","target":"TrackList","index":3},{"action":"key","key":"D1"},
    {"action":"analyse","deck":1,"seconds":360,"camelot":"8A"},{"action":"play","deck":1},{"action":"wait","seconds":5},
    {"action":"database"},{"action":"key","key":"Space"},{"action":"wait","seconds":1.0},shot("slots-one-deck"),
    {"action":"key","key":"Right"},{"action":"wait","seconds":0.6},shot("slots-caution")]
write("slots",a)
# Glance chips: search open on the fit-ranked list, then a crate chip, then a crate and a tag chip, then text that
# leaves nothing. A "database" step files the harness's made-up crates and tags into its demo database (the real services).
# Rail keys: Tab to the rail (first crate, Peak time), Enter adds it; four Downs
# reach the first tag (Vocal).
def glance_open(theme):
    a=[{"action":"scan","dir":LIB},{"action":"controller","on":True}]+apply_theme(theme)
    for ar,t,d,b,k in rows:
        a.append({"action":"row","track":f"{LIB}/{ar} - {t}.mp3","bpm":float(b),"camelot":k})
    a+=[{"action":"click","target":"TrackList","index":3},{"action":"key","key":"D1"},
        {"action":"analyse","deck":1,"seconds":360,"camelot":"8A"},{"action":"play","deck":1},{"action":"wait","seconds":5},
        {"action":"database"},{"action":"key","key":"Space"},{"action":"wait","seconds":1.0}]
    return a
def glance_chips(theme, slug):
    a=glance_open(theme)+[shot(f"glance-chips-{slug}-none")]
    a+=[{"action":"key","key":"Tab"},{"action":"key","key":"Enter"},{"action":"wait","seconds":0.8},
        shot(f"glance-chips-{slug}-crate")]
    a+=[{"action":"key","key":"Down"}]*4+[{"action":"key","key":"Enter"},{"action":"wait","seconds":0.8},shot(f"glance-chips-{slug}-both")]
    a+=[{"action":"type","text":"zzzq"},{"action":"wait","seconds":0.8},shot(f"glance-chips-{slug}-empty")]
    return a
write("glance-chips",glance_chips(hero,"hero"))
# Glance initials and tokens: search open on the fit-ranked list, then initials (artist + title run), then a BPM range plus key.
write("glance-initials",glance_open(hero)+[{"action":"type","text":"mcem"},{"action":"wait","seconds":0.8},shot("glance-initials")])
write("glance-tokens",glance_open(hero)+[{"action":"type","text":"bpm:124-128 key:8a"},{"action":"wait","seconds":0.8},shot("glance-tokens")])
# Settings ▸ Platter feel: the Backspin time and Backspin distance knobs, 2x. Cropped by compose.py.
a=[{"action":"scan","dir":LIB},{"action":"controller","on":True}]+apply_theme(hero)
a+=[{"action":"click","target":"Settings"},{"action":"wait","seconds":0.6},shot("settings-platter")]
write("settings",a)
