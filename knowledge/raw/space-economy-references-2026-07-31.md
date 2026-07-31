# Джерельний запис: звʼязок економіки, торгівлі та війни в міжпланетному масштабі

Зібрано 2026-07-31. Написано в scratchpad, **не в репозиторій**, бо в цей час
працював батч DIP-005, а правити дерево під час прогону заборонено. Перенести
після завершення.

Метод той самий: клонувати й міряти.

| Репозиторій | Комміт | Ліцензія | Що взято |
|---|---|---|---|
| endless-sky/endless-sky | `d08eaf4` | GPL-3 | `source` |
| naev/naev | `a9ab590` | GPL-3 | `src`, `dat/factions`, `dat/spob` |
| freeorion/freeorion | `5bd515c` | GPL-2 | `default/python/AI`, `scripting` |
| OpenSRProject/OpenStarRuler | `da21660` | — | `source`, `bin` |

**Star Ruler 2 виміряти не вдалось.** Відкритий репозиторій містить лише двигун
(`source`, `bin`) — ігрові скрипти AngelScript у ньому відсутні: `git ls-tree -r`
знаходить рівно **один** файл `.as`, і той — `doc/dump_wiki.as`. Економіку
звʼязаних ресурсів, заради якої його брали, без файлів даних не побачити.

---

## 1. Endless Sky — найповніший знайдений звʼязок, і він у 25 рядках

`PlayerInfo::RaidFleetAttraction`, `source/PlayerInfo.cpp:1532`. Це модель
«багатство притягує війну, зброя і territorial control її придушують».

```cpp
pair<double, double> PlayerInfo::RaidFleetFactors() const
{
    double attraction = 0., deterrence = 0.;
    for (const shared_ptr<Ship> &ship : Ships()) {
        if (ship->IsParked() || ship->IsDestroyed()) continue;
        attraction += ship->Attraction();
        deterrence += ship->Deterrence();
    }
    return make_pair(attraction, deterrence);
}

double PlayerInfo::RaidFleetAttraction(const RaidFleet &raid, const System *system) const
{
    ...
    if (raid.MaxAttraction() > 0 && factors.first > raid.MaxAttraction())
        return 0;
    attraction = .005 * (factors.first - factors.second - raid.MinAttraction());
    int64_t raidStrength = raidFleet->Strength();
    if (system && raidStrength)
        for (const auto &fleet : system->Fleets()) {
            const Government *gov = fleet.Get()->GetGovernment();
            if (gov) {
                double strength = fleet.Get()->Strength() / fleet.Period();
                attraction -= (gov->IsEnemy(raidGov) - gov->IsEnemy()) * (strength / raidStrength);
            }
        }
    return max(0., min(1., attraction));
}
```

Чотири властивості, і кожна варта окремо:

**Притягання мінус стримування.** Вантаж додає, озброєння віднімає. Одне число,
два протилежні внески — той самий прийом, що антикорельована пара в Petra.

**Поріг знизу (`MinAttraction`).** Нижче нього рейдер не турбується взагалі.
Дрібний торговець просто не є ціллю.

**Стеля згори (`MaxAttraction`)** — і це найцікавіше. Якщо ти **надто** ласий
шматок, цей рейдовий флот **не зʼявляється зовсім**. Тобто рейдери
**багатоярусні**: під кожен діапазон цінності здобичі свій хижак. Дрібнота не
лізе на конвой, який її розчавить.

**Патрулі знижують притягання.** Віднімається сила чужих флотів, ворожих
рейдеру, поділена на силу рейдера. Безпека **територіальна** і надається
третіми сторонами — не тільки твоєю зброєю, а й тим, хто патрулює систему.

Спавн: `noFleetProb = pow(1. - attraction, 10.)` (`PlayerInfo.cpp:4210`) —
десять незалежних кидків за інтервал, тож притягання 0.1 дає вже ~65% шансу.

Дотично: `MapPanel.cpp:187` рахує з цієї ж величини **небезпеку на карті** —
`danger += 10. * RaidFleetAttraction(...)`. Тобто одна формула живить і
симуляцію, і те, що бачить гравець.

---

## 2. Naev — ціна як функція місця й часу

`src/economy.c`, 1157 рядків. `economy_getPrice(commodity, system, spob)` і
`economy_getPriceAtTime(..., tme)`: ціна залежить від товару, **системи**,
**конкретного тіла** та **часу**.

Три режими на товар:
- посилальний (`commodity_price_ref`) — ціна похідна від іншого товару з
  модифікатором;
- сталий (`commodity_price_constant`);
- інакше — позиційна функція від часу в періодах (`ntime_convertSeconds / NT_PERIOD_SECONDS`).

