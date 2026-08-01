# OpenHV [![Continuous Integration](https://img.shields.io/github/actions/workflow/status/OpenHV/OpenHV/ci.yml)](https://github.com/OpenHV/OpenHV/actions/workflows/ci.yml) [![Documentation Status](https://readthedocs.org/projects/openhv/badge/?version=latest)](https://openhv.readthedocs.io/en/latest/?badge=latest) [![Discord](https://discordapp.com/api/guilds/840983316395720715/widget.png)](https://discord.gg/X3VUtPtBTu) [![Matrix](https://matrix.to/img/matrix-badge.svg)](https://matrix.to/#/+openhv:matrix.org) [![IRC/Jabber](https://img.shields.io/badge/IRC/Jabber-on%20FreeGameDev-blue.svg)](https://freegamedev.net/irc/#openhv)

A mod for [OpenRA](https://www.openra.net) based on the [Hard Vacuum](https://lostgarden.home.blog/2005/03/27/game-post-mortem-hard-vacuum/) design by Daniel Cook. It aims to be an open content real-time strategy game with no exceptions. Set in the distant future where mega corporation battle themselves this standalone title comes with multiplayer (LAN and internet) support, competent skirmish bots as well as an integrated map editor. It allows for spectators to join and replays to be shared.

![Turncoat Trail](https://www.openhv.net/images/readme/turncoat-trail.png)

# Getting Started [![Packaging status](https://repology.org/badge/tiny-repos/openhv.svg)](https://repology.org/project/openra/versions)

To launch the project from the development environment you must first compile the project by running `make.cmd` (Windows), or opening a terminal in the SDK directory and running `make` (Linux / macOS). You can then run `launch-game.cmd` (Windows) or `launch-game.sh` (Linux / macOS) to run the game. More details on [building](https://github.com/OpenHV/OpenHV/wiki/Build) the game are available at the wiki.

## Autonomous Simulation

### One-engine living world

The canonical playable/observable Universe is one persistent OpenHV process.
There is no campaign-cell selection and no separate tactical window: four AI
civilizations build settlements, gather resources, research, trade, negotiate,
mobilize, and fight on the same map while the local client remains a spectator.

```sh
./run-universe.sh
```

The default living world uses `coldrage`, normal visual speed, the aggressor,
economist, technologist, and fortress personalities, trade enabled, no
wall-clock watchdog, and an effectively unbounded observation horizon. Closing
the OpenHV window stops it. A natural victory or total civil collapse starts a
new autonomous epoch with the next deterministic seed.

The Python/Web planet remains useful as a scientific observer and mechanics
prototype, but it is not the canonical game runtime. `fight-cell.sh` remains a
bridge experiment; the one-engine mode does not wait for a Web click or launch
per-cell matches.

### Finite research runs

The local development fork can launch a hands-off AI match with the local client acting only as an observer:

```sh
./run-simulation.sh coldrage
```

The first argument is a map folder name. `coldrage` is the default and starts four AI factions in a free-for-all match at the fastest game speed.

Five strategy profiles are available: `aggressor`, `economist`,
`technologist`, `fortress`, and the non-attacking civil-development
`steward`. A mixed match that stops after 1,500 synchronized world ticks
(30 simulated seconds at the `fastest` 20 ms timestep) and writes a versioned
result can be launched with:

```sh
SIMULATION_HEADLESS=true \
SIMULATION_BOTS=aggressor,economist,technologist,fortress \
SIMULATION_MAX_TICKS=1500 \
SIMULATION_WATCHDOG_SECONDS=120 \
SIMULATION_SEED=42 \
SIMULATION_RESULT=/tmp/openhv-match.json \
./run-simulation.sh coldrage
```

The wall-clock watchdog only detects a stuck process; it does not define the
simulation horizon. `SIMULATION_DURATION` remains a deprecated compatibility
input and is converted to simulated ticks. Results follow
[`simulation-result-v1.schema.json`](schemas/simulation-result-v1.schema.json)
and distinguish `naturalWinners` from the composite `scoreLeader`.

Current results also expose synchronized civilization/settlement state.
Enable periodic JSONL snapshots and reason-coded events with
`SIMULATION_TELEMETRY_INTERVAL_TICKS=250`. Select the synchronized
`balanced` or `scarcity` civil fixture with
`SIMULATION_CIVILIZATION_PROFILE`; for example:

```sh
SIMULATION_HEADLESS=true \
SIMULATION_BOTS=steward \
SIMULATION_CIVILIZATION_PROFILE=scarcity \
SIMULATION_TELEMETRY_INTERVAL_TICKS=250 \
SIMULATION_MAX_TICKS=3500 \
SIMULATION_RESULT=/tmp/living-factions/result.json \
./run-simulation.sh coldrage
```

This writes `result.json`, `telemetry.jsonl`, and `events.jsonl` using the
schemas under [`schemas/`](schemas/). The reproducible balanced, peaceful, and
scarcity schedule is
[`batch-manifests/living-factions-v1.json`](batch-manifests/living-factions-v1.json).

`SIMULATION_HEADLESS=true` uses a no-window, no-OpenGL, no-audio runtime. The
normal game and `run-simulation.sh` remain graphical by default, while
`run-tournament.sh` defaults to headless execution. The build scripts apply the
version-pinned files under `engine-patches/` idempotently after fetching the
OpenRA SDK.

Verify tick cutoff, schema validity, headless deterministic reruns, and
invalid-bot rejection with:

```sh
./check-simulation-determinism.sh coldrage
```

Verify that a graphical and headless run produce the same synchronized result
with:

```sh
./check-headless-equivalence.sh coldrage
```

For unattended experiments, install the batch runner dependency and launch a
tracked declarative manifest:

```sh
python3 -m pip install -r requirements-simulation.txt
./run-batch.py batch-manifests/smoke-v1.json
```

Batch runs default to `../simulation-runs/<run-id>`. Each match attempt runs in
an isolated OS process and OpenRA support directory. The run records the
original and resolved manifests, runtime/Git provenance, session history,
aggregate summary, per-match config/status/result, attempt logs, failure
support files, configured replay samples, and enabled telemetry/event streams.
Resume a run without repeating
validated completed matches:

```sh
./run-batch.py batch-manifests/smoke-v1.json --resume
```

`--workers`, `--max-infrastructure-retries`, and `--retry-failures` control
execution without changing synchronized match inputs. Exit code `0` means all
matches completed, `2` means one or more matches reached a diagnosable
non-completed state, and `130` means the runner handled an external
interruption. Manifests follow
[`simulation-batch-manifest-v1.schema.json`](schemas/simulation-batch-manifest-v1.schema.json).

The checked-in 100-match infrastructure schedule can run sequentially or with
controlled concurrency:

```sh
./run-batch.py batch-manifests/soak-100-v1.json --workers 1
./run-batch.py batch-manifests/soak-100-v1.json --workers 4
```

Run a ten-match tournament across several maps with:

```sh
./run-tournament.sh
```

The simpler legacy tournament wrapper writes each match and an aggregate
`tournament.json` under `../tournament-results`. `MATCH_COUNT`, `MATCH_MAX_TICKS`,
`MATCH_WATCHDOG_SECONDS`, `TOURNAMENT_HEADLESS`, `TOURNAMENT_SEED`,
`TOURNAMENT_BOTS`, `TOURNAMENT_MAPS`, and `TOURNAMENT_RESULTS_DIR` can be
overridden through the environment.

Project architecture, experiments, decisions, and the current roadmap are
maintained in the persistent [project wiki](knowledge/wiki/index.md).

![MiniYAML](https://www.openhv.net/images/readme/miniyaml.png)

Game rules are defined in text files using a dialect called `MiniYAML` which has [IDE support in Visual Studio Code](https://marketplace.visualstudio.com/items?itemName=openra.oraide-vscode).

![MiniYAML](https://www.openhv.net/images/readme/lua.png)

Script missions or mini-games in Lua. See the [API](https://openhv.readthedocs.io/en/latest/release/lua/) for details and use the [VS Code extension](https://marketplace.visualstudio.com/items?itemName=openra.vscode-openra-lua) for code completion.

# Licensing
## Source Code [![GPL](https://img.shields.io/github/license/OpenHV/OpenHV)](https://www.gnu.org/licenses/gpl-3.0.html)
OpenHV just like the OpenRA engine and SDK scripts is made available under the [GPLv3](https://github.com/OpenHV/OpenHV/blob/main/COPYING) license.

## Content [![License: CC BY US 3.0](https://img.shields.io/badge/license-CC%20BY%203.0%20US-lightgrey.svg)](https://creativecommons.org/licenses/by/3.0/us/) [![License: CC BY 3.0](https://img.shields.io/badge/license-CC%20BY%203.0-lightgrey.svg)](https://creativecommons.org/licenses/by/3.0/) [![License: CC BY 4.0](https://img.shields.io/badge/license-CC%20BY%204.0-lightgrey.svg)](https://creativecommons.org/licenses/by/4.0/) [![License: CC BY-SA 4.0](https://img.shields.io/badge/license-CC%20BY--SA%204.0-lightgrey.svg)](https://creativecommons.org/licenses/by-sa/4.0/) [![License: CC0](https://img.shields.io/badge/license-CC0-lightgrey.svg)](https://creativecommons.org/publicdomain/zero/1.0/)
The mod data files (artwork, sound files, game rules, etc.) are not part of the source code and are distributed under different terms. Various [Creative Commons](https://creativecommons.org/) licenses apply. Check the ReadMe files in the sub folders for details.

# Sponsors
Free code signing on Windows provided by [SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).
