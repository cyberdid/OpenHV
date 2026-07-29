---
title: "Decision 0005: Use process-isolated resumable batches"
status: accepted
updated: 2026-07-29
sources:
  - ../architecture.md
  - ../experiments/2026-07-29-batch-runner-v1.md
  - ../../../run-batch.py
  - ../../../schemas/simulation-batch-manifest-v1.schema.json
tags:
  - decision
  - architecture
  - batch
  - reliability
---

# Decision 0005: Use process-isolated resumable batches

## Context

OpenRA retains substantial global/static runtime state and owns native,
network, support-directory, replay, and logging lifecycles. Reusing one process
for many matches would be faster to start but would create an additional
correctness surface: every singleton, cache, event subscription, task, file
handle, and native resource would need a proven reset contract.

Experiments also need cancellation, partial-failure diagnosis, reproducible
resume, and the ability to change safe worker controls without changing the
synchronized match schedule.

## Decision

Run every match attempt in a new OS process and process group. Give every
attempt its own OpenRA support directory, stdout/stderr logs, metadata, and
optional replay. Resolve a declarative Schema v1 manifest into an immutable
schedule with stable IDs, config fingerprints, and a schedule hash.

The runner:

- validates every successful result against Result Schema v1 and its resolved
  match config;
- atomically maintains status, session, and aggregate artifacts;
- skips only completed matches whose stored result still validates;
- never reuses an attempt number when any partial attempt artifact exists;
- retries only bounded infrastructure failures;
- treats invalid configuration as terminal;
- terminates the whole match process group on watchdog or signal;
- records signal interruption as resumable state;
- permits worker/retry/grace/replay-sampling changes on resume while rejecting
  synchronized schedule drift;
- retains failure diagnostics and samples successful replays at a configured
  interval.

## Why

The process boundary is simpler to audit than an in-process reset boundary and
contains crashes, leaks, global state, native resources, and logs to one
attempt. A 100-match sequential soak and a 100-match four-worker soak both
completed without retries. Four workers achieved 3.717× speedup, showing that
correctness isolation does not prevent useful throughput.

Stable resolved configs and append-only attempt artifacts make recovery
conservative: uncertain or incomplete work is rerun under a new attempt
number, while validated completed work is never overwritten.

## Consequences

- Startup cost is paid once per match. Short 100-tick fixtures are therefore
  startup-heavy; longer simulations should amortize it.
- The worker count must remain controlled because each worker owns a full
  OpenRA client/server world.
- Batch directories can become large when many failures retain support logs or
  when replay sampling is aggressive.
- Abrupt pre-game failure may not produce a replay; support logs and captured
  stdout/stderr remain the guaranteed diagnostic artifacts.
- Telemetry/event files will be added inside the same per-match boundary
  without changing the scheduling model.
- A future in-process runner must prove full state-reset equivalence and a
  material throughput benefit before replacing this design.

## Revisit conditions

Reconsider the process-per-match boundary only if startup dominates meaningful
long-horizon workloads, controlled concurrency cannot meet experiment
throughput, and an in-process prototype passes deterministic equivalence,
resource cleanup, failure isolation, and resume tests at least as strong as
the current suite.
