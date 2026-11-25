"""Original top-down 16-bit style character sprite sheet.
Grid: 48x48 cells, character centred in every cell (pivot = centre), facing RIGHT.
Rows per character: walk(8) | legs(8) | weapons idle/shoot(10) | bat swing(7) | deaths(3)
Last row: pickups, casings, blood, tracer.
Rendering: every shape is a lit 3D primitive (ellipsoid / capsule) -> 5-tone hue-shifted ramps,
contact shadows between overlapping parts, coloured selective outline.
Usage: python character_sheet.py <out_sheet.png> <preview.png>"""
import math, sys, random, colorsys
from PIL import Image, ImageDraw

CW = CH = 48
COLS = 10
CX, CY = 24.0, 24.0

_L = (-0.55, -0.65, 0.75)
_n = math.sqrt(sum(v * v for v in _L))
LX, LY, LZ = (v / _n for v in _L)
TH = (0.34, 0.52, 0.70, 0.87)          # intensity -> ramp index 0..4


def ramp(base):
    r, g, b = (v / 255 for v in base)
    h, s, v = colorsys.rgb_to_hsv(r, g, b)
    steps = [(0.40, 0.14, -0.06), (0.60, 0.08, -0.03), (0.80, 0.03, -0.01), (1.0, 0.0, 0.0), (1.17, -0.12, 0.025)]
    out = []
    for i, (vm, sa, hs) in enumerate(steps):
        if s < 0.08:                    # greys / whites: cool shadows, warm highlights
            hh = 0.62 if hs < 0 else (0.12 if hs > 0 else h)
            ss = [0.12, 0.08, 0.04, s, 0.03][i]
        else:
            hh = h
            if hs:
                target = 0.66 if hs < 0 else 0.15
                d = (target - h + 0.5) % 1 - 0.5
                hh = (h + math.copysign(min(abs(d), abs(hs)), d)) % 1
            ss = min(1, max(0, s + sa))
        rr, gg, bb = colorsys.hsv_to_rgb(hh, ss, min(1, v * vm))
        out.append((round(rr * 255), round(gg * 255), round(bb * 255), 255))
    return out


BASE = {
    "skin": (220, 168, 128), "denim": (74, 100, 142), "hair_brown": (92, 60, 38), "red": (168, 38, 40),
    "tee": (226, 224, 216), "jeans": (56, 66, 98), "sneaker": (226, 226, 220),
    "tank": (230, 228, 220), "pants_dark": (54, 54, 60), "boot": (70, 52, 38), "gold": (220, 176, 64),
    "track": (104, 70, 128), "hair_black": (46, 40, 42), "stripe": (226, 226, 226), "shoe_dark": (58, 58, 64),
    "shirt": (112, 146, 188), "capnavy": (40, 50, 88), "brim": (30, 32, 42), "pants_navy": (46, 54, 90),
    "shoe_black": (36, 36, 40), "radio": (44, 44, 48), "hair_police": (70, 50, 36),
    "metal": (92, 96, 108), "gunblack": (48, 50, 56), "wood": (132, 84, 46), "wood_dark": (96, 60, 34),
    "bat": (198, 152, 98), "blood": (150, 18, 24), "brass": (206, 162, 70), "shell": (168, 36, 36),
}
MAT = {k: ramp(v) for k, v in BASE.items()}
NO_OUT = {"blood"}

CHD = {
    "player":   dict(torso="denim", sleeve="denim", hair="hair_brown", head="bandana", legs="jeans", shoe="sneaker"),
    "thug":     dict(torso="tank", sleeve=None, hair=None, head="bald", legs="pants_dark", shoe="boot"),
    "resident": dict(torso="track", sleeve="track", hair="hair_black", head="hair", legs="track", shoe="shoe_dark"),
    "police":   dict(torso="shirt", sleeve="shirt", hair="hair_police", head="cap", legs="pants_navy", shoe="shoe_black"),
}


