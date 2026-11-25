"""Top-down pixel-art cars based on reference photos (no badges/logos/text).
Each sprite 32x64, front points UP, transparent background.
Usage: python real_car_sprites.py <out_dir> <preview.png>"""
import os, sys, math
from PIL import Image

W, H = 32, 64
CX = 16.0

def col(c, k=1.0, add=0):
    return tuple(max(0, min(255, int(v * k + add))) for v in c[:3]) + (255,)

GLASS, GLASS_HI, GLASS_D = (38, 50, 68, 255), (120, 150, 180, 255), (26, 32, 44, 255)
TIRE, RIM = (16, 16, 18, 255), (92, 94, 100, 255)
CHROME, CHROME_D = (210, 213, 220, 255), (130, 134, 140, 255)
HEAD, HEAD_D = (255, 248, 210, 255), (200, 196, 170, 255)
AMBER, TAIL, TAIL_D = (245, 150, 30, 255), (225, 32, 32, 255), (140, 16, 20, 255)
BLACKTRIM, WIPER = (28, 28, 30, 255), (14, 14, 16, 255)
GLASSY = (GLASS, GLASS_HI, GLASS_D)

class Car:
    def __init__(s, color, w, l, rf=3.0, rr=2.5, taper=0.0, taper_rows=0, dark=False):
        s.c = col(color); s.w, s.l = w, l
        s.rf, s.rr, s.taper, s.tr = rf, rr, taper, taper_rows
        s.y0 = (H - l) // 2
        s.dark = dark
        s.im = Image.new("RGBA", (W, H), (0, 0, 0, 0)); s.p = s.im.load()
        s.mask = set()

    # ---------- geometry ----------
    def hw(s, r):
        hw = s.w / 2
        if r < s.rf:
            hw -= s.rf - math.sqrt(max(0, s.rf ** 2 - (s.rf - r - 0.5) ** 2))
        rb = s.l - 1 - r
        if rb < s.rr:
            hw -= s.rr - math.sqrt(max(0, s.rr ** 2 - (s.rr - rb - 0.5) ** 2))
        if s.tr and r < s.tr:
            hw -= s.taper * (1 - r / s.tr)
        return hw

    def put(s, x, y, c):
        x, y = int(x), int(y)
        if 0 <= x < W and 0 <= y < H: s.p[x, y] = c

    def get(s, x, r): return s.p[int(x), s.y0 + r]
    def inside(s, x, r, inset=0.0): return abs(x + 0.5 - CX) < s.hw(r) - inset
    def edge(s, x, r): return s.hw(r) - abs(x + 0.5 - CX)

    # ---------- painting ----------
    def wheels(s, front, rear, length=8, out=1):
        for r0 in (front, rear):
            for r in range(r0, r0 + length):
                for x in range(W):
                    d = abs(x + 0.5 - CX)
                    if s.w / 2 - 2 < d < s.w / 2 + out:
                        mid = r0 + 1 <= r <= r0 + length - 2
                        outer = d > s.w / 2 + out - 1
                        s.put(x, s.y0 + r, RIM if (mid and outer and (r - r0) % 3 == 1) else TIRE)

    def body(s):
        c = s.c
        for r in range(s.l):
            for x in range(W):
                if not s.inside(x, r): continue
                d = x + 0.5 - CX; e = s.edge(x, r)
                if s.dark:
                    k = col(c, 1, 45) if (d < 0 and e < 1.5) else col(c, 1, 22) if (d < 0 and e < 3) else \
                        col(c, 0.75) if (d > 0 and e < 1.5) else c
                else:
                    k = col(c, 1.25) if (d < 0 and e < 1.5) else col(c, 1.1) if (d < 0 and e < 3) else \
                        col(c, 0.7) if (d > 0 and e < 1.5) else col(c, 0.88) if (d > 0 and e < 3) else c
                s.put(x, s.y0 + r, k); s.mask.add((x, s.y0 + r))

    def region(s, r0, r1, inset0, inset1=None, color=None, fn=None):
        inset1 = inset0 if inset1 is None else inset1
        for r in range(r0, r1 + 1):
            t = 0 if r1 == r0 else (r - r0) / (r1 - r0)
            ins = inset0 + (inset1 - inset0) * t
            for x in range(W):
                if s.inside(x, r, ins):
                    s.put(x, s.y0 + r, fn(x, r) if fn else color)

    def cabin(s, ws0, ws1, roof1, rw1, side=2.0, ws_top=4.5, rw_end=4.0, roof_col=None, roof_ins=1.3,
              bpillar=True, streak=True):
        """windshield ws0..ws1, roof ws1+1..roof1, rear window roof1+1..rw1. Side glass visible beside roof."""
        rc = roof_col or s.c
        g = lambda x, r: GLASS_HI if (streak and (x + r) % 9 == 0) else GLASS
        s.region(ws0, ws1, ws_top, side, fn=g)
        s.region(ws1 + 1, roof1, side, side, GLASS_D)                    # side windows
        s.region(roof1 + 1, rw1, side, rw_end, fn=lambda x, r: GLASS if (x + r) % 7 else GLASS_D)
        def roof(x, r):
            d = x + 0.5 - CX; e = s.hw(r) - side - roof_ins - abs(d)
            if e < 1: return col(rc, 1.2, 18 if s.dark else 0) if d < 0 else col(rc, 0.72)
            return rc
        s.region(ws1 + 1, roof1, side + roof_ins, fn=roof)
        if bpillar:  # pillar between front/rear door glass
            r = (ws1 + roof1) // 2 + 1
            for x in range(W):
                if s.inside(x, r, side) and not s.inside(x, r, side + roof_ins): s.put(x, s.y0 + r, col(rc, 0.85))
        # wipers resting at base of windshield
        for x in range(W):
            d = x + 0.5 - CX
            if s.inside(x, ws1, side + 1.0) and int(d) % 5 in (1, 2, -3, -4) and d < 4:
                s.put(x, s.y0 + ws1, WIPER)

    def seams(s, rows, depth=2.5, k=0.55):
        for r in rows:
            for x in range(W):
                if (x, s.y0 + r) in s.mask and s.edge(x, r) < depth and s.get(x, r) not in GLASSY:
                    s.put(x, s.y0 + r, col(s.get(x, r), k))

    def line(s, r0, r1, dxs, c):
        for r in range(r0, r1 + 1):
            for d in dxs: s.put(CX + d, s.y0 + r, c)

    def rowpix(s, r, dxs, c):
        for d in dxs: s.put(CX + d, s.y0 + r, c)

    def corners(s, r, span, inset, c):
        for x in range(W):
            d = abs(x + 0.5 - CX)
            if s.inside(x, r, inset) and s.hw(r) - inset - span < d: s.put(x, s.y0 + r, c)

    def headlights(s, r, span=3, inset=1.5, rows=2):
        for i in range(rows): s.corners(r + i, span, inset, HEAD if i == 0 else HEAD_D)

    def mirrors(s, r, c=None):
        c = c or col(s.c, 0.6); hw = s.hw(r)
        for dr in (0, 1):
            s.put(CX - hw - 1, s.y0 + r + dr, c); s.put(CX + hw, s.y0 + r + dr, c)

    def stripe(s, d0, d1, c, r0=0, r1=None):
        r1 = s.l - 1 if r1 is None else r1
        for r in range(r0, r1 + 1):
            for x in range(W):
                d = x + 0.5 - CX
                if d0 <= d <= d1 and (x, s.y0 + r) in s.mask and s.get(x, r) not in GLASSY:
                    s.put(x, s.y0 + r, c)

    def sides(s, r0, r1, depth, c):  # paint the outer strip of the body (doors seen from above)
        for r in range(r0, r1 + 1):
            for x in range(W):
                if (x, s.y0 + r) in s.mask and s.edge(x, r) < depth and s.get(x, r) not in GLASSY:
                    d = x + 0.5 - CX
                    s.put(x, s.y0 + r, col(c, 1.12) if d < 0 else col(c, 0.82))

    def finish(s, post=None):
        if post: post(s)
        oc = col(s.c, 0.3) if not s.dark else (8, 8, 10, 255)
        a = [[s.p[x, y][3] > 0 for x in range(W)] for y in range(H)]
        for y in range(H):
            for x in range(W):
                if not a[y][x] or s.p[x, y] in (TIRE, RIM): continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    X, Y = x + dx, y + dy
                    if not (0 <= X < W and 0 <= Y < H) or not a[Y][X]:
                        s.p[x, y] = oc; break


