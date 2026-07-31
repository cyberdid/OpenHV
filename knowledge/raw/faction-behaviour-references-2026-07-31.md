# Source Record: how open-source strategy games make factions behave differently

Retrieved 2026-07-31. Six repositories were cloned and measured rather than read
about, the same method the OpenCiv and strategy-reference inspections used.
Shallow sparse checkouts, so the counts below cover the AI and ruleset
directories listed and nothing else.

The question asked was: which open-source RTS or strategy games give factions
*different behaviour*, not merely different units — and what mechanism do they
use.

| Repository | Commit | Dated | Licence | Paths checked out |
|---|---|---|---|---|
| yairm210/Unciv | `6369eaa` | 2026-07-30 | MPL-2.0 | full |
| freeciv/freeciv | `f640fe4` | 2026-07-31 | GPL-2+ | full |
| wesnoth/wesnoth | `4f20b62` | 2026-07-30 | GPL-2 | `data/ai`, `data/multiplayer/factions`, `src/ai` |
| widelands/widelands | `7c5655c2` | 2026-07-16 | GPL-2 | `src/ai`, `data/tribes` |
| Warzone2100/warzone2100 | `d9863cf` | 2026-07-30 | GPL-2 | `data/mp/multiplay/skirmish`, `data/mp/stats` |
| 0ad/0ad | `61a3b95` | **2024-08-17** | GPL-2 (code) | `simulation/ai`, `simulation/data/civs` |

The 0 A.D. GitHub repository is a mirror and its tip is nearly two years old;
upstream development moved to the project's own Gitea. The Petra AI has not
changed structurally in that time, but any count taken from it is a 2024 count.

## The finding

**No game in this set ties temperament to the faction.** Every one of the six
attaches behaviour to the *player slot* — the AI instance — and leaves the
faction to decide what that AI can build. Wesnoth is the only one whose faction
files contain an `[ai]` block at all, and it sets one key.

What differs between them is where the behaviour parameters live and how they
combine.

## Five mechanisms, measured

### 1. Nothing at all — OpenRA, Widelands, 0 A.D.

The AI never asks which faction it is playing.

| | Faction-conditional sites in the AI |
|---|---|
| OpenRA `BotModules/` | **0** |
| Widelands `src/ai/*.cc` | **0** (7 matches are `tribe_ == nullptr` guards) |
| 0 A.D. Petra | **1 of 13** `getPlayerCiv()` calls changes a decision |

The single 0 A.D. exception is worth naming: `diplomacyManager.js:384` makes a
player more willing to accept an alliance when the other player shares its civ.
Kinship, not temperament. The other twelve are template lookups — Petra asks
what a civ calls its barracks, not how a civ fights.

Widelands is the interesting member of this group, because its tribes *do* play
differently and no code says so. The behaviour is in the data: **251 `aihints`
blocks across the tribe definitions, using 105 distinct keys.**

| Key | Buildings using it |
|---|---|
| `prohibited_till` | 144 |
| `very_weak_ai_limit` | 56 |
| `weak_ai_limit` | 56 |
| `basic_amount` | 46 |
| `working_positions` | 42 |
| `expansion` | 20 |
| `fighting` | 20 |
| `mountain_conqueror` | 17 |
| `space_consumer` | 17 |
| `needs_water` | 12 |

One generic AI reads these and plays Barbarians unlike Atlanteans because the
Barbarian buildings describe themselves differently. The annotation is on the
asset, not in a branch.

Widelands also carries something none of the others do: the AI's weights are a
mutable **"DNA"** (`ManagementData::new_dna_for_persistent`,
`MutatingIntensity{kNo, kNormal, kAgressive}`) that mutates and is saved in
`AiPersistentState`, so an AI player's temperament drifts across games rather
than being authored.

### 2. Named personality per nation, multiplying named terms — Unciv

Measured in the previous source record and repeated here for the comparison:
42 personalities of 16 dimensions each, 34 of 60 nations naming one, read at 38
sites across 5 automation files, and applied as a **multiplier** on each named
war-motivation term.

`declareWar` runs from 2 (Gandhi) to 8 (Ashurbanipal) on a 0–10 scale.

### 3. Trait ranges per nation, randomised per player — Freeciv

Four traits, `common/traits.h`:

```
TRAIT_EXPANSIONIST  TRAIT_TRADER  TRAIT_AGGRESSIVE  TRAIT_BUILDER
TRAIT_DEFAULT_VALUE 50
```

A nation declares a *range*, not a value, and each AI player draws from it:

```
[default_traits]
expansionist_min = 30 ; expansionist_max = 90 ; expansionist_default = 50
aggressive_min   = 30 ; aggressive_max   = 90 ; aggressive_default   = 50
```

Read at 12 AI sites, and again as a multiplier:

```c
founder_want *= (double)ai_trait_get_value(TRAIT_EXPANSIONIST, pplayer) ...
cur->want = cur->want * (0.5 + (ai_trait_get_value(TRAIT_BUILDER, pplayer) ...
aggr = ai_trait_get_value(TRAIT_AGGRESSIVE, pplayer);
```

The mechanism is per-nation but is almost entirely unused: **0 of 573 standard
nations override the defaults.** Only the `alien` ruleset pins traits, on 5 of
its 9 nations. So in a normal Freeciv game every nation has the same 30–90
range and the variety is per-player randomness, not national character.

### 4. Continuous personality drawn from a behaviour band — 0 A.D. Petra

The player picks a behaviour; the AI draws a continuous personality from it.

```js
let personalityList = {
    "random":     { "min": 0,    "max": 1 },
    "defensive":  { "min": 0,    "max": 0.27 },
    "balanced":   { "min": 0.37, "max": 0.63 },
    "aggressive": { "min": 0.73, "max": 1 }
};
let behavior = randFloat(-0.5, 0.5);
// make aggressive and defensive quite anticorrelated but not completely
let variation = 0.15 * randFloat(-1, 1) * Math.sqrt(Math.square(0.5) - Math.square(behavior));
```

One parameter produces two anticorrelated outputs plus an independent
`cooperative` roll, and a `personalityCut = {weak: 0.3, medium: 0.5, strong: 0.7}`
converts the continuous value back to a discrete branch where one is needed.

Applied at **29 sites across 8 files** — `attackManager`, `attackPlan`,
`defenseManager`, `diplomacyManager`, `headquarters`, `startingStrategy`,
`victoryManager`, `config` — and again as a multiplier:

```js
this.Military.towerLapseTime = Math.round(this.Military.towerLapseTime * (1.1 - 0.2 * this.personality.defensive));
this.priorities.defenseBuilding = Math.round(this.priorities.defenseBuilding * (0.9 + 0.2 * this.personality.defensive));
```

### 5. The personality *is* the AI — Warzone 2100

Seven separate skirmish AIs ship as independent scripts: `nb_generic`,
`nb_turtle`, `nb_hover`, `Cobra`, `Nexus`, `bonecrusher`, `semperfi`. A
personality file includes a shared ruleset and then declares a table of
**subpersonalities** it switches between at runtime:

```js
MR: {
    chatalias: "mr",
    weaponPaths: [ rockets_AT, machineguns, rockets_AS, rockets_AA, rockets_Arty ],
    earlyResearch: [ "R-Wpn-MG-Damage01", "R-Defense-Tower01", ... ],
    minTanks: 1,        // minimal attack force at game start
    becomeHarder: 3,    // how much to increase attack force every 5 minutes
    maxTanks: 16,
    vtolness: 65,       // chance % of not making droids when adaptation chooses vtols
    defensiveness: 65,  // set this to 100 to enable turtle AI specific code
    repairAt: 50,
}
```

`nb_turtle` differs from `nb_generic` by its research path — Pillbox, Tower,
MRL instead of Halftracks and a Power Module — and by `defensiveness: 100`,
which switches on a distinct code path rather than scaling one.

## Wesnoth: the only faction file with an AI block, and what it says

Wesnoth exposes **18 tunable AI aspects**, settable per side:

```
aggression  allow_ally_villages  caution  grouping  leader_aggression
leader_ignores_keep  leader_value  passive_leader  passive_leader_shares_keep
recruitment_diversity  recruitment_randomness  retreat_enemy_weight
retreat_factor  scout_village_targeting  simple_targeting  support_villages
village_value  villages_per_scout
```

with defaults `aggression 0.4`, `caution 0.25`, `leader_aggression -4.0`.

All 14 multiplayer faction files carry an `[ai]` block. Every one of them sets
exactly one key:

```
[ai]
    recruitment_pattern=fighter,fighter,fighter,mixed fighter,archer,scout   # Knalgans
    recruitment_pattern=fighter,fighter,archer,archer,mixed fighter,healer,scout   # Rebels
[/ai]
```

So the most complete per-side behaviour vocabulary in the set is available to
factions, and the factions use it to say what to recruit. Temperament stays with
the scenario or the player slot.

## The one thing all four parameterised designs share

Unciv, Freeciv, 0 A.D. and Warzone all apply their behaviour parameter as a
**multiplier on a situational value** — a want, a priority, a lapse time, a
motivation term. None of them adds a constant.

OpenHV adds a constant. `disposition` is a per-profile baseline that enters
`StrategicPressureAgainst` as `terms[Disposition]`, summed alongside the
situational terms rather than scaling them. That is a design difference from
four independent codebases in the same genre, and it is the same difference the
long-match baseline diagnosed from the other end: a constant cannot respond to a
narrowing field, so two survivors at peace stay at peace.

## Against what OpenHV has

| | Behaviour lives in | Shape | Faction-conditional AI code |
|---|---|---|---|
| OpenRA | nothing | — | 0 |
| Widelands | asset annotations | data hints | 0 |
| 0 A.D. | player slot | continuous, drawn from band, multiplies | 1 of 13 |
| Freeciv | nation *range*, drawn per player | 4 traits, multiply wants | n/a |
| Unciv | nation → leader personality | 16 dims, multiply terms | n/a |
| Warzone 2100 | the AI script itself | subpersonality tables | n/a |
| Wesnoth | scenario/side aspects | 18 aspects; factions set 1 | n/a |
| **OpenHV** | **bot profile** | **1 scalar, added** | **0** |

OpenHV has 4 factions declared in `mods/hv/rules/world.yaml` — `yi`, `sc`
(Corporations), `sw` (Swarm), Random — and **0** faction-conditional sites in
`OpenRA.Mods.HV/Traits/`. The Swarm is a re-skin, which is exactly where OpenRA,
Widelands and 0 A.D. also sit, so this is the genre norm rather than a gap.

## What is worth taking

**Widelands' `aihints`, adapted as traits.** It is the only mechanism here that
makes a faction play differently with no branch in the bot, and OpenRA's
architecture already puts declarative data on actors. A hint trait on Swarm
actors would let the existing bot play the Swarm differently without a single
`if (faction == "sw")`. This is the strongest fit of the six.

**Petra's anticorrelated draw.** One parameter, two opposed outputs, plus a
declared cut for the places that need a branch. That is a cheaper way to widen
profile variety than adding independent knobs, and DIP-002 already showed that
moving profile constants independently redistributes war without changing its
total.

**Wesnoth's aspect vocabulary.** `retreat_factor` and `retreat_enemy_weight` are
names for what AI-004 was reaching for and could not hold fixed; `caution`,
`grouping` and `village_value` name three more decisions OpenHV currently makes
with constants.

**The multiplier form**, from all four. Converting `disposition` from an addend
to a scale on the situational terms is a single-variable candidate of exactly
the kind the matrix is built to measure, and it is the first idea that addresses
the standing problem — a term that grows as the field narrows — from the
mechanism side rather than by adding another term.

Against it: Freeciv and Petra both *randomise* their parameter per player, and
that would put variance into the matrix that the paired comparison is designed
to remove. Any adoption of the draw-from-a-range idea has to stay seeded off
`World.BotRandom` and be treated as a change to the baseline, not a free
addition.

## Licence stance

All six are GPL-2, GPL-2+, or MPL-2.0, so all are compatible with OpenHV's
GPL-3 and code could be adapted rather than only ideas. Decision 0003's default
is unchanged: concepts inform native implementation, and anything adapted
verbatim records its provenance here.

## Related pages

- [Open-source strategy references](strategy-references-2026-07-31.md)
- [Second inspection of OpenCiv](openciv-2026-07-31.md)
- [Long-Match Baseline 112](../wiki/experiments/2026-07-31-long-match-baseline.md)
- [Compressed Disposition DIP-002](../wiki/experiments/2026-07-31-compressed-disposition-dip002.md)
- [Relative Power DIP-003](../wiki/experiments/2026-07-31-relative-power-dip003.md)
- [Decision 0003: Model Living Factions, Not Only War](../wiki/decisions/0003-living-factions-before-war.md)