class Cv:
    def __init__(s):
        s.m = [[None] * CW for _ in range(CH)]
        s.i = [[0.0] * CW for _ in range(CH)]
        s.fx = Image.new("RGBA", (CW, CH), (0, 0, 0, 0)); s.fp = s.fx.load()

    @staticmethod
    def shade(nx, ny, nz, flat):
        nx *= flat; ny *= flat
        l = math.sqrt(nx * nx + ny * ny + nz * nz) or 1
        return 0.16 + 0.95 * max(0.0, (nx * LX + ny * LY + nz * LZ) / l)

    def commit(s, pix, ao=0.2):
        if ao:                                   # contact shadow on what is underneath
            done = set()
            for (x, y) in pix:
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    q = (x + dx, y + dy)
                    if q in pix or q in done: continue
                    X, Y = q
                    if 0 <= X < CW and 0 <= Y < CH and s.m[Y][X] is not None:
                        s.i[Y][X] -= ao; done.add(q)
        for (x, y), (mat, I) in pix.items():
            s.m[y][x] = mat; s.i[y][x] = I

    def ell_pix(s, cx, cy, rx, ry, mat, flat=1.0, mod=None, bias=0.0):
        pix = {}
        for y in range(max(0, int(cy - ry - 1)), min(CH, int(cy + ry + 2))):
            for x in range(max(0, int(cx - rx - 1)), min(CW, int(cx + rx + 2))):
                dx = (x + .5 - cx) / rx; dy = (y + .5 - cy) / ry; r2 = dx * dx + dy * dy
                if r2 > 1: continue
                I = s.shade(dx, dy, math.sqrt(1 - r2), flat) + bias
                if mod: I = mod(x, y, I)
                pix[(x, y)] = (mat, I)
        return pix

    def cap_pix(s, x0, y0, x1, y1, r0, mat, r1=None, flat=1.0, mod=None, bias=0.0):
        r1 = r0 if r1 is None else r1
        R = max(r0, r1); vx, vy = x1 - x0, y1 - y0; L2 = vx * vx + vy * vy or 1e-9
        pix = {}
        for y in range(max(0, int(min(y0, y1) - R - 1)), min(CH, int(max(y0, y1) + R + 2))):
            for x in range(max(0, int(min(x0, x1) - R - 1)), min(CW, int(max(x0, x1) + R + 2))):
                px, py = x + .5, y + .5
                t = max(0.0, min(1.0, ((px - x0) * vx + (py - y0) * vy) / L2))
                r = r0 + (r1 - r0) * t
                dx, dy = px - (x0 + vx * t), py - (y0 + vy * t)
                d2 = dx * dx + dy * dy
                if d2 > r * r: continue
                I = s.shade(dx / r, dy / r, math.sqrt(max(0.0, 1 - d2 / (r * r))), flat) + bias
                if mod: I = mod(x, y, I)
                pix[(x, y)] = (mat, I)
        return pix

    def ell(s, *a, ao=0.2, **k): s.commit(s.ell_pix(*a, **k), ao)
    def cap(s, *a, ao=0.2, **k): s.commit(s.cap_pix(*a, **k), ao)

    def limb(s, pts, r0, r1, mat, ao=0.2, **k):
        pix, n = {}, len(pts) - 1
        for j in range(n):
            ra = r0 + (r1 - r0) * j / n; rb = r0 + (r1 - r0) * (j + 1) / n
            pix.update(s.cap_pix(*pts[j], *pts[j + 1], ra, mat, r1=rb, **k))
        s.commit(pix, ao)

    def px(s, x, y, mat, I):
        x, y = int(x), int(y)
        if 0 <= x < CW and 0 <= y < CH: s.m[y][x] = mat; s.i[y][x] = I

    def dim(s, x, y, d):
        x, y = int(x), int(y)
        if 0 <= x < CW and 0 <= y < CH and s.m[y][x] is not None: s.i[y][x] += d

    def fxp(s, x, y, c):
        x, y = int(math.floor(x)), int(math.floor(y))
        if 1 <= x < CW - 1 and 1 <= y < CH - 1 and c[3] >= s.fp[x, y][3]: s.fp[x, y] = c

    def render(s):
        im = Image.new("RGBA", (CW, CH), (0, 0, 0, 0)); p = im.load()
        for y in range(CH):
            for x in range(CW):
                m = s.m[y][x]
                if m: p[x, y] = MAT[m][sum(s.i[y][x] > t for t in TH)]
        for y in range(CH):                       # selective outline (light side lighter)
            for x in range(CW):
                if s.m[y][x] is not None: continue
                nb = {}
                for dx, dy in ((1, 0), (0, 1), (-1, 0), (0, -1)):
                    X, Y = x + dx, y + dy
                    if 0 <= X < CW and 0 <= Y < CH and s.m[Y][X] and s.m[Y][X] not in NO_OUT:
                        nb[(dx, dy)] = s.m[Y][X]
                if not nb: continue
                light = ((1, 0) in nb or (0, 1) in nb) and not ((-1, 0) in nb or (0, -1) in nb)
                c = MAT[next(iter(nb.values()))][0]; k = 0.70 if light else 0.42
                p[x, y] = (int(c[0] * k), int(c[1] * k), int(c[2] * k), 255)
        im.alpha_composite(s.fx)
        return im


