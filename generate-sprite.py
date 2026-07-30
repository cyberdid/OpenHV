#!/usr/bin/env python3

"""Render a posed creature to an OpenHV sprite sheet.

Image models cannot hold one creature steady across a walk cycle: ask for the
same animal in a new pose and a different animal comes back. A rig can be
posed. Parts are placed once in creature space, the pose functions move the
joints, and the whole body is rotated about its vertical axis for each facing,
so every frame of every sequence is the same animal by construction.

Legs are solved with two-bone inverse kinematics from a foot target rather than
swung from the hip, which is the difference between an insect walking and a
windmill turning: during the stance phase the foot is pinned to the ground and
the body travels over it.

Frames are drawn several times oversized and reduced by dominant colour rather
than by averaging, which keeps edges hard at thirty-two pixels instead of
dissolving them into a smear. The silhouette is outlined after the reduction,
so the outline is always exactly one pixel.
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

# Light sits above and behind the viewer's left shoulder, in creature space
# before rotation is applied - so the lit side turns with the creature.
LIGHT = (-0.45, -0.35, 0.82)

OUTLINE = (0, 0, 0)
BACKDROP = (255, 0, 255)

# Sampled from the reference art so the rig reads as the same creature.
CHITIN = (143, 124, 165)
CHITIN_DARK = (74, 52, 92)
FLESH = (196, 122, 132)
BONE = (226, 210, 182)


def is_team(colour):
    """Team parts are drawn as shades of pure cyan and remapped at quantise."""
    return colour[0] == 0 and colour[1] == colour[2] and colour[1] > 0


def shift(colour, amount):
    """Lighten for amount above zero, darken below."""
    if amount >= 0:
        return tuple(min(255, int(c + (255 - c) * amount)) for c in colour)
    return tuple(max(0, int(c * (1.0 + amount))) for c in colour)


def add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def scale3(a, k):
    return (a[0] * k, a[1] * k, a[2] * k)


def length3(a):
    return math.sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2])


def normalise(a):
    n = length3(a)
    return (0.0, 0.0, 1.0) if n < 1e-9 else (a[0] / n, a[1] / n, a[2] / n)


def solve_knee(hip, foot, upper, lower, bend):
    """Two-bone inverse kinematics; `bend` is the direction the joint breaks."""
    span = (foot[0] - hip[0], foot[1] - hip[1], foot[2] - hip[2])
    distance = min(length3(span), upper + lower - 1e-4)
    if distance < 1e-6:
        return add(hip, scale3(normalise(bend), upper))
    direction = scale3(span, 1.0 / distance)
    along = (upper * upper - lower * lower + distance * distance) / (2.0 * distance)
    out = math.sqrt(max(upper * upper - along * along, 0.0))
    # Project the requested bend direction onto the plane across the bone.
    dot = bend[0] * direction[0] + bend[1] * direction[1] + bend[2] * direction[2]
    perpendicular = normalise(
        (
            bend[0] - direction[0] * dot,
            bend[1] - direction[1] * dot,
            bend[2] - direction[2] * dot,
        )
    )
    return add(add(hip, scale3(direction, along)), scale3(perpendicular, out))


class Part:
    """One solid of the creature, in creature space.

    x runs right, y runs forward (the direction the creature faces), z runs up.
    """

    def __init__(self, a, b, radius, colour, taper=1.0):
        self.a = a
        self.b = b
        self.radius = radius
        self.colour = colour
        self.taper = taper


class Pose:
    """Everything an animation is allowed to move."""

    def __init__(self):
        self.lift = 0.0        # body rise and fall
        self.surge = 0.0       # body travel along its own forward axis
        self.pitch = 0.0       # nose down, radians
        self.roll = 0.0        # radians
        self.feet = None       # four ground targets, filled from REST_FEET
        self.sweep = 0.0       # scythes forward through the strike
        self.droop = 0.0       # scythes fall as the creature dies
        self.gape = 0.0        # mandibles open
        self.glow = 1.0        # dorsal strip brightness


class Species:
    """One creature of the brood, as numbers rather than a drawing.

    Every unit the swarm fields is the same animal under different pressures:
    longer legs for a runner, a heavier carapace for a bruiser, a swollen sac
    for a spitter. Holding those as parameters means a new unit costs a line,
    and every one of them animates the moment it exists.
    """

    def __init__(
        self,
        girth=1.0,          # carapace radius
        stretch=1.0,        # body length front to back
        stance=1.0,         # how far the feet plant from the body
        shank=1.0,          # leg length
        limb=1.0,           # leg thickness
        blade=1.0,          # scythe length
        thorns=1.0,         # dorsal spine height
        tail=1.0,           # tail length
        skull=1.0,          # head size
        ride=1.0,           # body height above the ground
        chitin=CHITIN,
        flesh=FLESH,
        bone=BONE,
    ):
        self.girth = girth
        self.stretch = stretch
        self.stance = stance
        self.shank = shank
        self.limb = limb
        self.blade = blade
        self.thorns = thorns
        self.tail = tail
        self.skull = skull
        self.ride = ride
        self.chitin = chitin
        self.dark = shift(chitin, -0.52)
        self.flesh = flesh
        self.bone = bone

        self.hip_y = (0.20 * stretch, -0.16 * stretch)
        self.hip_x = 0.16 * girth
        self.hip_z = 0.50 * ride
        self.femur = 0.38 * shank
        self.tibia = 0.44 * shank
        self.feet = [
            (side * 0.50 * stance, y * stance, 0.0)
            for y in (0.42 * stretch, -0.38 * stretch)
            for side in (-1, 1)
        ]


SPECIES = {
    # The line soldier: heavy carapace, two scything blades, four legs.
    "warrior": Species(),
    # Sniper role. Long legs, light shell, blades traded for a bone lance.
    "runner": Species(
        girth=0.78,
        stretch=1.12,
        stance=1.28,
        shank=1.30,
        limb=0.82,
        blade=1.45,
        thorns=0.55,
        tail=0.90,
        skull=0.88,
        ride=1.22,
        chitin=(120, 132, 150),
        bone=(232, 220, 198),
    ),
    # Shocker role. A swollen acid sac on short legs, blades all but gone.
    "spitter": Species(
        girth=1.30,
        stretch=0.86,
        stance=0.92,
        shank=0.74,
        limb=1.18,
        blade=0.45,
        thorns=1.30,
        tail=0.35,
        skull=1.15,
        ride=0.82,
        chitin=(126, 108, 152),
        flesh=(178, 152, 96),
        bone=(214, 206, 168),
    ),
}


def body_frame(pose):
    """Rotate a creature-space point by the body's own pitch and roll."""
    cp, sp = math.cos(pose.pitch), math.sin(pose.pitch)
    cr, sr = math.cos(pose.roll), math.sin(pose.roll)
    rise = pose.lift

    def place(point):
        x, y, z = point
        y, z = y * cp + z * sp, z * cp - y * sp
        x, z = x * cr - z * sr, z * cr + x * sr
        return (x, y + pose.surge, z + rise)

    return place


