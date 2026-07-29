---
title: AI Strategy Profiles
status: current
updated: 2026-07-29
sources:
  - ../../mods/hv/rules/bots.yaml
  - experiments/2026-07-29-baseline-tournament.md
  - experiments/2026-07-29-living-factions-v1.md
  - experiments/2026-07-29-civilization-ai-war-cost-v1.md
  - experiments/2026-07-29-baseline-112-v1.md
tags:
  - ai
  - balancing
---

# AI Strategy Profiles

The profiles share OpenRA's base construction, economy, scouting, repair,
support-power, and attack modules. Their squad timing, production priorities,
cash thresholds, and build limits differ.

## Aggressor — Vanguard AI

- Intended behavior: early pressure and frequent small attack groups.
- Small rush threshold and short attack delay.
- Favors inexpensive combat units.
- Backward-compatible `rogue` bot uses this personality.

## Economist — Foundry AI

- Intended behavior: preserve liquidity, expand income, and field a later
  balanced force.
- Higher minimum cash reserve and stronger miner/builder emphasis.
- Larger, slower attack groups.

## Technologist — Ascendant AI

- Intended behavior: delay commitment until advanced ground and air units are
  available.
- Prioritizes higher-tier tanks, aircraft, and specialized units.
- Medium attack cadence and cash reserve.

## Fortress — Bastion AI

- Intended behavior: defend infrastructure, use anti-air and artillery, then
  move out with large formations.
- Largest squad target and longest rush delay.
- Emphasizes repair, mine-laying, artillery, and defensive unit composition.

## Steward — Steward AI

- Intended behavior: peaceful civil-development fixture.
- Uses shared base, builder, repair, and economic construction logic.
- Builds no combat units and creates no attack squads.
- Exists to test population, infrastructure, research, shortages, trade, and
  diplomacy without making combat the default source of activity.
- Four Steward AIs reached tick 3,500 with population growth and zero
  kills/losses in the
  [Living Factions validation](experiments/2026-07-29-living-factions-v1.md).

Steward is an experiment control, not a substitute for runtime neutral
relationships.

## Shared Civilization AI v1

The tactical profiles now feed a synchronized multi-objective civilization
planner rather than only fixed build/squad parameters. Every 250 ticks it
scores survival, research, trade, security, recovery, and war; it selects
development, survival, research, trade, mobilization, or recovery.

War pressure includes personality but is reduced by shortages, instability,
bilateral trade dependency, research commitment, and prior casualties, and is
modified by relative power. Economist receives a trade-utility preference;
Technologist receives a research preference; Steward has zero offensive war
pressure. The selected state changes research budget and diplomacy. Military
mobilization and casualties alter workforce, production, population, and
stability for every profile.

The first evidence is in
[Civilization AI and War Cost v1 Validation](experiments/2026-07-29-civilization-ai-war-cost-v1.md):
trade dependency prevented a war seen in the paired no-trade control; five
wars triggered four mobilization states; peace triggered four recovery states;
and a repeat produced hash `8431018E`.

## Evidence status

The original
[baseline tournament](experiments/2026-07-29-baseline-tournament.md) only
confirmed startup behavior. The
[112-match baseline](experiments/2026-07-29-baseline-112-v1.md) now provides
held-out multi-map evidence:

- Aggressor deals the most kill value but has the lowest retained army and 12
  of 15 faction collapses, including nine on Doubles;
- Fortress has the highest mean population, prosperity, stability, army,
  assets, and score, with the lowest casualties;
- Economist avoids collapse but ends mobilized in 111/112 observations and
  does not yet express a trade-centered identity;
- Technologist completes no more technologies than its peers and instead has
  the lowest stability and highest casualties.

These are current-system observations, not permanent identity definitions.
They show that AI-003 must connect strategic state to real opening/economy/
technology plans, and AI-004 must add finishing, retreat, and regroup logic.
Every match ended at the tick ceiling, so timed score leaders are not winners.

## Civilization AI v2 planner implementation

AI-003 now separates high-level strategy from an executable civil plan:

- all profiles use an `opening` plan through tick 3,000;
- Economist then prefers `economy`, Technologist prefers `technology`, and
  other combat profiles use an economy/development plan unless crisis forces
  `recovery`;
- economy raises food, materials, and energy output while preserving ordinary
  research; technology triples knowledge with a small materials/energy
  opportunity cost; recovery prioritizes food and repair capacity;
- profile-specific technology priorities stop every faction from traversing
  the same early research order;
- a 1,000-tick bot pulse issues bounded miner, technician, observer, radar, or
  repair requests. The corrected budgets are one opening worker, at most two
  Economist miners, and one per technical/support type. Per-type synchronized
  budgets prevent consumed builders,
  deaths, or unavailable queues from causing an infinite request loop;
- final results and events expose the plan, reason, transition sequence, last
  requested actor, and request count.

Two identical 12,000-tick smoke runs produced hash `DEB138AE` and identical
plan/request/final metrics. The exact 112-match candidate schedule is frozen;
promotion remains conditional on its paired held-out report.

Candidate v1 was rejected because floor rounding erased non-Technologist
knowledge and support-unit value inflated civilian mobilization. Candidate v2
uses positive round-up, excludes explicit civilian/support actors from
mobilization, and matched two 12,000-tick runs at hash `9419B52D`; its exact
112-match gate is next.

The accepted v3 held-out result completed 112/112 matches. Technologist gained
1.464 technologies; Economist gained 41 prosperity, 46 stability, and 122
available workforce while reducing mobilization by 127. Total collapses
matched baseline and score-lead shares stayed within 10–40%. Technologist and
Fortress also exposed significant war/casualty/army regressions; these are now
AI-004 acceptance inputs, not ignored planner results.

## Shared Combat Planner AI-004

Each profile now carries its own target scoring and force-preservation
thresholds on top of the shared squad manager:

| Profile | Damage weight | Construction-yard bonus | Retreat health | Retreat power ratio | Regroup ticks |
|---|---|---|---|---|---|
| Aggressor | 24 | 7,000 | 55% | 130% | 150 |
| Economist | 18 | 5,500 | 70% | 150% | 500 |
| Technologist | 28 | 6,500 | 75% | 160% | 600 |
| Fortress | 20 | 6,000 | 65% | 140% | 750 |

Aggressor commits at the lowest health and returns fastest; Technologist and
Fortress disengage earliest and hold the longest, matching their measured war
exposure. Every profile also carries a finishing bonus that raises the score of
targets whose owner has already lost more than the loss threshold and retains
less than the assets threshold.

The profiles keep the engine default `DangerScanRadius`. Raising it widens the
"own building nearby" flee veto and suppresses retreat, which is why the first
candidate produced zero retreats in 12,000 ticks. See the
[combat decision boundary](architecture.md#combat-decision-boundary).
