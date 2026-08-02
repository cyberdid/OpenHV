---
title: Roadmap
status: current
updated: 2026-08-02
sources:
  - universe-north-star.md
  - universe-execution-plan.md
  - capability-migration-ledger.md
  - overview.md
  - architecture.md
  - ../../run-universe.sh
  - experiments/2026-07-29-baseline-tournament.md
  - experiments/2026-07-29-headless-runtime.md
  - experiments/2026-07-29-headless-performance-fix.md
  - experiments/2026-07-29-batch-runner-v1.md
  - experiments/2026-07-29-living-factions-v1.md
  - experiments/2026-07-29-civil-research-v1.md
  - experiments/2026-07-29-dynamic-diplomacy-v1.md
  - experiments/2026-07-29-stock-backed-trade-v1.md
  - experiments/2026-07-29-civilization-ai-war-cost-v1.md
  - experiments/2026-07-29-scenario-lifecycle-v1.md
  - experiments/2026-07-29-baseline-112-v1.md
tags:
  - roadmap
  - planning
---

# Roadmap

This page is the compact priority view. See the
[detailed execution plan](execution-plan.md) for implementation tasks,
dependency gates, metrics, experiment design, and delivery estimates.

## Active North Star program

The authoritative program is now the
[Universe execution plan](universe-execution-plan.md): one .NET/OpenHV state
graph, one complete planet first, three planets by final acceptance, autonomous
zero→life→race→civilization→space→interplanetary history, and same-world RTS.

Current phase: **Phase 0 — program lock and inventory**. The next executable
work package is **UNI-001**, which introduces Universe/System/three-Planet IDs,
the synchronized macro clock, lifeless planet-zero state, and result/telemetry
export without changing existing matches.

## Completed foundation

### P0 — Transitional one-engine runtime foundation

`run-universe.sh` is the current visual entry point. It launches one
OpenHV spectator process containing four autonomous civilizations and all
economic, civil, diplomatic, research, production, and combat systems. It has
no Web battle selection and no experiment watchdog. Natural victory or total
civil collapse begins the next deterministic epoch; manually closing the
window stops the launcher. The older Web→per-cell battle bridge remains a
compatibility experiment, not the product loop.

This proves observer-only single-process operation, but it begins with
pre-spawned civilizations. It is therefore a transitional foundation, not the
North Star's required zero-state planet history.

Simulation contract v1 and its deterministic graphical lifecycle are complete:

- versioned config/result DTOs and JSON Schema;
- exact synchronized world-tick cutoff with a separate wall-clock watchdog;
- pre-match map, bot, speed, seed, and limit validation;
- deterministic slot colors plus recorded faction, team, spawn, and home cell;
- atomic artifacts with separate natural winners and score leaders;
- paired-run, schema, and invalid-input validation.

See
[Simulation Contract v1 Validation](experiments/2026-07-29-simulation-contract-v1.md).

The headless correctness foundation is also complete:

- tracked, idempotent OpenRA SDK patching on normal build paths;
- no-op window/graphics/audio platform and unpaced logic loop;
- seed-derived lobby RNG and a renderer-independent bot RNG stream;
- 1,500-tick graphical/headless parity and repeat validation;
- dummy-audio profiling fix and repeated 6.173×+ real-time throughput.

See
[Deterministic Headless Runtime Validation](experiments/2026-07-29-headless-runtime.md).
The performance follow-up is
[Headless Dummy-Audio Performance Fix](experiments/2026-07-29-headless-performance-fix.md).

Reliable batch orchestration is complete:

- strict declarative manifest and immutable resolved schedule;
- one process/support directory per attempt;
- schema-validated aggregation, bounded retry, watchdog, signal cancellation,
  and conservative resume;
- retained failure diagnostics and configurable successful replay samples;
- 100/100 sequential and 100/100 four-worker exact-commit soaks;
- 3.717× four-worker speedup with no retry or infrastructure failure.

See
[Resumable Batch Runner v1 Validation](experiments/2026-07-29-batch-runner-v1.md).

## P0 — Headless simulation loop

