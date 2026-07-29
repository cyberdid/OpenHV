#!/bin/sh

set -e

PROJECT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
MATCH_COUNT="${MATCH_COUNT:-10}"
MATCH_MAX_TICKS="${MATCH_MAX_TICKS:-1500}"
MATCH_WATCHDOG_SECONDS="${MATCH_WATCHDOG_SECONDS:-120}"
TOURNAMENT_HEADLESS="${TOURNAMENT_HEADLESS:-true}"
TOURNAMENT_SEED="${TOURNAMENT_SEED:-20260729}"
TOURNAMENT_BOTS="${TOURNAMENT_BOTS:-aggressor,economist,technologist,fortress}"
TOURNAMENT_MAPS="${TOURNAMENT_MAPS:-coldrage doubles tournament-island abwinter}"
RESULTS_ROOT="${TOURNAMENT_RESULTS_DIR:-${PROJECT_DIR}/../tournament-results}"
TOURNAMENT_ID=$(date -u +%Y%m%dT%H%M%SZ)
RESULTS_DIR="${RESULTS_ROOT}/${TOURNAMENT_ID}"

mkdir -p "${RESULTS_DIR}"

match=1
map_index=0
set -- ${TOURNAMENT_MAPS}
map_count=$#

while [ "${match}" -le "${MATCH_COUNT}" ]; do
	map_index=$((map_index % map_count + 1))
	eval "match_map=\${${map_index}}"
	match_seed=$((TOURNAMENT_SEED + match))
	result_file=$(printf "%s/match-%02d.json" "${RESULTS_DIR}" "${match}")

	printf "Starting match %d/%d on %s with seed %d\n" \
		"${match}" "${MATCH_COUNT}" "${match_map}" "${match_seed}"

	SIMULATION_BOTS="${TOURNAMENT_BOTS}" \
	SIMULATION_HEADLESS="${TOURNAMENT_HEADLESS}" \
	SIMULATION_MAX_TICKS="${MATCH_MAX_TICKS}" \
	SIMULATION_WATCHDOG_SECONDS="${MATCH_WATCHDOG_SECONDS}" \
	SIMULATION_MATCH_ID="$(printf "match-%02d" "${match}")" \
	SIMULATION_SEED="${match_seed}" \
	SIMULATION_RESULT="${result_file}" \
	"${PROJECT_DIR}/run-simulation.sh" "${match_map}"

	match=$((match + 1))
done

if command -v jq >/dev/null 2>&1; then
	jq -s \
		'. as $matches | {
			matchCount: length,
			endReasons: (group_by(.endReason) | map({reason: .[0].endReason, matches: length})),
			naturalWins: (
				[.[].naturalWinners[].botType]
				| group_by(.)
				| map({bot: .[0], wins: length})
			),
			scoreLeads: (
				[.[].scoreLeader.botType]
				| group_by(.)
				| map({bot: .[0], leads: length})
			),
			standings: (
				[.[].players[]]
				| group_by(.botType)
				| map(
					.[0].botType as $bot
					| {
						bot: $bot,
						naturalWins: (
							$matches
							| [.[].naturalWinners[].botType]
							| map(select(. == $bot))
							| length
						),
						scoreLeads: (
							$matches
							| [.[].scoreLeader.botType]
							| map(select(. == $bot))
							| length
						),
						averageScore: ((map(.score) | add) / length | round),
						averageArmyValue: ((map(.armyValue) | add) / length | round),
						averageAssetsValue: ((map(.assetsValue) | add) / length | round),
						averageCashAndResources: ((map(.cashAndResources) | add) / length | round),
						averageSpent: ((map(.spent) | add) / length | round)
					}
				)
				| sort_by(-.naturalWins, -.scoreLeads, -.averageScore)
			),
			matches: .
		}' \
		"${RESULTS_DIR}"/match-*.json > "${RESULTS_DIR}/tournament.json"
fi

printf "Tournament completed. Results: %s\n" "${RESULTS_DIR}"
