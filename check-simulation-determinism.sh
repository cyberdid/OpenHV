#!/bin/sh

set -e

PROJECT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
CHECK_MAP="${1:-coldrage}"
CHECK_SEED="${CHECK_SEED:-424242}"
CHECK_MAX_TICKS="${CHECK_MAX_TICKS:-200}"
CHECK_BOTS="${CHECK_BOTS:-aggressor,economist}"
CHECK_WATCHDOG_SECONDS="${CHECK_WATCHDOG_SECONDS:-60}"
CHECK_RESULTS_DIR="${CHECK_RESULTS_DIR:-$(mktemp -d /tmp/universe-determinism.XXXXXX)}"
SCHEMA_PATH="${PROJECT_DIR}/schemas/simulation-result-v1.schema.json"

if ! command -v jq >/dev/null 2>&1; then
	echo "jq is required for the determinism check." >&2
	exit 1
fi

mkdir -p "${CHECK_RESULTS_DIR}"

run_match()
{
	run_id="$1"
	SIMULATION_BOTS="${CHECK_BOTS}" \
	SIMULATION_MAX_TICKS="${CHECK_MAX_TICKS}" \
	SIMULATION_WATCHDOG_SECONDS="${CHECK_WATCHDOG_SECONDS}" \
	SIMULATION_SEED="${CHECK_SEED}" \
	SIMULATION_MATCH_ID="${run_id}" \
	SIMULATION_RESULT="${CHECK_RESULTS_DIR}/${run_id}.json" \
	"${PROJECT_DIR}/run-simulation.sh" "${CHECK_MAP}"

	jq -S \
		'del(.startedUtc, .endedUtc, .config.matchId, .config.resultPath)' \
		"${CHECK_RESULTS_DIR}/${run_id}.json" > "${CHECK_RESULTS_DIR}/${run_id}.normalized.json"
}

run_match run-a
run_match run-b

if ! cmp -s \
	"${CHECK_RESULTS_DIR}/run-a.normalized.json" \
	"${CHECK_RESULTS_DIR}/run-b.normalized.json"; then
	diff -u \
		"${CHECK_RESULTS_DIR}/run-a.normalized.json" \
		"${CHECK_RESULTS_DIR}/run-b.normalized.json" || true
	echo "Determinism check failed: normalized results differ." >&2
	exit 1
fi

python3 - "${SCHEMA_PATH}" \
	"${CHECK_RESULTS_DIR}/run-a.json" \
	"${CHECK_RESULTS_DIR}/run-b.json" <<'PY'
import json
import sys

from jsonschema import Draft202012Validator, FormatChecker

schema_path, *result_paths = sys.argv[1:]
with open(schema_path, encoding="utf-8") as schema_file:
    schema = json.load(schema_file)

validator = Draft202012Validator(schema, format_checker=FormatChecker())
for result_path in result_paths:
    with open(result_path, encoding="utf-8") as result_file:
        validator.validate(json.load(result_file))
PY

if SIMULATION_BOTS=does-not-exist \
	SIMULATION_MAX_TICKS=1 \
	SIMULATION_WATCHDOG_SECONDS=10 \
	"${PROJECT_DIR}/run-simulation.sh" "${CHECK_MAP}" >/dev/null 2>&1; then
	echo "Invalid bot check failed: the process unexpectedly returned success." >&2
	exit 1
fi

jq -r \
	'"Determinism check passed at world tick \(.worldTick) with sync hash \(.synchronizedStateHash)."' \
	"${CHECK_RESULTS_DIR}/run-a.json"
printf "Validated artifacts: %s\n" "${CHECK_RESULTS_DIR}"
