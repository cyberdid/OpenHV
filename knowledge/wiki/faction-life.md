---
title: Living Factions Design
status: current
updated: 2026-07-29
sources:
  - ../raw/openciv-2026-07-29.md
  - overview.md
  - architecture.md
  - execution-plan.md
  - ../../engine/OpenRA.Game/Player.cs
tags:
  - civilization
  - population
  - economy
  - diplomacy
  - design
---

# Living Factions Design

## Product correction

The simulation is not primarily a war tournament. It is a real-time world of
autonomous societies. War remains possible and mechanically deep, but it is
one expensive policy instrument among trade, expansion, research, diplomacy,
specialization, migration, and cultural influence.

A faction should remain interesting when no shots are fired.

## Implemented vertical slice

LIFE-001–003 are implemented and validated:

- every player owns a synchronized `CivilizationState`;
- `BASE`/`BASE2` actors are `SettlementCore` capitals with deterministic
  children, adults, elders, population, founding tick, and local stocks;
- a 250-tick civil pulse computes jobs, employment, housing, food, materials,
  energy, knowledge, storage, demand, satisfaction, prosperity, stability, and
  migration pressure;
- a 3,000-tick demographic pulse applies births, baseline deaths, aging, and
  food-shortage mortality;
- base, generator, storage, trader, technology center, and ore-processing
  actors expose explicit civil capacity, production, consumption, and
  maintenance;
- infrastructure is assigned to the nearest owned settlement with actor-ID
  tie-breaking;
- `balanced` and `scarcity` are synchronized lobby profiles;
- the non-attacking `steward` bot develops infrastructure without forming
  combat squads;
- final results, periodic snapshots, and civil reason-coded events expose the
  full state.

Balanced, scarcity, determinism, peaceful-development, and overhead evidence
is recorded in
[Living Factions and Telemetry v1 Validation](experiments/2026-07-29-living-factions-v1.md).
Neutral diplomacy, trade, migration transfer, and casualty-to-workforce
coupling remain planned rather than implied.

LIFE-004 is now implemented as a five-node deterministic graph. Knowledge is
spent in actor-ID order on agricultural systems, energy grid, logistics,
civil engineering, and research networks; unlocks modify food, energy,
storage, housing, or knowledge production and emit telemetry events. See the
[research validation](experiments/2026-07-29-civil-research-v1.md).

## What OpenCiv contributes

OpenCiv provides a useful conceptual bridge between RTS bases and living
civilizations:

- a city has population rather than being only a collection of buildings;
- territory generates differentiated yields;
- population works a limited number of locations;
- buildings transform city output;
- food, production, knowledge, wealth, culture, and morale are separate;
- terrain supports bonus, strategic, and luxury resources;
- civilization identity includes geography, unique capabilities, and city
  history.

The implementation is TypeScript, turn-based, and currently incomplete. We
should adapt the concepts to OpenRA's synchronized real-time trait system, not
merge the codebases or reproduce Civilization content.

## Simulation layers

### Tactical layer

Runs every OpenRA world tick:

- movement;
- gathering;
- construction;
- production;
- combat;
- scouting;
- local threat response.

### Civil economy pulse

Runs at a configurable fixed world-tick interval, initially every 250 ticks:

- production and consumption;
- labor allocation;
- needs satisfaction;
- settlement output;
- resource transfer.

### Demographic cycle

Runs less frequently, initially every 3,000 ticks:

- births and deaths;
- migration;
- workforce changes;
- health and housing pressure;
- war casualties entering population records.

### Strategic/diplomatic cycle

Runs less frequently again, initially every 5,000 ticks:

- strategic goal selection;
- trade offers;
- treaties;
- grievances;
- border disputes;
- mobilization and war decisions.

All intervals are synchronized configuration values and will be tuned through
experiments.

## Population model

Do not simulate every civilian as an actor in the first versions. Thousands of
individual agents would be expensive and would bury the strategic model under
pathfinding noise.

Use deterministic settlement-level cohorts:

- total population;
- available workforce;
- farmers/extractors;
- industrial workers;
- researchers;
- service/cultural workers;
- mobilized military personnel;
- dependents.

