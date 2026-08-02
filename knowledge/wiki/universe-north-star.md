---
title: Universe North Star
status: current
updated: 2026-08-02
sources:
  - ../raw/user-vision-2026-08-02.md
  - ../raw/worldbox-2026-08-02.md
  - ../raw/openciv-2026-07-31.md
  - ../raw/space-economy-references-2026-07-31.md
  - ../raw/tyranid-lore-2026-07-31.md
tags:
  - vision
  - product
  - north-star
---

# Universe North Star

## Product statement

Universe is a deterministic, autonomous, real-time history of life across
three planets. The observer watches physical worlds form habitats, habitats
produce life, life produce civilizations, civilizations cross technological
ages, and spacefaring races trade, negotiate, colonize, and fight. War is RTS,
but it happens inside the same continuous world rather than in a launched
arena.

The first shippable world is one complete planet. Two additional planets are a
required architecture boundary from the first domain model, then become active
after the one-planet vertical slice is proven.

## Non-negotiable invariants

1. **Observer only.** No player faction, build orders, cell selection, battle
   button, diplomatic choice, or required intervention.
2. **One runtime.** Simulation, visualization, civilization, space, and RTS run
   in one .NET/OpenHV process and one authoritative state graph.
3. **Start from zero.** No pre-spawned race or city in the product scenario.
   Physics precedes habitability; life precedes sapience; sapience precedes
   civilization; technology precedes spaceflight.
4. **Continuous real time.** Slow systems use scheduled macro ticks, not turns.
   Local RTS continues at engine tick rate.
5. **Whole-world continuity.** The observer can always recover the planet or
   system context. Combat never replaces the world with an unrelated arena.
6. **No functional deletion.** Python/Web features remain reference or
   developer tools until a verified C# replacement exists.
7. **One native race per planet.** Race traits emerge from that planet's
   ecology and history rather than from a fixed start menu.
8. **Three-planet ready.** Every core identity uses `systemId`, `planetId`, and
   stable entity IDs even while only the first planet is active.
9. **Reproducible history.** Seed, configuration, save, replay, event spine,
   telemetry, and synchronized hashes make a run explainable.
10. **Tyranids are late, not initial.** Their invasion is a consequence of a
    mature space-capable world, not a shortcut around evolution.

## Chosen platform

The canonical platform is **C#/.NET on OpenHV/OpenRA**.

| Requirement | Why this platform wins |
|---|---|
| Real-time war in the same world | Existing actors, production, pathfinding, fog, weapons, squads, maps, and AI |
| Autonomous observer | Existing spectator startup and modular bots |
| Large deterministic simulation | Synchronized integer state, headless execution, replay, hashes, batch runner |
| Deep civil systems | Existing `CivilizationState`, research, diplomacy, trade, mobilization, casualties |
| Pixel whole-world view | Native tile/sprite renderer can add semantic planetary layers without a second engine |

A Web-only choice would require rebuilding the hardest and most mature part:
RTS pathfinding, combat, actor lifecycle, replays, content tools, and AI. The
.NET choice instead ports the planet systems while retaining the RTS base.

## One state graph, several time scales

```text
UniverseState
└── StarSystemState
    ├── PlanetState[0] — surface, climate, biosphere, native race, civilizations
    ├── PlanetState[1] — same contract, initially inactive
    ├── PlanetState[2] — same contract, initially inactive
    ├── OrbitalNetwork — stations, routes, ships, fleets
    └── InterplanetaryRelations — discovery, trade, diplomacy, war
```

| Clock | Typical cadence | Systems |
|---|---:|---|
| Engine | 40 ms | actors, movement, weapons, immediate orders |
| Surface | hours/days | temperature, moisture, wind, precipitation, hazards |
| Ecology | days/weeks | biomass, food webs, mutation, competition, migration |
| Civilization | weeks/months | population, production, policy, culture, research, trade |
| Strategic | months/years | eras, institutions, colonization, interplanetary relations |

All clocks are deterministic schedules inside the same OpenHV world. Planet
physics stores synchronized fixed-point state; transient calculations must
quantize at macro-tick boundaries so floats never enter the sync hash.

## Historical ladder

Progress is conditional, not a timer:

1. lifeless physical planet;
2. abiogenesis and microbial ecology;
3. complex multicellular ecosystems;
4. sapient species and prehistory;
5. tribal and early agrarian societies;
6. cities, states, writing, organized trade, and classical institutions;
7. feudal/imperial consolidation;
8. industrialization;
9. electrical, atomic, and information ages;
10. planetary civilization;
11. spaceflight and orbital industry;
12. interplanetary civilization and technological Imperium.

An age transition requires material, demographic, knowledge, institutional,
and environmental conditions. Collapse may delay or reverse capabilities; a
label alone never grants a technology.

## Visual contract

The presentation combines WorldBox's living pixel world with Civilization's
global legibility and RTS immediacy through semantic zoom:

- **system zoom:** three planets, orbits, routes, ships, flotillas, invasions;
- **planet zoom:** climate, biomes, ecosystems, borders, cities, trade lanes;
- **regional zoom:** districts, infrastructure, armies, migration, convoys;
- **local zoom:** individual buildings, units, projectiles, and RTS battles.

Zoom changes representation, never authority. A fleet, city, route, or battle
is the same entity at every level. The initial planet uses a 2:1 global grid
with chunked simulation and rendering; exact resolution is selected by a
performance gate rather than by shrinking the intended world.

## Definition of product completion

A single seeded run, without user input, must:

- begin with no life or civilization;
- reach a stable physical climate and create a biosphere when conditions allow;
- produce a native sapient race and settlements through simulated emergence;
- advance at least one civilization from prehistory to spaceflight;
- visibly preserve ecology, society, economy, politics, diplomacy, trade,
  characters, events, migration, construction, and warfare;
- activate three planets and generate a distinct native race on each;
- build ships, frigates, and flotillas; discover, travel, trade, colonize, and
  negotiate across planets;
- resolve wars in real time on the same surfaces and orbital network;
- trigger and simulate a late Tyranid invasion;
- save, reload, replay, export telemetry, and reproduce the synchronized hash;
- satisfy every row of the [capability migration ledger](capability-migration-ledger.md).

Anything less is an intermediate vertical slice, not completion of the goal.
