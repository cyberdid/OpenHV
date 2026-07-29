---
title: Resumable Batch Runner v1 Validation
status: complete
updated: 2026-07-29
sources:
  - ../../raw/experiments/2026-07-29-batch-runner-v1.csv
  - ../../../run-batch.py
  - ../../../schemas/simulation-batch-manifest-v1.schema.json
  - ../../../batch-manifests/smoke-v1.json
  - ../../../batch-manifests/soak-100-v1.json
  - ../../../batch-manifests/interruption-v1.json
  - ../../../batch-manifests/isolation-v1.json
  - ../../../tests/test_batch_runner.py
  - ../decisions/0005-process-isolated-resumable-batches.md
tags:
  - experiment
  - batch
  - reliability
  - replay
---

# Resumable Batch Runner v1 Validation

## Purpose

Validate that one deterministic headless match can be promoted into a safe,
unattended experiment system: declarative scheduling, one process per attempt,
bounded concurrency, schema validation, failure isolation, selective retry,
signal cancellation, lossless resume, provenance, and replay diagnostics.

## Configuration

- Date: 2026-07-29
- Code commit: `5f1162f1c767e79148ef775279b5e87a4b83dc9f`
- Engine commit: `12f7ca7324a8dfc2e92a03d207a37b5bb905f923`
- Git tree recorded by every final run: clean
- Machine: Apple M4 Max, 14 logical CPUs, macOS Arm64
- Runtime: .NET SDK 8.0.423 / host 8.0.29, Python 3.13.2
- Bots: aggressor, economist, technologist, fortress for the soak; two-bot
  fixtures for interruption and invalid-input isolation
- Soak maps: Cold Rage, Doubles, Tournament Island, A Nuclear Winter
- Soak seeds: 6001–6100, rotating maps in a fixed explicit 100-entry schedule
- Soak horizon: 100 world ticks per match
- Replay smoke horizon: 500 world ticks per match
- Compact immutable evidence:
  [2026-07-29-batch-runner-v1.csv](../../raw/experiments/2026-07-29-batch-runner-v1.csv)

The soak horizon is intentionally short. It tests process/artifact
infrastructure and startup diversity, not late-game strategy or memory
behavior.

## Reproduction

Install the one Python dependency and run the tracked manifests:

```sh
python3 -m pip install -r requirements-simulation.txt

./run-batch.py batch-manifests/smoke-v1.json
./run-batch.py batch-manifests/soak-100-v1.json --workers 1
./run-batch.py batch-manifests/soak-100-v1.json --workers 4
```

Resume uses the same manifest and run root:

```sh
./run-batch.py batch-manifests/soak-100-v1.json --workers 4 --resume
```

The final evidence used fresh temporary result roots. The checked-in soak
manifest resolves to schedule hash
`be9a5c777bf8fd2fa053fc273247891ccf3ca112e53efb93f613c7a9d6130b39`.

## Contract and artifact model

Manifest Schema v1 rejects unknown fields, duplicate bot entries, a disabled
batch watchdog, out-of-range execution controls, and invalid identifiers.
Defaults and per-match overrides resolve into explicit immutable configs.
Missing IDs derive from schedule position and a canonical config hash. The
resolved schedule hash covers synchronized match inputs but excludes worker,
retry, timeout-grace, and replay-sampling controls, so safe execution controls
may change on resume without pretending the match schedule changed.

Each run contains:

```text
<results-root>/<run-id>/
  manifest.json
  resolved-manifest.json
  runtime.json
  summary.json
  runner.log
  sessions/<session-id>.json
  matches/<match-id>/
    config.json
    status.json
    result.json
    attempt-N.json
    attempt-N.stdout.log
    attempt-N.stderr.log
    attempt-N.orarep
    attempt-N-support/
```

`attempt-N.orarep` is retained for every failed attempt when OpenRA produced
one and for successful matches selected by
`successfulReplaySampleEvery`. Failure support directories retain OpenRA logs
even when a replay was not created. Successful unsampled support directories
are removed after artifact harvesting. Attempt numbering considers every
partial attempt artifact, so a crash between child exit and status write
cannot cause resume to overwrite prior evidence.

Writes of config, status, summary, session, and resolved schedule use
same-directory atomic replace. Original manifest and resolved schedule are
immutable. Runtime metadata records code/engine/machine provenance separately
from synchronized match inputs.

