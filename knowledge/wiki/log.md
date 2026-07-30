# Project Log

This file is append-only. Entries summarize durable project events and link to
the pages where current knowledge is maintained.

## [2026-07-29] change | Autonomous simulation launcher

Added a hands-off OpenHV match mode in which the local client spectates and AI
players occupy the combat slots. Recorded in
[Architecture](architecture.md) and
[Decision 0001](decisions/0001-openhv-foundation.md).

## [2026-07-29] change | Four AI profiles and tournament runner

Added aggressor, economist, technologist, and fortress profiles; configurable
seed and duration; JSON results; and multi-map tournament aggregation. See
[AI profiles](ai-profiles.md) and [Architecture](architecture.md).

## [2026-07-29] experiment | Baseline four-profile tournament

Completed 10/10 timed matches across four maps. Technologist and Aggressor each
led three cutoffs; the short run validated infrastructure but not combat
balance. See the
[baseline experiment](experiments/2026-07-29-baseline-tournament.md).

## [2026-07-29] ingest | Karpathy LLM Wiki pattern

Adopted a persistent, Git-backed project memory with immutable sources,
agent-maintained synthesis, an index, an append-only log, and explicit
maintenance workflows. See
[Decision 0002](decisions/0002-persistent-project-wiki.md) and the
[source record](../raw/karpathy-llm-wiki-2026-07-29.md).

## [2026-07-29] query | Detailed execution plan

Expanded the compact roadmap into an evidence-gated plan from deterministic
headless execution through telemetry, strategic AI, faction identity,
automated improvement, and a persistent world. Local engine inspection showed
that the dedicated server does not advance `World` and that the normal client
loop couples logic and rendering, making a logic-only client runtime the first
recommended spike. See the [detailed execution plan](execution-plan.md).

## [2026-07-29] ingest | OpenCiv civilization concepts

Inspected `RyanGrieb/OpenCiv` at commit
`113eb908ffda2f50ff5c0d5a4a0bbe697904293a`. Its city population, worked
territory, differentiated yields, resources, building data, and civilization
configuration are useful conceptual references, but the TypeScript turn-based
implementation is incomplete and is not merged into OpenRA. See the
[source record](../raw/openciv-2026-07-29.md).

## [2026-07-29] decision | Living factions before war

Reframed the project from an autonomous war tournament into a real-time
simulation of living societies. Added population, needs, settlements, civil
economy, research, trade, migration, and dynamic diplomacy as core product
systems. Warfare remains possible but costly and non-mandatory. See
[Decision 0003](decisions/0003-living-factions-before-war.md) and
[Living Factions Design](faction-life.md).

## [2026-07-29] change | Commit and push workflow

The user established a standing delivery rule: meaningful completed work must
update the project wiki, pass relevant validation, be committed as a focused
change, and be pushed to the user-owned GitHub feature branch. Pull requests
remain a separate explicit action. The operational rule is recorded in
[AGENTS.md](../../AGENTS.md).

## [2026-07-29] change | Simulation contract v1 and deterministic cutoff

Completed Sprint 1 / SIM-001–002. Added strongly typed versioned
configuration/results, stable end reasons, JSON Schema, atomic output,
pre-match input validation, build/map/slot metadata, synchronized state hash,
and separate natural-winner/score-leader semantics. Replaced the primary
wall-clock duration with an exact `WorldTick` horizon; wall clock is now only a
deadlock watchdog. Deterministic lobby colors remove the remaining cosmetic
configuration drift between paired runs. The tournament aggregate now reports
end reasons, natural wins, and score leads separately. See
[Architecture](architecture.md) and
[Simulation Contract v1 Validation](experiments/2026-07-29-simulation-contract-v1.md).

## [2026-07-29] experiment | Contract v1 repeat and failure paths

Two graphical matches with map Cold Rage, seed 424242, and a 50-tick horizon
both stopped at tick 50 with sync hash `4F20B62A`. Normalized artifacts matched,
both passed JSON Schema v1, and an unknown bot failed before the match with a
non-zero process result. Compact evidence is preserved in
[the experiment record](experiments/2026-07-29-simulation-contract-v1.md).