# ======================= body parts =======================
def strands(x, y, I): return I + 0.11 * math.sin((y + 0.5) * 2.3 + x * 0.45)


def torso(cv, ch, cx=CX, cy=CY):
    D = CHD[ch]
    if ch == "thug":
        cv.cap(cx - 1, cy - 4.4, cx - 1, cy + 4.4, 4.0, "skin", flat=0.8)
        cv.cap(cx - 1.3, cy - 2.6, cx - 1.3, cy + 2.6, 3.9, "tank", flat=0.75, ao=0.14)
        for sg in (-1, 1):
            cv.cap(cx - 4.2, cy + sg * 3.4, cx + 2.0, cy + sg * 3.4, 0.8, "tank", ao=0.1)
        for sg in (-1, 1):
            cv.px(cx + 2, cy + sg * 3.6, "gold", 0.95); cv.px(cx + 3, cy + sg * 2.8, "gold", 0.7)
    else:
        cv.cap(cx - 1, cy - 4.4, cx - 1, cy + 4.4, 4.1, D["torso"], flat=0.8)
    if ch == "player":
        for sg in (-1, 1):
            for x in range(int(cx - 4), int(cx + 1)): cv.dim(x, cy + sg * 4.4, -0.13)   # yoke seams
            cv.cap(cx + 0.6, cy + sg * 3.6, cx + 2.4, cy + sg * 1.8, 0.9, "denim", bias=0.12, ao=0.12)
    if ch == "resident":
        for sg in (-1, 1):
            cv.cap(cx + 0.8, cy + sg * 3.4, cx + 2.2, cy + sg * 1.6, 0.9, "track", bias=0.12, ao=0.12)
    if ch == "police":
        for sg in (-1, 1):
            cv.cap(cx - 3.8, cy + sg * 6.4, cx + 0.6, cy + sg * 6.4, 0.8, "shirt", bias=0.06, ao=0.14)
        cv.px(cx + 2, cy - 5, "gold", 0.95); cv.px(cx + 1, cy - 5, "gold", 0.75); cv.px(cx + 2, cy - 4, "gold", 0.55)
        cv.cap(cx + 0.4, cy + 5.0, cx + 1.8, cy + 5.0, 0.9, "radio", ao=0.14)


def arm(cv, ch, S, H, E=None, side=1):
    D = CHD[ch]
    if E is None: E = ((S[0] + H[0]) / 2, (S[1] + H[1]) / 2 + side * 0.7)
    dx, dy = H[0] - E[0], H[1] - E[1]; L = math.hypot(dx, dy) or 1
    W = (H[0] - dx / L * 1.2, H[1] - dy / L * 1.2)
    if D["sleeve"]:
        cv.limb([S, E, W], 2.1, 1.7, D["sleeve"], flat=0.9)
        if ch == "resident":
            o = side * 1.25
            cv.limb([(S[0], S[1] + o), (E[0], E[1] + o), (W[0], W[1] + o * 0.8)], 0.6, 0.55, "stripe", ao=0)
    else:
        cv.limb([S, E, W], 2.1, 1.6, "skin", flat=0.9)
    cv.ell(H[0], H[1], 1.8, 1.8, "skin", flat=0.8, ao=0.16)


