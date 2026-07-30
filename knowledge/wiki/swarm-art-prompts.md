# Hive Swarm art: what to generate where

The swarm is a re-skin, so it needs one image per actor the Yuruki line can
build. Two production paths, split by what image models can and cannot hold.

## The split, and why

| | buildings | units |
|---|---|---|
| path | Nano Banana Pro | `generate-sprite.py` |
| why | one viewing angle, static shape, frames differ only by a breathing pulse | eight rotations of the same animal, and a diffusion model returns a different animal every time |
| evidence | five buildings shipped and read well | three attempts, all inconsistent; the working warrior came from the rig |

The limit on the image model is not quota. It is that a unit needs the *same*
creature from eight angles and through a walk cycle, and nothing in the prompt
can pin that down. A rig can be posed, so units come from the rig.

## Buildings still missing

The swarm inherits `structures.yi`, so only the Yuruki line matters. `BARRIER`
is excluded: bots never build it.

| actor | footprint | frame | frames | what it is |
|---|---|---|---|---|
| `RADAR` | 2×2 | 35×46 | 2 | sensor mast |
| `TRADER` | 3×3 | 40×40 | 5 | market, exchanges resources |
| `STARPORT` | 2×3 | 40×60 | 23 | air unit production |
| `TECHCENTER` | 2×3 | 36×52 | 12 | unlocks the late tree |
| `OREPURIFIER` | — | 24×44 | 8 | raises resource yield |
| `BUNKER` | 1×1 | 22×23 | 2 | garrison for infantry |
| `TURRET` | — | 47×40 | 18 | anti-ground gun |
| `AATURRET` | — | 40×40 | 12 | anti-air gun |
| `FIELD` | 2×2 | 40×42 | 14 | shield projector |
| `UPLINK` | 2×3 | 40×60 | 8 | support powers |
| `TELEVATOR` | 2×1 | 40×40 | 8 | teleport node |
| `HARBOR` | 3×3 | 53×67 | 10 | naval production |

## The shared style block

Paste this above every building prompt. It is the part that keeps twelve
separate generations looking like one faction.

```
A single organic structure grown by an insectile hive, drawn as pixel art for a
top-down real-time strategy game. Camera looks down at the structure from 35
degrees off vertical, the same angle for every image, no perspective drift.

Material: violet-grey chitin plates (#8F7CA5) with deep purple seams (#4A345C)
between them, exposed pink muscle (#C47A84) in the gaps where plates meet, and
pale bone (#E2D2B6) on any spur, claw or spike. The plates overlap like an
insect's abdomen, each one catching light on its upper edge.

Every structure carries a glowing cyan strip (#00FFFF) somewhere on its upper
surface, wide enough to read clearly at forty pixels. This is the only cyan in
the image and it must not appear anywhere else.

Light falls from the upper left. Hard-edged shading in flat bands, not soft
gradients. One solid black outline around the whole silhouette.

Background: flat solid magenta #FF00FF filling the entire canvas edge to edge.
No ground, no shadow, no texture, no gradient, no vignette, no grid lines, no
cell borders, no labels, no text, no watermark. Only the structure and the
magenta.

The structure is grown, not built: no straight machined edges, no bolts, no
panels, no antennas made of metal. Everything is bone, shell and muscle.
```

## Per-building prompts

Append one of these to the style block. The footprint line matters most: it is
what stops a 1×1 bunker coming back the size of a base.

### RADAR — sensor mast
```
Subject: a tall sensor organ. A thick chitin stalk rising from a squat armoured
base, opening at the top into a fleshy dish of stretched membrane ringed with
short bone spines. The dish tilts slightly toward the viewer. The cyan strip
runs up the front of the stalk and spreads across the underside of the dish.
Roughly twice as tall as it is wide.
```

### TRADER — market
```
Subject: a wide squat organ with three fleshy mouths opening on its upper
surface, each ringed with bone teeth, arranged around a central chitin dome.
Thick ribbed tubes connect the mouths to the dome. The cyan strip circles the
base of the dome. As wide as it is deep, low to the ground.
```