## [2026-07-29] change | Deterministic headless client runtime

Completed SIM-003–005. Added an idempotent, version-pinned OpenRA SDK patch,
no-op graphics/sound platform, unpaced headless loop, deterministic local
server RNG, execution-mode metadata, headless-default tournament runs, and
graphical/headless parity automation. A lobby-start polling fallback also
removed an event-order race. See [Architecture](architecture.md) and
[Decision 0004](decisions/0004-logic-only-headless-runtime.md).

## [2026-07-29] correction | Separate bot randomness from renderer cosmetics

Longer parity testing disproved the earlier assumption that seeding the lobby
alone made bot behavior repeatable. OpenRA bot modules used the same local RNG
as renderer effects; graphical and headless runs consumed different sequences
before later strategic choices. Added a seed-derived `World.BotRandom` and
routed stock/OpenHV bot modules through it. This correction is documented in
the
[headless runtime experiment](experiments/2026-07-29-headless-runtime.md).

## [2026-07-29] experiment | Headless parity and performance

Graphical, headless, and repeated headless matches on Cold Rage with seed
424242 all stopped at tick 1,500 with synchronized hash `0AC799D4`; normalized
artifacts matched and passed Schema v1. Headless initialized no SDL, OpenGL, or
audio backend. The 30 simulated seconds took 40.66 wall seconds (0.738×), so
the 5× performance gate remains open. See
[Deterministic Headless Runtime Validation](experiments/2026-07-29-headless-runtime.md).

## [2026-07-29] experiment | Dummy-audio performance profile

A 15-second managed CPU/GC trace attributed 87.49% of the sampled main-thread
interval to OGG construction beneath `Sound.LoadSound`. The dummy sound
reported tracks complete immediately, causing the normal playlist lifecycle to
decode another file repeatedly. This replaced the earlier hypothesis that
synchronized gameplay was already the dominant cost. See
[Headless Dummy-Audio Performance Fix](experiments/2026-07-29-headless-performance-fix.md).

## [2026-07-29] change | Close the Sprint 2 throughput gate

Added a dummy-engine capability guard before lazy effect/music loading.
Repeated 1,500-tick runs fell from 40.66 seconds to 4.73 and 4.86 seconds
(6.342× and 6.173× real time) while preserving hash `0AC799D4`, graphical
parity, Schema v1 validity, and the full test suite. Sprint 2 is complete; the
next active work is the isolated manifest batch runner.

## [2026-07-29] change | Process-isolated resumable batch runner

Completed the core of SIM-006–007 at commit
`06ac1a31f8cedbb9cdc337d36d9935c80a517a5f`. Added strict Manifest Schema v1,
stable resolved schedules and config fingerprints, one process group per
match, worker limits, hard watchdogs, infrastructure-only retry, atomic
status/session/summary artifacts, validated aggregation, exit codes, signal
handling, cumulative resume, and synthetic failure-path integration tests.

## [2026-07-29] correction | Close cancellation and replay artifact gaps

An immediate-signal regression test exposed a race between logging a match
start and registering its child process; a post-registration cancellation
check now prevents escaped children. Artifact review then showed that shared
OpenRA support state mixed logs/replays and that artificial cutoffs produced
readable replays with `FinalGameTick=0`. Commit
`5f1162f1c767e79148ef775279b5e87a4b83dc9f` added per-attempt support
isolation, failure diagnostics, configurable replay samples, partial-attempt
number preservation, and terminal world finalization. Four replay smoke
artifacts now report tick 500 while normalized simulation results remain
identical.

## [2026-07-29] experiment | Close the Sprint 3 batch gate

On clean commit `5f1162f1`, the tracked four-map schedule completed 100/100
sequential matches in 364.466 seconds and 100/100 four-worker matches in
98.061 seconds, with no retries or infrastructure failures. Four workers
delivered 3.717× speedup and 92.9% efficiency. Resume skipped all 100 without
new attempts. A signal run preserved two completions, interrupted two active
process groups, left no child, and resumed only the unfinished matches. A
terminal invalid bot remained at attempt 1 while its valid neighbor completed.
See the
[batch experiment](experiments/2026-07-29-batch-runner-v1.md) and
[Decision 0005](decisions/0005-process-isolated-resumable-batches.md).

