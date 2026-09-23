"""Create visual QA contact sheets from the live Gateway menu (Pillow required)."""

import argparse
import io
import json
import urllib.request
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps


def get(url):
    with urllib.request.urlopen(url, timeout=15) as response:
        return response.read()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--gateway", default="http://127.0.0.1:5100")
    parser.add_argument("--branch", type=int, default=1)
    parser.add_argument("--out-dir", type=Path, required=True)
    args = parser.parse_args()
    base = args.gateway.rstrip("/")
    menu = json.loads(get(f"{base}/api/mobile/v1/branches/{args.branch}/menu"))
    items = [
        {
            "itemId": dish["dishId"],
            "name": dish["name"],
            "category": category["categoryName"],
            "branchId": args.branch,
            "branchName": menu["branchName"],
            "imageUrl": dish["image"],
        }
        for category in menu["categories"]
        for dish in category["dishes"]
    ]
    args.out_dir.mkdir(parents=True, exist_ok=True)
    (args.out_dir / "inventory.json").write_text(
        json.dumps(items, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 18)
    for page in range((len(items) + 12) // 13):
        sheet = Image.new("RGB", (1400, 1040), "white")
        draw = ImageDraw.Draw(sheet)
        for index, item in enumerate(items[page * 13 : (page + 1) * 13]):
            image = Image.open(io.BytesIO(get(base + item["imageUrl"]))).convert("RGB")
            thumb = ImageOps.contain(image, (340, 210))
            x, y = (index % 4) * 350, (index // 4) * 260
            sheet.paste(thumb, (x, y))
            draw.text((x + 4, y + 212), f'{item["itemId"]} {item["name"]}', fill="black", font=font)
            draw.text((x + 4, y + 235), item["category"], fill="gray", font=font)
        sheet.save(args.out_dir / f"menu-qa-{page + 1}.jpg", quality=92)
    print(f"{len(items)} items, {(len(items) + 12) // 13} contact sheets: {args.out_dir}")


if __name__ == "__main__":
    main()
