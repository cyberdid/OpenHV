#!/bin/sh

# Launch the actual product: one persistent OpenHV world, with the local client
# observing and every civilization controlled by AI.  This is intentionally
# separate from run-simulation.sh so the product defaults and output location
# remain explicit; finite watchdogs and tick ceilings are opt-in experiments.

set -u

PROJECT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
SUPPORT_DIR="${OPENHV_SUPPORT_DIR:-${PROJECT_DIR}/../.openhv-support}"
WORLD_DIR="${LIVING_WORLD_RESULT_DIR:-${SUPPORT_DIR}/living-world}"
WORLD_MAP="${LIVING_WORLD_MAP:-coldrage}"
WORLD_BOTS="${LIVING_WORLD_BOTS:-aggressor,economist,technologist,fortress}"
WORLD_FACTIONS="${LIVING_WORLD_FACTIONS:-sw,yi,sc,sw}"
WORLD_SPEED="${LIVING_WORLD_SPEED:-default}"
WORLD_PROFILE="${LIVING_WORLD_PROFILE:-balanced}"
WORLD_HEADLESS="${LIVING_WORLD_HEADLESS:-false}"
WORLD_MAX_TICKS="${LIVING_WORLD_MAX_TICKS:-2147000000}"
WORLD_HORIZON="${LIVING_WORLD_OBSERVATION_HORIZON_TICKS:-${WORLD_MAX_TICKS}}"
WORLD_WATCHDOG="${LIVING_WORLD_WATCHDOG_SECONDS:-0}"
WORLD_AUTO_RESTART="${LIVING_WORLD_AUTO_RESTART:-false}"
WORLD_SEED="${LIVING_WORLD_SEED:-42}"

mkdir -p "${WORLD_DIR}"

session_id="$(date +%Y%m%d-%H%M%S)-$$"
epoch=1
while :; do
	match_id="universe-${session_id}-${WORLD_SEED}-epoch-${epoch}"
	result_path="${WORLD_DIR}/${match_id}.json"

	echo "Starting autonomous Universe epoch ${epoch} on ${WORLD_MAP}."
	echo "  AI civilizations: ${WORLD_BOTS}"
	echo "  One OpenHV world; observer input is optional."
	echo "  Close the OpenHV window to stop the Universe."

	if SIMULATION_HEADLESS="${WORLD_HEADLESS}" \
		SIMULATION_BOTS="${WORLD_BOTS}" \
		SIMULATION_FACTIONS="${WORLD_FACTIONS}" \
		SIMULATION_SPEED="${WORLD_SPEED}" \
		SIMULATION_SEED="${WORLD_SEED}" \
		SIMULATION_MAX_TICKS="${WORLD_MAX_TICKS}" \
		SIMULATION_SCENARIO_MODE=living-world \
		SIMULATION_OBSERVATION_HORIZON_TICKS="${WORLD_HORIZON}" \
		SIMULATION_STALEMATE_WINDOW_TICKS=0 \
		SIMULATION_STALEMATE_TERMINATES=false \
		SIMULATION_WATCHDOG_SECONDS="${WORLD_WATCHDOG}" \
		SIMULATION_CIVILIZATION_PROFILE="${WORLD_PROFILE}" \
		SIMULATION_TRADE_ENABLED=true \
		SIMULATION_MATCH_ID="${match_id}" \
		SIMULATION_RESULT="${result_path}" \
		"${PROJECT_DIR}/run-simulation.sh" "${WORLD_MAP}"
	then
		exit_code=0
	else
		exit_code=$?
	fi

	# Closing the window or a startup failure produces no completed result.  In
	# either case honour the user's decision and do not create a restart loop.
	if [ ! -f "${result_path}" ]; then
		echo "Universe stopped without a completed epoch (exit ${exit_code})."
		exit "${exit_code}"
	fi

	end_reason=$(python3 -c \
		"import json,sys; print(json.load(open(sys.argv[1])).get('endReason','unknown'))" \
		"${result_path}")
	echo "Universe epoch ${epoch} ended: ${end_reason}."

	if [ "${WORLD_AUTO_RESTART}" != "true" ]; then
		exit "${exit_code}"
	fi

	case "${end_reason}" in
		natural-victory|faction-collapse)
			# Compatibility path for explicitly finite/legacy lifecycle settings.
			epoch=$((epoch + 1))
			WORLD_SEED=$((WORLD_SEED + 1))
			echo "A new autonomous epoch will begin."
			sleep 2
			;;
		*)
			# Observation/tick limits are only expected in explicit smoke tests.
			# Never loop on a configuration or infrastructure terminal condition.
			exit "${exit_code}"
			;;
	esac
done
