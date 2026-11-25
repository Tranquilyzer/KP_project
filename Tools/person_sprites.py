"""Draws ORIGINAL top-down characters into the same slice rects as person_SpriteList.png,
so existing .anim / scene references keep working. Characters face +X (right)."""
import re, math, sys, json
from PIL import Image, ImageDraw

SRC_META, SRC_PNG, OUT_PNG, PREVIEW = sys.argv[1:5]

meta = open(SRC_META, encoding="utf-8").read()
src = Image.open(SRC_PNG)
SW, SH = src.size
slices = {}
for blk in re.split(r"\n    - serializedVersion: 2\n", meta)[1:]:
    m = re.search(r"name: person_SpriteList_(\d+)", blk)
    if not m: continue
    g = lambda k: float(re.search(r"\b" + k + r": (-?[\d.e-]+)", blk).group(1))
    r = re.search(r"rect:\s+serializedVersion: 2\s+x: ([\d.]+)\s+y: ([\d.]+)\s+width: ([\d.]+)\s+height: ([\d.]+)", blk)
    x, y, w, h = (int(float(v)) for v in r.groups())
    al = int(re.search(r"alignment: (\d+)", blk).group(1))
    pv = re.search(r"pivot: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", blk)
    px, py = (float(pv.group(1)), float(pv.group(2))) if al == 9 else (0.5, 0.5)
    slices[int(m.group(1))] = dict(x=x, y=y, w=w, h=h, px=px, py=py)

OUTL = (18, 14, 22, 255)
SKIN = (236, 186, 146, 255)
PAL = {
    "player":   dict(body=(48, 96, 178), arm=(48, 96, 178), hair=(92, 52, 26), band=(205, 40, 40),
                     legs=(32, 40, 68), shoe=(235, 235, 235)),
    "resident": dict(body=(132, 62, 160), arm=(132, 62, 160), hair=(28, 24, 26), stripe=(240, 240, 240),
                     legs=(60, 60, 64), shoe=(30, 30, 30)),
    "police":   dict(body=(96, 140, 200), arm=(96, 140, 200), hair=(30, 34, 70), cap=(28, 36, 78), badge=(240, 200, 40),
                     legs=(30, 36, 70), shoe=(20, 20, 20)),
}
BLOOD, BLOOD_D = (170, 18, 24, 255), (110, 8, 14, 255)

def C(c, k=1.0):
    return tuple(max(0, min(255, int(v * k))) for v in c[:3]) + (255,)

class F:
    def __init__(s, w, h):
        s.w, s.h = w, h
        s.im = Image.new("RGBA", (w, h), (0, 0, 0, 0)); s.p = s.im.load()
    def put(s, x, y, c):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < s.w and 0 <= y < s.h: s.p[x, y] = c
    def ell(s, cx, cy, rx, ry, col, lit=True):
        for y in range(s.h):
            for x in range(s.w):
                dx, dy = (x - cx) / rx, (y - cy) / ry
                if dx * dx + dy * dy <= 1.0:
                    k = 1.0
                    if lit:
                        t = dx + dy
                        k = 1.22 if t < -0.75 else (0.72 if t > 0.75 else 1.0)
                    s.put(x, y, C(col, k))
    def line(s, x0, y0, x1, y1, col, wdt=1):
        n = int(max(abs(x1 - x0), abs(y1 - y0)) * 2) + 1
        for i in range(n + 1):
            t = i / n; x = x0 + (x1 - x0) * t; y = y0 + (y1 - y0) * t
            if wdt == 1: s.put(x, y, col)
            else:
                r = (wdt - 1) / 2
                for yy in range(int(math.floor(y - r)), int(math.ceil(y + r)) + 1):
                    for xx in range(int(math.floor(x - r)), int(math.ceil(x + r)) + 1):
                        if (xx - x) ** 2 + (yy - y) ** 2 <= r * r + 0.3: s.put(xx, yy, col)
    def outline(s):
        a = [[s.p[x, y][3] > 0 for x in range(s.w)] for y in range(s.h)]
        for y in range(s.h):
            for x in range(s.w):
                if a[y][x]: continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    X, Y = x + dx, y + dy
                    if 0 <= X < s.w and 0 <= Y < s.h and a[Y][X] and s.p[X, Y][:3] not in (BLOOD[:3], BLOOD_D[:3]):
                        s.p[x, y] = OUTL; break