## [2026-07-29] change | Complete SIM-006–007

Sprint 3 is complete. The project now has a repeatable unattended experiment
boundary with exact manifests, code/engine provenance, isolated attempts,
validated results, retained failure evidence, replay sampling, conservative
resume, and checked-in smoke/soak/failure fixtures. The next active work is
Telemetry Schema v1 together with the first observable Living Factions civil
state.

## [2026-07-29] lint | Batch-stage wiki consistency

Validated all batch manifests against Schema v1, confirmed the checked-in soak
schedule hash matches the executed schedule, parsed the raw experiment CSV
with a uniform 21-column shape, and checked all wiki frontmatter and relative
Markdown links. Removed stale “batch remains open” claims from compiled pages;
historical append-only log statements remain unchanged.

## [2026-07-29] change | Living Factions civil vertical slice

Completed LIFE-001–003. Added synchronized `CivilizationState`,
`SettlementCore`, and `CivilInfrastructure` traits; deterministic 250-tick
civil and 3,000-tick demographic pulses; explicit population cohorts,
workforce, jobs, housing, food, materials, energy, knowledge, stocks,
production, needs, prosperity, stability, and shortage mortality. Existing
buildings now contribute civil capacity and flows. Added synchronized
`balanced`/`scarcity` profiles and the non-attacking Steward AI control. See
[Living Factions Design](faction-life.md).

## [2026-07-29] change | Telemetry and Event Schemas v1

Added line-flushed periodic snapshots, synchronized checkpoint hashes,
complete civil state, and reason-coded lifecycle/founding/population/shortage
events. Result Schema v1 gained backward-compatible optional civil fields.
Batch retry/resume now preserves prior telemetry and event streams, covered by
the seven-test integration suite. See [Architecture](architecture.md).

## [2026-07-29] experiment | Balanced growth, scarcity, peace, and overhead

Balanced factions grew from 1,000 to 1,006 residents; the scarcity control
emitted a food shortage at tick 2,250 and fell to 992 at tick 3,000. Four
Steward AIs developed to tick 3,500 with zero kills/losses. Telemetry on/off
at 10,000 ticks preserved hash `748B3676`; observed user-CPU overhead was
0.58%. All JSON/JSONL artifacts passed Schema v1. See
[Living Factions and Telemetry v1 Validation](experiments/2026-07-29-living-factions-v1.md).

## [2026-07-29] lint | Sprint 4 contract and wiki consistency

Validated 19 wiki pages for required frontmatter, catalog inclusion, and
relative links; parsed the immutable six-row/23-column experiment CSV;
validated all five batch manifests; validated balanced, scarcity, and Steward
result/snapshot/event artifacts; and confirmed that a legacy Result v1 object
without the new optional civil fields still passes the backward-compatible
schema.

## [2026-07-29] correction | Hash civilization profile as an integer

The first post-publication runtime build showed that OpenRA rejects
`[VerifySync]` on strings. Replaced the civilization-profile sync member with
an integer code and derived string label, then completed a 6,500-tick run.

## [2026-07-29] change | Deterministic civil research graph

Implemented LIFE-004 knowledge spending and five technology unlocks with
food, energy, storage, housing, and research modifiers. Four Steward factions
completed agricultural systems at tick 5,250 and emitted reason-coded events.
See [Civil Research v1 Validation](experiments/2026-07-29-civil-research-v1.md).

## [2026-07-29] change | Engine-effective dynamic diplomacy

Completed DIP-001 with one synchronized relation per active faction pair,
default-neutral native OpenRA masks, grievance-driven war, loss-driven
exhaustion and peace, cooldowns, collapse handling, final/snapshot state, and
reason-coded transition events. Result Schema v1 remains backward compatible;
current telemetry and event schemas validate the new records. See
[Dynamic Diplomacy v1 Validation](experiments/2026-07-29-dynamic-diplomacy-v1.md).

## [2026-07-29] experiment | Neutral, war, peace, and repeat validation