def head(cv, ch, cx=CX, cy=CY):
    D = CHD[ch]; hx = cx + 0.6; h = D["head"]
    for sg in (-1, 1): cv.ell(hx + 0.5, cy + sg * 3.5, 1.0, 1.2, "skin", ao=0.12)
    cv.ell(hx, cy, 3.7, 3.8, "skin", flat=0.9, ao=0.3)
    if h in ("hair", "bandana"):
        cv.ell(hx - 0.7, cy, 3.3, 3.85, D["hair"], flat=0.9, mod=strands, ao=0.08)
    if h == "bandana":
        cv.cap(hx + 1.0, cy - 3.4, hx + 1.0, cy + 3.4, 0.9, "red", ao=0.12)
        cv.ell(hx - 3.5, cy + 0.9, 1.2, 1.2, "red", ao=0.1)
        cv.cap(hx - 3.8, cy + 1.3, hx - 6.6, cy + 3.0, 0.75, "red", r1=0.55, ao=0.1)
        cv.cap(hx - 3.8, cy + 0.5, hx - 6.8, cy + 0.1, 0.7, "red", r1=0.5, ao=0.1)
    elif h == "cap":
        cv.ell(hx - 0.3, cy, 3.9, 4.0, "capnavy", flat=0.8, ao=0.12)
        cv.cap(hx + 3.3, cy - 3.0, hx + 3.3, cy + 3.0, 1.0, "brim", ao=0.15)
        cv.px(hx + 1.6, cy - 0.5, "gold", 0.95); cv.px(hx + 1.6, cy + 0.5, "gold", 0.6)
    elif h == "bald":
        for y in range(int(cy - 4), int(cy + 5)):
            for x in range(int(hx - 4), int(hx)):
                if (x + y) % 2 == 0 and ((x + .5 - hx) / 3.7) ** 2 + ((y + .5 - cy) / 3.8) ** 2 < 0.8:
                    cv.dim(x, y, -0.14)
    cv.px(hx + 3.7, cy, "skin", 0.6)                                 # nose


# ======================= weapons / fx =======================
def gun(cv, kind, hx, gy):
    if kind == "pistol":
        cv.cap(hx - 0.5, gy + 0.6, hx - 1.6, gy + 3.6, 1.0, "gunblack")
        cv.cap(hx - 1.2, gy, hx + 5.5, gy, 1.2, "metal", flat=0.6)
        return hx + 6.2
    if kind == "smg":
        cv.cap(hx - 6.5, gy, hx - 3.5, gy, 0.8, "gunblack")
        cv.cap(hx + 2.0, gy + 0.8, hx + 2.6, gy + 6.0, 1.0, "gunblack")
        cv.cap(hx - 3.5, gy, hx + 6.5, gy, 1.5, "metal", flat=0.6)
        cv.cap(hx + 6.5, gy, hx + 9.5, gy, 0.8, "metal")
        return hx + 10
    if kind == "rifle":
        cv.cap(hx - 9, gy + 0.3, hx - 3, gy, 1.7, "wood", r1=1.3, flat=0.7)
        cv.limb([(hx + 0.8, gy + 1.0), (hx + 1.8, gy + 4.0), (hx + 3.4, gy + 6.4)], 1.1, 1.0, "gunblack")
        cv.cap(hx - 3, gy, hx + 5, gy, 1.4, "metal", flat=0.6)
        cv.cap(hx + 5, gy, hx + 10, gy, 1.4, "wood", flat=0.7)
        cv.cap(hx + 10, gy, hx + 14.5, gy, 0.8, "metal")
        cv.px(hx + 13.6, gy - 1, "metal", 0.8)
        return hx + 15
    # shotgun
    cv.cap(hx - 9, gy + 0.3, hx - 2.5, gy, 1.8, "wood", r1=1.3, flat=0.7)
    cv.cap(hx - 2.5, gy, hx + 3.5, gy, 1.5, "metal", flat=0.6)
    cv.cap(hx + 3.5, gy + 1.1, hx + 13, gy + 1.1, 0.8, "gunblack")
    cv.cap(hx + 3.5, gy - 0.3, hx + 15, gy - 0.3, 0.9, "metal")
    cv.cap(hx + 6, gy + 0.5, hx + 10.5, gy + 0.5, 1.4, "wood_dark", flat=0.7)
    return hx + 15.5