def head(f, cx, cy, who):
    P = PAL[who]
    f.ell(cx + 0.2, cy, 4.5, 4.5, OUTL, lit=False)
    if who == "police":
        f.ell(cx + 0.5, cy, 3.6, 3.6, SKIN, lit=False)
        f.ell(cx - 0.3, cy, 3.4, 3.8, P["cap"])
        for y in range(int(cy - 3), int(cy + 4)): f.put(cx + 3, y, C(P["cap"], 0.6))  # brim
        f.put(cx + 1, cy, P["badge"] + (255,))
    else:
        f.ell(cx + 0.5, cy, 3.6, 3.6, SKIN, lit=False)
        f.ell(cx - 0.6, cy, 3.0, 3.6, P["hair"])
        if who == "player":
            for y in range(int(cy - 4), int(cy + 5)): f.put(cx + 0.5, y, P["band"] + (255,))
            f.put(cx - 2.5, cy + 1, P["band"] + (255,)); f.put(cx - 3.5, cy + 2, P["band"] + (255,))
        f.put(cx + 2.6, cy - 1, C(SKIN, 0.75)); f.put(cx + 2.6, cy + 1, C(SKIN, 0.75))

def torso(f, cx, cy, who, ry):
    P = PAL[who]
    f.ell(cx - 1, cy, 3.4, ry, P["body"])
    if who == "resident":
        for y in range(int(cy - ry + 1), int(cy + ry)):
            f.put(cx - 4, y, P["stripe"] + (255,)) if abs(y - cy) > ry - 3 else None
    if who == "police":
        f.put(cx + 1, cy - ry + 3, P["badge"] + (255,))

def arm(f, sx, sy, hx, hy, who):
    f.line(sx, sy, hx, hy, C(PAL[who]["arm"], 0.85), 3)
    f.ell(hx, hy, 1.4, 1.4, SKIN, lit=False)

def walker(w, h, cx, cy, who, phase, ry=7):
    f = F(w, h)
    sw = math.sin(phase)
    arm(f, cx - 1, cy - ry + 2, cx + 2 + 3 * sw, cy - ry + 1.5, who)
    arm(f, cx - 1, cy + ry - 2, cx + 2 - 3 * sw, cy + ry - 1.5, who)
    torso(f, cx, cy, who, ry)
    head(f, cx, cy, who)
    f.outline(); return f.im