def assemble(pose, kind):
    """Build the creature at this pose, as a flat list of solids."""
    place = body_frame(pose)
    parts = []
    g, s = kind.girth, kind.stretch

    ride = kind.ride

    # Tail, drawn first so the body overlaps its root.
    if kind.tail > 0.01:
        tail = [
            (0.0, (-0.60 - 0.09 * step) * s * kind.tail, (0.36 + 0.05 * step) * ride)
            for step in range(4)
        ]
        for index, (first, second) in enumerate(zip(tail, tail[1:])):
            radius = (0.085 - 0.022 * index) * g
            parts.append(Part(place(first), place(second), radius, kind.chitin, taper=0.7))
            parts.append(Part(place(second), place(second), radius * 0.85, kind.dark))
        parts.append(Part(place(tail[-1]), place((0.0, tail[-1][1] - 0.10 * s * kind.tail, tail[-1][2] + 0.06 * ride)), 0.026 * g, kind.bone, taper=0.15))

    # Abdomen into thorax into neck: overlapping plates of falling radius, with
    # a darker capsule between each pair so the segments read as separate.
    profile = [
        (-0.62, 0.34, 0.08),
        (-0.50, 0.42, 0.17),
        (-0.36, 0.48, 0.24),
        (-0.20, 0.53, 0.28),
        (-0.04, 0.56, 0.29),
        (0.11, 0.55, 0.26),
        (0.24, 0.51, 0.20),
    ]
    spine = [((0.0, y * s, z * ride), r * g) for y, z, r in profile]
    for (first, r0), (second, r1) in zip(spine, spine[1:]):
        parts.append(Part(place(first), place(second), r0, kind.chitin, taper=r1 / r0))
        seam = (0.0, (first[1] + second[1]) / 2, (first[2] + second[2]) / 2)
        parts.append(Part(place(seam), place(seam), (r0 + r1) / 2 * 0.93, kind.dark))

    # Underbelly, visible from the front and the sides.
    parts.append(
        Part(place((0.0, -0.34 * s, 0.36 * ride)), place((0.0, 0.12 * s, 0.38 * ride)), 0.13 * g, kind.flesh)
    )

    # Dorsal strip, the team colour. Shaded along its length so the ramp is used.
    ridge = [(0.0, y * s, (0.72 - abs(y + 0.06) * 0.18) * ride) for y in (-0.46, -0.24, 0.00, 0.22)]
    for index, (first, second) in enumerate(zip(ridge, ridge[1:])):
        level = 120 + 45 * index
        tint = (0, max(int(level * pose.glow), 1), max(int(level * pose.glow), 1))
        parts.append(Part(place(first), place(second), 0.050 * g, tint))

    # Thorns down the back, and a pair off each shoulder. These carry the
    # silhouette: at this size an outline reads before any interior detail.
    if kind.thorns > 0.01:
        for y, height, lean in ((-0.38, 0.13, -0.07), (-0.19, 0.17, -0.06), (0.02, 0.15, -0.05)):
            for side in (-1, 1):
                root = (side * 0.10 * g, y * s, 0.62 * ride)
                tip = (
                    side * (0.10 + 0.05 * kind.thorns) * g,
                    (y + lean) * s,
                    (0.62 + height * kind.thorns) * ride,
                )
                parts.append(Part(place(root), place(tip), 0.034 * g, kind.dark, taper=0.15))

    # Head: skull, brow, two sunken eyes, and a pair of working mandibles.
    skull = kind.skull
    parts.append(
        Part(place((0.0, 0.24 * s, 0.52 * ride)), place((0.0, 0.48 * s, 0.45 * ride)), 0.145 * g * skull, kind.chitin, taper=0.82)
    )
    parts.append(
        Part(place((0.0, 0.38 * s, 0.58 * ride)), place((0.0, 0.52 * s, 0.53 * ride)), 0.055 * g * skull, kind.dark)
    )
    for side in (-1, 1):
        parts.append(Part(place((side * 0.09 * g, 0.45 * s, 0.50 * ride)), place((side * 0.09 * g, 0.45 * s, 0.50 * ride)), 0.036 * g * skull, (18, 12, 24)))
        root = (side * 0.07 * g, 0.52 * s, 0.42 * ride)
        tip = (
            side * (0.06 + 0.12 * pose.gape) * g,
            (0.76 + 0.04 * pose.gape) * s,
            (0.38 - 0.04 * pose.gape) * ride,
        )
        parts.append(Part(place(root), place(tip), 0.042 * g, kind.bone, taper=0.28))

    # Walking legs, three bones each. The foot is a world target and the knee is
    # solved to reach it, so a planted foot stays planted.
    feet = pose.feet or kind.feet
    for index, foot in enumerate(feet):
        side = -1 if index % 2 == 0 else 1
        socket = place((side * kind.hip_x, kind.hip_y[index // 2], kind.hip_z))
        hip = place((side * kind.hip_x * 1.9, kind.hip_y[index // 2], kind.hip_z * 1.04))
        knee = solve_knee(hip, foot, kind.femur, kind.tibia, (side * 0.75, 0.0, 0.95))
        parts.append(Part(socket, hip, 0.070 * kind.limb, kind.chitin, taper=0.92))
        parts.append(Part(hip, knee, 0.058 * kind.limb, kind.chitin, taper=0.72))
        parts.append(Part(knee, knee, 0.050 * kind.limb, kind.flesh))
        # A spur on the back of the knee, and a claw where the foot meets ground.
        parts.append(Part(knee, (knee[0] + side * 0.10, knee[1] - 0.06, knee[2] + 0.04), 0.030 * kind.limb, kind.dark, taper=0.2))
        # Shin in chitin, only the claw in bone: a leg that is pale end to end
        # reads as one bright wedge once it is twelve pixels long.
        ankle = (knee[0] + (foot[0] - knee[0]) * 0.62, knee[1] + (foot[1] - knee[1]) * 0.62, knee[2] + (foot[2] - knee[2]) * 0.62)
        parts.append(Part(knee, ankle, 0.050 * kind.limb, kind.chitin, taper=0.62))
        parts.append(Part(ankle, foot, 0.032 * kind.limb, kind.bone, taper=0.42))

    # Scythe arms: shoulder, forearm, blade. The blade is a two-piece curve so
    # it hooks rather than pointing straight out, with a dark edge along it.
    for side in (-1, 1):
        shoulder = place((side * 0.17 * g, 0.16 * s, 0.60 * ride))
        elbow, middle, tip = scythe(pose.sweep, pose.droop, kind.blade)
        elbow, middle, tip = place(mirror(elbow, side)), place(mirror(middle, side)), place(mirror(tip, side))
        parts.append(Part(shoulder, elbow, 0.098 * g, kind.flesh, taper=0.72))
        parts.append(Part(elbow, elbow, 0.072 * g, kind.dark))
        parts.append(Part(elbow, middle, 0.070 * g, kind.bone, taper=0.66))
        parts.append(Part(middle, tip, 0.046 * g, kind.bone, taper=0.12))
        # The cutting edge: a thinner dark line riding the inside of the blade.
        inner = (middle[0] - side * 0.03, middle[1] + 0.02, middle[2] - 0.03)
        parts.append(Part(inner, (tip[0] - side * 0.01, tip[1], tip[2] - 0.02), 0.020 * g, kind.dark, taper=0.3))

    return parts


def mirror(point, side):
    return (point[0] * side, point[1], point[2])


# Elbow, blade middle and blade tip on the right-hand side, at three moments of
# the swing. The blade is two segments so it hooks instead of pointing straight.
SCYTHE_BACK = ((0.44, 0.02, 0.56), (0.56, 0.24, 0.54), (0.54, 0.52, 0.50))
SCYTHE_REST = ((0.40, 0.24, 0.48), (0.50, 0.52, 0.38), (0.34, 0.80, 0.28))
SCYTHE_STRIKE = ((0.30, 0.42, 0.44), (0.24, 0.78, 0.30), (0.02, 1.02, 0.20))


def scythe(sweep, droop, reach=1.0):
    """Blade joints for a swing running -1 (drawn back) to 1 (fully struck)."""
    if sweep >= 0.0:
        first, second, blend = SCYTHE_REST, SCYTHE_STRIKE, min(sweep, 1.0)
    else:
        first, second, blend = SCYTHE_REST, SCYTHE_BACK, min(-sweep, 1.0)
    joints = []
    for index, (a, b) in enumerate(zip(first, second)):
        x, y, z = (a[i] + (b[i] - a[i]) * blend for i in range(3))
        # Longer blades grow from the elbow out, not from the shoulder.
        if index:
            y *= reach
        # Dying, the blades fall outward and drag on the ground.
        joints.append((x * (1.0 + 0.30 * droop), y * (1.0 - 0.45 * droop), z * (1.0 - 0.80 * droop)))
    return joints


def smoothstep(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3.0 - 2.0 * t)


def pose_stand(phase, kind):
    """Breathing, mandibles working. Nothing travels."""
    breath = math.sin(2 * math.pi * phase)
    pose = Pose()
    pose.lift = 0.016 * breath
    pose.gape = 0.5 + 0.5 * math.sin(2 * math.pi * phase + 1.0)
    pose.sweep = 0.04 * breath
    pose.feet = kind.feet
    return pose


STRIDE = 0.30
LIFT = 0.15
DUTY = 0.62
# Diagonals move together: front-left with rear-right, front-right with rear-left.
GAIT_OFFSET = (0.0, 0.5, 0.5, 0.0)


def pose_move(phase, kind):
    pose = Pose()
    feet = []
    for index, rest in enumerate(kind.feet):
        step = (phase + GAIT_OFFSET[index]) % 1.0
        if step < DUTY:
            # Stance: the foot is planted and the body travels over it.
            travel = step / DUTY
            feet.append((rest[0], rest[1] + STRIDE * (0.5 - travel), 0.0))
        else:
            # Swing: lift, carry forward, set down.
            travel = (step - DUTY) / (1.0 - DUTY)
            feet.append(
                (
                    rest[0],
                    rest[1] + STRIDE * (travel - 0.5),
                    LIFT * math.sin(math.pi * travel),
                )
            )
    pose.feet = feet
    # The body rises twice per cycle, once over each diagonal.
    pose.lift = 0.030 * math.sin(4 * math.pi * phase)
    pose.roll = 0.10 * math.sin(2 * math.pi * phase)
    pose.pitch = -0.05
    pose.sweep = 0.10 + 0.06 * math.sin(2 * math.pi * phase + 0.8)
    pose.gape = 0.3
    return pose


def pose_attack(phase, kind):
    """Wind up, strike, recover. The strike is the fast part."""
    if phase < 0.34:
        reach = -0.30 * smoothstep(phase / 0.34)
    elif phase < 0.50:
        reach = -0.30 + 1.30 * smoothstep((phase - 0.34) / 0.16)
    else:
        reach = 1.00 * (1.0 - smoothstep((phase - 0.50) / 0.50))
    pose = Pose()
    pose.sweep = reach
    pose.surge = 0.09 * max(reach, 0.0)
    pose.pitch = -0.18 * max(reach, 0.0) + 0.06 * max(-reach, 0.0)
    pose.lift = -0.02 * max(reach, 0.0)
    pose.gape = min(1.0, max(0.0, reach * 1.4))
    # The legs brace: the front pair steps out as the body lunges.
    pose.feet = [
        (
            rest[0] * (1.0 + 0.16 * max(reach, 0.0)),
            rest[1] + (0.06 if index < 2 else -0.04) * max(reach, 0.0),
            0.0,
        )
        for index, rest in enumerate(kind.feet)
    ]
    return pose


def pose_die(phase, kind):
    """Legs give way, body settles, the dorsal strip goes out."""
    fall = smoothstep(phase)
    pose = Pose()
    pose.lift = -0.34 * fall
    pose.pitch = 0.55 * fall
    pose.roll = 0.30 * fall * fall
    pose.droop = fall
    pose.sweep = -0.25 * fall
    pose.gape = 1.0 - fall
    pose.glow = max(0.0, 1.0 - fall * 1.6)
    pose.feet = [
        (
            rest[0] * (1.0 + 0.55 * fall),
            rest[1] * (1.0 - 0.30 * fall),
            0.0,
        )
        for rest in kind.feet
    ]
    return pose


POSES = {
    "stand": pose_stand,
    "move": pose_move,
    "attack": pose_attack,
    "die": pose_die,
}


def rotate_z(point, angle):
    x, y, z = point
    c, s = math.cos(angle), math.sin(angle)
    return (x * c - y * s, x * s + y * c, z)


def project(point, scale, tilt):
    """Top-down camera: distance compresses vertically, height lifts on screen.

    The camera sits south of the scene and looks north and down, tilted back
    from vertical by `tilt`, so its up axis is (0, cos, sin): travelling north
    and standing taller both move a point up the screen.
    """
    x, y, z = point
    return (x * scale, (y * math.cos(tilt) + z * math.sin(tilt)) * scale)


def depth(point, tilt):
    """How near the camera a point is; larger is nearer."""
    x, y, z = point
    return z * math.cos(tilt) - y * math.sin(tilt)


def draw_capsule(draw, start, end, r0, r1, colour, light, tone):
    """A stack of circles, each with a highlight offset toward the light."""
    steps = max(3, int(math.dist(start, end) / 1.2) + 3)
    base = colour if is_team(colour) else shift(colour, tone)
    peak = colour if is_team(colour) else shift(colour, tone + 0.30)
    for i in range(steps + 1):
        t = i / steps
        x = start[0] + (end[0] - start[0]) * t
        y = start[1] + (end[1] - start[1]) * t
        r = r0 + (r1 - r0) * t
        draw.ellipse([x - r, y - r, x + r, y + r], fill=base)
        if r > 1.6:
            hr = r * 0.58
            hx = x + light[0] * r * 0.34
            hy = y + light[1] * r * 0.34
            draw.ellipse([hx - hr, hy - hr, hx + hr, hy + hr], fill=peak)


def render_frame(parts, facing, size, supersample, scale):
    """Draw one facing at supersampled resolution on the backdrop colour."""
    w, h = size[0] * supersample, size[1] * supersample
    canvas = Image.new("RGB", (w, h), BACKDROP)
    draw = ImageDraw.Draw(canvas)
    tilt = math.radians(CAMERA_TILT_DEGREES)
    # scale is the creature's height as a fraction of the frame.
    unit = scale * min(size) * supersample
    cx, cy = w / 2, h * 0.62

    lit = rotate_z(LIGHT, facing)
    screen_light = project(lit, 1.0, tilt)
    norm = math.hypot(*screen_light) or 1.0
    screen_light = (screen_light[0] / norm, -screen_light[1] / norm)

    placed = []
    for part in parts:
        a = rotate_z(part.a, facing)
        b = rotate_z(part.b, facing)
        placed.append((max(depth(a, tilt), depth(b, tilt)), part, a, b))

    near = max(item[0] for item in placed)
    far = min(item[0] for item in placed)
    span = max(near - far, 1e-6)

    # Painter's algorithm: far parts first, so near limbs occlude the body.
    for nearness, part, a, b in sorted(placed, key=lambda item: item[0]):
        pa = project(a, unit, tilt)
        pb = project(b, unit, tilt)
        # Parts further from the camera sit in shadow, which separates the
        # legs on the far side from the legs on the near side.
        tone = -0.34 + 0.34 * ((nearness - far) / span)
        draw_capsule(
            draw,
            (cx + pa[0], cy - pa[1]),
            (cx + pb[0], cy - pb[1]),
            part.radius * unit,
            part.radius * unit * part.taper,
            part.colour,
            screen_light,
            tone,
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
                if colour not in cache:
                    if is_team(colour):
                        step = round(colour[1] / 255 * (len(TEAM_RAMP) - 1))
                        cache[colour] = TEAM_RAMP[min(max(step, 0), len(TEAM_RAMP) - 1)]
                    else:
                        cache[colour] = min(general, key=lambda i: dist(colour, palette[i]))
                target[ox + x, oy + y] = cache[colour]
    return sheet


class Sequence:
    def __init__(self, spec):
        parts = spec.split(":")
        if len(parts) not in (2, 3) or parts[0] not in POSES:
            raise SystemExit(
                f"Bad --sequence {spec!r}; expected NAME:LENGTH[:FACINGS] "
                f"with NAME one of {', '.join(sorted(POSES))}"
            )
        self.name = parts[0]
        self.length = int(parts[1])
        self.facings = int(parts[2]) if len(parts) == 3 else 8
        if self.length < 1 or self.facings < 1:
            raise SystemExit(f"Bad --sequence {spec!r}; length and facings must be positive")
        self.start = 0

    @property
    def count(self):
        return self.length * self.facings


def parse_args():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    parser.add_argument(
        "--sequence",
        action="append",
        required=True,
        metavar="NAME:LENGTH[:FACINGS]",
        help="Animation block, repeated in the order they should be packed",
    )
    parser.add_argument("--creature", choices=tuple(SPECIES), default="warrior")
    parser.add_argument("--frame-size", default="32x32")
    parser.add_argument("--columns", type=int, default=8)
    parser.add_argument("--scale", type=float, default=0.48, help="Creature height as a fraction of the frame")
    parser.add_argument("--supersample", type=int, default=6)
    parser.add_argument("--palette", type=Path, default=DEFAULT_PALETTE)
    parser.add_argument("--actor", help="Actor name for the printed sequence block")
    parser.add_argument("--author", required=True)
    parser.add_argument("--license", default="CC-BY-SA-4.0")
    return parser.parse_args()


def main():
    args = parse_args()
    width, height = (int(v) for v in args.frame_size.replace(",", "x").split("x"))
    size = (width, height)
    palette = read_palette(args.palette)
    kind = SPECIES[args.creature]
    sequences = [Sequence(spec) for spec in args.sequence]

    frames = []
    for sequence in sequences:
        sequence.start = len(frames)
        build = POSES[sequence.name]
        for facing in range(sequence.facings):
            # Facing 0 looks north, away from the viewer, and the index turns
            # clockwise from there - the convention every stock sheet uses.
            angle = math.radians(180.0 - 360.0 * facing / sequence.facings)
            for step in range(sequence.length):
                # Cycles wrap, so the last frame must not repeat the first.
                phase = step / sequence.length
                if sequence.name in ("attack", "die"):
                    phase = step / max(sequence.length - 1, 1)
                parts = assemble(build(phase, kind), kind)
                frame = render_frame(parts, angle, size, args.supersample, args.scale)
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
    for sequence in sequences:
        print(f"  {sequence.name:<8} start {sequence.start:>4}  {sequence.count} frames")
    print(f"  team-colour pixels: {team} of {opaque} opaque ({100 * team // max(opaque, 1)}%)")

    if args.actor:
        print(f"\n{args.actor}:")
        for sequence in sequences:
            print(f"\t{sequence.name}:")
            print(f"\t\tFilename: {args.output.name}")
            if sequence.start:
                print(f"\t\tStart: {sequence.start}")
            if sequence.facings > 1:
                print(f"\t\tFacings: {sequence.facings}")
            if sequence.length > 1:
                print(f"\t\tLength: {sequence.length}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