Later versions may add named leaders or notable individuals without converting
the entire population into agents.

### Growth model

Population growth depends on:

- food security;
- housing capacity;
- energy access;
- health;
- stability;
- deaths and combat casualties;
- inward/outward migration.

All synchronized calculations use integer or OpenRA fixed-point arithmetic.

Conceptually:

`population change = births - natural deaths - casualties + net migration`

Birth and migration rates are modified by needs satisfaction rather than
directly optimized as arbitrary bonuses.

## Needs

Each settlement tracks normalized satisfaction:

- food;
- housing;
- energy;
- health;
- safety;
- social cohesion.

Shortages create consequences instead of an immediate game-over:

- low food → slower growth, mortality, migration;
- low housing → growth cap and instability;
- low energy → reduced production and research;
- low safety → flight, defensive spending, radicalization;
- low health → mortality and lower labor output;
- low cohesion → strikes, unrest, separatism, or revolt.

## Resource model

### Version 0.1

- **Food:** population survival and growth.
- **Materials:** buildings, infrastructure, and equipment.
- **Energy:** operation of industry, advanced buildings, and technology.
- **Knowledge:** research progress.
- **Credits:** OpenHV's existing treasury and exchange medium.

### Later

- consumer goods;
- medicine;
- influence/prestige;
- rare strategic resources;
- luxury resources that affect cohesion, diplomacy, and trade.

Physical resources should have production, storage, transport, and consumption.
Credits should not silently substitute for missing food or energy unless a
trade/import action actually occurs.

## Settlements

A base becomes a settlement when it has a `SettlementCore` and population.

Settlement state includes:

- name and founding tick;
- population and cohorts;
- territory/influence;
- housing;
- jobs;
- local stocks and production;
- infrastructure;
- needs;
- prosperity;
- stability;
- cultural identity;
- owner and historical owners.

### Buildings as civil infrastructure

Existing and new actors can provide:

- farms/food processing;
- housing;
- power;
- mining/materials;
- factories;
- laboratories;
- clinics;
- markets/logistics;
- cultural institutions;
- administrative capacity;
- defense.

Each building must expose explicit jobs, inputs, outputs, capacity, and
maintenance.

## Territory

Territory is not merely attack range.

It represents:

- settlement influence;
- access to resources;
- transport safety;
- borders;
- taxation/administration reach;
- cultural pressure.

The first implementation can use cell influence around settlement and outpost
actors. Overlapping influence creates a disputed border instead of immediate
war.

## Research and technology

Researchers, laboratories, infrastructure, and policy generate knowledge.

A small original technology graph should unlock:

- better food production;
- storage/logistics;
- energy efficiency;
- housing/health;
- communication and scouting;
- industrial production;
- defensive and military capabilities.

Technology should create opportunity costs. A faction investing in research
has fewer workers/materials available for immediate expansion or war.

## Culture and institutions

Culture is not only a victory score. It shapes behavior and cohesion.

Potential faction values:

- collective ↔ individual;
- expansionist ↔ isolationist;
- technocratic ↔ traditional;
- centralized ↔ decentralized;
- militarist ↔ pacifist;
- egalitarian ↔ hierarchical.

Institutions and historical events can slowly move these values. They influence
AI utility weights, diplomacy, migration attractiveness, and crisis response.

This starts as a faction-level model; internal political groups are a later
layer.

## Diplomacy

Current OpenRA player relationship masks support ally, neutral, and enemy but
are initialized from lobby/map state. Living factions require a synchronized
runtime diplomacy manager.

### Relationship state

Track independent signals:

- trust;
- fear;
- grievance;
- trade dependency;
- cultural affinity;
- border pressure;
- relative power.

Derived diplomatic states:

- unknown;
- neutral;
- friendly;
- trade partner;
- allied;
- rival;
- hostile;
- war;
- armistice.

### Treaties/actions

- contact;
- resource trade;
- recurring trade route;
- non-aggression pact;
- research agreement;
- defensive pact;
- alliance;
- ultimatum;
- embargo;
- war declaration;
- armistice and peace treaty.