Move autonomous match orchestration and result capture away from the rendered
client.

Acceptance:

- no game window or audio device;
- one command runs at least 100 sequential matches;
- deterministic reruns with the same map, composition, seed, and commit;
- failed matches are isolated and reported without losing completed results.

Status: complete. One-match no-device execution, deterministic cross-mode
parity, the 5× throughput gate, process/failure isolation, replay diagnostics,
resume, sequential soak, and controlled-concurrency soak all passed.

## P0 — Deterministic scenario lifecycle

Support both finite conflict scenarios and open-ended living-world observation
windows, with deterministic cutoffs and a wall-clock watchdog for deadlocks.

Acceptance:

- natural victory, faction collapse, observation horizon, stalemate, timeout,
  crash, and invalid-map outcomes are distinct;
- final world tick and the scenario-specific outcome are recorded;
- timed leaders are never labeled as natural winners.

Status: complete for SIM-009. The v1 identifiers, natural victory, observation
horizon, partial and total faction collapse, hard tick limit, advisory-first
stalemate detector, active-war guard, watchdog, invalid configuration,
crash/desync, retained failure artifacts, and external-cancel/resume paths
exist. Exact repeats preserved the observation hash; a reviewed five-war
scenario was not falsely terminated. Default collapse thresholds and the
first naturally raised stalemate advisory remain balance evidence questions,
not missing lifecycle plumbing. See
[Scenario Lifecycle v1 Validation](experiments/2026-07-29-scenario-lifecycle-v1.md).

## P0.5 — Living Factions vertical slice

Add an OpenRA-native civilization layer inspired by the useful concepts found
in OpenCiv: settlement population, worked territory, differentiated yields,
building effects, and civilization identity.

Acceptance:

- four factions can coexist without automatic war;
- population consumes food and responds to housing/shortages;
- settlements expose jobs, outputs, needs, prosperity, and stability;
- a faction can grow and research without combat;
- war requires an explicit relationship transition;
- military mobilization and losses affect civilian life.

See [Living Factions Design](faction-life.md).

Status: LIFE-001–004 and DIP-001 complete. Four capitals run synchronized civil
and demographic pulses; balanced growth, shortage mortality, explicit
building flows, peaceful Steward development, deterministic research unlocks,
default-neutral diplomacy, explicit war/peace, repeat hashes, and strategic
telemetry passed. Stock-backed trade stabilizes deficit settlements. Army
mobilization reduces workforce/production, combat losses remove adults, and
peace triggers recovery. Migration transfer remains open, so the full P0.5
acceptance gate is not yet complete.

## P1 — Strategic telemetry

Capture time-series and final metrics for population, needs, migration,
economy, construction, technology, trade, diplomacy, army composition,
territory, scouting, damage, and combat efficiency.

Acceptance:

- metrics have documented definitions and units;
- schema version is included in every result;
- tournament aggregation can compare profiles by map and spawn.

Status: foundation complete. Result Schema v1 now admits civil state, and
strict Telemetry/Event Schemas v1 validate snapshots and reason-coded civil
events. A 10,000-tick paired run preserved the synchronized hash with 0.58%
observed user-CPU overhead. Tactical production/scouting/combat events and
broader strategic dimensions remain open.

## P1 — Dynamic diplomacy and trade

Allow neutral factions to discover each other, exchange resources, form or
break agreements, develop grievances, declare war, and negotiate peace.

Acceptance:

- relationship changes are synchronized and reproducible;
- trade reflects real stocks, demand, routes, and risk;
- neutral factions are not auto-targeted;
- every treaty, grievance, and war transition is recorded;
- AI can rationally prefer peace, deterrence, or limited war.

Status: DIP-001–002 complete. Bilateral neutral/war/alliance state is synchronized;
neutral is the effective default; rivalry declares war through native OpenRA
enemy masks; losses drive exhaustion and peace; transition snapshots/events
validated; repeated inputs produced hash `E5E13643`. Trade moves real food,
materials, and energy under reserve/storage/capacity/risk constraints, and
war suspends the matching route. The first Civilization AI utility/state layer
is also complete: trade dependency changed a paired war outcome; research
spends physical inputs; mobilization, casualties, stability damage, and
post-war recovery are synchronized and observable. Tactical
build/retreat/target planners, richer treaties, and physical cargo remain
open.

