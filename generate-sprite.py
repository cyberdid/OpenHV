#!/usr/bin/env python3

"""Render a parametric creature to an OpenHV sprite sheet.

Image models cannot hold one creature steady through eight rotations; a model
of the creature can. Parts are placed once in creature space, then the whole
body is rotated about its vertical axis for each facing and projected with the
game's top-down camera, so every facing is the same animal by construction.

Frames are drawn at several times the target size and reduced by dominant
colour rather than by averaging, which keeps edges hard at twenty pixels
instead of dissolving them into a smear. The silhouette is outlined after the
reduction, so the outline is always exactly one pixel.
"""

from __future__ import annotations

import argparse
import math
import os
from collections import Counter
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError:  # pragma: no cover - environment guard
    raise SystemExit("generate-sprite.py requires Pillow: python3 -m pip install pillow")


PROJECT_DIR = Path(__file__).resolve().parent
DEFAULT_PALETTE = PROJECT_DIR / "mods/hv/bits/palettes/colors.pal"

TRANSPARENT_INDEX = 255
TEAM_RAMP = tuple(range(48, 64))
RESERVED = set(TEAM_RAMP) | {TRANSPARENT_INDEX}

# The camera the mod draws with: looking down, tilted back from vertical.
CAMERA_TILT_DEGREES = 35.0

OUTLINE = (0, 0, 0)
TEAM = (0, 255, 255)
BACKDROP = (255, 0, 255)


class Part:
    """One solid of the creature, in creature space.

    x runs right, y runs forward (the direction the creature faces), z runs up.
    """

    def __init__(self, a, b, radius, colour, taper=1.0, team=False):
        self.a = a
        self.b = b
        self.radius = radius
        self.colour = colour
        self.taper = taper
        self.team = team


def rotate_z(point, angle):
    x, y, z = point
    c, s = math.cos(angle), math.sin(angle)
    return (x * c - y * s, x * s + y * c, z)


def project(point, scale, tilt):
    """Top-down camera: depth compresses vertically, height lifts on screen."""
    x, y, z = point
    return (x * scale, (y * math.cos(tilt) - z * math.sin(tilt)) * scale)


def depth(point, tilt):
    x, y, z = point
    return y * math.sin(tilt) + z * math.cos(tilt)


def draw_capsule(draw, start, end, r0, r1, colour):
    steps = max(3, int(math.dist(start, end) / 1.5) + 3)
    for i in range(steps + 1):
        t = i / steps
        x = start[0] + (end[0] - start[0]) * t
        y = start[1] + (end[1] - start[1]) * t
        r = r0 + (r1 - r0) * t
        draw.ellipse([x - r, y - r, x + r, y + r], fill=colour)


def render_frame(parts, facing, size, supersample, scale):
    """Draw one facing at supersampled resolution on the backdrop colour."""
    w, h = size[0] * supersample, size[1] * supersample
    canvas = Image.new("RGB", (w, h), BACKDROP)
    draw = ImageDraw.Draw(canvas)
    tilt = math.radians(CAMERA_TILT_DEGREES)
    # scale is the creature's height as a fraction of the frame.
    unit = scale * min(size) * supersample
    cx, cy = w / 2, h / 2

    placed = []
    for part in parts:
        a = rotate_z(part.a, facing)
        b = rotate_z(part.b, facing)
        placed.append((max(depth(a, tilt), depth(b, tilt)), part, a, b))

    # Painter's algorithm: far parts first, so near limbs occlude the body.
    for _, part, a, b in sorted(placed, key=lambda item: item[0]):
        pa = project(a, unit, tilt)
        pb = project(b, unit, tilt)
        colour = TEAM if part.team else part.colour
        draw_capsule(
            draw,
            (cx + pa[0], cy - pa[1]),
            (cx + pb[0], cy - pb[1]),
            part.radius * unit,
            part.radius * unit * part.taper,
            colour,
        )
    return canvas


def reduce_dominant(image, size, supersample):
    """Downscale by most-common colour, so edges stay hard."""
    out = Image.new("RGB", size, BACKDROP)
    source = image.load()
    target = out.load()
    for y in range(size[1]):
        for x in range(size[0]):
            counts = Counter()
            for dy in range(supersample):
                for dx in range(supersample):
                    counts[source[x * supersample + dx, y * supersample + dy]] += 1
            # A block that is mostly backdrop stays backdrop; otherwise the
            # most common non-backdrop colour wins, so thin limbs survive.
            solid = [(n, c) for c, n in counts.items() if c != BACKDROP]
            if not solid:
                continue
            total = sum(n for n, _ in solid)
            if total * 2 < supersample * supersample:
                continue
            target[x, y] = max(solid)[1]
    return out


def add_outline(image):
    """One-pixel black edge around the silhouette, drawn after reduction."""
    w, h = image.size
    pixels = image.load()
    edges = []
    for y in range(h):
        for x in range(w):
            if pixels[x, y] != BACKDROP:
                continue
            for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if 0 <= nx < w and 0 <= ny < h and pixels[nx, ny] != BACKDROP:
                    edges.append((x, y))
                    break
    for x, y in edges:
        pixels[x, y] = OUTLINE
    return image


def read_palette(path):
    lines = [line.strip() for line in Path(path).read_text().splitlines()]
    if not lines or lines[0] != "JASC-PAL":
        raise SystemExit(f"{path} is not a JASC palette")
    count = int(lines[2])
    return [tuple(int(v) for v in line.split()[:3]) for line in lines[3 : 3 + count]]


