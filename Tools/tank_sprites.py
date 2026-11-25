"""Top-down 16-bit style desert tank: separate HULL and TURRET sprites (front points UP).
Both images are 64x96 and the turret-ring centre is the exact image centre (32,48) in both,
so with Pivot = Center the turret sits correctly on the hull and rotates around its ring.
Uses the shading engine from character_sheet.py.
Usage: python tank_sprites.py <out_dir> <preview.png>"""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import character_sheet as cs
from PIL import Image, ImageDraw

cs.CW, cs.CH = 64, 96
W, H = 64, 96
for k, v in {"sand": (196, 176, 132), "sand_d": (170, 150, 108), "track": (58, 56, 54), "trackpin": (96, 92, 86),
             "mesh": (150, 134, 98), "steel": (84, 86, 92), "optic": (40, 60, 86), "rubber": (40, 38, 38),
             "lamp": (230, 226, 200)}.items():
    cs.MAT[k] = cs.ramp(v)


def poly_pix(cv, pts, mat, bevel=1.6, tilt=0.9, bias=0.0, mod=None):
    """flat plate with bevelled edges (edges tilt outward -> catch / lose the light)."""
    area = sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1] for i in range(len(pts)))
    ccw = area > 0
    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
    pix = {}
    for y in range(max(0, int(min(ys)) - 1), min(H, int(max(ys)) + 2)):
        for x in range(max(0, int(min(xs)) - 1), min(W, int(max(xs)) + 2)):
            px, py = x + .5, y + .5
            inside = False
            for i in range(len(pts)):
                (x0, y0), (x1, y1) = pts[i], pts[(i + 1) % len(pts)]
                if (y0 > py) != (y1 > py) and px < x0 + (py - y0) * (x1 - x0) / (y1 - y0): inside = not inside
            if not inside: continue
            best, nrm = 1e9, (0, 0)
            for i in range(len(pts)):
                (x0, y0), (x1, y1) = pts[i], pts[(i + 1) % len(pts)]
                vx, vy = x1 - x0, y1 - y0; L2 = vx * vx + vy * vy or 1e-9
                t = max(0, min(1, ((px - x0) * vx + (py - y0) * vy) / L2))
                d = math.hypot(px - x0 - vx * t, py - y0 - vy * t)
                if d < best:
                    L = math.sqrt(L2); best = d
                    nrm = (vy / L, -vx / L) if ccw else (-vy / L, vx / L)
            k = tilt * max(0.0, 1 - best / bevel) if bevel else 0
            I = cv.shade(nrm[0] * k, nrm[1] * k, 1.0, 1.0) + bias
            if mod: I = mod(x, y, I)
            pix[(x, y)] = (mat, I)
    return pix


def poly(cv, pts, mat, ao=0.2, **k): cv.commit(poly_pix(cv, pts, mat, **k), ao)
def rect(cv, x0, y0, x1, y1, mat, **k): poly(cv, [(x0, y0), (x1, y0), (x1, y1), (x0, y1)], mat, **k)


def hull():
    cv = cs.Cv()
    for x0 in (7.5, 48.5):                                    # tracks (visible at both ends)
        rect(cv, x0, 8, x0 + 8, 89, "track", bevel=1.2, ao=0,
             mod=lambda x, y, I: I - 0.22 if y % 3 == 0 else (I + 0.06 if y % 3 == 1 else I))
        for y in range(9, 89, 3): cv.px(x0 + 4, y, "trackpin", 0.75)
    # main hull deck with chamfered front / rear
    poly(cv, [(16, 7), (48, 7), (51, 12), (51, 86), (48, 90), (16, 90), (13, 86), (13, 12)], "sand", bevel=2.2)
    # glacis plate (front slope) and rear plate
    poly(cv, [(17, 8), (47, 8), (49, 13), (15, 13)], "sand", bevel=1.0, bias=0.1, ao=0.12)
    poly(cv, [(16, 86), (48, 86), (47, 89), (17, 89)], "sand_d", bevel=0.8, ao=0.12)
    # side skirts over the tracks with panel seams
    for x0 in (7, 49):
        rect(cv, x0, 14, x0 + 8, 83, "sand", bevel=1.4, ao=0.15,
             mod=lambda x, y, I: I - 0.2 if (y - 14) % 11 == 0 else I)
        rect(cv, x0, 12, x0 + 8, 14, "sand_d", bevel=0.8, ao=0.1)              # front skirt lip
    # headlights + driver's hatch / periscopes
    for x in (18, 44):
        cv.cap(x, 10, x + 2, 10, 1.0, "lamp", ao=0.15)
        cv.cap(x - 0.5, 12, x + 2.5, 12, 0.6, "steel", ao=0.05)
    cv.ell(32, 18, 3.4, 2.8, "sand_d", flat=0.6, ao=0.25)
    cv.cap(29, 15.2, 35, 15.2, 0.6, "optic", ao=0.1)
    # engine deck grilles (rear)
    for y0 in (70, 77):
        rect(cv, 19, y0, 45, y0 + 5, "sand_d", bevel=0.8, ao=0.25,
             mod=lambda x, y, I: I - 0.25 if x % 2 == 0 else I)
    rect(cv, 20, 84, 44, 86, "steel", bevel=0.6, ao=0.2,
         mod=lambda x, y, I: I - 0.3 if x % 2 else I)                         # exhaust grille
    # tow hooks / tie-downs
    for x in (17, 47): cv.ell(x, 88.2, 1.0, 1.0, "steel", ao=0.1)
    for (x, y) in ((18, 26), (46, 26), (18, 62), (46, 62)): cv.ell(x, y, 0.9, 0.9, "sand_d", ao=0.15)
    # turret ring (hidden under the turret, visible when turret is removed / destroyed)
    cv.ell(32, 48, 13, 13, "sand_d", flat=0.4, ao=0.25)
    cv.ell(32, 48, 11.5, 11.5, "steel", flat=0.3, ao=0.15)
    return cv.render()


