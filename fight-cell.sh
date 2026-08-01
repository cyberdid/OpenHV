#!/bin/sh
#
# Fight over one cell of the campaign planet.
#
#   ./fight-cell.sh <x> <y> [planet-url]
#
# Builds a battle-request-v1 document from the live campaign, runs the match it
# describes, and reports who took the cell. This is the whole loop: the planet
# is floating-point Python, the battle is integer lockstep, and the request
# document is the only thing that crosses between them.
#
# The match is a separate process on purpose. Launch.Simulation is read once by
# PanelLoadScreen at startup, so a battle cannot begin inside a process that is
# already running - which is also why the in-game button writes a request rather
# than pretending to start one.

set -e

PROJECT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
SUPPORT_DIR="${OPENHV_SUPPORT_DIR:-${PROJECT_DIR}/../.openhv-support}"
REQUEST_DIR="${SUPPORT_DIR}/battles"

usage() {
	echo "usage: fight-cell.sh <x> <y> [planet-url]" >&2
	echo "       fight-cell.sh --request <path>" >&2
	exit 1
}

# Replaying a document rather than rebuilding one is not a convenience. The
# campaign advances while you look at it, so asking for cell (34,18) a minute
# after the planet screen wrote a request for cell (34,18) produces a different
# seed, a different map and different army values. The request IS the battle;
# if one exists, fight that one.
if [ "$1" = "--request" ]; then
	REQUEST="${2:?--request needs a path}"
	[ -f "${REQUEST}" ] || { echo "No request at ${REQUEST}" >&2; exit 1; }
	CELL_X=$(python3 -c "import json;print(json.load(open('${REQUEST}'))['cell']['longitudeIndex'])")
	CELL_Y=$(python3 -c "import json;print(json.load(open('${REQUEST}'))['cell']['latitudeIndex'])")
	mkdir -p "${REQUEST_DIR}"
	echo "Replaying ${REQUEST}"
else
	[ -n "$2" ] || usage
	CELL_X="$1"
	CELL_Y="$2"
	PLANET_URL="${3:-http://localhost:8791/api/planet}"
	REQUEST="${REQUEST_DIR}/request-${CELL_X}-${CELL_Y}.json"
	mkdir -p "${REQUEST_DIR}"
	echo "Asking the campaign for cell (${CELL_X},${CELL_Y})..."
	"${PROJECT_DIR}/utility.sh" --battle-from-planet "${CELL_X}" "${CELL_Y}" "${PLANET_URL}" "${REQUEST}"
fi

RESULT="${REQUEST_DIR}/result-${CELL_X}-${CELL_Y}.json"

# Read the request back rather than keeping the values in shell variables: the
# document on disk is what the run is defined by, so if the two ever disagree
# the document wins.
read_field() {
	python3 -c "import json,sys; d=json.load(open('${REQUEST}')); print(${1})"
}

MAP=$(read_field "d['map']['name']")
SEED=$(read_field "d['determinism']['seed']")
MAX_TICKS=$(read_field "d['determinism']['maxWorldTicks']")
SPEED=$(read_field "d['determinism'].get('gameSpeed','fastest')")
WATCHDOG=$(read_field "d['determinism'].get('watchdogSeconds',180)")
BOTS=$(read_field "','.join(p['botType'] for p in d['participants'])")
FACTIONS=$(read_field "','.join(p.get('faction','') for p in d['participants'])")
MATCH_ID=$(read_field "d['requestId']")
BIOME=$(read_field "d['environment']['biome']")
TEMP=$(read_field "d['environment']['surfaceTemperatureK']")

echo "  cell is ${BIOME} at ${TEMP} K"
echo "  ${BOTS} on ${MAP}, seed ${SEED}"
echo

SIMULATION_HEADLESS="${SIMULATION_HEADLESS:-true}" \
SIMULATION_BOTS="${BOTS}" \
SIMULATION_FACTIONS="${FACTIONS}" \
SIMULATION_SEED="${SEED}" \
SIMULATION_MAX_TICKS="${MAX_TICKS}" \
SIMULATION_SPEED="${SPEED}" \
SIMULATION_WATCHDOG_SECONDS="${WATCHDOG}" \
SIMULATION_MATCH_ID="${MATCH_ID}" \
SIMULATION_RESULT="${RESULT}" \
"${PROJECT_DIR}/run-simulation.sh" "${MAP}"

echo
if [ -f "${RESULT}" ]; then
	python3 - "${REQUEST}" "${RESULT}" <<'PY'
import json, sys

request = json.load(open(sys.argv[1]))
result = json.load(open(sys.argv[2]))

cell = request["cell"]
print(f"cell ({cell['longitudeIndex']},{cell['latitudeIndex']}) resolved")
print(f"  end reason  {result.get('endReason', 'unknown')}")
print(f"  ticks       {result.get('worldTick', '?')}")
print(f"  hash        {result.get('synchronizedStateHash', '?')}")

# simulation-result-v1 names these naturalWinners and playerName. There is
# deliberately no battle-result-v1 - this document already carries everything
# an outcome needs - so the field names here are the ones the result format
# actually uses rather than ones invented for the campaign.
players = result.get("players", [])

# PanelLoadScreen fills every open slot on the map, cycling the bot list, so
# the participant count is only honoured when the map has exactly that many
# spawn points. Say so rather than quietly renaming four players as two.
if len(players) != len(request["participants"]):
    print(f"  WARNING     map seats {len(players)}, the request committed "
          f"{len(request['participants'])}")

# Slots are assigned from the seed, so identify a side by the faction it plays
# rather than by position. botType would collide the moment both sides use the
# same bot.
by_faction = {p.get("faction"): p for p in request["participants"]}

def label_for(player):
    who = by_faction.get(player.get("faction"))
    if who is None:
        return player.get("playerName", "?"), ""
    return who["name"], who["role"]

for p in players:
    name, role = label_for(p)
    print(f"  {name:<12} {role:<9} kills {p.get('killsValue', 0):>8}"
          f"  losses {p.get('deathsValue', 0):>8}")

# The stake the request recorded, applied to what actually happened. Written
# down in the request so the decision is auditable even if the campaign later
# changes its mind about what a win is worth.
if request.get("stakes", {}).get("cellControl"):
    winners = result.get("naturalWinners") or []
    if winners:
        print(f"  cell control -> {', '.join(str(w) for w in winners)}")
    else:
        # scoreLeader is the player object, not an index.
        leader = result.get("scoreLeader")
        if isinstance(leader, dict):
            name, _ = label_for(leader)
            print(f"  cell control -> unchanged, no natural winner (ahead on score: {name})")
        else:
            print("  cell control -> unchanged, no natural winner")
PY
else
	echo "No result document at ${RESULT}." >&2
	exit 1
fi