Four Steward factions stayed neutral through tick 6,000 with zero combat. A
26,000-tick Rogue/Fortress run produced 11 rivalry wars and 11
exhaustion-peace transitions. Two identical 11,000-tick runs matched hash
`E5E13643`, final relations, transition counts, and combat totals. All
result/telemetry/event lines passed Schema v1 and all seven batch integration
tests passed.

## [2026-07-29] change | Stock-backed bilateral trade

Completed DIP-002 with the asymmetric `trade` profile, synchronized trade
enable/control option, demand reserves, real source subtraction and
destination addition, storage bounds, infrastructure throughput,
distance/third-party-war risk, direct-war suspension, directional cumulative
flows, route snapshots, and reason-coded state/shipment events. Batch
fingerprints now include `tradeEnabled`. See
[Stock-Backed Trade v1 Validation](experiments/2026-07-29-stock-backed-trade-v1.md).

## [2026-07-29] experiment | Trade benefit, determinism, and war suspension

Two 4,000-tick enabled runs matched hash `FA34F889` and moved the same 750
units in 75 reconciled shipments. Trade raised food satisfaction in deficit
settlements from the disabled control's 280 to 680 and prevented the observed
population decline from 1,000 to 984–992. A 6,000-tick conflict suspended
exactly five routes for five bilateral wars. All current artifacts passed
Schema v1; post-pulse stocks remained within storage.

## [2026-07-29] change | Multi-objective Civilization AI and war costs

Completed AI-001–002's first layer. `CivilizationState` now scores survival,
research, trade, security, recovery, and pair-specific war utility; selects
six observable strategies; budgets physical-input research; and replaces
fixed diplomacy pressure. Army value reserves workforce, active war increases
mobilization, death value removes adults, and war/casualties damage stability.
See
[Civilization AI and War Cost v1 Validation](experiments/2026-07-29-civilization-ai-war-cost-v1.md).

## [2026-07-29] experiment | Dependency-driven peace and recovery

With trade enabled, dependency reached 616 and an Aggressor/Economist scenario
kept all six pairs neutral; the identical no-trade control produced one war,
five casualties, survival/mobilization states, lower population, and minimum
stability 678. A paired war run matched hash `8431018E` with 382 mobilized
adults and 32 casualties. A longer case entered recovery at tick 10,000 after
peace, then returned to research as stability recovered.

## [2026-07-29] change | Deterministic scenario lifecycle

Completed SIM-009. Added explicit conflict/living-world modes, a declared
observation horizon separate from the hard tick ceiling, partial and total
faction-collapse recording, and advisory-first stalemate state with an
explicit terminating option. Lifecycle state now appears in final results,
telemetry, events, batch manifests, validation, and fingerprints. Civil
collapse checks run at a synchronized 250-tick cadence.

## [2026-07-29] experiment | Observation, collapse, and active-war guard

Two identical living-world observations ended at tick 2,000 with no winner and
hash `5578A2DB`. A partial-collapse case recorded two societies at tick 3,000
and continued to tick 4,000; a deliberate all-collapse fixture ended as
`faction-collapse`. A five-war case with terminating stalemate enabled reached
its hard tick limit without false termination. Five results, 61 snapshots, and
160 events passed Schema v1; all nine batch-runner integration tests passed.
See
[Scenario Lifecycle v1 Validation](experiments/2026-07-29-scenario-lifecycle-v1.md).

## [2026-07-29] lint | Scenario lifecycle publication gate

Ran the full `make check test test-simulation` gate: Release compilation,
explicit-interface checks, conditional-trait checks, every OpenHV map's
MiniYAML/Fluent validation, sprite-sequence validation, and nine batch
integration tests passed. Validated all 24 wiki pages for required
frontmatter, index coverage, and relative links; checked all four JSON schemas
against Draft 2020-12; parsed the immutable five-row/30-column lifecycle CSV;
and schema-validated every final lifecycle result, snapshot, and event.

## [2026-07-29] change | Freeze the SIM-010 baseline suite

Added a deterministic generator for a 112-match held-out matrix: four maps,
four cyclic profile/slot assignments, and seven seeds per cell. Added a
standard-library analyzer that rejects incomplete, mixed-commit, dirty, or
profile-invalid runs; writes match/player observations; and reports profile,
map, faction, and spawn distributions with Wilson and deterministic
2,000-resample bootstrap 95% intervals. Three focused tests protect schedule
balance, bilateral trade reconciliation, and repeatable aggregation.