def quantise(frames, size, columns, palette):
    general = [i for i in range(len(palette)) if i not in RESERVED]
    ramp = [palette[i] for i in TEAM_RAMP]
    lum = lambda c: 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]
    low = min(lum(c) for c in ramp)
    span = max(max(lum(c) for c in ramp) - low, 1.0)
    dist = lambda a, b: (a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2

    rows = (len(frames) + columns - 1) // columns
    sheet = Image.new("P", (size[0] * columns, size[1] * rows))
    flat = []
    for colour in palette:
        flat.extend(colour)
    flat.extend([0] * (768 - len(flat)))
    sheet.putpalette(flat)
    target = sheet.load()
    for y in range(sheet.height):
        for x in range(sheet.width):
            target[x, y] = TRANSPARENT_INDEX

    cache = {}
    for index, frame in enumerate(frames):
        ox = (index % columns) * size[0]
        oy = (index // columns) * size[1]
        source = frame.load()
        for y in range(size[1]):
            for x in range(size[0]):
                colour = source[x, y]
                if colour == BACKDROP:
                    continue
                if colour == TEAM:
                    step = round((lum(colour) - low) / span * (len(TEAM_RAMP) - 1))
                    target[ox + x, oy + y] = TEAM_RAMP[min(max(step, 0), len(TEAM_RAMP) - 1)]
                    continue
                if colour not in cache:
                    cache[colour] = min(general, key=lambda i: dist(colour, palette[i]))
                target[ox + x, oy + y] = cache[colour]
    return sheet


def warrior(pose):
    """Blade creature. `pose` runs 0 to 1 across one attack swing."""
    chitin = (74, 66, 92)
    flesh = (150, 84, 92)
    bone = (214, 206, 174)
    swing = math.sin(pose * math.pi)

    parts = [
        Part((0, -0.15, 0.55), (0, 0.35, 0.45), 0.34, chitin),          # thorax
        Part((0, 0.35, 0.45), (0, 0.72, 0.34), 0.20, flesh),            # head
        Part((0, -0.10, 0.80), (0, 0.22, 0.76), 0.26, chitin, team=True),  # dorsal plate
    ]
    for side in (-1, 1):
        for i, (fx, fy) in enumerate(((0.30, 0.24), (0.34, -0.16))):
            knee = (side * (fx + 0.26), fy + 0.06, 0.30)
            foot = (side * (fx + 0.40), fy - 0.04, 0.02)
            parts.append(Part((side * fx, fy, 0.50), knee, 0.10, chitin, taper=0.8))
            parts.append(Part(knee, foot, 0.08, flesh, taper=0.6))
        # Blades sweep forward and inward through the swing.
        reach = 0.55 + 0.45 * swing
        inward = 0.55 - 0.45 * swing
        elbow = (side * 0.46, 0.20, 0.58)
        tip = (side * inward, 0.30 + reach, 0.44)
        parts.append(Part((side * 0.28, 0.18, 0.58), elbow, 0.11, flesh, taper=0.9))
        parts.append(Part(elbow, tip, 0.09, bone, taper=0.25))
    return parts


BUILDERS = {"warrior": warrior}


def parse_args():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    parser.add_argument("--creature", choices=tuple(BUILDERS), default="warrior")
    parser.add_argument("--frame-size", default="20x20")
    parser.add_argument("--facings", type=int, default=8)
    parser.add_argument("--length", type=int, default=1, help="Frames per facing")
    parser.add_argument("--columns", type=int, default=8)
    parser.add_argument("--scale", type=float, default=0.42, help="Creature height as a fraction of the frame")
    parser.add_argument("--supersample", type=int, default=6)
    parser.add_argument("--palette", type=Path, default=DEFAULT_PALETTE)
    parser.add_argument("--author", required=True)
    parser.add_argument("--license", default="CC-BY-SA-4.0")
    return parser.parse_args()


def main():
    args = parse_args()
    width, height = (int(v) for v in args.frame_size.replace(",", "x").split("x"))
    size = (width, height)
    build = BUILDERS[args.creature]
    palette = read_palette(args.palette)

    frames = []
    for facing in range(args.facings):
        # Facing 0 points south, at the viewer, and each step turns clockwise.
        angle = math.radians(360.0 * facing / args.facings)
        for step in range(args.length):
            pose = step / max(args.length - 1, 1) if args.length > 1 else 0.0
            frame = render_frame(build(pose), angle, size, args.supersample, args.scale)
            frames.append(add_outline(reduce_dominant(frame, size, args.supersample)))

    sheet = quantise(frames, size, min(args.columns, len(frames)), palette)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    temporary = args.output.with_name(f".{args.output.name}.{os.getpid()}.tmp")
    sheet.save(temporary, "PNG", optimize=False)
    os.replace(temporary, args.output)
    args.output.with_suffix(".yaml").write_text(
        f"FrameSize: {width},{height}\n"
        f"FrameAmount: {len(frames)}\n"
        f"Offset: 0,0\n"
        f"Author: {args.author}\n"
        f"License: {args.license}\n"
    )

    data = list(sheet.getdata())
    opaque = sum(1 for i in data if i != TRANSPARENT_INDEX)
    team = sum(1 for i in data if i in TEAM_RAMP)
    print(f"Wrote {args.output} ({sheet.width}x{sheet.height}, {len(frames)} frames).")
    print(f"  team-colour pixels: {team} of {opaque} opaque ({100 * team // max(opaque, 1)}%)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