# ======================= cars =======================
def sports_yellow():  # yellow sports coupe, black removable roof
    c = Car((242, 196, 24), 21, 47, rf=5, rr=3.5, taper=1.5, taper_rows=9)
    c.wheels(7, 34, 9); c.body()
    c.region(1, 3, 3, 2.5, col(c.c, 1.08))
    c.line(4, 18, [-0.5], col(c.c, 1.12))
    for r in (5, 6, 7): c.rowpix(r, [-7.5, -6.5, -5.5, 4.5, 5.5, 6.5], col(c.c, 0.86))  # pop-up light lids
    c.corners(1, 2.5, 2.5, AMBER)
    c.line(12, 16, [-8.5, 7.5], col(c.c, 0.8))                      # fender vents
    c.cabin(19, 24, 31, 37, side=2.5, ws_top=5, rw_end=5, roof_col=(22, 22, 26))
    c.seams([25])
    c.region(38, 45, 3.5, 3.5, col(c.c, 0.96))
    c.rowpix(46, [-7.5, -5.5, 4.5, 6.5], TAIL); c.rowpix(45, [-7.5, -5.5, 4.5, 6.5], TAIL_D)
    c.rowpix(46, [-1.5, 0.5], CHROME)                               # exhaust
    c.mirrors(22)
    c.finish(); return c