## P1 — Automated experiment loop

Use controlled batches to test one configuration change at a time.

Acceptance:

- baseline and candidate use identical seed/map schedules;
- reports include uncertainty, not only point estimates;
- changes are kept only when they improve an explicit target without violating
  guardrails.

Status: first baseline complete. SIM-010 completed 112/112 attempt-1 matches
across its balanced map/slot/seed matrix with no infrastructure failure. The
analyzer reported Wilson/bootstrapped uncertainty, paired profile differences,
and map/faction/spawn sensitivity. Fortress was the most resilient;
Aggressor collapsed disproportionately on Doubles; Economist over-mobilized;
Technologist showed no technology edge. All matches hit the tick ceiling, so
AI-003–004 must be compared against this exact schedule before promotion. See
[Civil and Military Baseline 112 v1](experiments/2026-07-29-baseline-112-v1.md).

AI-003 is implemented and awaiting its full held-out gate. The runtime now
selects explicit opening, economy, technology, and recovery plans; couples
them to civil output trade-offs, profile-specific research ordering, and
bounded native production requests; and records plan decisions in final and
event telemetry. The candidate manifest reuses all 112 baseline map, slot,
profile, and seed cells exactly, and `compare-candidate.py` pairs every
match/profile observation before estimating differences.

Candidate v1 completed but was not promoted. It reduced total collapses from
15 to 13 and made final plans observable, yet integer rounding removed almost
all non-Technologist research; Economist wellbeing regressed and Technologist
paid significantly higher casualty/war costs without advancing beyond its
baseline one technology. AI-003 therefore remains active for a narrower v2
correction.

The v2 mechanics are implemented: positive round-up preserves ordinary
research, Technologist receives a direct 3× knowledge path, explicit
civilian/support actors no longer consume military mobilization, and native
request budgets are smaller. Two corrected repeats matched `9419B52D`; the
clean 112-match v2 report is the remaining AI-003 gate.

Candidate v2 is also retained but not promoted. It delivered a real
Technologist edge and substantially improved Economist wellbeing, but an
inconsistent military-power definition drove significant Technologist and
Fortress war/casualty regressions. v3 is limited to sharing the civilian-aware
army calculation between mobilization and diplomacy.

The v3 shared calculation is implemented and deterministic at `9419D0D9`;
its exact 112-match matrix is the final AI-003 promotion test.

AI-003 is complete after the clean v3 held-out matrix. It established distinct
economy and technology behavior without increasing total collapse or violating
score-lead share bounds. AI-004 then took up the measured Technologist/Fortress
war exposure and the 100% tick-ceiling rate.

AI-004 candidate v1 is implemented and rejected. Target scoring, a finishing
bonus, and retreat/regroup/re-engagement all fire significantly for every
profile, including the defending squads that run most of the combat and
previously never evaluated retreat at all. Neither acceptance target moved:
all 112 matches still ended at the tick ceiling and collapses rose from 15 to
27. Technologist and Fortress each gained 0.7 active wars while losing army, so
preserved squads fed the war utility instead of surviving quietly. v2 must hold
the war decision fixed while retreat changes — either exclude retreating
strength from relative power, or raise the war threshold by what the preserved
army adds. See
[Combat Planner AI-004](experiments/2026-07-30-combat-planner-ai004.md).

## P2 — Strong societal and faction identity

Progress from four parameter profiles to factions with distinct build orders,
needs priorities, institutions, technology paths, trade behavior, expansion
logic, diplomacy, scouting models, and counter-strategies.

Acceptance:

- telemetry demonstrates distinguishable behavior;
- each faction has strengths, weaknesses, and viable counters;
- no single faction dominates across the full map suite.

## P2 — Persistent world layer

Evaluate whether matches should remain independent or feed a larger world with
territory, diplomacy, adaptation, and faction history. This is intentionally
deferred until the core match simulator is fast and measurable.