FORE = {"smg": 5.0, "rifle": 7.5, "shotgun": 8.2}


def flash(cv, mx, my, big):
    for r, col in ((big, (238, 120, 36, 255)), (big * 0.72, (255, 206, 84, 255)), (big * 0.42, (255, 250, 226, 255))):
        cx, rx, ry = mx + r * 0.7, r * 0.95, r * 0.6
        for y in range(int(my - ry - 1), int(my + ry + 2)):
            for x in range(int(cx - rx - 1), int(cx + rx + 2)):
                if ((x + .5 - cx) / rx) ** 2 + ((y + .5 - my) / ry) ** 2 <= 1: cv.fxp(x, y, col)
    for sg in (-1, 1):
        for t in range(int(big * 1.6)):
            cv.fxp(mx + 1 + t, my + sg * (0.5 + t * 0.45), (255, 196, 70, max(0, 220 - t * 25)))
    cv.fxp(mx + big * 2.0, my, (255, 220, 120, 180))


# ======================= frames =======================
def walk(ch, k):
    cv = Cv(); s = math.sin(math.tau * k / 8) if k else 0.0
    torso(cv, ch)
    for side, sg in ((-1, 1), (1, -1)):
        arm(cv, ch, (CX - 0.5, CY + side * 6.6), (CX + 2.2 + 4.0 * s * sg, CY + side * 7.4), side=side)
    head(cv, ch)
    return cv.render()


def legs(ch, k):
    cv = Cv(); D = CHD[ch]; s = math.sin(math.tau * k / 8) if k else 0.0
    order = sorted(((-1, 1), (1, -1)), key=lambda t: 6.5 * s * t[1])
    for side, sg in order:
        fx, fy = CX + 6.5 * s * sg, CY + side * 3.5
        cv.cap(CX - 1, CY + side * 3.2, fx, fy, 2.3, D["legs"], r1=2.0, flat=0.85)
        cv.ell(fx + 1.3, fy, 2.7, 1.9, D["shoe"], flat=0.8, ao=0.18)
        if D["shoe"] == "sneaker":
            cv.dim(fx + 1, fy, -0.2); cv.dim(fx + 2, fy, -0.2)
    return cv.render()


def armed(ch, kind, shoot=False):
    if kind == "unarmed": return walk(ch, 0)
    cv = Cv()
    if kind == "bat":
        torso(cv, ch)
        arm(cv, ch, (CX - 0.5, CY - 6.6), (CX + 2.2, CY - 7.4), side=-1)
        cv.cap(CX + 3.2, CY + 7.2, CX - 10, CY + 12.5, 0.9, "bat", r1=1.6, flat=0.8)
        arm(cv, ch, (CX - 0.5, CY + 6.6), (CX + 3.2, CY + 7.2), side=1)
        head(cv, ch); return cv.render()
    rec = 0.8 if shoot else 0.0
    gy = CY + 1.2
    if kind == "pistol":
        hx = CX + 8.5 - rec
        mx = gun(cv, kind, hx, gy)
        torso(cv, ch)
        arm(cv, ch, (CX - 0.5, CY - 6.4), (hx - 0.6, gy - 1.0), E=(CX + 3.5, CY - 5.0), side=-1)
        arm(cv, ch, (CX - 0.5, CY + 6.4), (hx - 0.6, gy + 1.0), E=(CX + 3.5, CY + 5.0), side=1)
        big = 2.6
    else:
        hx = CX + (5.2 if kind == "smg" else 4.2) - rec
        mx = gun(cv, kind, hx, gy)
        torso(cv, ch)
        arm(cv, ch, (CX - 0.5, CY + 6.4), (hx - 0.4, gy + 1.3), E=(CX + 2.0, CY + 5.6), side=1)
        arm(cv, ch, (CX - 0.5, CY - 6.4), (hx + FORE[kind], gy - 0.4), E=(CX + 3.8, CY - 5.4), side=-1)
        big = 3.0 if kind == "smg" else 3.4
    head(cv, ch)
    if shoot: flash(cv, mx, gy - (0.3 if kind == "shotgun" else 0), big)
    return cv.render()


