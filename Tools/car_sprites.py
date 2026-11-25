"""Original top-down retro pixel-art cars (late-90s top-down crime-game style).
Each sprite is 32x64, front of the car points UP. Transparent background."""
import os, sys
from PIL import Image

W, H = 32, 64
OUT = sys.argv[1]

def shade(c, k):
    return tuple(max(0, min(255, int(v * k))) for v in c[:3]) + (255,)

GLASS = (40, 55, 75, 255)
GLASS_HI = (110, 140, 170, 255)
TIRE = (20, 20, 22, 255)
HEAD = (255, 250, 200, 255)
TAIL = (210, 30, 30, 255)
CHROME = (190, 190, 195, 255)

class Car:
    def __init__(self, color, w=18, l=44):
        self.img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        self.px = self.img.load()
        self.c = color + (255,) if len(color) == 3 else color
        self.w, self.l = w, l
        self.x0 = (W - w) // 2
        self.y0 = (H - l) // 2
        self.x1 = self.x0 + w - 1
        self.y1 = self.y0 + l - 1

    def put(self, x, y, c):
        if 0 <= x < W and 0 <= y < H:
            self.px[x, y] = c

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.put(x, y, c)

    def body(self, corner=2):
        x0, y0, x1, y1, c = self.x0, self.y0, self.x1, self.y1, self.c
        # wheels peeking out
        for wy in (y0 + 6, y1 - 11):
            self.rect(x0 - 1, wy, x0, wy + 6, TIRE)
            self.rect(x1, wy, x1 + 1, wy + 6, TIRE)
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                dy = min(y - y0, y1 - y)
                dx = min(x - x0, x1 - x)
                if dx + dy < corner:
                    continue
                edge = dx == 0 or dy == 0 or dx + dy == corner
                if edge:
                    col = shade(c, 0.45)
                elif x - x0 <= 1:
                    col = shade(c, 1.25)
                elif x1 - x <= 1:
                    col = shade(c, 0.72)
                else:
                    col = c
                self.put(x, y, col)
        # center hood crease
        cx = (x0 + x1) / 2
        for y in range(y0 + 3, y0 + 9):
            self.put(int(cx), y, shade(c, 1.12))

    def cabin(self, ws, roof_end, rear_end, inset=2, roof_color=None):
        """ws: windshield top y offset; roof_end: roof bottom offset; rear_end: rear window bottom offset."""
        x0, x1, y0 = self.x0 + inset, self.x1 - inset, self.y0
        rc = roof_color or shade(self.c, 1.05)
        # windshield (trapezoid, wider at bottom)
        for i, y in enumerate(range(y0 + ws, y0 + ws + 5)):
            pad = max(0, 2 - i // 2)
            for x in range(x0 + pad, x1 - pad + 1):
                self.put(x, y, GLASS_HI if (x - x0 + i) % 7 == 1 else GLASS)
        # side windows
        top = y0 + ws + 5
        for y in range(top, y0 + roof_end):
            self.put(x0, y, GLASS)
            self.put(x1, y, GLASS)
        # roof
        self.rect(x0 + 1, top, x1 - 1, y0 + roof_end - 1, rc)
        self.rect(x0 + 1, top, x0 + 1, y0 + roof_end - 1, shade(rc, 1.2))
        self.rect(x1 - 1, top, x1 - 1, y0 + roof_end - 1, shade(rc, 0.75))
        # rear window
        for i, y in enumerate(range(y0 + roof_end, y0 + rear_end)):
            pad = i // 2
            for x in range(x0 + pad, x1 - pad + 1):
                self.put(x, y, GLASS)

    def lights(self, head=True):
        x0, x1, y0, y1 = self.x0, self.x1, self.y0, self.y1
        if head:
            self.rect(x0 + 2, y0 + 1, x0 + 4, y0 + 1, HEAD)
            self.rect(x1 - 4, y0 + 1, x1 - 2, y0 + 1, HEAD)
        self.rect(x0 + 2, y1 - 1, x0 + 4, y1 - 1, TAIL)
        self.rect(x1 - 4, y1 - 1, x1 - 2, y1 - 1, TAIL)

    def mirrors(self, y):
        self.put(self.x0 - 1, self.y0 + y, shade(self.c, 0.5))
        self.put(self.x1 + 1, self.y0 + y, shade(self.c, 0.5))

    def stripe(self, c, width=2):
        cx = (self.x0 + self.x1) // 2
        for y in range(self.y0 + 1, self.y1):
            for x in range(cx - width + 1, cx + width - (1 if width > 1 else 0) + 1):
                if self.px[x, y][3] and self.px[x, y][:3] != GLASS[:3] and self.px[x, y][:3] != GLASS_HI[:3]:
                    self.put(x, y, c)


def sedan(color=(40, 90, 170)):
    c = Car(color, 18, 42); c.body(); c.cabin(12, 26, 30); c.lights(); c.mirrors(17); return c

def taxi():
    c = Car((235, 190, 30), 18, 42); c.body(); c.cabin(12, 26, 30); c.lights(); c.mirrors(17)
    cx = (c.x0 + c.x1) // 2
    c.rect(cx - 3, c.y0 + 19, cx + 2, c.y0 + 21, (250, 250, 240, 255))  # roof sign
    c.rect(cx - 3, c.y0 + 21, cx + 2, c.y0 + 21, (60, 60, 60, 255))
    for x in range(c.x0 + 1, c.x1):  # checker band at doors
        c.put(x, c.y0 + 23, (20, 20, 20, 255) if x % 2 else (240, 240, 240, 255))
    return c

def police():
    c = Car((235, 235, 235), 18, 44); c.body(); c.cabin(12, 27, 31, roof_color=(245, 245, 245, 255)); c.lights(); c.mirrors(17)
    c.rect(c.x0 + 1, c.y0 + 2, c.x1 - 1, c.y0 + 10, (30, 30, 35, 255))   # dark hood
    c.rect(c.x0 + 1, c.y1 - 8, c.x1 - 1, c.y1 - 2, (30, 30, 35, 255))    # dark trunk
    c.lights()
    cx = (c.x0 + c.x1) // 2
    c.rect(c.x0 + 3, c.y0 + 20, cx - 1, c.y0 + 21, (230, 30, 30, 255))   # lightbar
    c.rect(cx, c.y0 + 20, c.x1 - 3, c.y0 + 21, (30, 80, 240, 255))
    return c

def sports(color=(200, 20, 30)):
    c = Car(color, 18, 40); c.body(corner=3); c.cabin(15, 25, 28, inset=3); c.lights(); c.mirrors(18)
    c.stripe((245, 245, 245, 255), 1)
    c.cabin(15, 25, 28, inset=3)
    return c

def muscle(color=(20, 110, 60)):
    c = Car(color, 18, 44); c.body(); c.cabin(15, 27, 30)
    c.lights(); c.mirrors(19)
    cx = (c.x0 + c.x1) // 2
    c.rect(cx - 2, c.y0 + 6, cx + 1, c.y0 + 11, (25, 25, 25, 255))  # hood scoop
    c.rect(c.x0 + 2, c.y1 - 2, c.x1 - 2, c.y1 - 2, shade(c.c, 0.5))  # spoiler
    return c

def pickup(color=(150, 70, 30)):
    c = Car(color, 18, 46); c.body(); c.cabin(13, 22, 24); c.lights(); c.mirrors(18)
    # cargo bed
    c.rect(c.x0 + 2, c.y0 + 26, c.x1 - 2, c.y1 - 3, shade(c.c, 0.55))
    c.rect(c.x0 + 3, c.y0 + 27, c.x1 - 3, c.y1 - 4, shade(c.c, 0.7))
    for y in range(c.y0 + 29, c.y1 - 4, 3):
        c.rect(c.x0 + 3, y, c.x1 - 3, y, shade(c.c, 0.6))
    return c

def van(color=(220, 220, 215)):
    c = Car(color, 20, 50); c.body(); c.cabin(6, 44, 46, inset=2, roof_color=shade(c.c, 1.0)); c.lights(); c.mirrors(10)
    # roof ribs
    for y in range(c.y0 + 16, c.y0 + 42, 5):
        c.rect(c.x0 + 4, y, c.x1 - 4, y, shade(c.c, 0.85))
    return c

def ambulance():
    c = van((240, 240, 240))
    cx = (c.x0 + c.x1) // 2
    red = (210, 25, 25, 255)
    c.rect(cx - 1, c.y0 + 22, cx, c.y0 + 31, red)
    c.rect(cx - 5, c.y0 + 26, cx + 4, c.y0 + 27, red)
    c.rect(c.x0 + 3, c.y0 + 12, cx - 1, c.y0 + 13, red)
    c.rect(cx, c.y0 + 12, c.x1 - 3, c.y0 + 13, (30, 80, 240, 255))
    return c

def truck(color=(40, 60, 120)):
    c = Car(color, 22, 60); c.body(corner=1)
    c.cabin(4, 12, 12, inset=2)
    # trailer box
    c.rect(c.x0 + 1, c.y0 + 15, c.x1 - 1, c.y1 - 1, (200, 200, 195, 255))
    c.rect(c.x0 + 1, c.y0 + 15, c.x1 - 1, c.y0 + 15, (60, 60, 60, 255))
    c.rect(c.x0 + 2, c.y0 + 16, c.x0 + 2, c.y1 - 2, (225, 225, 220, 255))
    c.rect(c.x1 - 2, c.y0 + 16, c.x1 - 2, c.y1 - 2, (160, 160, 155, 255))
    for y in range(c.y0 + 20, c.y1 - 2, 6):
        c.rect(c.x0 + 3, y, c.x1 - 3, y, (180, 180, 175, 255))
    c.lights(); c.mirrors(7)
    return c

def convertible(color=(240, 130, 180)):
    c = Car(color, 18, 42); c.body(corner=3); c.lights(); c.mirrors(17)
    x0, x1, y0 = c.x0 + 2, c.x1 - 2, c.y0
    for i, y in enumerate(range(y0 + 13, y0 + 16)):
        for x in range(x0 + 1, x1):
            c.put(x, y, GLASS if i < 2 else CHROME)
    seat = (110, 60, 40, 255)
    c.rect(x0, y0 + 16, x1, y0 + 31, (60, 40, 30, 255))  # interior
    c.rect(x0 + 1, y0 + 18, x0 + 5, y0 + 22, seat)
    c.rect(x1 - 5, y0 + 18, x1 - 1, y0 + 22, seat)
    c.rect(x0 + 1, y0 + 26, x1 - 1, y0 + 29, seat)
    c.put(x0 + 3, y0 + 16, (30, 30, 30, 255)); c.put(x0 + 4, y0 + 16, (30, 30, 30, 255))  # wheel
    return c

def bus():
    c = Car((230, 170, 20), 22, 62); c.body(corner=1); c.lights(); c.mirrors(3)
    c.rect(c.x0 + 2, c.y0 + 2, c.x1 - 2, c.y0 + 4, GLASS)
    c.rect(c.x0 + 2, c.y0 + 6, c.x1 - 2, c.y1 - 3, (245, 245, 240, 255))
    for y in range(c.y0 + 10, c.y1 - 4, 8):
        c.rect(c.x0 + 5, y, c.x1 - 5, y + 2, (200, 200, 195, 255))  # roof vents
    return c

CARS = [
    ("sedan_blue", lambda: sedan((40, 90, 170))),
    ("sedan_grey", lambda: sedan((120, 125, 130))),
    ("taxi", taxi),
    ("police", police),
    ("sports_red", sports),
    ("sports_black", lambda: sports((35, 35, 40))),
    ("muscle_green", muscle),
    ("convertible_pink", convertible),
    ("pickup", pickup),
    ("van_white", van),
    ("ambulance", ambulance),
    ("truck", truck),
    ("bus", bus),
]

os.makedirs(OUT, exist_ok=True)
sheet = Image.new("RGBA", (W * len(CARS), H), (0, 0, 0, 0))
for i, (name, fn) in enumerate(CARS):
    img = fn().img
    img.save(os.path.join(OUT, f"car_{name}.png"))
    sheet.paste(img, (i * W, 0))
sheet.save(os.path.join(OUT, "cars_sheet.png"))

# preview on asphalt, scaled x6
S = 6
prev = Image.new("RGBA", sheet.size, (70, 72, 75, 255))
prev.alpha_composite(sheet)
prev = prev.resize((sheet.width * S, sheet.height * S), Image.NEAREST)
prev.save(os.path.join(sys.argv[2], "preview.png"))
print("ok", len(CARS))
