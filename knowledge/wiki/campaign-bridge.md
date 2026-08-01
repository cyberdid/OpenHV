---
title: The Campaign Bridge
status: current
updated: 2026-07-31
sources:
  - ../../schemas/planet-state-v1.schema.json
  - ../../schemas/battle-request-v1.schema.json
  - ../../schemas/simulation-result-v1.schema.json
  - ../../OpenRA.Mods.HV/UtilityCommands/FetchPlanetState.cs
  - ../raw/coruscantsim-analysis-2026-07-31.md
tags:
  - architecture
  - campaign
---

# The Campaign Bridge

How the planet simulation and the battle engine become one game.

## The wall

They cannot be one process, and the reason is not taste.

The planet's atmosphere needs floating point — its energy budget closes to
about 10 W/m² and would not close at all in fixed point. The battle needs
integer lockstep — `synchronizedStateHash` is what makes a match reproducible
and a multiplayer session possible, and a single float in the synchronised
state desynchronises it. This project has already moved that hash by accident
once, by putting `[VerifySync]` on new fields.

So they are two processes with two clocks:

| | clock | arithmetic |
|---|---|---|
| Planet | one day a step | float64 numpy |
| Battle | 20 ms a tick | integer, hash-verified |

**4,320,000 to 1.** Nothing bridges that inside one loop.

## The resolution: split by time scale, not by feature

An atmosphere changes over days; a battle lasts twenty minutes. The physics
never needs to be inside the battle loop, and the battle never needs to be
inside the day loop. What they need is a contract at each boundary.

```
      Python — the simulation server            OpenRA — the one client
   physics, biosphere, emergence,          planet view: 144 x 72 tiles
   civilization, fleets, economy           battle: an ordinary map
              │
              │  planet-state-v1  ──── HTTP, polled ────>
              │
              <──── battle-request-v1 ──── on contact ────
              │
              │  <──── simulation-result-v1 ──── on resolution
```

## Three contracts, and why there are only three

**`planet-state-v1`** — one frame of a planet, shaped for a tile renderer.
Flat arrays in row-major order rather than ten thousand JSON objects, quantised
to bytes rather than full-precision floats: 57 KB a frame at half grid, polled
about once a second. Population is on a **log** scale, because density spans
six orders of magnitude between a founder brood and a peak swarm and a linear
scale would render both as the same black.

Deliberately not a reshaping of the existing `/api/state`: the globe viewer
wants nested objects at full precision, a tile map wants flat channels, and
converting one into the other every frame would serve neither.

**`battle-request-v1`** — the campaign asking for a battle. Carries what the
tactical engine cannot infer: which cell, the physics state of that cell so
terrain follows the atmosphere, each side's committed army and condition, the
stakes, and the determinism block without which the reply cannot be reproduced.

**`simulation-result-v1`** — the reply, unchanged. There is deliberately **no**
`battle-result-v1`: the existing document already carries players with kills
and losses, natural winners, end reason and the hash, which is everything
needed to apply an outcome. A second result format could only drift from the
first, and `tests/test_battle_contract.py` fails if anyone adds one.

## Why this cannot desynchronise anything

The planet channel is one-directional and outside the simulation. The client
never writes back through it, and nothing served over it enters a match — a
battle is set up **once** from a request document and is deterministic from
that tick onward. The planet view is a widget drawing data, not a world being
simulated.

That separation is the whole design. It is what lets one side be floating-point
Python and the other integer lockstep without either compromising.

## Why OpenRA renders both

Checked rather than assumed:

| Needed | Already present |
|---|---|
| HTTP client in .NET | `OpenRA.Game/Support/HttpClientFactory` — the engine already fetches map previews and server lists |
| Mod-defined UI | seven logic classes in `OpenRA.Mods.HV/Widgets/Logic/` |
| Mod-defined tooling | three commands in `OpenRA.Mods.HV/UtilityCommands/` |
| A map the size of a planet | the grid is 144 × 72; the mod ships maps up to 258 × 258 |

Both halves of the bridge existed before anyone set out to build it, on both
sides, written independently.