## [2026-07-29] experiment | Complete the 112-match civil/military baseline

SIM-010 completed 112/112 clean-commit matches on attempt 1 in 522.827 seconds
with no infrastructure failure. Validated 112 results, 1,456 snapshots, 8,574
events, and four tick-12,000 replays. Fortress led societal resilience;
Aggressor produced the most damage but 12 collapses; Economist survived but
ended mobilized in 111 observations; Technologist gained no research edge and
had the highest casualties. Only 23 trade shipments occurred. Every match hit
the hard ceiling, so the natural-completion guardrail failed at 100% and timed
leaders remain non-winners. See
[Civil and Military Baseline 112 v1](experiments/2026-07-29-baseline-112-v1.md).

## [2026-07-29] lint | SIM-010 evidence publication gate

Validated all 112 results, 1,456 snapshots, and 8,574 events against Schema
v1; confirmed one clean simulation commit, exactly one attempt per match, no
stderr, and four readable tick-12,000 replays. All 13 simulation/batch/
analysis tests passed. Checked all 25 Wiki pages for frontmatter, index
coverage, and relative links; checked four JSON schemas; and parsed the
immutable one-row/37-column provenance and 20-row/23-column metric records.
The six-row/17-column paired-difference record preserves every quoted
within-match comparison.

## [2026-07-29] change | Executable civilization planners

Implemented AI-003's opening, economy, technology, and recovery layer.
Civilization plans now change synchronized civil production, choose
profile-specific technology paths, and issue bounded native production
requests for economic, information, technical, and repair units. Added final
state fields plus reason-coded plan transition/request events. A synchronized
per-type request budget prevents endless orders after units deploy, die, or
cannot enter a queue. See
[Civilization Planner AI-003](experiments/2026-07-29-civilization-planner-ai003.md).

## [2026-07-29] experiment | Freeze the paired AI-003 candidate gate

Two 12,000-tick smoke runs matched hash `DEB138AE` and identical final
plan/request metrics. Added a generated candidate manifest that reuses all 112
baseline map/slot/profile/seed cells, extended aggregation with plan fields,
and added an identity-checking paired comparison tool. Full held-out execution
is the remaining AI-003 acceptance step.

## [2026-07-29] experiment | Reject AI-003 candidate v1

The clean `bfee9494` exact-schedule candidate completed 112/112 matches on
attempt 1 with 112 valid results, 1,456 snapshots, 11,388 events, and four
tick-12,000 replays. It made plans visible and reduced total collapses from 15
to 13, but its research separation was an integer-rounding artifact:
non-Technologist knowledge pulses fell to zero while Technologist remained at
one technology. Economist prosperity/stability/workforce and Technologist
casualties/active wars regressed significantly. Candidate v1 is retained as a
failed hypothesis; v2 will correct production rounding, civilian mobilization,
and request budgets.

## [2026-07-29] change | Correct AI-003 candidate mechanics

Candidate v2 preserves positive one-unit knowledge pulses with rounded plan
production, triples Technologist knowledge directly, reduces external
production budgets, and excludes explicit civilian/support actor value from
military mobilization. Two 12,000-tick repeats matched hash `9419B52D`;
ordinary profiles retained a first technology and Technologist completed
three. The generated v2 manifest preserves all 112 baseline cells.

## [2026-07-29] experiment | Reject AI-003 candidate v2

The clean `0afb8b5c` matrix completed 112/112 attempt-1 matches with 112 valid
results, 1,456 snapshots, 10,959 events, and four tick-12,000 replays.
Technologist gained 1.464 technologies and Economist wellbeing/workforce
improved significantly, correcting v1. Promotion still failed: diplomacy
continued treating civilian/support value as army after mobilization stopped
doing so, and Technologist/Fortress wars, casualties, and retained army
regressed significantly. v3 will centralize that power calculation.

## [2026-07-29] change | Unify civil and diplomatic military value