SWING = [(-125, 0), (-95, 0), (-50, 1), (-10, 1), (30, 1), (70, 0), (100, 0)]


def swing(ch, k):
    cv = Cv(); ang, trail = SWING[k]; a = math.radians(ang)
    H = (CX + 3 + 5 * math.cos(a), CY + 7.5 * math.sin(a)); L = 14.0
    while L > 8:                                 # keep bat + smear 2px inside the cell
        E = (H[0] + L * math.cos(a), H[1] + L * math.sin(a))
        if 3.5 <= E[0] <= CW - 3.5 and 3.5 <= E[1] <= CH - 3.5: break
        L -= 0.5
    top = ang < 0
    torso(cv, ch)
    arm(cv, ch, (CX - 0.5, CY + (6.6 if top else -6.6)), (CX + 2.2, CY + (7.4 if top else -7.4)),
        side=1 if top else -1)
    cv.cap(H[0], H[1], E[0], E[1], 0.9, "bat", r1=1.6, flat=0.8)
    arm(cv, ch, (CX - 0.5, CY + (-6.6 if top else 6.6)), H, side=-1 if top else 1)
    head(cv, ch)
    if trail:                                    # motion smear behind the bat
        for da in range(4, 72, 2):
            b = a - math.radians(da)
            for j in range(10):
                rr = L - 4.5 + j * 0.55
                al = int(190 * (1 - da / 72) * (0.35 + 0.65 * j / 9))
                cv.fxp(H[0] + rr * math.cos(b), H[1] + rr * math.sin(b), (236, 238, 246, al))
    return cv.render()


def blob(cv, pcx, pcy, prx, pry, rnd, mat="blood"):
    ph1, ph2 = rnd.uniform(0, 6.3), rnd.uniform(0, 6.3)
    pix = {}
    for y in range(max(0, int(pcy - pry * 1.4)), min(CH, int(pcy + pry * 1.4) + 1)):
        for x in range(max(0, int(pcx - prx * 1.4)), min(CW, int(pcx + prx * 1.4) + 1)):
            dx, dy = (x + .5 - pcx) / prx, (y + .5 - pcy) / pry
            an = math.atan2(dy, dx); rr = 1 + 0.17 * math.sin(3 * an + ph1) + 0.11 * math.sin(5 * an + ph2)
            r2 = dx * dx + dy * dy
            if r2 > rr * rr: continue
            I = 0.62
            if r2 > 0.72 * rr * rr: I = 0.45
            if (dx + 0.35) ** 2 + (dy + 0.38) ** 2 < 0.05: I = 0.95
            elif (dx + 0.3) ** 2 + (dy + 0.3) ** 2 < 0.12: I = 0.78
            pix[(x, y)] = (mat, I)
    for _ in range(int(prx)):
        an = rnd.uniform(0, math.tau); d = rnd.uniform(1.15, 1.6); r = rnd.uniform(0.6, 1.4)
        pix.update(cv.ell_pix(pcx + d * prx * math.cos(an), pcy + d * pry * math.sin(an), r, r, mat, flat=0.4))
    cv.commit(pix, 0)


