---
title: Stock-Backed Trade v1 Validation
status: current
updated: 2026-07-29
sources:
  - ../../raw/experiments/2026-07-29-stock-backed-trade-v1.csv
  - ../faction-life.md
  - ../../../OpenRA.Mods.HV/Traits/World/TradeManager.cs
tags:
  - experiment
  - trade
  - scarcity
  - determinism
---

# Stock-Backed Trade v1 Validation

## Purpose

Validate DIP-002 as physical civil-resource movement rather than a score bonus:
an exporter must own stock above reserve, an importer must have demand and
free storage, route throughput and risk limit the shipment, war suspends the
bilateral route, and cumulative flow reconciles with events. Measure whether
trade changes population outcomes under identical asymmetric production.

The immutable measurements are in
[2026-07-29-stock-backed-trade-v1.csv](../../raw/experiments/2026-07-29-stock-backed-trade-v1.csv).
The runs used base commit `a1d00c29` plus the Sprint 5 trade working tree; the
published implementation is the commit containing this record.

## Scenario

The synchronized `trade` civilization profile gives each faction one of three
specializations derived from stable client order:

- food exporter: high initial food and doubled food production;
- materials exporter: high initial materials and doubled materials production;
- energy exporter: high initial energy and doubled energy production.

Non-specialized outputs are quartered. This creates complementary geography
without inventing resources during exchange.

Each capital supplies 12 abstract throughput; a Trader supplies 48 more. A
route retains eight civil pulses of demand or a minimum stock reserve, ships
only surplus into an actual deficit, respects destination storage, and moves
at most effective route capacity. Capital distance and third-party wars add
risk; direct war sets risk to 1000 and suspends the route.

## Commands

The paired peaceful runs used:

```sh
SIMULATION_HEADLESS=true \
SIMULATION_BOTS=steward \
SIMULATION_SEED=7901 \
SIMULATION_MAX_TICKS=4000 \
SIMULATION_WATCHDOG_SECONDS=120 \
SIMULATION_TELEMETRY_INTERVAL_TICKS=250 \
SIMULATION_CIVILIZATION_PROFILE=trade \
SIMULATION_TRADE_ENABLED=true \
SIMULATION_RESULT=/tmp/trade-enabled/result.json \
./run-simulation.sh coldrage
```

The control changed only `SIMULATION_TRADE_ENABLED=false`. The suspension case
used `rogue,fortress`, seed 7902, and 6,000 ticks.

## Results

The two enabled peaceful runs each completed 75 shipments totaling 750 stock
units: 450 food, 70 materials, and 230 energy. All six routes remained active.
Both runs produced synchronized hash `FA34F889`, identical route flows, events,
stocks, needs, and populations.

With trade, food-deficit settlements ended at satisfaction 680 and population
1,000; the food specialist reached 1,006. With the same map, production
profile, seed, and Steward behavior but trade disabled, deficit settlements
ended at food satisfaction 280 and population 984–992. The paired difference
is a civil consequence of imported food, not military activity.

The 6,000-tick conflict run declared five bilateral wars and suspended exactly
five corresponding routes with reason `war-suspension`; the one non-hostile
route remained active. It recorded 94 pre/surviving-route shipments totaling
912 units.

For every case:

- final cumulative route flow equaled the sum of `trade-shipment` event
  amounts;
- all post-initial-pulse food, materials, and energy stocks stayed within
  0 and declared storage;
- every result and every telemetry/event line passed Schema v1;
- the seven batch-runner integration tests passed with `tradeEnabled` included
  in the synchronized config fingerprint.

## Interpretation and limits

DIP-002 is complete as the first stock-backed route model. Trade now changes
real settlement survival, is synchronized and reproducible, responds to
diplomatic war, and is controllable for paired experiments.

Routes are currently strategic abstractions between primary settlements.
Distance affects risk, but terrain passability, physical cargo actors,
interception, escort, prices/credits, negotiated terms, and multi-settlement
routing remain later layers. Shipment events are snapshot-derived, so an
experiment that needs every shipment must sample at the 250-tick trade
interval, as this validation did.

The next implementation step is Civilization AI: replace fixed disposition
pressure with utilities informed by shortages, trade dependency, relative
power, research opportunity cost, and civilian war damage.
