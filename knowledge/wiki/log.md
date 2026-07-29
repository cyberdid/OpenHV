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
