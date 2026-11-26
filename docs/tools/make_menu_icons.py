#!/usr/bin/env python3
"""Shift Jam menu icons (D-0226): at-icons SVGs -> hard-pixel PNGs in his neon palette, plus the Kenney prompt glyphs.

What it does, per icon in ICONS below:
  1. rasterizes each at-icons SVG on its native 16 x 16 grid (or a smaller grid for the three-in-a-row icons) with HARD
     pixels: the SVG is drawn 8 times larger, each 8 x 8 block's coverage is measured, and a pixel is in when at least
     half of it is covered (no soft edges, no anti-aliasing);
  2. paints it in his palette (Art/Flat/PixelShiftPalette.png): the body in one colour, its top edge one step lighter;
  3. puts a 1-pixel dark outline round it (#0a0812, his ink), which also fills the small holes inside a glyph (the
     globe's lines, the gear's middle), as the mockup's icons are drawn;
  4. trims it to what is drawn and writes icon_<id>.png at 1x (at most 16 + 2 pixels of outline high) into
     <out>/Icons, and a big copy of every icon into the contact sheet (the game scales the 1x file by whole pixels).
The Kenney prompt glyphs (CC0) are copied as they are (Kenney's own style, 64 x 64) into <out>/Prompts.

To make another icon: add a line to ICONS (an id, the at-icons name, its colour) and run this again; then run the
Unity menu Shift Jam > UI > Land Menu Icons (D-0226) and put the icon's id on a menu.

Run:  python3 make_menu_icons.py --library "<his Asset Library folder>" --out Assets/_Game/Art/UI [--sheet FILE]
      (--library may be left out when the SJ_ASSET_LIBRARY environment variable names that folder; it must hold
      2D/UI/at-icons_v1.2.1 and 2D/Kenney Input Prompts 1.5)
Needs: Python 3, cairosvg, Pillow.
"""
import argparse
import io
import os
import shutil
import sys

import cairosvg
from PIL import Image

# The two free packs, inside his Asset Library folder (--library, or the SJ_ASSET_LIBRARY environment variable).
AT_ICONS_IN = "2D/UI/at-icons_v1.2.1/addons/at-icons"     # at-icons v1.2.1, MIT, (c) Valentin Fossati (Voxybuns)
KENNEY_IN = "2D/Kenney Input Prompts 1.5"                  # Kenney Input Prompts 1.5, CC0
AT_ICONS = KENNEY = ""                                     # set by main() from the library folder

# His palette (Art/Flat/PixelShiftPalette.png): each body colour and the lighter step used on its top edge.
INK = (0x0a, 0x08, 0x12, 255)
COLOURS = {
    "cyan": ((0x73, 0xf1, 0xff), (0xc3, 0xf9, 0xff)),
    "pink": ((0xff, 0x72, 0xcc), (0xff, 0xa8, 0xe0)),
    "gold": ((0xf5, 0xc8, 0x4c), (0xff, 0xe4, 0xb5)),
    "pale": ((0xff, 0xf0, 0xfc), (0xff, 0xff, 0xff)),
    "purple": ((0xb2, 0x4d, 0xff), (0xff, 0x93, 0xd8)),
}

# id -> list of parts: (at-icons name, colour, grid size in pixels, x, y). A part is drawn on a grid of that many
# pixels (16 = the icon's native size) and placed with its top left at x, y of the icon (before the outline).
# Three-in-a-row icons (Colours, Parties) draw three small parts, as the mockup does.
ICONS = {
    "workshop":      [("wrench", "pale", 16, 0, 0)],
    "raceway":       [("flag_checkered", "pale", 16, 0, 0)],
    "colours":       [("droplet", "cyan", 12, 0, 4), ("droplet", "pink", 12, 8, 0), ("droplet", "gold", 12, 16, 4)],
    "online":        [("pixels:globe", "cyan", 16, 0, 0)],
    "parties":       [("smiley_face", "cyan", 11, 0, 5), ("smiley_face", "pink", 11, 9, 0), ("smiley_face", "gold", 11, 18, 5)],
    "options":       [("cog", "gold", 16, 0, 0)],
    "quit":          [("arrow_right_from_bracket", "gold", 16, 0, 0)],
    "resume":        [("play", "cyan", 16, 0, 0)],
    "title":         [("house", "pink", 16, 0, 0)],
    "leave-party":   [("doorway_exit", "pink", 16, 0, 0)],
    "sticker-chart": [("sticker", "gold", 16, 0, 0)],
    "playground":    [("cube", "pink", 16, 0, 0)],
    "quick-play":    [("lightning_bolt", "gold", 16, 0, 0)],
    "volume":        [("speaker", "cyan", 16, 0, 0)],
}