Це **не** симуляція попиту й пропозиції: ціна детермінована відносно часу й
місця, а не є станом, що змінюється від того, скільки в неї продали. Для
одиночної гри це правильний вибір — гравець може вивчити маршрут. Для нашої
задачі це радше контрприклад: детермінована ціна не створює звʼязку між війною
й економікою, бо війна на неї не впливає.

---

## 3. FreeOrion — торгівлі немає, звʼязок в іншому

Найбільший відкритий 4X. ШІ — 16 818 рядків Python.

**Слово «trade» трапляється в ньому 3 рази**, і всі — у `ShipDesignAI.py`.
Торгівлі як механіки немає взагалі.

Замість неї два механізми, обидва релевантні.

**Розподільник пріоритетів.** `PriorityAI.py` щохода рахує й виставляє:
`RESOURCE_PRODUCTION`, `RESOURCE_RESEARCH`, `PRODUCTION_EXPLORATION`,
`PRODUCTION_COLONISATION`, `PRODUCTION_OUTPOST`, `PRODUCTION_INVASION`,
`PRODUCTION_MILITARY`, `PRODUCTION_BUILDINGS`. Економіка й війна конкурують за
**один бюджет**, і загроза входить у розрахунок військового пріоритету.

Там же — явний запобіжник проти надреакції:

```python
# scale monster threat so that in early - mid game big monsters
# don't over-drive military production
if base_monster_threat >= 2000:
    monster_threat = 2000 + (current_turn / 100.0 - 1) * (base_monster_threat - 2000)
```

Загроза понад 2000 враховується **тим сильніше, чим пізніша стадія гри**. Тобто
одна страшна річ на початку не має права зламати економіку.

**Постачання як контестована територія.** 113 згадок `supply`. Флот, що
опинився поза `fleetSupplyableSystemIDs`, не лагодиться й не поповнюється
(`AIFleetMission.py:703`). Це і є міжпланетний звʼязок економіки з війною в
FreeOrion: не товари, а **дальність, на яку економіка здатна тримати флот**.

**Характер як множники.** `character/character_module.py` — інтерфейс із
методів, майже всі з яких повертають коефіцієнти:
`military_priority_scaling()`, `invasion_priority_scaling()`,
`preferred_colonization_portion()`, `max_defense_portion()`,
`preferred_discount_multiplier()`, `may_surge_industry(totalPP, totalRP)`.

Останній вартий уваги: рішення «розігнати промисловість» приймається з огляду
**одночасно на виробничі й дослідницькі очки**. Характер тут не константа
агресії, а функція від стану економіки.

Рівні агресії названі: `beginner`, `turtle`, … (`character_strings_module.py:61`),
і від них залежить навіть назва столиці.

---

## 4. Stellaris — не підтверджено

Хотів звірити канонічну комерційну модель (торгова цінність збирається
базами, тече гіперлініями до столиці, незахищена породжує піратство). На вікі
версії 4.4 сторінка `Piracy` **порожня**, а `Trade_value` перенаправляє на
`Resources`. Схоже, систему переробили. Веб-пошук був недоступний (місячний
ліміт акаунта), тож історичну механіку не перевіряв і не переказую.

---

## 5. Що з цього придатне нам

| Механізм | Джерело | Чому підходить |
|---|---|---|
| **Притягання мінус стримування** | Endless Sky | одне число, дві протилежні складові; у нас уже є `armyValue` (стримування) і `assetsValue` + вантажі (притягання) |
| **Ярусні хижаки** | Endless Sky | стеля притягання не дає дрібним нападати на великих — природний спосіб не робити бідних безборонними |
| **Патрулі знижують ризик** | Endless Sky | безпека територіальна; у нас `TradeManager` уже має `risk` на маршруті, але він не залежить від того, хто поруч |
| **Спільний бюджет пріоритетів** | FreeOrion | економіка й війна конкурують явно, а не через окремі підсистеми |
| **Кламп надреакції на загрозу** | FreeOrion | загроза враховується тим сильніше, чим пізніша стадія — прямий запобіжник проти зриву економіки |
| **Постачання як межа проєкції сили** | FreeOrion | війна обмежена тим, що економіка здатна утримати, а не тільки тим, що встигла побудувати |

Найближче до наявного коду — **ризик маршруту**. У `TradeManager` він уже є
(`tradeRoutes[].risk`, у довгому baseline медіана 280), але сьогодні це
властивість самого маршруту. Формула Endless Sky перетворює його на функцію
від **цінності вантажу, сили сторін і того, хто контролює простір навколо** —
і саме це замикає петлю торгівля → війна → торгівля, якої в нас немає.

## Пов'язане

- `knowledge/raw/faction-behaviour-references-2026-07-31.md`
- `knowledge/raw/strategy-references-2026-07-31.md`
- `knowledge/wiki/experiments/2026-07-31-long-match-baseline.md`
