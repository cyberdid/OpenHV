---
title: Universe Execution Plan
status: current
updated: 2026-08-02
sources:
  - universe-north-star.md
  - capability-migration-ledger.md
  - decisions/0006-single-dotnet-universe-runtime.md
tags:
  - execution
  - milestones
  - acceptance
---

# Universe Execution Plan

Every phase ends in a runnable vertical slice, tests, telemetry evidence, wiki
update, focused commit, and push. A later phase cannot hide a failed earlier
gate.

## Phase 0 — Program lock and inventory

Deliverables:

- authoritative North Star and one-runtime decision;
- complete capability migration ledger;
- ordered dependency plan and definition of completion;
- raw records for user direction and design references.

Gate: every current system has an explicit retain/port/verify destination and
no product document still calls the Web bridge canonical.

## Phase 1 — Universe kernel

Create synchronized C# types for `UniverseState`, `StarSystemState`, three
`PlanetState` slots, stable IDs, deterministic multi-rate clock, event spine,
configuration, checkpoint/save schema, and load/resume.

Vertical slice: one empty planet advances geological days in graphical and
headless modes; save/reload at a macro-tick boundary reproduces state and hash.

Gate: 10,000 macro ticks repeat exactly, headless/graphical normalized state is
identical, and planet two/three can be constructed without renderer changes.

## Phase 2 — Physical planet and large surface

Port radiation, atmosphere, wind, hydrology, precipitation, terrain, climate
calibration, and diagnostics to deterministic fixed-point systems. Generate a
large 2:1 surface in chunks and render planet-wide climate/terrain overlays.

Vertical slice: a lifeless seeded planet equilibrates visibly from zero.

Gate: Python golden scenarios meet agreed tolerances for mean temperature,
energy residual, forcing response, wind bounds, water conservation, and
deterministic hash; frame time and memory meet the selected grid-size budget.

## Phase 3 — Biosphere, ecology, and emergence

Port habitability/biomass/complexity, then add species populations, trophic
energy, mutation, competition, migration, extinction, and abiogenesis. Sapience
must emerge from accumulated ecological conditions and inherit planet-derived
traits.

Vertical slice: some seeds remain barren; a viable seed grows from microbial
life to one native sapient race with no scripted spawn.

Gate: conservation/carrying-capacity tests pass, repeat histories hash equally,
and deliberately different planets yield measurably different race traits.

## Phase 4 — Civilization and historical ages

Unify the emergent race with settlements, population cohorts, needs, jobs,
housing, resources, specialization, migration, social cohesion, government,
policy, culture, characters, chronicle, research, institutions, trade, and
technology-era gates.

Vertical slice: prehistory autonomously produces tribes, cities, states,
industrialization, and a planetary civilization; collapse and recovery remain
possible.

Gate: every `CIV/SOC/ECO/LOG/POL/DRM/CHR/EVT/AGE` ledger row has tests and
telemetry; no city or technology is granted only because time elapsed.

## Phase 5 — Semantic whole-world visualization

Build native system/planet/region/local zoom. Port the informative function of
all Web views: climate layers, territories, settlements, routes, characters,
events, chronicle, and strata. Use LOD aggregation and chunk activation so the
entire planet is legible while local actors remain RTS entities.

Vertical slice: the observer follows the whole history without opening another
application or choosing a faction.

Gate: each `VIS` ledger row is verified; selection may inspect but never issue
orders; zooming never creates or replaces simulation state.

## Phase 6 — Same-world RTS warfare

Connect civilization population, industry, logistics, technology, diplomacy,
and terrain directly to OpenHV actors and armies. Wars emerge from AI relations;
units mobilize, travel, fight, occupy, consume supplies, and create civilian
consequences on the persistent planet surface.

Vertical slice: two autonomous states trade, enter conflict, fight visibly,
change a border, make peace or collapse, while the rest of the world continues.

Gate: no battle launcher, arena map, watchdog-defined outcome, or score-leader
territory change; replay and telemetry explain every war and consequence.

## Phase 7 — Space age and orbital economy

Add launch infrastructure, orbital industry, fuel/material budgets, stations,
freighters, warships, frigates, carriers, fleet command, travel time, orbital
routes, and solar-system visualization.

Vertical slice: the first civilization crosses explicit spaceflight gates,
builds a ship from real stocks, reaches orbit, and establishes a station and
route without intervention.

Gate: ship mass, cargo, fuel, travel, damage, construction, and ownership are
conserved and visible; planet society pays the economic opportunity cost.

## Phase 8 — Three planets and first contact

Activate planets two and three from independent physical seeds. Each runs the
same zero-to-life pipeline and produces its own native race. Add discovery,
communication, translation, trade, treaties, migration, colonization, and
interplanetary conflict.

Vertical slice: one race discovers another and autonomously chooses an
observable relationship based on needs, culture, power, and prior events.

Gate: all three planets advance concurrently in one process; inactive/remote
LOD does not change outcomes; travel and trade conserve entities and cargo.

## Phase 9 — Tyranid invasion and final parity

Integrate the separately developed Tyranid assets and behavior as an external
spaceborne ecology: fleet approach, adaptation, landing, biomass consumption,
reproduction, and strategic response by existing races.

Final acceptance run: from three lifeless planets to three native races,
spaceflight, first contact, trade/diplomacy/war, then invasion—without user
input, process switching, or a second authoritative engine.

Gate: every ledger row is `verified`; save/load/replay and normalized hashes
match; CoruscantSim is retained as history/reference but no longer required by
the product runtime.

## Completed work package: UNI-001

Phase 1's first executable slice delivered:

1. introduce immutable IDs and definitions for Universe, system, and three
   planets;
2. add a synchronized macro clock independent of RTS actor ticks;
3. start planet zero as lifeless; keep planet one/two defined but inactive;
4. export the state in result/telemetry without changing existing matches;
5. prove same-seed repeat and graphical/headless parity.

No physics or life behavior enters UNI-001. Its purpose is to establish the
stable container every later system depends on. At tick 500 the synchronized
clock reached macro day 2 with zero remainder. Two identical headless runs and
the graphical/headless pair all produced hash `3A94C592`; result JSON and
periodic JSONL telemetry contained the same three-slot Universe snapshot.

## Immediate work package: UNI-002

Complete the remaining Phase 1 kernel before physical simulation begins:

1. replace the provisional flat planet-slot counters with explicit
   `StarSystemState` and `PlanetState` synchronized objects;
2. add deterministic macro-event IDs and an append-only event spine;
3. define the versioned Universe checkpoint schema;
4. save only on a macro-tick boundary and reload into the same IDs/state;
5. prove uninterrupted and save/reload runs end with identical normalized
   Universe state and synchronized hash.