# The prompt glyphs: file name in the project -> the Kenney file (Default = 64 x 64).
PROMPTS = {
    "prompt_kb_move.png": "Keyboard & Mouse/Default/keyboard_arrows.png",
    "prompt_kb_select.png": "Keyboard & Mouse/Default/keyboard_enter.png",
    "prompt_kb_back.png": "Keyboard & Mouse/Default/keyboard_escape.png",
    "prompt_xbox_move.png": "Xbox Series/Default/xbox_dpad.png",
    "prompt_xbox_select.png": "Xbox Series/Default/xbox_button_color_a.png",
    "prompt_xbox_back.png": "Xbox Series/Default/xbox_button_color_b.png",
    "prompt_ps_move.png": "PlayStation Series/Default/playstation_dpad.png",
    "prompt_ps_select.png": "PlayStation Series/Default/playstation_button_color_cross.png",
    "prompt_ps_back.png": "PlayStation Series/Default/playstation_button_color_circle.png",
}

SUPER = 8  # the SVG is drawn this many times larger, then each block's coverage decides its pixel

# Glyphs laid by hand, pixel by pixel ('#' = in), for an at-icons drawing whose fine lines do not survive 16 pixels.
# A part named "pixels:<key>" uses one. globe: at-icons' globe has hairline gaps between its pieces, which at 16 x 16
# merge into a square blob; this is the same globe (two curved meridians and a middle one, two latitudes) on the grid.
PIXEL_MAPS = {
    "globe": [
        ".....#####.....",
        "...##.#.#.##...",
        "..###.#.#.###..",
        ".###.##.##.###.",
        "...............",
        "####.##.##.####",
        "####.##.##.####",
        "####.##.##.####",
        "####.##.##.####",
        "####.##.##.####",
        "...............",
        ".###.##.##.###.",
        "..###.#.#.###..",
        "...##.#.#.##...",
        ".....#####.....",
    ],
}


def svg_path(name):
    path = os.path.join(AT_ICONS, "control", name + ".svg")
    if not os.path.isfile(path):
        sys.exit("no at-icons SVG called %s (looked in %s)" % (name, path))
    return path


def mask(name, grid):
    """The glyph as a grid x grid list of booleans: a pixel is in when at least half of it is covered."""
    if name.startswith("pixels:"):
        rows = PIXEL_MAPS[name[len("pixels:"):]]
        return [[(x < len(r) and r[x] == "#") for x in range(grid)] for r in rows] + [[False] * grid] * (grid - len(rows))
    big = grid * SUPER
    png = cairosvg.svg2png(url=svg_path(name), output_width=big, output_height=big)
    alpha = Image.open(io.BytesIO(png)).convert("RGBA").getchannel("A")
    a = alpha.tobytes()
    out = []
    for y in range(grid):
        row = []
        for x in range(grid):
            total = 0
            for yy in range(y * SUPER, (y + 1) * SUPER):
                base = yy * big
                total += sum(a[base + x * SUPER: base + (x + 1) * SUPER])
            row.append(total >= 255 * SUPER * SUPER * 0.5)
        out.append(row)
    return out


def build(parts):
    """One icon: its parts painted and placed, then the dark outline round all of it. Returns an RGBA image."""
    placed = []
    w = h = 0
    for name, colour, grid, px, py in parts:
        m = mask(name, grid)
        placed.append((m, COLOURS[colour], px, py, grid))
        w, h = max(w, px + grid), max(h, py + grid)
    # Trimmed to what is drawn (rows and columns): every icon is centred on its own pixels, and the game sizes each by
    # the same whole number of screen pixels per icon pixel, so a narrower glyph is narrower, never stretched.
    body = [[None] * w for _ in range(h)]
    for m, (fill, light), px, py, grid in placed:
        for y in range(grid):
            for x in range(grid):
                if not m[y][x]:
                    continue
                top_edge = y == 0 or not m[y - 1][x]
                body[py + y][px + x] = (light if top_edge else fill) + (255,)
    cols = [x for x in range(w) if any(body[y][x] for y in range(h))]
    rows = [y for y in range(h) if any(body[y])]
    body = [row[min(cols):max(cols) + 1] for row in body[min(rows):max(rows) + 1]]
    w, h = max(cols) - min(cols) + 1, max(rows) - min(rows) + 1
    img = Image.new("RGBA", (w + 2, h + 2), (0, 0, 0, 0))
    pix = img.load()
    for y in range(h):
        for x in range(w):
            if body[y][x]:
                pix[x + 1, y + 1] = body[y][x]
    # The outline: every empty pixel touching the body (8 ways) turns ink.
    ink = []
    for y in range(h + 2):
        for x in range(w + 2):
            if pix[x, y][3]:
                continue
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w + 2 and 0 <= ny < h + 2 and pix[nx, ny][3] and pix[nx, ny] != INK:
                        ink.append((x, y))
                        break
                else:
                    continue
                break
    for x, y in ink:
        pix[x, y] = INK
    return img