def corpse(ch, v):
    cv = Cv(); D = CHD[ch]; rnd = random.Random(sum(map(ord, ch)) * 10 + v)
    cx, cy = 22.0, 24.0
    blob(cv, cx + 3, cy + 1, *((14, 11) if v == 2 else (9.5, 7.5)), rnd)
    for sg in (-1, 1):                                                    # legs
        spread = 1.2 if v == 0 else 0.4
        hip = (cx - 5.5, cy + sg * 2.8)
        knee = (cx - 10.5, cy + sg * (3.4 + spread))
        foot = (cx - 15.5, cy + sg * (4.2 + spread * 1.8))
        cv.limb([hip, knee, foot], 2.3, 1.9, D["legs"], flat=0.85)
        cv.ell(foot[0] - 1.6, foot[1], 1.7, 2.1, D["shoe"], flat=0.8, ao=0.18)
    if ch == "thug":
        cv.ell(cx, cy, 6.0, 7.8, "skin", flat=0.6)
        cv.ell(cx - 0.3, cy, 5.6, 5.6, "tank", flat=0.6, ao=0.14)
        if v != 1:
            for d in (-3, -2, 2, 3): cv.px(cx + 4, cy + d, "gold", 0.9 if d < 0 else 0.6)
            cv.px(cx + 5, cy - 1, "gold", 0.8); cv.px(cx + 5, cy + 1, "gold", 0.6)
    else:
        cv.ell(cx, cy, 6.0, 7.8, D["torso"], flat=0.6)
    if ch == "player" and v != 1:
        cv.cap(cx - 5, cy, cx + 5, cy, 1.5, "tee", flat=0.6, ao=0.16)      # open jacket, t-shirt
    if ch == "police" and v != 1:
        cv.px(cx + 3, cy - 4, "gold", 0.95); cv.px(cx + 2, cy - 4, "gold", 0.7)
    if ch == "resident":
        for sg in (-1, 1): cv.cap(cx - 5.2, cy + sg * 6.4, cx + 4.8, cy + sg * 6.4, 0.55, "stripe", ao=0)
    for (x, y) in ((cx - 1, cy - 2), (cx, cy - 1), (cx - 2, cy + 2)) if v != 1 else ((cx - 2, cy - 3), (cx - 3, cy + 1)):
        cv.px(x, y, "blood", 0.4)
    sh = cx + 4.3
    if v == 0:
        arms = [[(sh, cy - 6.6), (sh + 3.5, cy - 10.5), (sh + 7.5, cy - 13.0)],
                [(sh, cy + 6.6), (sh + 1.5, cy + 11.0), (sh + 2.5, cy + 15.0)]]
    elif v == 1:
        arms = [[(sh, cy - 6.8), (sh - 4.5, cy - 8.8), (sh - 9.0, cy - 8.4)],
                [(sh, cy + 6.8), (sh - 4.5, cy + 8.8), (sh - 9.0, cy + 8.4)]]
    else:
        arms = [[(sh, cy - 6.6), (cx + 1.5, cy - 3.8), (cx - 1.5, cy + 1.2)],
                [(sh, cy + 6.6), (sh + 4.0, cy + 10.0), (sh + 8.5, cy + 11.0)]]
    for pts in arms:
        cv.limb(pts, 2.1, 1.7, D["sleeve"] or "skin", flat=0.9)
        cv.ell(*pts[-1], 1.8, 1.8, "skin", flat=0.8, ao=0.16)
    hx = cx + 10.2
    cv.ell(hx, cy, 3.7, 3.8, "skin", flat=0.9, ao=0.28)
    hair = D["hair"]
    if v == 1:                                                            # face down
        if hair: cv.ell(hx + 0.2, cy, 3.6, 3.75, hair, flat=0.9, mod=strands, ao=0.05)
        else:
            for y in range(int(cy - 3), int(cy + 4)):
                for x in range(int(hx - 3), int(hx + 4)):
                    if (x + y) % 2 == 0: cv.dim(x, y, -0.12)
        if D["head"] == "bandana": cv.cap(hx - 1.2, cy - 3.5, hx - 1.2, cy + 3.5, 0.8, "red", ao=0.1)
    else:                                                                 # face up
        if hair: cv.ell(hx + 1.6, cy, 2.4, 3.8, hair, flat=0.9, mod=strands, ao=0.05)
        if D["head"] == "bandana": cv.cap(hx + 0.9, cy - 3.5, hx + 0.9, cy + 3.5, 0.8, "red", ao=0.1)
        cv.px(hx - 0.2, cy - 1.4, "brim", 0.3); cv.px(hx - 0.2, cy + 1.2, "brim", 0.3)   # closed eyes
        cv.px(hx - 1.4, cy, "skin", 0.95); cv.px(hx - 2.6, cy, "skin", 0.38)              # nose, mouth
    if D["head"] == "cap":                                                # cap knocked off
        px, py = (hx + 6.5, cy + 8.5) if v != 2 else (hx + 5.5, cy - 9)
        cv.ell(px, py, 3.4, 3.5, "capnavy", flat=0.8)
        cv.cap(px + 3.0, py - 2.6, px + 3.0, py + 2.6, 0.9, "brim", ao=0.14)
        cv.px(px + 1.2, py, "gold", 0.9)
    return cv.render()


