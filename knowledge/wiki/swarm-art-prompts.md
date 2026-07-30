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

## Six at a time, not one at a time

Twelve separate generations cost twelve chances to drift in palette, lighting
and camera angle, and twelve rounds of downloading and importing. Six subjects
on one 4096-pixel canvas share all three by construction and still give every
building about a thousand pixels to itself, which is twenty-five times what a
forty-pixel frame needs.

Six is the practical limit, and it is set by the model rather than by
resolution: past that it starts blending one description into the next.

## The shared style block

Paste this above either batch. It is the part that keeps the two generations
looking like one faction.

```
Six separate organic structures grown by an insectile hive, drawn as pixel art
for a top-down real-time strategy game, arranged on one canvas in two rows of
three with generous empty space between them. Each structure is a distinct,
self-contained object. Nothing overlaps, nothing touches, and no structure is
connected to another by tendrils, roots, ground or shadow.

Camera looks down at every structure from the same 35 degrees off vertical, no
perspective drift between them.

Material: violet-grey chitin plates (#8F7CA5) with deep purple seams (#4A345C)
between them, exposed pink muscle (#C47A84) in the gaps where plates meet, and
pale bone (#E2D2B6) on any spur, claw or spike. The plates overlap like an
insect's abdomen, each catching light on its upper edge.

Every structure carries a glowing cyan strip (#00FFFF) somewhere on its upper
surface, wide enough to read clearly at forty pixels. This is the only cyan in
the image.

Light falls from the upper left on all six. Hard-edged shading in flat bands,
not soft gradients. One solid black outline around each silhouette.

Background: flat solid magenta #FF00FF filling the entire canvas edge to edge,
including all the space between the structures. No ground, no shadows, no
texture, no gradient, no grid lines, no cell borders, no labels, no text, no
watermark, no numbering. Only the six structures and the magenta.

Everything is grown, not built: no straight machined edges, no bolts, no
panels, no metal.
```

## Batch one

Append this to the style block. The reading order matters: the importer matches
what it finds to the names in the same order, left to right and top to bottom.

```
Top row, left to right:

1. SENSOR MAST. A thick chitin stalk rising from a squat armoured base, opening
   at the top into a fleshy dish of stretched membrane ringed with short bone
   spines. Twice as tall as wide.
2. MARKET. A wide squat organ with three fleshy mouths opening on its upper
   surface, each ringed with bone teeth, arranged around a central chitin dome.
   Low and broad, as wide as it is deep.
3. AIR NEST. A wide chitin cradle with an open muscled maw at the front angled
   upward, ribbed like a throat, braced by two heavy bone buttresses. Half again
   as deep as wide.

Bottom row, left to right:

4. BRAIN ORGAN. A swollen translucent sac of nerve tissue held in a cage of
   curved bone ribs on a low chitin plinth, faint branching veins across the
   sac. Taller than wide.
5. REFINING GUT. A vertical column of stacked chitin rings, each slightly wider
   than the one below, pink muscle in the joints, topped by a puckered fleshy
   vent. Twice as tall as wide.
6. BUNKER. A low dome of thick overlapping chitin plates with a single dark
   slit at the front, ringed with short bone spines. The smallest of the six:
   squat, heavy, no protrusions.
```

## Batch two

```
Top row, left to right:

1. GROUND TURRET. A heavy chitin base ring carrying a swivelling head shaped as
   a thick muscled barrel of ribbed flesh ending in a bone-rimmed orifice,
   angled slightly down. Wider than deep.
2. SKY TURRET. A chitin base ring carrying four slender bone spines angled
   steeply upward and splayed apart, each socketed in pink muscle. Narrow at the
   top, broad at the base.
3. SHIELD BOWL. A shallow chitin bowl opening upward, its inner surface a
   stretched translucent membrane, ringed by six short bone prongs. Wide and
   low.

Bottom row, left to right:

4. SPIRE. A slender tapering chitin spire with three fleshy antennae fronds
   curling outward near the top and a knot of exposed muscle at the base. Much
   taller than wide.
5. TELEPORT PAD. A flat oval chitin pad set into the ground, its centre an open
   pit of swirling membrane, ringed by eight short bone teeth. Wide and very
   low, almost flush.
6. HARBOUR. A broad chitin shelf sloping down at the front into an open muscled
   channel, flanked by two curved bone arms reaching forward like crab claws.
   The largest of the six.
```

## Importing what comes back

One command per sheet. The names are matched to what the detector finds in
reading order, so they must be listed in the same order the prompt asked for.

```bash
python3 import-sprite.py mods/hv/bits/sprites/buildings \
  --background 8D9894 --background-tolerance 40 --team-tolerance 150 \
  --animation "batch=<downloaded sheet>,detect=6,facings=0" \
  --subject "hive-radar=35x46"      --subject "hive-trader=40x40" \
  --subject "hive-starport=40x60"   --subject "hive-techcenter=36x52" \
  --subject "hive-orepurifier=24x44" --subject "hive-bunker=22x23" \
  --author "Nano Banana Pro, Universe"
```

`--background` is the backdrop the model actually produced, which is often not
the magenta it was asked for; read a corner pixel first. `detect=6` finds the
subjects whatever layout came back, including sheets with ruled cell borders.

Then stamp and check:

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
