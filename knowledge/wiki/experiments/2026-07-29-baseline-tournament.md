---
title: Baseline Four-Profile Tournament
status: complete
updated: 2026-07-29
sources:
  - ../../raw/experiments/2026-07-29-baseline-tournament.csv
  - ../../../run-tournament.sh
tags:
  - experiment
  - baseline
  - balancing
---

# Baseline Four-Profile Tournament

## Purpose

Verify that four autonomous AI profiles can run reproducibly across multiple
maps, terminate without human input, and produce aggregate results.

## Configuration

- Date: 2026-07-29
- Code commit after implementation: `2b927897`
- Matches: 10
- Wall-clock duration limit: 30 seconds per match
- Bots: aggressor, economist, technologist, fortress
- Maps: Cold Rage, Doubles, Tournament Island, Yet Another Beautiful Winter
- Seeds: 20260730 through 20260739
- Command: `MATCH_COUNT=10 MATCH_DURATION=30 ./run-tournament.sh`
- Raw compact results:
  [2026-07-29-baseline-tournament.csv](../../raw/experiments/2026-07-29-baseline-tournament.csv)
- Full local run artifact:
  `/Users/helenshkirenko/Universe/tournament-results/20260729T072330Z/tournament.json`

## Results

| Profile | Timed score leads | Average score | Average army value |
|---|---:|---:|---:|
| Technologist | 3 | 12,918 | 295 |
| Aggressor | 3 | 12,828 | 315 |
| Fortress | 2 | 12,836 | 285 |
| Economist | 2 | 12,580 | 220 |

The spread between highest and lowest average score was about 2.7%, so no
profile dominated this short startup window.

## Interpretation

- The launcher, bot selection, map rotation, deterministic seeds, timeout, JSON
  writer, and aggregation pipeline all worked for 10/10 matches.
- Aggressor had the highest average army value, which is directionally
  consistent with its intended early-unit bias.
- The other score differences are too small and the matches too short to
  validate strategic identity.
- Every match ended on the time limit. `winner` therefore means composite-score
  leader at cutoff, not natural combat victory.

## Limitations

- Only about 23.6–24.6 simulated seconds elapsed per match.
- No meaningful kill/death evidence was observed.
- Ten matches are insufficient for balance conclusions.
- Map positions and random faction allocation may dominate the small sample.
- The graphical client adds startup cost to every match.

## Next experiment

Implement a headless or server-side runner, then run at least 100 matches long
enough to produce natural combat outcomes. Record faction, spawn, economy over
time, technology timing, army composition, damage, territory, and victory time.
