#!/usr/bin/env python3

"""Pack generated art into an OpenHV sprite sheet and its sequence definition.

A unit sheet is one indexed PNG holding every animation back to back. Each
animation occupies `Facings` x `Length` consecutive frames and is addressed by
its `Start` offset, the way `blaster` and `shocker` are defined in
mods/hv/sequences/infantry.yaml. This tool takes one folder or strip per
animation, keys the flat generated backdrop, trims and downscales each frame to
the real frame size, routes the team-colour key into the player ramp, quantises
the rest against the shared palette, packs everything in declaration order, and
prints the matching sequence block with the offsets already worked out.

The mod renders units with `PlayerPalette: green`, so indices 48-63 are replaced
per player and must never receive ordinary art. Index 255 is transparent.
"""

from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:  # pragma: no cover - environment guard
    raise SystemExit("import-sprite.py requires Pillow: python3 -m pip install pillow")


PROJECT_DIR = Path(__file__).resolve().parent
DEFAULT_PALETTE = PROJECT_DIR / "mods/hv/bits/palettes/colors.pal"
FRAME_SUFFIXES = (".png", ".webp", ".jpg", ".jpeg")

TRANSPARENT_INDEX = 255

# PlayerColorPalette@GreenRemap in mods/hv/rules/world.yaml, and the range every
# stock unit sprite actually uses.
TEAM_RAMP = tuple(range(48, 64))
RESERVED = set(TEAM_RAMP) | {TRANSPARENT_INDEX}

RESAMPLERS = {
    "lanczos": Image.LANCZOS,
    "bicubic": Image.BICUBIC,
    "box": Image.BOX,
    "nearest": Image.NEAREST,
}


class Animation:
    OPTION_KEYS = frozenset(
        {"facings", "length", "tick", "cells", "offset", "cols", "rows", "row"}
    )

    def __init__(self, name: str, spec: str):
        self.name = name
        self.facings = 8
        self.length = 1
        self.tick: int | None = None
        self.cells: int | None = None
        self.offset: str | None = None
        self.cols: int | None = None
        self.rows = 1
        self.row: int | None = None

        # Generated files are routinely named "Image July 29, 2026 - 6_46PM.jpg",
        # so the path cannot simply be everything before the first comma. Peel
        # recognised options off the end instead and keep the rest as the path.
        parts = spec.split(",")
        options: list[str] = []
        while len(parts) > 1:
            candidate = parts[-1].strip()
            key = candidate.partition("=")[0].strip().lower()
            if key in self.OPTION_KEYS:
                options.insert(0, candidate)
                parts.pop()
                continue

            # offset=X,Y is itself comma separated, so a bare tail belongs to it.
            if (
                "=" not in candidate
                and len(parts) > 2
                and parts[-2].strip().lower().startswith("offset=")
            ):
                options.insert(0, f"{parts[-2].strip()},{candidate}")
                parts.pop()
                parts.pop()
                continue

            break

        self.path = Path(",".join(parts).strip()).expanduser()
        for option in options:
            key, _, value = option.partition("=")
            key = key.strip().lower()
            if key == "facings":
                self.facings = int(value)
            elif key == "length":
                self.length = int(value)
            elif key == "tick":
                self.tick = int(value)
            elif key == "cells":
                self.cells = int(value)
            elif key == "offset":
                self.offset = value
            elif key == "cols":
                self.cols = int(value)
            elif key == "rows":
                self.rows = int(value)
            elif key == "row":
                self.row = int(value)
            else:
                raise SystemExit(f"--animation {name}: unknown option {key!r}")

        if self.facings < 0 or self.length < 1:
            raise SystemExit(f"--animation {name}: facings/length must be positive")

        self.start = 0
        self.frames: list[Image.Image] = []

    @property
    def expected(self) -> int:
        return max(self.facings, 1) * self.length


