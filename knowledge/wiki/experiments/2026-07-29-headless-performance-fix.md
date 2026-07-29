---
title: Headless Dummy-Audio Performance Fix
status: complete
updated: 2026-07-29
sources:
  - ../../raw/experiments/2026-07-29-headless-performance-fix.csv
  - ../../../engine-patches/openra-headless.patch
  - 2026-07-29-headless-runtime.md
tags:
  - experiment
  - headless
  - performance
  - profiling
---

# Headless Dummy-Audio Performance Fix

## Purpose

Find and remove the dominant cost that left the correct no-device runtime at
0.738× real time, then revalidate determinism and graphical compatibility.

## Configuration

- Date: 2026-07-29
- Code commit: `d266049646aeea47349bebbb56610c5e9e3153d6`
- Engine commit: `12f7ca7324a8dfc2e92a03d207a37b5bb905f923`
- Machine: Apple M4 Max, macOS Arm64, .NET 8.0.29
- Map: Cold Rage
- Bots by slot: aggressor, economist, aggressor, economist
- Seed: 424242
- Benchmark horizon: 1,500 ticks / 30 simulated seconds
- Profiler: `dotnet-trace` 9.0.661903,
  `dotnet-sampled-thread-time,gc-verbose`
- Compact evidence:
  [2026-07-29-headless-performance-fix.csv](../../raw/experiments/2026-07-29-headless-performance-fix.csv)

## Profile result

The 15-second managed trace attributed:

- 87.68% of the sampled main-thread interval to `Sound.LoadSound`;
- 87.49% to construction of `OggFormat`;
- 90.70% to the underlying OGG page reader.

`HeadlessSound` correctly reported completion immediately. The normal
`Sound.Tick` music lifecycle interpreted that as “song finished,” advanced the
playlist, decoded another OGG, and repeated. The no-op backend therefore
performed expensive work whose result it could never play.

## Change

When `Sound.DummyEngine` is true:

- individual world/UI sound playback returns before touching the lazy sound
  cache;
- `PlayMusicThen` and `PlayMusic` return before opening or decoding media.

Graphical and real audio backends are unchanged. This is a narrow
device-capability guard, not a change to synchronized gameplay.

## Results

| Measure | Before | After A | After B |
|---|---:|---:|---:|
| Wall seconds | 40.66 | 4.73 | 4.86 |
| Real-time multiple | 0.738× | 6.342× | 6.173× |
| World tick | 1,500 | 1,500 | 1,500 |
| State hash | `0AC799D4` | `0AC799D4` | `0AC799D4` |

The conservative repeated result is 6.173× real time, above the Sprint 2
minimum of 5×. Relative to the recorded pre-fix run, wall time fell by 88.0%
and throughput improved by 8.36 times.

The optimized headless artifact matched the 1,500-tick graphical artifact
after normalization. A second optimized headless artifact also matched.
Schema v1 validation and the complete Release/OpenHV test suite passed.

## Interpretation

The 5× performance gate is closed and Sprint 2 is complete. The preferred
logic-only client did not require a standalone simulator; profiling found a
backend lifecycle defect at the boundary between the normal audio manager and
the dummy device.

The aspirational 20× target is not claimed. The batch follow-up measures
end-to-end startup/short-horizon throughput across four maps; later-game
horizons, where unit count, bot work, and pathfinding can become more
expensive, remain open.

## Follow-up

The manifest-driven isolated runner and mixed-map soak are complete; see
[Resumable Batch Runner v1 Validation](2026-07-29-batch-runner-v1.md).
Sequential and four-worker throughput, failure classes, resume, and replay
artifacts are now measured. Peak memory and later-game scaling remain open and
must be measured alongside Telemetry Schema v1. Any additional engine
optimization must continue to pass the long graphical/headless equivalence
harness.
