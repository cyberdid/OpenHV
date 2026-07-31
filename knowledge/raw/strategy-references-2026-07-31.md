# Source Record: open-source strategy games with working civil systems

Retrieved 2026-07-31. Both repositories were cloned and measured rather than
read about, the same method the OpenCiv inspection used.

The question asked was narrow: which open-source games actually implement
science, diplomacy and trade, and what algorithms can be taken from them.

## Unciv — yairm210/Unciv

- Commit `6369eaa`, dated 2026-07-30, so a day old at inspection.
- Licence: Mozilla Public License 2.0, which carries a secondary-licence
  clause making it compatible with GPL-3.
- Kotlin, libGDX, desktop and Android.

| File | Lines | What it holds |
|---|---|---|
| `logic/civilization/diplomacy/DiplomacyManager.kt` | 961 | relationship state |
| `logic/civilization/diplomacy/CityStateFunctions.kt` | 845 | minor-power influence |
| `logic/automation/civilization/NextTurnAutomation.kt` | 732 | the AI turn |
| `logic/automation/civilization/DiplomacyAutomation.kt` | 639 | who to befriend, denounce, ally |
| `logic/trade/TradeEvaluation.kt` | 561 | prices every offer |
| `logic/civilization/managers/TechManager.kt` | 552 | research |
| `logic/civilization/diplomacy/DeclareWar.kt` | 383 | the consequences of war |
| `logic/automation/civilization/MotivationToAttackAutomation.kt` | 381 | whether to attack |

### The two ideas worth taking

**Relations as a bag of named modifiers, not a scalar.**
`DiplomacyManager` keeps `diplomaticModifiers: HashMap<String, Float>` against
roughly forty named entries — `DeclaredWarOnUs`, `CapturedOurCities`,
`Denunciation`, `StealingTerritory`, `SpiedOnUs` on one side, `YearsOfPeace`,
`SharedEnemy`, `LiberatedCity`, `DeclarationOfFriendship` on the other. Opinion
is their sum; entries decay toward a resting point over time; a smoothed
opinion is kept alongside the raw one.

**The war decision as a list of attributable terms.**
`MotivationToAttackAutomation` builds a list of `(name, value)` pairs and sums
them, short-circuiting once the running total can no longer reach the threshold
asked for:

```
Base motivation            -15 x inverse(DeclareWar personality)
Relative combat strength
Concurrent wars            -20 each
Their concurrent wars       +3 each
Their allies
Relative score
Over unit supply
Relative production
Far away cities / Close cities
Research Agreement         -5 x science x commerce
Declaration of Friendship  -10 x loyal
Defensive Pact             -15 x loyal
Relationship
Denunciation                +5 x inverse(diplomacy)
Receiving trade resources   -8 x commerce
Isolated city              +10 x aggressive
Attack paths               -30 to +10
```

Personality is a sixteen-dimension vector — Production, Food, Gold, Science,
Culture, Happiness, Faith, Military, Aggressive, DeclareWar, Commerce,
Diplomacy, Loyal, Expansion, DenounceWillingness — each 0 to 10 with 5 neutral,
and it **multiplies terms** rather than forming the base.

## Freeciv — freeciv/freeciv

- Commit `f640fe4`, dated 2026-07-31, so same-day at inspection.
- Licence: GPL-2 "or, at your option, any later version", so GPL-3 compatible.
- C, thirty years of development.

| File | Lines |
|---|---|
| `server/citytools.c` | 3,718 |
| `ai/default/daidiplomacy.c` | 2,247 |
| `ai/default/daimilitary.c` | 2,068 |
| `common/research.c` | 1,383 |
| `common/traderoutes.c` | 788 |

Its diplomacy runs on a single `love` value per player pair with thresholds
`req_love_for_peace` and `req_love_for_alliance`, and a `greed()` function that
converts missing love into the price of a treaty — squared, so indifference is
cheap to buy off and hostility is not.

Freeciv is the deeper implementation and the harder read: thirty years of C, an
AI split across a dozen `dai*.c` files, and rules that assume a Civ-2 economy.
Unciv is the better reference for shape, Freeciv for the parts that have been
balanced against real players for decades.

## Against what OpenHV has

The relevant comparison is narrow. OpenHV's war decision is
`StrategicPressureAgainst` — 39 lines returning **one clamped number** from a
per-profile baseline minus penalties for prosperity, stability, trade
dependency, research commitment and casualties, and war starts when
`GrievanceA + GrievanceB` crosses a threshold.

DIP-001 measured what that costs: the profile baseline dominates every other
term, so 100% of wars involved the Aggressor and two of the other pairings
fought once each in 112 matches. A single scalar cannot say *why* a war started,
and cannot be changed in one place without moving everything else — which is
exactly what AI-004 asked for and could not get.

Unciv's shape answers both. A list of named terms is attributable: the telemetry
could record which term carried the decision, the Relations panel could show it,
and a candidate could change one term while holding the rest fixed. That is the
missing instrument, not a missing feature.

## Licence stance

Both are compatible with OpenHV's GPL-3, so code could be adapted rather than
only ideas. That is a change from OpenCiv, where the question never arose
because there was nothing to take. It does not change decision 0003's default:
concepts inform native implementation, and anything adapted verbatim would need
its provenance recorded here.

## Related pages

- [Second inspection of OpenCiv](openciv-2026-07-31.md)
- [Strategic Interval DIP-001](../wiki/experiments/2026-07-30-strategic-interval-dip001.md)
- [Combat Planner AI-004](../wiki/experiments/2026-07-30-combat-planner-ai004.md)
- [Decision 0003: Model Living Factions, Not Only War](../wiki/decisions/0003-living-factions-before-war.md)