def read_jasc_palette(path: Path) -> list[tuple[int, int, int]]:
    lines = [line.strip() for line in path.read_text().splitlines()]
    if not lines or lines[0] != "JASC-PAL":
        raise SystemExit(f"{path} is not a JASC palette")
    count = int(lines[2])
    colors = []
    for line in lines[3 : 3 + count]:
        parts = line.split()
        if len(parts) < 3:
            raise SystemExit(f"{path} has a malformed colour row: {line!r}")
        colors.append((int(parts[0]), int(parts[1]), int(parts[2])))
    if len(colors) != count:
        raise SystemExit(f"{path} declares {count} colours but lists {len(colors)}")
    return colors


def parse_size(value: str) -> tuple[int, int]:
    separator = "x" if "x" in value else ","
    try:
        width, height = (int(part) for part in value.split(separator))
    except ValueError:
        raise SystemExit(f"Bad size {value!r}; expected WIDTHxHEIGHT") from None
    if width <= 0 or height <= 0:
        raise SystemExit(f"Bad size {value!r}; both dimensions must be positive")
    return width, height


def parse_color(value: str) -> tuple[int, int, int]:
    text = value.lstrip("#")
    if len(text) != 6:
        raise SystemExit(f"Bad colour {value!r}; expected RRGGBB")
    try:
        return tuple(int(text[i : i + 2], 16) for i in (0, 2, 4))
    except ValueError:
        raise SystemExit(f"Bad colour {value!r}; expected hexadecimal") from None