def sheet(icons, prompts, path):
    """The contact sheet: every icon at 4x and 1x on his dark purple, then the prompt glyphs, each with its id."""
    from PIL import ImageDraw
    cell_w, cell_h, scale = 170, 120, 4
    items = list(icons.items())
    cols = 5
    rows = (len(items) + cols - 1) // cols
    prow = 1
    W = cols * cell_w
    H = rows * cell_h + 30 + prow * 100 + 20
    img = Image.new("RGBA", (W, H), (0x1c, 0x10, 0x2e, 255))
    d = ImageDraw.Draw(img)
    for i, (key, ic) in enumerate(items):
        cx, cy = (i % cols) * cell_w, (i // cols) * cell_h
        big = ic.resize((ic.width * scale, ic.height * scale), Image.NEAREST)
        img.alpha_composite(big, (cx + 10, cy + 10))
        img.alpha_composite(ic, (cx + 10 + big.width + 8, cy + 10 + big.height - ic.height))
        d.text((cx + 10, cy + 92), "%s  %dx%d" % (key, ic.width, ic.height), fill=(0xff, 0xf0, 0xfc, 255))
    y0 = rows * cell_h + 30
    d.text((10, y0 - 22), "prompts (Kenney Input Prompts 1.5, CC0), shown at 48 px", fill=(0xf5, 0xc8, 0x4c, 255))
    for i, (key, p) in enumerate(prompts.items()):
        small = p.resize((48, 48), Image.LANCZOS)
        img.alpha_composite(small, (10 + i * 92, y0))
        d.text((10 + i * 92, y0 + 54), key.replace(".png", "").replace("prompt_", ""), fill=(0xff, 0xf0, 0xfc, 255))
    img.save(path)


def main():
    global AT_ICONS, KENNEY
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--library", default=os.environ.get("SJ_ASSET_LIBRARY", ""), help="his Asset Library folder (holds 2D/UI/at-icons_v1.2.1 and 2D/Kenney Input Prompts 1.5)")
    ap.add_argument("--out", required=True, help="folder that gets Icons/ and Prompts/ (the game's: Assets/_Game/Art/UI)")
    ap.add_argument("--sheet", default="", help="also write a contact sheet PNG of every icon and glyph here (keep it out of Assets/)")
    args = ap.parse_args()
    if not args.library:
        sys.exit("say where the Asset Library is: --library \"<folder>\" (or set SJ_ASSET_LIBRARY)")
    AT_ICONS, KENNEY = os.path.join(args.library, AT_ICONS_IN), os.path.join(args.library, KENNEY_IN)
    for folder in (AT_ICONS, KENNEY):
        if not os.path.isdir(folder):
            sys.exit("not found: %s" % folder)
    icons_dir, prompts_dir = os.path.join(args.out, "Icons"), os.path.join(args.out, "Prompts")
    os.makedirs(icons_dir, exist_ok=True)
    os.makedirs(prompts_dir, exist_ok=True)

    made = {}
    for key, parts in ICONS.items():
        img = build(parts)
        img.save(os.path.join(icons_dir, "icon_" + key + ".png"))
        made[key] = img
        print("icon %-14s %2dx%-2d  %s" % (key, img.width, img.height, " + ".join(p[0] for p in parts)))
    shutil.copyfile(os.path.join(AT_ICONS, "LICENSE.txt"), os.path.join(icons_dir, "LICENSE.txt"))

    prompts = {}
    for name, src in PROMPTS.items():
        full = os.path.join(KENNEY, src)
        if not os.path.isfile(full):
            sys.exit("no Kenney file %s" % full)
        shutil.copyfile(full, os.path.join(prompts_dir, name))
        prompts[name] = Image.open(full).convert("RGBA")
        print("prompt %-16s <- %s" % (name, src))
    shutil.copyfile(os.path.join(KENNEY, "License.txt"), os.path.join(prompts_dir, "Kenney License.txt"))

    if args.sheet:
        sheet(made, prompts, args.sheet)
        print("contact sheet:", args.sheet)


if __name__ == "__main__":
    main()
