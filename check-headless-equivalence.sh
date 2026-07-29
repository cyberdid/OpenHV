#!/bin/sh

set -e

PROJECT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
CHECK_MAP="${1:-coldrage}"
CHECK_SEED="${CHECK_SEED:-424242}"
CHECK_MAX_TICKS="${CHECK_MAX_TICKS:-500}"
CHECK_BOTS="${CHECK_BOTS:-aggressor,economist}"
CHECK_WATCHDOG_SECONDS="${CHECK_WATCHDOG_SECONDS:-60}"
CHECK_RESULTS_DIR="${CHECK_RESULTS_DIR:-$(mktemp -d /tmp/universe-headless-equivalence.XXXXXX)}"
SCHEMA_PATH="${PROJECT_DIR}/schemas/simulation-result-v1.schema.json"

if ! command -v jq >/dev/null 2>&1; then
	echo "jq is required for the headless equivalence check." >&2
	exit 1
fi

mkdir -p "${CHECK_RESULTS_DIR}"

run_match()
{
	run_id="$1"
	headless="$2"
	log_path="${CHECK_RESULTS_DIR}/${run_id}.log"

	if ! SIMULATION_HEADLESS="${headless}" \
		SIMULATION_BOTS="${CHECK_BOTS}" \
		SIMULATION_MAX_TICKS="${CHECK_MAX_TICKS}" \
		SIMULATION_WATCHDOG_SECONDS="${CHECK_WATCHDOG_SECONDS}" \
		SIMULATION_SEED="${CHECK_SEED}" \
		SIMULATION_MATCH_ID="${run_id}" \
		SIMULATION_RESULT="${CHECK_RESULTS_DIR}/${run_id}.json" \
		"${PROJECT_DIR}/run-simulation.sh" "${CHECK_MAP}" >"${log_path}" 2>&1; then
		cat "${log_path}" >&2
		exit 1
	fi

	cat "${log_path}"
	jq -S \
		'del(
			.startedUtc,
			.endedUtc,
			.config.matchId,
			.config.resultPath,
			.config.headless,
			.build.executionMode
		)' \
		"${CHECK_RESULTS_DIR}/${run_id}.json" > "${CHECK_RESULTS_DIR}/${run_id}.normalized.json"
}

run_match graphical false
run_match headless true

if grep -E "Using SDL|OpenGL renderer|Using default sound device" "${CHECK_RESULTS_DIR}/headless.log" >/dev/null; then
	echo "Headless check failed: a graphical or audio backend was initialized." >&2
	exit 1
fi

if ! grep -F "Using headless graphics and sound platform." "${CHECK_RESULTS_DIR}/headless.log" >/dev/null; then
	echo "Headless check failed: the no-op platform was not selected." >&2
	exit 1
fi

if ! cmp -s \
	"${CHECK_RESULTS_DIR}/graphical.normalized.json" \
	"${CHECK_RESULTS_DIR}/headless.normalized.json"; then
	diff -u \
		"${CHECK_RESULTS_DIR}/graphical.normalized.json" \
		"${CHECK_RESULTS_DIR}/headless.normalized.json" || true
	echo "Headless check failed: synchronized results differ from graphical reference." >&2
	exit 1
fi

python3 - "${SCHEMA_PATH}" \
	"${CHECK_RESULTS_DIR}/graphical.json" \
	"${CHECK_RESULTS_DIR}/headless.json" <<'PY'
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

jq -r \
	'"Headless equivalence passed at world tick \(.worldTick) with sync hash \(.synchronizedStateHash)."' \
	"${CHECK_RESULTS_DIR}/headless.json"
printf "Validated artifacts: %s\n" "${CHECK_RESULTS_DIR}"