def coupe_blue_stripes():  # long-hood blue coupe, twin white stripes
    c = Car((44, 58, 190), 22, 46, rf=5, rr=3.5, taper=1.0, taper_rows=7)
    c.wheels(7, 33, 9); c.body()
    c.stripe(-4.0, -1.0, (240, 240, 245, 255)); c.stripe(1.0, 4.0, (240, 240, 245, 255))
    for r in (8, 9, 10, 11): c.rowpix(r, [-7.5, -6.5, 5.5, 6.5], col(c.c, 0.45))   # hood vents
    c.line(3, 4, [-0.5], col(c.c, 0.45))                              # center scoop
    c.headlights(1, 3, 2.5)
    c.line(14, 18, [-9.5, 8.5], col(c.c, 0.7))                        # side gills
    c.cabin(20, 25, 33, 37, side=3, ws_top=5.5, rw_end=5)
    c.stripe(-4.0, -1.0, (240, 240, 245, 255), 26, 33); c.stripe(1.0, 4.0, (240, 240, 245, 255), 26, 33)
    c.rowpix(45, [-7.5, -6.5, 5.5, 6.5], TAIL)
    c.rowpix(45, [-0.5], CHROME)
    c.mirrors(23)
    c.finish(); return c

def luxury_sedan():  # 80s long luxury sedan, vinyl roof, chrome bumpers
    c = Car((176, 186, 206), 22, 56, rf=1.5, rr=1.5)
    c.wheels(8, 40, 9); c.body()
    c.region(0, 1, 0.5, 0.5, CHROME); c.region(54, 55, 0.5, 0.5, CHROME)
    c.region(2, 3, 4, 4, CHROME_D)
    for d in range(-3, 3): c.rowpix(2, [d - 0.5 + 0.5], CHROME if d % 2 else CHROME_D)   # vertical grille
    c.line(3, 4, [-0.5], CHROME)                                         # hood ornament
    c.headlights(2, 3.5, 1.5)
    c.rowpix(4, [-9.5, 8.5], AMBER)
    c.line(5, 18, [-4.5, 3.5], col(c.c, 0.88))
    c.cabin(19, 23, 37, 40, side=2.5, ws_top=4.5, rw_end=4, roof_col=(150, 160, 182))
    for r in range(25, 37, 2): c.rowpix(r, [-5.5, 4.5], (138, 148, 170, 255))  # vinyl grain
    c.seams([22, 30, 39])
    c.region(41, 53, 2.5, 2.5, col(c.c, 1.03))
    c.rowpix(53, [-9.5, -8.5, -7.5, -6.5, 5.5, 6.5, 7.5, 8.5], TAIL)
    c.mirrors(21, CHROME)
    c.finish(); return c

