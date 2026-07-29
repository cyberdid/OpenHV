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