### STARPORT — air unit production
```
Subject: a broad launch organ. A wide chitin cradle with an open muscled maw at
the front angled upward, ribbed like a throat, large enough for a flying
creature to be expelled from it. Two heavy bone buttresses brace the sides. The
cyan strip runs along the inner rim of the maw. Half again as deep as it is
wide.
```

### TECHCENTER — tech unlock
```
Subject: a bulbous brain-organ. A swollen translucent sac of nerve tissue held
in a cage of curved bone ribs, mounted on a low chitin plinth. Faint branching
veins across the sac. The cyan strip is the glow from inside the sac showing
through the gaps in the ribs. Taller than it is wide.
```

### OREPURIFIER — resource yield
```
Subject: a narrow digestive organ. A vertical column of stacked chitin rings,
each ring slightly wider than the one below, with pink muscle visible in the
joints between them, topped by a puckered fleshy vent. The cyan strip runs the
full height of the column on one side. Twice as tall as it is wide.
```

### BUNKER — infantry garrison
```
Subject: a small armoured shell. A low dome of thick overlapping chitin plates
with a single dark slit opening at the front, ringed with short bone spines.
Squat and heavy, wider than it is tall. The cyan strip is a short bar above the
slit. This is the smallest structure in the set: compact, no protrusions.
```

### TURRET — anti-ground gun
```
Subject: a mounted weapon organ. A heavy chitin base ring carrying a swivelling
head, the head shaped as a thick muscled barrel of ribbed flesh ending in a
bone-rimmed orifice, angled slightly downward. The cyan strip runs along the
top of the barrel. Wider than it is deep.
```

### AATURRET — anti-air gun
```
Subject: a mounted weapon organ aimed at the sky. A chitin base ring carrying a
cluster of four slender bone spines angled steeply upward, splayed apart, each
socketed in pink muscle. The cyan strip is a ring around the base of the
cluster. Tall and narrow at the top, broad at the base.
```

### FIELD — shield projector
```
Subject: a shield organ. A shallow chitin bowl opening upward, its inner
surface a stretched translucent membrane, ringed by six short bone prongs
around the rim. The cyan strip is the membrane itself, glowing across the whole
bowl. Wide and low.
```

### UPLINK — support powers
```
Subject: a tall communication organ. A slender tapering chitin spire with three
fleshy antennae fronds curling outward near the top, and a knot of exposed
muscle at the base. The cyan strip runs the full height of the spire on the
front face. Much taller than it is wide.
```

### TELEVATOR — teleport node
```
Subject: a teleport organ. A flat oval chitin pad set into the ground, its
centre an open pit of swirling membrane, ringed by eight short bone teeth
around the edge. The cyan strip is the pit itself. Wide and very low, almost
flush with the ground.
```

### HARBOR — naval production
```
Subject: a large coastal organ. A broad chitin shelf sloping down at the front
into an open muscled channel, flanked by two curved bone arms reaching forward
like a crab's claws framing the channel mouth. The cyan strip runs down the
centre of the channel. The largest structure in the set.
```

## Importing what comes back

```bash
python3 import-sprite.py mods/hv/bits/sprites/buildings/hive-radar.png \
  --frame-size 35x46 --team-tolerance 150 \
  --animation "idle=<downloaded sheet>,detect=2,facings=0" \
  --author "Nano Banana Pro, Universe"
```

`detect=N` finds the drawn subjects whatever layout the model used, including
sheets with ruled cell borders. `facings=0` means the frames are an animation
loop rather than rotations.

Then stamp the sheet and check it:

```bash
./utility.sh --png-sheet-import ../mods/hv/bits/sprites/buildings/hive-radar.png
./utility.sh --check-missing-sprites
```

## Units

Not prompts. Each unit is a line in `SPECIES` in `generate-sprite.py`, and
every one of them gets the full 154-frame lifecycle for free: stand, walk with
inverse-kinematic legs, attack swing, death collapse.

```bash
python3 generate-sprite.py mods/hv/bits/sprites/infantry/swarm-runner.png \
  --frame-size 32x32 --supersample 8 --creature runner \
  --sequence stand:4 --sequence move:8 --sequence attack:6 --sequence die:10:1 \
  --author "Procedural, Universe"
```