# ======================= extras =======================
def pickup(kind):
    cv = Cv()
    if kind == "bat":
        cv.cap(12, 30, 36, 18, 0.9, "bat", r1=1.7, flat=0.8)
    else:
        gun(cv, kind, {"pistol": 21, "smg": 21, "rifle": 17, "shotgun": 16.5}[kind], 24)
    return cv.render()


def casings():
    cv = Cv(); rnd = random.Random(5)
    for _ in range(9):
        x, y, a = rnd.uniform(9, 37), rnd.uniform(9, 37), rnd.uniform(0, math.pi)
        cv.cap(x, y, x + 2.2 * math.cos(a), y + 2.2 * math.sin(a), 0.8, "brass", ao=0.05)
    for _ in range(3):
        x, y, a = rnd.uniform(10, 34), rnd.uniform(10, 34), rnd.uniform(0, math.pi)
        cv.cap(x, y, x + 3.2 * math.cos(a), y + 3.2 * math.sin(a), 1.05, "shell", ao=0.05)
        cv.ell(x, y, 1.0, 1.0, "brass", ao=0.05)
    return cv.render()


def splat(seed, size):
    cv = Cv(); blob(cv, 24, 24, size, size * 0.8, random.Random(seed)); return cv.render()


def tracer():
    cv = Cv()
    for x in range(6, 40):
        t = (x - 6) / 34
        cv.fxp(x, 24, (255, 236, 160, int(40 + 215 * t)))
        if t > 0.6: cv.fxp(x, 23, (255, 220, 120, int(90 * (t - 0.6) / 0.4)))
    cv.fxp(40, 24, (255, 255, 255, 255)); cv.fxp(41, 24, (255, 250, 220, 200))
    return cv.render()


# ======================= layout =======================
def build():
    rows, labels = [], []
    for ch in CHD:
        rows.append([walk(ch, k) for k in range(8)]); labels.append(f"{ch}: idle + walk 1-7")
        rows.append([legs(ch, k) for k in range(8)]); labels.append(f"{ch}: legs idle + walk 1-7")
        r = [armed(ch, "unarmed"), armed(ch, "bat")]
        for g in ("pistol", "smg", "rifle", "shotgun"): r += [armed(ch, g), armed(ch, g, True)]
        rows.append(r); labels.append(f"{ch}: unarmed, bat, pistol/smg/rifle/shotgun (idle, shoot)")
        rows.append([swing(ch, k) for k in range(7)]); labels.append(f"{ch}: bat swing 1-7")
        rows.append([corpse(ch, v) for v in range(3)]); labels.append(f"{ch}: dead (back, face down, blood pool)")
    rows.append([pickup(k) for k in ("bat", "pistol", "smg", "rifle", "shotgun")] +
                [casings(), splat(1, 5), splat(2, 8), splat(3, 11), tracer()])
    labels.append("pickups x5 | casings | blood x3 | tracer")
    sheet = Image.new("RGBA", (COLS * CW, len(rows) * CH), (0, 0, 0, 0))
    for r, row in enumerate(rows):
        for c, im in enumerate(row): sheet.paste(im, (c * CW, r * CH))
    return sheet, labels


if __name__ == "__main__":
    sheet, labels = build()
    sheet.save(sys.argv[1])
    Z, LW = 2, 470
    pv = Image.new("RGBA", (sheet.width * Z + LW, sheet.height * Z), (40, 38, 44, 255))
    d = ImageDraw.Draw(pv)
    for r in range(sheet.height // CH):
        for c in range(COLS):
            col = (104, 100, 96, 255) if (r + c) % 2 else (94, 90, 87, 255)
            d.rectangle([c * CW * Z, r * CH * Z, (c + 1) * CW * Z - 1, (r + 1) * CH * Z - 1], fill=col)
    pv.alpha_composite(sheet.resize((sheet.width * Z, sheet.height * Z), Image.NEAREST))
    for r, t in enumerate(labels):
        d.text((sheet.width * Z + 10, r * CH * Z + CH * Z // 2 - 6), f"row {r}: {t}", fill=(255, 226, 120, 255))
    pv.save(sys.argv[2])
    print(sheet.size, len(labels), "rows")