Candidate v3 uses one civilian-aware military-value calculation for both
workforce mobilization and diplomatic relative power. Plan bonuses, research
order, request budgets, and tactical weights are unchanged. Two 12,000-tick
repeats matched `9419D0D9`; the generated exact-schedule v3 matrix is the final
AI-003 gate.

## [2026-07-29] experiment | Complete AI-003 with candidate v3

The clean `947fe446` matrix completed 112/112 attempt-1 matches in 508.710
seconds with 112 valid results, 1,456 snapshots, 10,955 events, and four
tick-12,000 replays. Technologist gained 1.464 technologies; Economist
prosperity, stability, and workforce improved significantly; collapse count
matched baseline and score-lead shares stayed inside 10–40%. The shared power
definition did not remove Technologist/Fortress war regressions, falsifying
that narrow hypothesis. AI-003 closes; measured war exposure, force loss, and
100% tick ceilings move to active AI-004.

## [2026-07-30] change | Implement the AI-004 combat planner

Added squad target scoring, a finishing bonus, and retreat/regroup/re-engage
with per-profile thresholds, captured the engine side in a tracked patch, and
exported the decisions on the synchronized blackboard as counters plus
reason-coded `combat-decision` events. Smoke evidence first showed zero
retreats: protection squads run about 65% of combat and never evaluated
retreat, and the profiles had widened the own-building flee veto above the
engine default. Both were corrected; two 12,000-tick repeats then matched
`7191667E` with retreats and regroups rising from zero.

## [2026-07-30] experiment | Reject AI-004 candidate v1

The clean `7a946325` exact-schedule matrix completed 112/112 matches in 9m15s
with 448 paired observations against baseline `3308752a`. Retreats, regroups,
re-engagements, and target selections rose significantly for every profile, so
the mechanism works. Neither acceptance target moved: all 112 matches still
ended at the tick ceiling and collapses rose from 15 to 27. Technologist lost
2,527 army while taking 3,983 more damage, Fortress lost 1,343 army and 4.1
more casualties, and both gained 0.7 active wars — the change reached the war
decision instead of staying tactical. Recorded as a falsified hypothesis; v2
must hold the war decision fixed while retreat changes. See
[Combat Planner AI-004](experiments/2026-07-30-combat-planner-ai004.md).

## [2026-07-30] experiment | Reject AI-005 regroup locality

A graphical match showed one bot standing beside its base at twelve regroups
against one each for the other two of its profile. AI-005 sent retreating
ground and protection squads to the nearest own building instead of a random
one, measured against a baseline re-run at the immediately preceding commit so
the two differ by that change alone. Both runs completed 112/112 in about 524
wall seconds. Three of eighty paired differences cleared 95%, all civil, which
is what noise looks like at eighty comparisons; every combat metric straddles
zero. The reason is frequency: Aggressor regroups 4.05 times per match,
Fortress 0.19, so where the squad walks cannot matter. New idle and committed
counters also rule out the competing explanation — the pool waiting at base
never averages above nine while squads hold eleven to twenty-nine. Fixing the
result schema was a precondition: `config.factions` had been emitted since
faction selection landed and was never declared, so every batch since would
have scored 112/112 invalid-result. See
[Regroup Locality AI-005](experiments/2026-07-30-regroup-locality-ai005.md).

## [2026-07-30] experiment | Accept ECON-001 levelling trade

Trade had moved nothing, ever: 672 routes across the 112-match baseline carried
zero goods. Shipments were capped by how far a partner sat below an absolute
reserve floor, and no settlement in 448 civilisations was ever below it — the
poorest recorded holdings were 600 food against a floor of 200. Trade now moves
half the gap between two holdings instead, and the same 672 routes carried
161,936. Forty of eighty paired differences cleared 95%, all telling one story:
Economist, Technologist and Fortress each gained army and stability while
shedding four to eight thousand in deaths and up to 1.5 active wars, and
Aggressor absorbed the difference — 1.69 more wars, 2,559 more deaths, 11,913
less score. No profile's score-lead rate moved. Both standing acceptance targets
are still untouched: 112/112 at the tick ceiling, collapses 18 to 19. See
[Levelling Trade ECON-001](experiments/2026-07-30-levelling-trade-econ001.md).
