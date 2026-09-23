"""Render pre-API-26 launcher bitmaps from the VietTable lotus/bowl mark.

The adaptive icon lives in drawable XML. This keeps the legacy mipmap WebP
assets aligned with that mark without scaling up a small source bitmap.
"""

from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1] / "app/src/main/res"
SIZES = {"mdpi": 48, "hdpi": 72, "xhdpi": 96, "xxhdpi": 144, "xxxhdpi": 192}
SCALE = 8


def render(size: int, round_icon: bool) -> Image.Image:
    canvas = 108 * SCALE
    image = Image.new("RGBA", (canvas, canvas), "#F5F0E5")
    draw = ImageDraw.Draw(image)

    def point(x: float, y: float) -> tuple[int, int]:
        return (round((x * 1.6 + 15.6) * SCALE), round((y * 1.6 + 15.6) * SCALE))

    draw.ellipse((*point(4, 4), *point(44, 44)), fill="#2E7D5B")
    lotus = [(24, 14), (21, 19), (18, 21), (16, 22), (20, 25), (23, 27),
             (24, 30), (25, 27), (28, 25), (32, 22), (30, 21), (27, 19)]
    draw.polygon([point(x, y) for x, y in lotus], fill="#CCA862")
    bowl = [(14, 32), (34, 32), (33, 35), (30, 38), (27, 39), (21, 39),
            (18, 38), (15, 35)]
    draw.polygon([point(x, y) for x, y in bowl], fill="white")

    if round_icon:
        mask = Image.new("L", image.size)
        ImageDraw.Draw(mask).ellipse((0, 0, canvas - 1, canvas - 1), fill=255)
        image.putalpha(mask)
    return image.resize((size, size), Image.Resampling.LANCZOS)


for density, size in SIZES.items():
    directory = ROOT / f"mipmap-{density}"
    render(size, False).save(directory / "ic_launcher.webp", "WEBP", quality=95)
    render(size, True).save(directory / "ic_launcher_round.webp", "WEBP", quality=95)
    print(density, size)