def turret():
    cv = cs.Cv()
    # gun: mantlet, barrel with thermal sleeve bands, bore evacuator, muzzle
    rect(cv, 27, 26, 37, 35, "sand", bevel=1.2, ao=0)
    cv.cap(32, 28, 32, 4, 1.6, "sand", flat=0.9,
           mod=lambda x, y, I: I - 0.18 if y in (22, 9) else I)
    cv.ell(32, 15, 2.3, 2.6, "sand", flat=0.9, ao=0.15)                        # bore evacuator
    cv.cap(32, 4.5, 32, 2.5, 1.9, "sand_d", flat=0.9, ao=0.15)                 # muzzle
    cv.px(32, 2, "rubber", 0.2)
    # angular wedge turret
    poly(cv, [(24, 31), (40, 31), (48, 39), (49, 61), (46, 65), (18, 65), (15, 61), (16, 39)], "sand", bevel=2.4)
    poly(cv, [(25, 32), (39, 32), (45, 38), (19, 38)], "sand", bevel=0.8, bias=0.08, ao=0.1)   # front cheek slope
    # rear bustle rack (mesh basket)
    rect(cv, 17, 65, 47, 73, "mesh", bevel=0.8, ao=0.25,
         mod=lambda x, y, I: I - 0.35 if (x % 3 == 0 or y % 3 == 0) else I)
    # stowage boxes on the sides
    rect(cv, 13, 46, 16, 59, "sand_d", bevel=0.8, ao=0.2)
    rect(cv, 48, 46, 51, 59, "sand_d", bevel=0.8, ao=0.2)
    # smoke grenade launchers
    for sx, d in ((17, -1), (47, 1)):
        for i in range(3): cv.cap(sx + d * i * 0.2, 41 + i * 2, sx + d * 2.2, 40.4 + i * 2, 0.8, "steel", ao=0.12)
    # gunner's sight box + commander's sight
    rect(cv, 22, 37, 28, 42, "sand", bevel=1.0, bias=0.06, ao=0.25)
    cv.cap(23, 37.4, 27, 37.4, 0.6, "optic", ao=0)
    rect(cv, 37, 39, 42, 43, "sand", bevel=0.9, bias=0.06, ao=0.25)
    cv.cap(38, 39.4, 41, 39.4, 0.6, "optic", ao=0)
    # commander's cupola with hatch + machine gun
    cv.ell(40, 53, 4.6, 4.6, "sand_d", flat=0.6, ao=0.3)
    cv.ell(40, 53, 3.4, 3.4, "sand", flat=0.5, ao=0.2)
    cv.cap(40.8, 51, 41.2, 43, 0.75, "steel", ao=0.15)
    cv.ell(41, 51.5, 1.3, 1.6, "steel", ao=0.1)
    # loader's hatch + MG
    cv.ell(24, 54, 3.8, 3.8, "sand", flat=0.5, ao=0.3)
    cv.cap(22, 54, 26, 54, 0.5, "sand_d", ao=0.05)
    cv.cap(26.5, 50, 26.5, 44.5, 0.65, "steel", ao=0.15)
    # antennas / wind sensor
    for (x, y) in ((19, 63), (45, 63)): cv.ell(x, y, 1.0, 1.0, "steel", ao=0.12)
    cv.cap(32, 46.5, 32, 44, 0.5, "steel", ao=0.1)
    return cv.render()


if __name__ == "__main__":
    out, prev = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    h, t = hull(), turret()
    h.save(os.path.join(out, "tank_hull.png")); t.save(os.path.join(out, "tank_turret.png"))
    full = h.copy(); full.alpha_composite(t); full.save(os.path.join(out, "tank_assembled.png"))
    rot = Image.new("RGBA", (W, H)); rot.alpha_composite(h)
    rot.alpha_composite(t.rotate(-35, resample=Image.NEAREST, center=(32, 48)))
    Z = 5
    pv = Image.new("RGBA", (W * 4 * Z + 30, H * Z + 30), (150, 140, 112, 255))
    d = ImageDraw.Draw(pv)
    for i, (im, lab) in enumerate(((h, "hull"), (t, "turret"), (full, "assembled"), (rot, "turret rotated 35"))):
        pv.alpha_composite(im.resize((W * Z, H * Z), Image.NEAREST), (10 + i * (W * Z + 3), 25))
        d.text((14 + i * (W * Z + 3), 6), lab, fill=(40, 34, 24, 255))
    pv.save(prev)
    print("ok")