def sedan_black():  # compact late-90s sedan, black
    c = Car((34, 36, 40), 21, 45, rf=4, rr=3.5, dark=True)
    c.wheels(7, 32, 8); c.body()
    c.region(0, 1, 4.5, 4.5, (58, 60, 66, 255))
    c.rowpix(1, [-2.5, -1.5, 0.5, 1.5], (120, 122, 130, 255))           # split grille
    c.headlights(1, 4, 1.5); c.rowpix(2, [-9.5, 8.5], AMBER)
    c.line(3, 13, [-3.5, 2.5], (52, 54, 60, 255))
    c.cabin(14, 19, 29, 33, side=2.5, ws_top=4.5, rw_end=4.5)
    c.seams([22, 30])
    c.rowpix(44, [-8.5, -7.5, -6.5, -5.5, 4.5, 5.5, 6.5, 7.5], TAIL)
    c.rowpix(43, [-8.5, 7.5], TAIL_D)
    c.mirrors(17, (18, 18, 20, 255))
    c.finish(); return c

def hatch_yellow():  # boxy 80s 5-door hatchback, black bumpers
    c = Car((222, 200, 60), 20, 39, rf=1.8, rr=1.5)
    c.wheels(5, 28, 8); c.body()
    c.region(0, 1, 0.5, 0.5, BLACKTRIM); c.region(37, 38, 0.5, 0.5, BLACKTRIM)
    c.region(2, 3, 4.5, 4.5, (40, 40, 40, 255))
    c.line(2, 3, [-1.5, 1.5], (70, 70, 70, 255))
    for side in (-1, 1):                                               # round headlights
        c.rowpix(2, [side * 6.5 - (1 if side < 0 else 0)], HEAD); c.rowpix(3, [side * 6.5 - (1 if side < 0 else 0)], HEAD_D)
    c.line(4, 9, [-3.5, 2.5], col(c.c, 0.9))
    c.cabin(10, 14, 30, 33, side=2.0, ws_top=4, rw_end=2.5)
    c.seams([20, 28])
    c.rowpix(36, [-8.5, -7.5, -6.5, 5.5, 6.5, 7.5], TAIL)
    c.mirrors(12, BLACKTRIM)
    c.finish(); return c

def hatch_teal():  # 3-door hatch, sloped rear glass, roof spoiler
    c = Car((32, 160, 186), 20, 40, rf=3.5, rr=2)
    c.wheels(5, 29, 8); c.body()
    c.region(0, 0, 1, 1, BLACKTRIM); c.region(39, 39, 0.5, 0.5, BLACKTRIM)
    c.headlights(1, 4, 1.5)
    c.line(3, 10, [-0.5], col(c.c, 1.12))
    c.cabin(11, 16, 25, 35, side=2.0, ws_top=4.5, rw_end=3, bpillar=False)
    c.region(26, 26, 1.0, 1.0, col(c.c, 0.6)); c.region(27, 27, 1.5, 1.5, col(c.c, 0.8))   # spoiler
    c.seams([22])
    c.region(37, 38, 1.0, 1.0, TAIL_D); c.rowpix(37, [-7.5, -6.5, -5.5, 4.5, 5.5, 6.5], TAIL)
    c.rowpix(39, [-6.5, -5.5], CHROME)                                   # twin exhaust
    c.mirrors(13, BLACKTRIM)
    c.finish(); return c

def suv_black():  # boxy 90s 4-door SUV, roof rails
    c = Car((36, 38, 40), 22, 45, rf=1.5, rr=1.5, dark=True)
    c.wheels(6, 32, 9, out=2); c.body()
    c.region(0, 1, 0.5, 0.5, BLACKTRIM)
    for d in (-3.5, -2.5, -1.0, 0.5, 2.0, 3.0): c.rowpix(1, [d], (78, 80, 86, 255))  # slot grille
    c.headlights(1, 3, 1.0); c.rowpix(2, [-10.5, 9.5], AMBER)
    c.line(3, 10, [-5.5, 4.5], (50, 52, 56, 255))
    c.cabin(11, 14, 40, 42, side=2.5, ws_top=3.5, rw_end=3)
    c.line(16, 39, [-6.5, 5.5], (128, 130, 136, 255))                   # roof rails
    for r in (17, 38): c.stripe(-6.0, 6.0, (100, 102, 108, 255), r, r)
    c.seams([24, 33])
    c.rowpix(44, [-9.5, -8.5, 7.5, 8.5], TAIL)
    c.rowpix(43, [-1.5, -0.5, 0.5], (70, 72, 76, 255))                  # spare/handle
    c.mirrors(13, (14, 14, 16, 255))
    c.finish(); return c

