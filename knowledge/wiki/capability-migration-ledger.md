---
title: Capability Migration Ledger
status: current
updated: 2026-08-02
sources:
  - universe-north-star.md
  - decisions/0006-single-dotnet-universe-runtime.md
  - ../raw/coruscantsim-analysis-2026-07-31.md
  - ../../../CoruscantSim/run_all_tests.py
  - ../../OpenRA.Mods.HV/Traits/Player/CivilizationState.cs
tags:
  - migration
  - parity
  - architecture
---

# Capability Migration Ledger

This is the enforceable meaning of “keep all functionality.” `reference` means
the source still owns the behavior; `partial` means OpenHV has part of it;
`native` means an OpenHV implementation exists; only `verified` permits source
runtime retirement.

## Planet, life, and society

| ID | Capability and source | C# destination | Gate | Status |
|---|---|---|---|---|
| PHY-01 | Canonical planet configuration and calibration (`config.py`) | `Universe/Planet/PlanetDefinition` | config snapshot + validation | reference |
| PHY-02 | Radiation, greenhouse, surface/atmosphere energy (`planet_physics.py`) | `PlanetPhysicsState/System` | temperature range + energy residual golden suite | reference |
| PHY-03 | Pressure, Coriolis, wind, CFL stepping | `AtmosphereSystem` | forcing response + stability invariants | reference |
| PHY-04 | Evaporation, clouds, condensation, precipitation, water fixer | `HydrologySystem` | water conservation + precipitation parity | reference |
| PHY-05 | Diagnostics, calibration, sweeps, texture fields | native telemetry + headless scenario tools | reproducible diagnostic export | reference |
| BIO-01 | Habitability, biomass, complexity (`biosphere.py`) | `BiosphereState/System` | barren/no-life + growth/carrying-capacity tests | reference |
| BIO-02 | Food web, species competition, mutation, migration | `EcosystemState/System` | deterministic diversity and extinction invariants | new |
| EMR-01 | Race emergence and inherited traits (`emergence.py`) | `SapienceEmergenceSystem` | different planets produce different traits | reference |
| CIV-01 | Population, needs, housing, jobs, resources | extend `CivilizationState` | conservation + scarcity/growth tests | partial |
| SOC-01 | Expected SoL, radicals, hope, unrest, turmoil | `SocialCohesionState/System` | Tocqueville + turmoil logistics tests | reference |
| SOC-02 | Migration from gradients and crises | `MigrationSystem` | population transfer conservation | reference |
| FAC-01 | Faction influence, territory, survive/expand/trade/consolidate BT | `TerritorySystem` + Civilization AI | deterministic control + action tests | partial |
| ECO-01 | Cell specialization, production, diffusion economy | `PlanetEconomySystem` | stock/yield conservation and specialization effects | partial |
| LOG-01 | Entity convoys, cargo, piracy, delivery | extend `TradeManager` + physical transports | sent=delivered+lost+transit | partial |
| POL-01 | Policy sliders, laws, voting, promises | `GovernmentState/System` | neutral policy, vote, deadline consequences | reference |
| DRM-01 | Synapse/feral/dormant storyteller | `StorytellerSystem` | distinct deterministic pacing + relief effects | reference |
| CHR-01 | Event-born characters, memory, actions, rise/fall | `CharacterState/System` | identity/memory/cap/action tests | reference |
| EVT-01 | Event spine, JSONL, eras, chronicle | extend simulation events + native chronicle | same-seed history + replay linkage | partial |

## Civilization, RTS, and observation

| ID | Capability and source | C# destination | Gate | Status |
|---|---|---|---|---|
| AGE-01 | Prehistory-to-Imperium progression | `TechnologyEraSystem` | condition-gated full ladder, no timer unlocks | new |
| RES-01 | Research graph and production modifiers | extend civil research | reachable unlocks + real actor effects | native |
| DIP-01 | Neutrality, war/peace, exhaustion, treaties | extend `DiplomacyManager` | relationship masks + reason-coded transitions | native |
| TRD-01 | Stock-backed bilateral trade | extend `TradeManager` | real transfer, risk, war suspension | native |
| WAR-01 | Construction, units, pathfinding, combat, bases | OpenHV actors/world | existing MiniYAML/build/simulation suites | native |
| WAR-02 | Mobilization and civilian cost of losses | `CivilizationState` + planner | workforce/population/stability consequences | native |
| AI-01 | Aggressor/economist/technologist/fortress/steward profiles | modular bots + Civilization AI | statistically distinguishable strategies | native |
| VIS-01 | Scientific globe and climate/data overlays (`index.html`) | native planet/system renderer | all layers visible at planet zoom | reference |
| VIS-02 | Whole-world command map (`city.html`) | semantic planet renderer | borders, cities, routes, events, inspection | reference |
| VIS-03 | Living RTS diorama (`kingdoms.html`) | actual OpenHV surface | no proxy actors; same authoritative entities | partial |
| VIS-04 | Chronicle and characters (`chronicle.html`) | native chronicle UI | filterable eras/events/characters | reference |
| VIS-05 | Vertical city/planet strata (`city_levels.html`) | native strata/underground view | five layer classes or explicit superseding model | reference |
| OBS-01 | Results, telemetry, events, batch, headless, hashes | existing simulation toolchain | full suite + schema validation | native |
| SAV-01 | Persistent save/load across geological-to-space history | Universe save schema + replay checkpoints | save/reload hash equality | partial: native checkpoint/resume + Universe parity; legacy AI hash parity pending |
| BRG-01 | Planet/battle contracts and deterministic request replay | developer import/replay tools | schema tests retained; no product click dependency | native |

## Space and multi-planet scope

| ID | Capability | C# destination | Gate | Status |
|---|---|---|---|---|
| SPC-01 | Orbital industry and spaceflight threshold | `OrbitalState/System` | civilization cannot launch before material/tech gates | new |
| SPC-02 | Ships, freighters, frigates, carriers, flotillas | actors + `FleetState/System` | construction, fuel, cargo, damage, command | partial assets/RTS |
| SPC-03 | Orbital/interplanetary routes and travel time | `OrbitalNetwork` | mass/cargo conservation + deterministic arrival | new |
| SPC-04 | Discovery, colonization, interplanetary trade/diplomacy/war | `InterplanetaryRelations` | observable autonomous first contact | new |
| MUL-01 | Three planet states and native races | `StarSystemState.Planets[3]` | isolated evolution + distinct emergent traits | partial: stable slots/clock native; planet state/races pending |
| INV-01 | Late Tyranid fleet, landing, biomass consumption, adaptation | invasion scenario systems + swarm actors | autonomous trigger through planetary consequences | partial assets/lore |

## Parity closure rule

The fourteen CoruscantSim suites—physics, integration, civilization, factions,
economy, events, social, storyteller, characters, policy, transport,
biosphere, emergence, and campaign bridge—remain required evidence. Each must
be replaced by a C# invariant suite or a documented golden/statistical parity
test before CoruscantSim can stop being a runtime reference.