GUNS = {  # length, extra parts
    156: ("pistol", 7), 264: ("smg", 10), 310: ("ak", 15), 381: ("shotgun", 15),
}
def gunner(w, h, cx, cy, kind, ln, who="player"):
    f = F(w, h); ry = min(7, (h - 4) // 2)
    hx, hy = cx + 8, cy + 1
    gx1 = min(w - 2, hx + ln)
    metal, wood = (58, 58, 64, 255), (130, 78, 36, 255)
    if kind in ("ak", "shotgun"):
        f.line(cx + 2, cy + 1, hx, hy, wood if kind == "ak" else metal, 2)  # stock to shoulder
    f.line(hx - 1, hy, gx1, hy, metal, 2)
    f.line(hx, hy - 1, gx1 - 1, hy - 1, (110, 110, 118, 255))
    if kind == "ak":
        f.line(hx + 4, hy + 1, hx + 5, hy + 4, wood, 2)
        f.line(hx + 8, hy, hx + 12, hy, wood, 2)
    if kind == "smg":
        f.line(hx + 3, hy + 1, hx + 3, hy + 5, metal, 2)
    if kind == "shotgun":
        f.line(hx + 6, hy, hx + 10, hy, wood, 2)
    arm(f, cx - 1, cy - ry + 2, hx + (5 if kind != "pistol" else 0), hy - 0.5, who)
    arm(f, cx - 1, cy + ry - 2, hx, hy + 0.5, who)
    torso(f, cx, cy, who, ry)
    head(f, cx, cy, who)
    f.outline(); return f.im

SWING = {399: (-110, 0), 419: (-80, 0), 438: (-25, 1), 469: (5, 1), 497: (30, 1), 528: (70, 0), 553: (100, 0)}
def swinger(w, h, cx, cy, ang, trail, who="player"):
    f = F(w, h); ry = 7
    a = math.radians(ang)
    hx, hy = cx + 2 + 4 * math.cos(a), cy + 6 * math.sin(a)
    L = 13
    while L > 5:
        ex, ey = hx + L * math.cos(a), hy + L * math.sin(a)
        if 1 <= ex <= w - 2 and 1 <= ey <= h - 2: break
        L -= 1
    if trail:
        for k in range(1, 7):
            b = a - math.radians(9 * k)
            col = (255, 255, 255, 255) if k < 3 else (200, 225, 255, 255)
            f.put(hx + (L - 1) * math.cos(b), hy + (L - 1) * math.sin(b), col)
            if k < 4: f.put(hx + (L - 2) * math.cos(b), hy + (L - 2) * math.sin(b), col)
    bat, bat_d = (196, 150, 90, 255), (120, 84, 44, 255)
    f.line(hx, hy, hx + L * math.cos(a), hy + L * math.sin(a), bat, 2)
    f.line(hx, hy, hx + 3 * math.cos(a), hy + 3 * math.sin(a), bat_d, 2)
    arm(f, cx - 1, cy + (-ry + 2 if ang < 0 else ry - 2), hx, hy, who)
    arm(f, cx - 1, cy + (ry - 2 if ang < 0 else -ry + 2), cx + 2, cy + (ry - 1.5 if ang < 0 else -ry + 1.5), who)
    torso(f, cx, cy, who, ry)
    head(f, cx, cy, who)
    f.outline(); return f.im

def legs(w, h, cx, cy, phase, who="player"):
    f = F(w, h); P = PAL[who]
    s = math.sin(phase)
    for side, sgn in ((-1, 1), (1, -1)):
        fx = cx + 6 * s * sgn
        fy = cy + side * 3
        f.line(cx - 1, fy, fx, fy, C(P["legs"]), 3)
        f.ell(fx + 1, fy, 2.4, 1.6, P["shoe"], lit=False)
        f.put(fx + 2, fy - 1, C(P["shoe"], 0.8))
    f.outline(); return f.im

def corpse(w, h, who, variant):
    f = F(w, h); P = PAL[who]
    cy = h / 2
    hx = w - 6
    f.ell(hx - 12, cy + 1, 13, min(h / 2 - 1, 9), BLOOD, lit=False)
    f.ell(hx - 9, cy + 2, 6, 4, BLOOD_D, lit=False)
    tx0, tx1 = hx - 18, hx - 4
    for sgn in (-1, 1):  # legs
        f.line(tx0, cy + sgn * 2.5, 4, cy + sgn * (5 + 2 * variant), C(P["legs"]), 4)
        f.ell(3, cy + sgn * (5 + 2 * variant), 1.6, 2.2, P["shoe"], lit=False)
    for sgn, k in ((-1, 1.0), (1, 0.6 + 0.6 * variant)):  # arms spread
        ex = hx - 6 + 6 * k; ey = cy + sgn * (h / 2 - 2)
        f.line(tx1 - 2, cy + sgn * 5, ex, ey, C(P["arm"], 0.85), 3)
        f.ell(ex, ey, 1.5, 1.5, SKIN, lit=False)
    for x in range(int(tx0), int(tx1) + 1):
        for y in range(int(cy - 6), int(cy + 7)):
            f.put(x, y, C(P["body"], 1.15 if y < cy - 3 else (0.75 if y > cy + 3 else 1)))
    if who == "police": f.put(tx1 - 3, cy - 3, P["badge"] + (255,))
    if who == "resident":
        for x in range(int(tx0), int(tx1) + 1): f.put(x, cy - 6, P["stripe"] + (255,)); f.put(x, cy + 6, P["stripe"] + (255,))
    for y in range(int(cy - 1), int(cy + 2)): f.put(tx1 + 1, y, SKIN)
    # head seen from above, face up
    f.ell(hx, cy, 3.8, 3.8, SKIN, lit=False)
    f.ell(hx + 1.2, cy, 2.6, 3.6, P.get("cap", P["hair"]))
    f.put(hx - 1, cy - 1, OUTL); f.put(hx - 1, cy + 1, OUTL)
    for i in range(5): f.put(tx1 - 2 + i % 3, cy - 2 + i, BLOOD)
    f.outline(); return f.im

# ---- frame assignments (phase in radians) ----
TAU = math.tau
jobs = {}
for k, n in enumerate([451, 452, 453, 454, 455, 456]): jobs[n] = ("walk", "player", TAU * k / 6 + 0.5)
jobs[611] = ("walk", "player", 0.0)
for k, n in enumerate([532, 547, 533, 566, 589, 613, 637]): jobs[n] = ("walk", "resident", TAU * (k + 1) / 8)
jobs[531] = ("walk", "resident", 0.0)
for k, n in enumerate([534, 535, 567, 568, 569, 590, 614, 638]): jobs[n] = ("walk", "police", TAU * k / 8)
jobs[614] = ("walk", "police", 0.0)
for k, n in enumerate([581, 605, 621, 651, 554, 594, 648, 581, 448, 475, 503, 544, 560, 596]):
    jobs.setdefault(n, ("legs", "player", TAU * k / 14))
for n in GUNS: jobs[n] = ("gun", "player", 0)
for n in SWING: jobs[n] = ("swing", "player", 0)
jobs[12] = ("corpse", "resident", 0); jobs[13] = ("corpse", "police", 0); jobs[22] = ("corpse", "police", 1)

sheet = Image.new("RGBA", (SW, SH), (0, 0, 0, 0))
rendered = {}
for n, (kind, who, ph) in jobs.items():
    s = slices[n]; w, h = s["w"], s["h"]
    cx, cy = s["px"] * w - 0.5, (1 - s["py"]) * h - 0.5
    if kind == "walk": im = walker(w, h, cx, cy, who, ph, ry=min(7, (h - 4) // 2) if h < 18 else 7)
    elif kind == "legs": im = legs(w, h, cx, cy, ph)
    elif kind == "gun": im = gunner(w, h, cx, cy, *GUNS[n])
    elif kind == "swing": im = swinger(w, h, cx, cy, *SWING[n])
    else: im = corpse(w, h, who, ph)
    rendered[n] = im
    sheet.paste(im, (s["x"], SH - s["y"] - h))
sheet.save(OUT_PNG)

# ---- preview: rows per animation, x5 on floor colour ----
rows = [("player walk", [611, 451, 452, 453, 454, 455, 456]),
        ("legs", [581, 605, 621, 651, 554, 594, 648, 448, 475, 503, 544, 560, 596]),
        ("player attack", [399, 419, 438, 469, 497, 528, 553]),
        ("player weapons", [156, 264, 310, 381]),
        ("resident", [531, 532, 547, 533, 566, 589, 613, 637]),
        ("police", [534, 535, 567, 568, 569, 590, 614, 638]),
        ("corpses", [12, 13, 22])]
Z, cell = 5, 64
pv = Image.new("RGBA", (13 * cell * Z // 1, len(rows) * (cell - 18) * Z), (58, 46, 66, 255))
d = ImageDraw.Draw(pv)
for r, (label, ns) in enumerate(rows):
    x = 0
    for n in ns:
        im = rendered[n].resize((rendered[n].width * Z, rendered[n].height * Z), Image.NEAREST)
        pv.alpha_composite(im, (x + 10, r * (cell - 18) * Z + 30))
        x += (max(rendered[n].width, 30) + 4) * Z
    d.text((6, r * (cell - 18) * Z + 6), label, fill=(255, 230, 120, 255))
pv.save(PREVIEW)
print("frames", len(rendered))