def suv_green():  # big dark-green SUV, grey cladding & bumpers, roof rails
    c = Car((30, 46, 40), 23, 47, rf=2, rr=1.5, dark=True)
    grey = (122, 126, 128, 255)
    c.wheels(7, 33, 9, out=2); c.body()
    for r0 in (6, 32):
        for r in range(r0, r0 + 11):
            for x in range(W):
                if (x, c.y0 + r) in c.mask and c.edge(x, r) < 1.5: c.put(x, c.y0 + r, grey)
    c.region(0, 1, 0.5, 0.5, grey); c.region(45, 46, 0.5, 0.5, grey)
    c.region(2, 3, 5, 5, (66, 70, 72, 255))
    c.line(2, 3, [-2.5, -0.5, 1.5], (110, 114, 116, 255))
    c.headlights(2, 3.5, 1.0); c.rowpix(3, [-10.5, 9.5], AMBER)
    c.region(7, 9, 9, 9, (16, 20, 18, 255))                            # hood scoop
    c.cabin(13, 17, 42, 44, side=2.5, ws_top=4, rw_end=3)
    c.line(19, 41, [-7.5, 6.5], (152, 156, 158, 255))
    c.region(21, 25, 7, 7, GLASS_D)                                      # sunroof
    c.seams([27, 35])
    c.rowpix(44, [-9.5, 8.5], TAIL)
    c.mirrors(15, (14, 18, 16, 255))
    c.finish(); return c

def police_cruiser():  # 90s full-size police sedan: dark body, white roof & doors, lightbar, push bar
    c = Car((26, 30, 50), 22, 55, rf=5, rr=5, dark=True)
    white = (236, 236, 236, 255)
    c.wheels(9, 40, 9); c.body()
    c.sides(18, 38, 3.0, white)                                         # white doors
    c.region(1, 2, 5, 5, CHROME_D); c.line(1, 2, [-3.5, -1.5, 0.5, 2.5], CHROME)   # chrome grille
    c.headlights(2, 4, 2.0); c.corners(3, 1, 2.0, AMBER)
    c.line(4, 16, [-0.5], (44, 50, 76, 255))
    c.cabin(17, 22, 36, 40, side=2.5, ws_top=5, rw_end=4.5, roof_col=white)
    def lightbar(s):  # across front of roof: blue | clear | red (drawn after mirroring)
        for x in range(W):
            if s.inside(x, 24, 3.0):
                d = x + 0.5 - CX
                cc = (40, 90, 240, 255) if d < -1.5 else (235, 30, 30, 255) if d > 1.5 else (220, 225, 230, 255)
                s.put(x, s.y0 + 24, cc); s.put(x, s.y0 + 25, col(cc, 0.75))
    c.rowpix(31, [-1.5, -0.5, 0.5], (30, 30, 34, 255))                  # roof unit number block
    c.seams([28], 3.0, 0.75)
    c.rowpix(19, [-11.5, 10.5], CHROME)                                 # spotlight
    c.mirrors(20, (20, 22, 30, 255))
    c.region(52, 53, 2, 2, (44, 50, 76, 255))
    c.rowpix(53, [-9.5, -8.5, -7.5, -6.5, 5.5, 6.5, 7.5, 8.5], TAIL)
    # push bar sticking out in front
    for d in (-5.5, 4.5):
        c.put(CX + d, c.y0 - 1, (12, 12, 14, 255)); c.put(CX + d, c.y0 - 2, (12, 12, 14, 255))
    for d in range(-6, 6): c.put(CX + d, c.y0 - 2, (12, 12, 14, 255))
    c.finish(lightbar); return c

CARS = [("sports_yellow", sports_yellow), ("coupe_blue_stripes", coupe_blue_stripes),
        ("luxury_sedan", luxury_sedan), ("sedan_black", sedan_black),
        ("hatch_yellow", hatch_yellow), ("hatch_teal", hatch_teal),
        ("suv_black", suv_black), ("suv_green", suv_green),
        ("police_cruiser", police_cruiser)]