## Failure and retry semantics

| Class | Batch status | Automatic retry |
|---|---|---|
| Valid game end, including an observation tick limit | `completed` | no |
| Unknown map/bot or malformed launch input | `invalid-configuration` | no |
| Crash, desync, missing/invalid result, non-zero process exit | `failed` | bounded |
| Internal or hard runner watchdog | `failed` | bounded |
| External `SIGINT`/`SIGTERM` | `interrupted` | only on explicit resume |
| Unexpected runner exception | `failed` / `runner-error` | no |

Exit code `0` means every match completed, `2` means the batch reached a
diagnosable non-completed terminal state, and `130` means signal interruption.
Only infrastructure classifications consume
`maxInfrastructureRetries`; a deliberately invalid match therefore remained
at attempt 1 even with a retry budget of 2.

## Results

| Scenario | Workers | Completed | Attempts | Wall seconds | Result |
|---|---:|---:|---:|---:|---|
| Four-map replay smoke | 1 | 4/4 | 4 | 16.548 | pass |
| 100-match sequential soak | 1 | 100/100 | 100 | 364.466 | pass |
| Sequential resume | 1 | 100/100 skipped | 100 | 364.653 cumulative | pass |
| 100-match controlled parallel soak | 4 | 100/100 | 100 | 98.061 | pass |
| Parallel resume | 4 | 100/100 skipped | 100 | 98.255 cumulative | pass |
| Signal interruption | 2 | 2 complete, 2 interrupted | 4 | 4.971 | pass |
| Signal resume | 2 | 4/4 | 6 | 10.266 cumulative | pass |
| Invalid-match isolation | 2 | 1 complete, 1 invalid | 2 | 4.035 | pass |

The four-worker soak was 3.717× faster than sequential execution, or 92.9%
parallel efficiency. It completed 61.19 matches/minute versus 16.46
matches/minute sequentially on this short startup-heavy fixture.

Both 100-match runs produced exactly 100 config, result, status, and attempt
metadata files; every result passed Schema v1. No retry, crash, timeout,
desync, missing result, invalid result, or retained success support directory
occurred. Each resume skipped exactly 100 completed matches and created no new
attempt.

The signal fixture completed two matches, terminated both active process
groups, retained their support logs, returned 130, and left no OpenRA child
process. Resume preserved the two completed attempt-1 artifacts and ran only
the two interrupted matches as attempt 2.

The replay smoke retained four approximately 30 KiB replays. OpenRA
`--replay-metadata` read all four and reported `FinalGameTick=500`. Their
synchronized state hashes were:

- Cold Rage: `52A8D8F4`
- Doubles: `6FCA22C7`
- Tournament Island: `E2FDCBB1`
- A Nuclear Winter: `F5D0ED14`

Calling terminal world finalization after result capture corrected replay
metadata from an initially discovered tick 0 to tick 500. Normalized result
artifacts before and after that correction were identical on all four maps,
so the change affected replay finalization, not synchronized simulation state.

## Automated validation

`make test-simulation` runs seven integration tests covering:

- infrastructure-only retry and terminal invalid-configuration isolation;
- manifest watchdog validation;
- resolved-schedule drift rejection;
- partial-attempt preservation without number reuse;
- hard process timeout classification;
- signal interruption followed by resume;
- the signal race between “starting” and child-process registration.

The complete Release build, MiniYAML/map/Fluent/sprite tests, explicit-interface
check, and conditional-trait-interface check also passed. `make check` still
reports pre-existing analyzer warnings in the headless patch and result code,
but returns success with zero errors.

## Interpretation

SIM-006 and SIM-007 are complete. The runner is a reliable laboratory
boundary, not merely a loop around a shell script. Process, support-directory,
artifact, schedule, and failure isolation are explicit and tested.

The 100 short matches are not evidence of AI balance. Every soak match ended at
its declared tick limit, so any score leader is not a natural winner. The
experiment also does not claim late-world stability, peak-memory bounds, a
natural-victory distribution, or telemetry overhead.

## Next experiment

Implement Telemetry Schema v1 and measure its overhead against this batch
baseline. Then add longer observation fixtures that exercise later-game unit
counts, memory, natural victory, stalemate advisory logic, and the first
peaceful Living Factions vertical slice.
