"""Regenerates the built-in hidden images in assets/.

    python tools/generate-assets.py

Each image is greyscale: a bright subject on a black background. The screensaver samples one
pixel per character cell, so only bold shapes survive - keep details large and high-contrast.
Requires Pillow.
"""
from PIL import Image, ImageChops, ImageDraw, ImageFilter
import os

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "assets")
S = 4  # supersampling factor


def canvas(w, h):
    img = Image.new("L", (w * S, h * S), 0)
    return img, ImageDraw.Draw(img)


def finish(img, w, h, blur=3):
    return img.resize((w, h), Image.LANCZOS).filter(ImageFilter.GaussianBlur(blur))


def skull(w=800, h=1000):
    img, d = canvas(w, h)
    e = lambda b, f: d.ellipse([v * S for v in b], fill=f)
    p = lambda pts, f: d.polygon([(x * S, y * S) for x, y in pts], fill=f)
    r = lambda b, f, rad=0: d.rounded_rectangle([v * S for v in b], radius=rad * S, fill=f)

    e((150, 40, 650, 620), 225)                                     # cranium
    p([(175, 420), (625, 420), (600, 610), (540, 690), (260, 690), (200, 610)], 215)
    e((160, 470, 300, 610), 205); e((500, 470, 640, 610), 205)      # cheekbones
    r((265, 640, 535, 790), 205, 40)                                # upper jaw
    p([(215, 640), (250, 760), (300, 880), (400, 910), (500, 880), (550, 760), (585, 640),
       (545, 660), (520, 780), (400, 820), (280, 780), (255, 660)], 190)   # mandible
    r((280, 770, 520, 860), 195, 30)
    p([(215, 395), (300, 370), (375, 395), (385, 470), (345, 530), (255, 530), (215, 470)], 8)
    p([(585, 395), (500, 370), (425, 395), (415, 470), (455, 530), (545, 530), (585, 470)], 8)
    p([(400, 560), (372, 600), (355, 655), (390, 672), (400, 660), (410, 672), (445, 655), (428, 600)], 10)
    for x in range(282, 530, 31):
        r((x, 705, x + 5, 835), 40)
    r((275, 762, 525, 772), 30)
    e((225, 600, 300, 700), 120); e((500, 600, 575, 700), 120)      # cheek hollows

    img = finish(img, w, h)
    grad = Image.radial_gradient("L").resize((w * 2, h * 2)).crop((w // 2, h // 4, w // 2 + w, h // 4 + h))
    return ImageChops.multiply(img, ImageChops.invert(grad).point(lambda v: 150 + v * 105 // 255))


def alien(w=800, h=1000):
    img, d = canvas(w, h)
    e = lambda b, f: d.ellipse([v * S for v in b], fill=f)
    p = lambda pts, f: d.polygon([(x * S, y * S) for x, y in pts], fill=f)

    e((110, 90, 690, 600), 215)                                     # cranium
    p([(150, 360), (650, 360), (555, 700), (400, 870), (245, 700)], 210)   # tapering face
    # Large slanted almond eyes, built as rotated ellipses with a clear gap between them.
    for right in (False, True):
        eye = Image.new("L", (250 * S, 120 * S), 0)
        ImageDraw.Draw(eye).ellipse([0, 0, 250 * S - 1, 120 * S - 1], fill=255)
        eye = eye.rotate(16 if right else -16, expand=True, resample=Image.BICUBIC)
        img.paste(0, ((410 if right else 115) * S, 350 * S), eye)
    e((368, 640, 388, 684), 130); e((412, 640, 432, 684), 130)      # nostrils
    p([(360, 750), (440, 750), (400, 776)], 140)                    # mouth

    img = finish(img, w, h)
    grad = Image.radial_gradient("L").resize((w * 2, h * 2)).crop((w // 2, h // 4, w // 2 + w, h // 4 + h))
    return ImageChops.multiply(img, ImageChops.invert(grad).point(lambda v: 165 + v * 90 // 255))


def figure_1730():
    """The "1730" silhouette: converted from the supplied artwork in assets/sources/.

    The artwork is a flat bright figure on black, which is already the shape the effect wants;
    this just flattens it to greyscale, stretches the contrast and softens the edges.
    """
    src = Image.open(os.path.join(OUT, "sources", "1730-source.png")).convert("RGB")
    # Brightest channel, so a saturated colour on black becomes a full-strength mask.
    grey = src.split()[0]
    for channel in src.split()[1:]:
        grey = ImageChops.lighter(grey, channel)
    lo, hi = grey.getextrema()
    span = max(1, hi - lo)
    grey = grey.point(lambda v: max(0, min(255, (v - lo) * 255 // span)))
    return grey.filter(ImageFilter.GaussianBlur(2))


def triple_zero(w=1200, h=460):
    """Triple Zero Labs mark: three rings spaced like an ellipsis."""
    img, d = canvas(w, h)
    # Thick rings: the screensaver samples one pixel per character cell, so thin strokes vanish.
    radius, gap, stroke = 150, 390, 62
    cy = h // 2
    first = w // 2 - gap
    for i in range(3):
        cx = first + i * gap
        box = [(cx - radius) * S, (cy - radius) * S, (cx + radius) * S, (cy + radius) * S]
        d.ellipse(box, outline=245, width=stroke * S)
    return finish(img, w, h, blur=2)


if __name__ == "__main__":
    for name, image in (("skull", skull()), ("alien", alien()),
                        ("triplezero", triple_zero()), ("1730", figure_1730())):
        path = os.path.join(OUT, name + ".png")
        image.save(path)
        print("wrote", path)