Factions begin neutral unless a scenario says otherwise. Units do not
automatically attack neutral factions.

Relationship transitions must be synchronized, bilateral where appropriate,
and logged as world events.

## Trade

Trade exists because geography creates asymmetry:

- one settlement has food;
- another has energy;
- another has rare materials or research capacity.

Trade routes require:

- agreement;
- source stock;
- destination demand;
- transport capacity;
- a viable route;
- security.

Raiding a route generates material gain, grievance, and diplomatic risk.

## Civilization AI

The AI should optimize a multi-objective civilization utility, not kill count.

Core objectives:

- survival;
- food/energy security;
- prosperity;
- population wellbeing;
- knowledge;
- cohesion;
- influence;
- strategic security.

The AI chooses goals such as:

- prevent famine;
- build housing;
- secure energy;
- open trade;
- establish an outpost;
- recover from disaster;
- research a technology;
- deter a rival;
- negotiate peace;
- mobilize;
- wage a limited or total war.

War is chosen when expected strategic value exceeds military, demographic,
economic, diplomatic, and stability costs.

## Consequences of war

Combat must feed back into civilian life:

- military recruitment removes workforce;
- casualties reduce population;
- destroyed power/farms/housing create shortages;
- prolonged mobilization creates war exhaustion;
- refugees migrate;
- trade routes collapse;
- grievances persist;
- occupied settlements resist or assimilate;
- victory can still leave the winner poorer and less stable.

This makes peace and recovery meaningful without weakening the RTS combat
layer.

## Non-war stories the simulation should generate

- a resource-poor faction survives through trade;
- a research-focused city attracts migrants;
- a food crisis forces policy change;
- a border settlement becomes culturally mixed;
- former rivals form a defensive pact;
- a rich capital becomes dependent on distant energy;
- migration weakens one faction and revitalizes another;
- a disaster causes political fragmentation;
- a small faction survives through diplomacy rather than army size.

## Living Factions v0.1 vertical slice

### Systems

1. `CivilizationState` synchronized player trait.
2. `SettlementCore` synchronized actor trait.
3. Population and workforce cohorts.
4. Food, materials, energy, knowledge, and credits.
5. Food production/consumption and storage.
6. Housing and jobs.
7. Population growth, shortage, mortality, and migration pressure.
8. A small six-to-eight-node original technology graph.
9. Settlement prosperity/stability telemetry.
10. Default-neutral relationships and explicit war state.

### Minimal content

- one capital settlement per faction;
- farm/food producer;
- housing;
- material extractor;
- power structure;
- workshop/factory;
- laboratory;
- market/logistics building;
- defensive building.

Existing OpenHV actors can be temporarily assigned these roles where sensible;
new art is not required for the first behavioral experiment.

### First scenarios

1. **Peaceful growth:** abundant resources, war disabled.
2. **Food scarcity:** unequal food access and migration pressure.
3. **Energy asymmetry:** one faction must trade or invest in alternatives.
4. **Border pressure:** overlapping influence without automatic combat.
5. **Shock recovery:** destruction of food/power infrastructure.
6. **War cost:** identical factions with and without prolonged mobilization.

### Metrics

- population and growth;
- workforce allocation;
- food/energy security;
- housing utilization;
- prosperity;
- stability;
- research rate;
- migration pressure;
- trade volume and dependency;
- time at peace/war;
- civilian and military losses;
- recovery time after shock.

### Acceptance

- four factions can coexist without automatic war;
- at least one faction can prosper without combat;
- food/housing shortages change population outcomes;
- building choices visibly change settlement output;
- research unlocks a useful civil capability;
- war requires a diplomatic transition;
- military losses affect workforce/population;
- observer telemetry explains why a faction grows, stagnates, migrates, or
  collapses.

## What not to copy

- Civilization names, text, assets, or trademarked presentation;
- OpenCiv's browser/network architecture;
- turn-based timing;
- incomplete code paths;
- a victory-first design where every system exists mainly to increase conquest
  score.

If actual MIT-licensed code is ever adapted rather than independently
implemented, preserve copyright and license attribution and record exact file
provenance.
