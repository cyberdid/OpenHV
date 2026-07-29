---
title: Civil Research v1 Validation
status: current
updated: 2026-07-29
sources:
  - ../../raw/experiments/2026-07-29-civil-research-v1.csv
  - ../faction-life.md
tags:
  - experiment
  - research
  - technology
---

# Civil Research v1 Validation

Four Steward AIs ran Cold Rage to tick 6,500 with seed 7701 and telemetry every
250 ticks. Each spent settlement knowledge deterministically, completed
`agricultural-systems` at tick 5,250, increased food output from 30 to 37 per
civil pulse, grew to population 1,012, and advanced 10/60 knowledge into
`energy-grid`. Final synchronized hash: `017BAB7D`.

The run also exposed that OpenRA cannot hash a `[VerifySync] string`. The
published profile state was corrected to a synchronized integer with a derived
label before continuing. The next test should cover the full five-node graph
and research opportunity costs under scarcity.