def distance(a: tuple[int, int, int], b: tuple[int, int, int]) -> int:
    return (a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2


def luminance(color: tuple[int, int, int]) -> float:
    return 0.2126 * color[0] + 0.7152 * color[1] + 0.0722 * color[2]


def load_animation_frames(animation: Animation) -> list[Image.Image]:
    path = animation.path
    if path.is_dir():
        files = sorted(
            entry
            for entry in path.iterdir()
            if entry.suffix.lower() in FRAME_SUFFIXES
        )
        if not files:
            raise SystemExit(f"--animation {animation.name}: {path} holds no images")
        return [Image.open(entry).convert("RGBA") for entry in files]

    if not path.is_file():
        raise SystemExit(f"--animation {animation.name}: {path} does not exist")

    sheet = Image.open(path).convert("RGBA")

    if animation.row is not None:
        # One row of a shared multi-animation sheet. Every cell must be the same
        # size, so the grid has to be rigid rather than laid out by eye.
        cols = animation.cols or animation.cells or animation.expected
        if cols < 1 or animation.rows < 1:
            raise SystemExit(f"--animation {animation.name}: cols/rows must be positive")
        if not 0 <= animation.row < animation.rows:
            raise SystemExit(
                f"--animation {animation.name}: row {animation.row} is outside "
                f"0..{animation.rows - 1}"
            )
        if sheet.width % cols or sheet.height % animation.rows:
            raise SystemExit(
                f"--animation {animation.name}: sheet {sheet.width}x{sheet.height} "
                f"does not divide into {cols}x{animation.rows} cells"
            )
        cell_width = sheet.width // cols
        cell_height = sheet.height // animation.rows
        top = animation.row * cell_height
        return [
            sheet.crop((i * cell_width, top, (i + 1) * cell_width, top + cell_height))
            for i in range(cols)
        ]

    cells = animation.cells or animation.expected
    if cells < 1:
        raise SystemExit(f"--animation {animation.name}: cells must be positive")
    if sheet.width % cells:
        raise SystemExit(
            f"--animation {animation.name}: strip width {sheet.width} is not "
            f"divisible by {cells} cells"
        )
    cell = sheet.width // cells
    return [
        sheet.crop((i * cell, 0, (i + 1) * cell, sheet.height)) for i in range(cells)
    ]


def hue_and_saturation(color: tuple[int, int, int]) -> tuple[float, float]:
    r, g, b = (channel / 255 for channel in color)
    high = max(r, g, b)
    low = min(r, g, b)
    chroma = high - low
    if chroma == 0:
        return 0.0, 0.0
    if high == r:
        hue = ((g - b) / chroma) % 6
    elif high == g:
        hue = (b - r) / chroma + 2
    else:
        hue = (r - g) / chroma + 4
    return hue * 60, chroma / high


def key_background(
    frame: Image.Image,
    background: tuple[int, int, int],
    tolerance: int,
    hue_tolerance: float,
    minimum_saturation: float,
) -> Image.Image:
    """Make the flat generated backdrop transparent.

    Image models light the subject, so the backdrop arrives with a drop shadow
    that is the key colour darkened rather than the key colour itself. An exact
    match leaves that shadow behind as dark pixels. Matching the hue family
    instead removes it, and the saturation floor keeps a desaturated subject -
    grey-violet chitin against magenta - from being eaten with it.
    """
    pixels = frame.load()
    threshold = tolerance * tolerance
    background_hue, _ = hue_and_saturation(background)

    for y in range(frame.height):
        for x in range(frame.width):
            r, g, b, a = pixels[x, y]
            if a == 0:
                pixels[x, y] = (0, 0, 0, 0)
                continue

            color = (r, g, b)
            if distance(color, background) <= threshold:
                pixels[x, y] = (0, 0, 0, 0)
                continue

            if hue_tolerance > 0:
                hue, saturation = hue_and_saturation(color)
                separation = abs(hue - background_hue) % 360
                separation = min(separation, 360 - separation)
                if separation <= hue_tolerance and saturation >= minimum_saturation:
                    pixels[x, y] = (0, 0, 0, 0)

    return frame


def trim_inset(frame: Image.Image, inset: int) -> Image.Image:
    """Drop the cell border a generated sheet draws between frames."""
    if inset <= 0:
        return frame
    if frame.width <= inset * 2 or frame.height <= inset * 2:
        raise SystemExit(f"--inset {inset} removes the whole {frame.width}x{frame.height} cell")
    return frame.crop((inset, inset, frame.width - inset, frame.height - inset))


def team_mask(frame: Image.Image, team_key: tuple[int, int, int], tolerance: int):
    """Mark the team-colour key at source resolution.

    A keyed detail is often a blade edge a few pixels wide. Once the cell is
    reduced to 11x15 that edge is a fraction of a pixel and blends away, so
    matching the colour after downscaling finds nothing. Marking it first and
    carrying the mask through the same resize preserves it.
    """
    mask = Image.new("L", frame.size, 0)
    source = frame.load()
    target = mask.load()
    threshold = tolerance * tolerance
    for y in range(frame.height):
        for x in range(frame.width):
            r, g, b, a = source[x, y]
            if a >= 128 and distance((r, g, b), team_key) <= threshold:
                target[x, y] = 255
    return mask


def fit_to_frame(
    frame: Image.Image,
    mask: Image.Image,
    size: tuple[int, int],
    resample: int,
) -> tuple[Image.Image, Image.Image]:
    """Trim to the drawn content, scale to fit, and centre on the frame canvas."""
    width, height = size
    box = frame.getbbox()
    if box is None:
        return (
            Image.new("RGBA", size, (0, 0, 0, 0)),
            Image.new("L", size, 0),
        )

    content = frame.crop(box)
    content_mask = mask.crop(box)
    scale = min(width / content.width, height / content.height)
    scaled_size = (
        max(1, round(content.width * scale)),
        max(1, round(content.height * scale)),
    )
    scaled = content.resize(scaled_size, resample)

    # Area averaging keeps a thin marked edge present as partial coverage
    # instead of dropping it on a nearest-neighbour sample.
    scaled_mask = content_mask.resize(scaled_size, Image.BOX)

    origin = ((width - scaled.width) // 2, (height - scaled.height) // 2)
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    canvas.paste(scaled, origin)
    canvas_mask = Image.new("L", size, 0)
    canvas_mask.paste(scaled_mask, origin)
    return canvas, canvas_mask


def build_index_map(palette: list[tuple[int, int, int]]):
    """Return a function mapping one RGBA pixel plus its team mask to an index."""
    general = [i for i in range(len(palette)) if i not in RESERVED]
    if not general:
        raise SystemExit("Palette has no entries left after reserving the team ramp")

    ramp = [palette[i] for i in TEAM_RAMP]
    ramp_low = min(luminance(color) for color in ramp)
    ramp_span = max(max(luminance(color) for color in ramp) - ramp_low, 1.0)
    cache: dict[tuple[int, int, int], int] = {}

    def convert(pixel: tuple[int, int, int, int], team: bool) -> int:
        r, g, b, a = pixel
        if a < 128:
            return TRANSPARENT_INDEX

        color = (r, g, b)
        if team:
            # Spread the keyed region across the ramp by brightness so shading
            # survives the recolour instead of flattening to a single shade.
            position = (luminance(color) - ramp_low) / ramp_span
            step = round(min(max(position, 0.0), 1.0) * (len(TEAM_RAMP) - 1))
            return TEAM_RAMP[step]

        cached = cache.get(color)
        if cached is not None:
            return cached

        index = min(general, key=lambda i: distance(color, palette[i]))
        cache[color] = index
        return index

    return convert


def compose_sheet(
    frames: list[tuple[Image.Image, Image.Image]],
    size: tuple[int, int],
    columns: int,
    palette: list[tuple[int, int, int]],
    convert,
    mask_threshold: int,
) -> Image.Image:
    rows = (len(frames) + columns - 1) // columns
    width, height = size
    sheet = Image.new("P", (width * columns, height * rows))

    flat: list[int] = []
    for color in palette:
        flat.extend(color)
    flat.extend([0] * (768 - len(flat)))
    sheet.putpalette(flat)

    # Cells past the last frame stay transparent rather than black.
    target = sheet.load()
    for y in range(sheet.height):
        for x in range(sheet.width):
            target[x, y] = TRANSPARENT_INDEX

    for position, (frame, mask) in enumerate(frames):
        origin_x = (position % columns) * width
        origin_y = (position // columns) * height
        source = frame.load()
        marked = mask.load()
        for y in range(height):
            for x in range(width):
                target[origin_x + x, origin_y + y] = convert(
                    source[x, y], marked[x, y] >= mask_threshold
                )

    return sheet


def sequence_block(
    actor: str, filename: str, animations: list[Animation], icon: str | None
) -> str:
    lines = [f"{actor}:"]
    for animation in animations:
        lines.append(f"\t{animation.name}:")
        lines.append(f"\t\tFilename: {filename}")
        if animation.start:
            lines.append(f"\t\tStart: {animation.start}")
        if animation.facings:
            lines.append(f"\t\tFacings: {animation.facings}")
        if animation.length > 1:
            lines.append(f"\t\tLength: {animation.length}")
        if animation.tick is not None:
            lines.append(f"\t\tTick: {animation.tick}")
        if animation.offset:
            lines.append(f"\t\tOffset: {animation.offset}")
    if icon:
        lines.append("\ticon:")
        lines.append(f"\t\tFilename: {icon}")
    return "\n".join(lines)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument(
        "output", type=Path, help="Destination .png under mods/hv/bits/sprites"
    )
    parser.add_argument(
        "--animation",
        action="append",
        required=True,
        metavar="NAME=PATH[,facings=8][,length=1][,tick=N][,cells=N]"
        "[,cols=N,rows=M,row=K][,offset=X,Y]",
        help="Animation block, repeat in the order they should be packed. PATH is "
        "a folder of frames, one horizontal strip, or - with cols/rows/row - one "
        "row of a shared rigid-grid sheet.",
    )
    parser.add_argument(
        "--frame-size",
        default="21x18",
        help="Frame size: 21x18 matches vehicles, 11x15 matches infantry",
    )
    parser.add_argument("--columns", type=int, default=8, help="Frames per sheet row")
    parser.add_argument("--actor", help="Actor name for the printed sequence block")
    parser.add_argument("--icon", help="Icon filename for the printed sequence block")
    parser.add_argument("--palette", type=Path, default=DEFAULT_PALETTE)
    parser.add_argument("--background", default="FF00FF", help="Backdrop key colour")
    parser.add_argument("--background-tolerance", type=int, default=60)
    parser.add_argument(
        "--background-hue-tolerance",
        type=float,
        default=30.0,
        help="Also key pixels within this many degrees of the backdrop hue, which "
        "removes its drop shadow. Set 0 to key the exact colour only.",
    )
    parser.add_argument(
        "--background-min-saturation",
        type=float,
        default=0.35,
        help="Hue keying ignores pixels below this saturation, so a desaturated "
        "subject survives a saturated backdrop",
    )
    parser.add_argument(
        "--inset",
        type=int,
        default=0,
        help="Pixels to trim from every cell edge, for sheets drawn with borders",
    )
    parser.add_argument("--team-key", default="00FFFF", help="Team-colour key colour")
    parser.add_argument("--team-tolerance", type=int, default=90)
    parser.add_argument(
        "--team-mask-threshold",
        type=int,
        default=64,
        help="Downscaled team-mask coverage, 0-255, above which a pixel joins the "
        "player ramp. Lower keeps thin keyed edges; higher avoids bleed.",
    )
    parser.add_argument("--author", required=True)
    parser.add_argument("--license", default="CC-BY-SA-4.0")
    parser.add_argument(
        "--resample",
        choices=tuple(RESAMPLERS),
        default="lanczos",
        help="Downscale filter; box suits already-blocky art",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    size = parse_size(args.frame_size)
    palette = read_jasc_palette(args.palette)
    resample = RESAMPLERS[args.resample]
    background = parse_color(args.background)

    team_key = parse_color(args.team_key)

    animations = []
    for entry in args.animation:
        if "=" not in entry:
            raise SystemExit(f"--animation expects NAME=PATH, got {entry!r}")
        name, _, spec = entry.partition("=")
        animations.append(Animation(name.strip(), spec.strip()))

    packed: list[Image.Image] = []
    for animation in animations:
        frames = load_animation_frames(animation)
        if len(frames) != animation.expected:
            print(
                f"Note: {animation.name} supplied {len(frames)} frames but "
                f"facings x length is {animation.expected}; using the supplied count.",
                file=sys.stderr,
            )
        animation.start = len(packed)
        animation.frames = []
        for frame in frames:
            keyed = key_background(
                trim_inset(frame, args.inset),
                background,
                args.background_tolerance,
                args.background_hue_tolerance,
                args.background_min_saturation,
            )
            marked = team_mask(keyed, team_key, args.team_tolerance)
            animation.frames.append(fit_to_frame(keyed, marked, size, resample))
        packed.extend(animation.frames)

    if not packed:
        raise SystemExit("No frames to import")

    convert = build_index_map(palette)
    sheet = compose_sheet(
        packed, size, args.columns, palette, convert, args.team_mask_threshold
    )

    args.output.parent.mkdir(parents=True, exist_ok=True)
    temporary = args.output.with_name(f".{args.output.name}.{os.getpid()}.tmp")
    sheet.save(temporary, "PNG", optimize=False)
    os.replace(temporary, args.output)

    sidecar = args.output.with_suffix(".yaml")
    sidecar.write_text(
        f"FrameSize: {size[0]},{size[1]}\n"
        f"FrameAmount: {len(packed)}\n"
        f"Offset: 0,0\n"
        f"Author: {args.author}\n"
        f"License: {args.license}\n"
    )

    data = list(sheet.getdata())
    opaque = sum(1 for index in data if index != TRANSPARENT_INDEX)
    team_pixels = sum(1 for index in data if index in TEAM_RAMP)

    print(
        f"Wrote {args.output} ({sheet.width}x{sheet.height}, {len(packed)} frames of "
        f"{size[0]}x{size[1]}) and {sidecar.name}."
    )
    for animation in animations:
        print(
            f"  {animation.name:<12} start {animation.start:>3}  "
            f"{len(animation.frames)} frames"
        )

    if opaque == 0:
        print(
            "Warning: every pixel is transparent. Check --background and "
            "--background-tolerance against the generated backdrop.",
            file=sys.stderr,
        )
    elif team_pixels == 0:
        print(
            "Warning: no team-colour pixels, so every player will look identical. "
            "Check --team-key and --team-tolerance.",
            file=sys.stderr,
        )
    else:
        print(
            f"  team-colour pixels: {team_pixels} of {opaque} opaque "
            f"({100 * team_pixels // opaque}%)"
        )

    if args.actor:
        print("\nAdd to mods/hv/sequences/:\n")
        print(sequence_block(args.actor, args.output.name, animations, args.icon))

    print(f"\nNext: ./utility.sh --png-sheet-import {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