The alternatives were weighed and rejected. Porting the RTS to the browser is
what had already failed — OpenRA is fifteen years of pathfinding, lockstep, map
format, replays and an art pipeline. Porting the planet into OpenRA as a
simulated world hits the arithmetic wall above. Two separate applications works
but costs two installers, two windows and a break in style mid-game.

One engine and one art style is not a compromise: a planet drawn in the same
pixel art as the battles reads as one game. The Three.js globe remains
available as a separate scientific view.

The cost that is real: OpenRA cannot draw a sphere. The planet view is an
equirectangular projection and the poles stretch, which is the price every
flat-map 4X pays.

## Verified without a renderer

`--fetch-planet` reads a frame and prints it. If the terrain histogram matches
what the simulation says it produced, the bridge works and everything after it
is drawing.

```
Tyranthos at step 900
  grid       72 x 36 = 2592 cells
  terrain
    barrens         341   13.2%
    steppe          606   23.4%
    growth          909   35.1%
    deep-growth     736   28.4%
  biomass    mean 0.437 of standing capacity
  fleets
    Synaptrix    #3373D9    696 cells
    ...
  race       woke at step 687 in cell (28,0)
             appetite 1.11  reciprocity 0.93  expectation 1.02
```

The command checks array lengths hard. A short array would draw a **torn map**
rather than fail, and a torn map looks like an art problem for as long as it
takes to think of looking here.

## The loop, closed

A cell of the planet now becomes a battle. Three pieces:

**The planet screen** (`PlanetMapWidget`) fetches one frame and draws it as a
grid — terrain, biomass, swarm density, holder — with a cell inspector. Built
like `RadarWidget`: one BGRA sheet per fetch drawn as a single scaled sprite,
because ten thousand `FillRect` calls would be a draw call per cell every frame
for a picture that changes once a second. Two departures from `RadarWidget`: no
pointers, since the engine assembly sets `AllowUnsafeBlocks` and this one does
not; and the fetch runs off the UI thread, so a simulation that is not running
gives a message instead of a frozen menu.

**`BattleRequestBuilder`** turns a cell into a `battle-request-v1`, and is shared
by the screen and by `--battle-from-planet`. Two builders would drift, and a
request that differs by which button made it cannot be reproduced from the
campaign it came from.

**`fight-cell.sh`** runs the match and applies the stakes. It takes
`--request <path>` and **replays** the document rather than rebuilding it — the
campaign advances while you look at it, so asking for the same cell a minute
later gives a different seed, map and army values. The request is the battle.

The match is a separate process because it has to be: `Launch.Simulation` is
read once by `PanelLoadScreen` at startup, so a battle cannot begin inside a
process that is already running. That is why the in-game button writes a request
instead of pretending to start one.

### What running it corrected

- **Longitude was −180..180.** The schema wants 0..360, which is what
  `events.py` normalises to, so a district lookup on either side lands on the
  same place. The schema was right.
- **The map decided how many sides fought.** `PanelLoadScreen` fills every open
  slot, cycling the bot list, so two participants on a four-spawn map became a
  2v2 and the result reported four players the campaign never committed.
  `coldrage`, the harness default everywhere else, has four spawns.
- **Two spawn points did not mean a fight.** All twenty-two two-spawn maps were
  run at aggressor vs fortress, seed 12345, 30000 ticks. `river-fight` and
  `nowheres-land` ended 0 kills and 0 earned — both sides built armies and in
  ten minutes of game time reached neither each other nor any resources. The
  surviving twenty ran from 16,900 kills to 289,500.

## Still owed

1. **`armyValue` is carried and not applied.** The request commits a military
   value; no `Launch` argument accepts one, so the match ignores it.
2. **Ground is not chosen by biome.** All sixty-nine maps declare
   `Tileset: PLANET` and there is no generator, so a steppe cell cannot yet be
   fought on steppe. The `environment` block already carries the physics, so a
   generator can honour it without the contract changing.
3. **Outcomes do not return to the campaign.** `fight-cell.sh` reports who took
   the cell; nothing writes it back into the planet.

## Related pages

- [Architecture](architecture.md)
- [Analysis of the predecessor project](../raw/coruscantsim-analysis-2026-07-31.md)
- [Space economy references](../raw/space-economy-references-2026-07-31.md)