def wreck(src, seed):
    """Burnt-out version of a car sprite: charred paint, rust, smashed glass, embers, dented edges."""
    import random
    rnd = random.Random(seed)
    im = src.copy(); p = im.load()
    gw, gh = 6, 10                                  # low-res value noise for burn patches
    grid = [[rnd.random() for _ in range(gw + 1)] for _ in range(gh + 1)]
    def noise(x, y):
        fx, fy = x / W * gw, y / H * gh
        x0, y0 = int(fx), int(fy); tx, ty = fx - x0, fy - y0
        a = grid[y0][x0] * (1 - tx) + grid[y0][x0 + 1] * tx
        b = grid[y0 + 1][x0] * (1 - tx) + grid[y0 + 1][x0 + 1] * tx
        return a * (1 - ty) + b * ty
    CHAR, ASH, RUST = (34, 31, 30), (78, 74, 70), (118, 64, 34)
    glass = {GLASS[:3], GLASS_HI[:3], GLASS_D[:3]}
    lights = {HEAD[:3], HEAD_D[:3], AMBER[:3], TAIL[:3], TAIL_D[:3]}
    def mix(a, b, t): return tuple(int(a[i] * (1 - t) + b[i] * t) for i in range(3))
    for y in range(H):
        for x in range(W):
            r, g, b, al = p[x, y]
            if al == 0: continue
            c = (r, g, b)
            if c in (TIRE[:3], RIM[:3]):            # tyres burnt away -> bare dark rims
                p[x, y] = (24, 22, 22, 255) if c == TIRE[:3] else (60, 58, 56, 255); continue
            if c in glass:                           # smashed windows
                p[x, y] = (70, 76, 82, 255) if rnd.random() < 0.12 else (10, 10, 12, 255); continue
            if c in lights:
                p[x, y] = (20, 18, 18, 255); continue
            n = noise(x, y) + rnd.uniform(-0.04, 0.04)
            lum = (r * 0.3 + g * 0.59 + b * 0.11) / 255
            if n > 0.62:   base = CHAR
            elif n > 0.4:  base = mix(CHAR, ASH, 0.6)
            elif n > 0.25: base = RUST
            else:          base = mix(c, CHAR, 0.55)   # a little original paint survives
            out = mix(base, (int(30 + lum * 120),) * 3, 0.45)
            if n > 0.6 and rnd.random() < 0.012: out = rnd.choice([(255, 140, 30), (255, 200, 60), (220, 60, 20)])
            p[x, y] = out + (255,)
    # dents: chew random pixels off the silhouette edge
    a = [[p[x, y][3] > 0 for x in range(W)] for y in range(H)]
    for y in range(H):
        for x in range(W):
            if a[y][x] and rnd.random() < 0.08 and any(
                    not (0 <= x + dx < W and 0 <= y + dy < H) or not a[y + dy][x + dx]
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                p[x, y] = (0, 0, 0, 0)
    # re-outline in soot
    a = [[p[x, y][3] > 0 for x in range(W)] for y in range(H)]
    for y in range(H):
        for x in range(W):
            if a[y][x] and any(not (0 <= x + dx < W and 0 <= y + dy < H) or not a[y + dy][x + dx]
                               for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                p[x, y] = (12, 10, 10, 255)
    # scorch ring on the ground around the car (semi-transparent)
    for y in range(H):
        for x in range(W):
            if p[x, y][3] == 0 and any(0 <= x + dx < W and 0 <= y + dy < H and a[y + dy][x + dx]
                                       for dx in (-1, 0, 1) for dy in (-1, 0, 1)) and rnd.random() < 0.4:
                p[x, y] = (0, 0, 0, rnd.choice([60, 90, 120]))
    return im

if __name__ == "__main__":
    out, prev_path = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    sheet = Image.new("RGBA", (W * len(CARS), H), (0, 0, 0, 0))
    wsheet = Image.new("RGBA", (W * len(CARS), H), (0, 0, 0, 0))
    for i, (n, fn) in enumerate(CARS):
        im = fn().im
        im.save(os.path.join(out, f"car_{n}.png")); sheet.paste(im, (i * W, 0))
        wr = wreck(im, i + 1)
        wr.save(os.path.join(out, f"car_{n}_wrecked.png")); wsheet.paste(wr, (i * W, 0))
    sheet.save(os.path.join(out, "real_cars_sheet.png"))
    wsheet.save(os.path.join(out, "real_cars_wrecked_sheet.png"))
    pv = Image.new("RGBA", (sheet.width, H * 2), (78, 80, 84, 255))
    pv.alpha_composite(sheet, (0, 0)); pv.alpha_composite(wsheet, (0, H))
    pv.resize((pv.width * 8, pv.height * 8), Image.NEAREST).save(prev_path)
    print("ok", len(CARS))
