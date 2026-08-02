#!/bin/sh

set -e

PROJECT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
CHECK_MAP="${1:-coldrage}"
CHECK_SEED="${CHECK_SEED:-112}"
CHECK_MAX_TICKS="${CHECK_MAX_TICKS:-500}"
CHECK_CHECKPOINT_TICK="${CHECK_CHECKPOINT_TICK:-250}"
CHECK_BOTS="${CHECK_BOTS:-aggressor,economist,aggressor,economist}"
CHECK_WATCHDOG_SECONDS="${CHECK_WATCHDOG_SECONDS:-60}"
CHECK_RESULTS_DIR="${CHECK_RESULTS_DIR:-$(mktemp -d /tmp/universe-checkpoint-equivalence.XXXXXX)}"
CHECKPOINT_NAME="universe-equivalence.orasav"
RESULT_SCHEMA_PATH="${PROJECT_DIR}/schemas/simulation-result-v1.schema.json"
CHECKPOINT_SCHEMA_PATH="${PROJECT_DIR}/schemas/universe-checkpoint-v1.schema.json"
SUPPORT_DIR="${CHECK_RESULTS_DIR}/support"

if ! command -v jq >/dev/null 2>&1; then
	echo "jq is required for the checkpoint equivalence check." >&2
	exit 1
fi

mkdir -p "${CHECK_RESULTS_DIR}"

SIMULATION_HEADLESS=true \
SIMULATION_BOTS="${CHECK_BOTS}" \
SIMULATION_MAX_TICKS="${CHECK_MAX_TICKS}" \
SIMULATION_WATCHDOG_SECONDS="${CHECK_WATCHDOG_SECONDS}" \
SIMULATION_SEED="${CHECK_SEED}" \
SIMULATION_MATCH_ID=checkpoint-continued \
SIMULATION_CHECKPOINT_WORLD_TICK="${CHECK_CHECKPOINT_TICK}" \
SIMULATION_CHECKPOINT_NAME="${CHECKPOINT_NAME}" \
SIMULATION_RESULT="${CHECK_RESULTS_DIR}/continued.json" \
OPENHV_SUPPORT_DIR="${SUPPORT_DIR}" \
	"${PROJECT_DIR}/run-simulation.sh" "${CHECK_MAP}"

checkpoint_path=$(find "${SUPPORT_DIR}/Saves" -type f -name "${CHECKPOINT_NAME}" -print -quit)
if [ -z "${checkpoint_path}" ] || [ ! -f "${checkpoint_path}.json" ]; then
	echo "Checkpoint equivalence failed: native save or JSON manifest is missing." >&2
	exit 1
fi

SIMULATION_HEADLESS=true \
SIMULATION_BOTS="${CHECK_BOTS}" \
SIMULATION_MAX_TICKS="${CHECK_MAX_TICKS}" \
SIMULATION_WATCHDOG_SECONDS="${CHECK_WATCHDOG_SECONDS}" \
SIMULATION_SEED="${CHECK_SEED}" \
SIMULATION_MATCH_ID=checkpoint-resumed \
SIMULATION_LOAD_CHECKPOINT="${CHECKPOINT_NAME}" \
SIMULATION_RESULT="${CHECK_RESULTS_DIR}/resumed.json" \
OPENHV_SUPPORT_DIR="${SUPPORT_DIR}" \
	"${PROJECT_DIR}/run-simulation.sh" "${CHECK_MAP}"

for result in continued resumed; do
	jq -S \
		'del(
			.startedUtc,
			.endedUtc,
			.config.matchId,
			.config.resultPath,
			.config.checkpointWorldTick,
			.config.checkpointName,
			.config.loadCheckpointName
		)' \
		"${CHECK_RESULTS_DIR}/${result}.json" > "${CHECK_RESULTS_DIR}/${result}.normalized.json"
done

if ! cmp -s \
	"${CHECK_RESULTS_DIR}/continued.normalized.json" \
	"${CHECK_RESULTS_DIR}/resumed.normalized.json"; then
	diff -u \
		"${CHECK_RESULTS_DIR}/continued.normalized.json" \
		"${CHECK_RESULTS_DIR}/resumed.normalized.json" || true
	echo "Checkpoint equivalence failed: continued and resumed branches differ." >&2
	exit 1
fi

python3 - \
	"${RESULT_SCHEMA_PATH}" \
	"${CHECKPOINT_SCHEMA_PATH}" \
	"${checkpoint_path}.json" \
	"${CHECK_RESULTS_DIR}/continued.json" \
	"${CHECK_RESULTS_DIR}/resumed.json" <<'PY'
import json
import sys

from jsonschema import Draft202012Validator, FormatChecker
from referencing import Registry, Resource

result_schema_path, checkpoint_schema_path, checkpoint_path, *result_paths = sys.argv[1:]
with open(result_schema_path, encoding="utf-8") as stream:
    result_schema = json.load(stream)
with open(checkpoint_schema_path, encoding="utf-8") as stream:
    checkpoint_schema = json.load(stream)

result_validator = Draft202012Validator(result_schema, format_checker=FormatChecker())
for result_path in result_paths:
    with open(result_path, encoding="utf-8") as stream:
        result_validator.validate(json.load(stream))

registry = Registry().with_resource(
    result_schema["$id"], Resource.from_contents(result_schema)
)
checkpoint_validator = Draft202012Validator(
    checkpoint_schema,
    registry=registry,
    format_checker=FormatChecker(),
)
with open(checkpoint_path, encoding="utf-8") as stream:
    checkpoint_validator.validate(json.load(stream))
PY

jq -r \
	'"Checkpoint equivalence passed at world tick \(.worldTick) with sync hash \(.synchronizedStateHash) and BotRandom count \(.botRandomTotalCount)."' \
	"${CHECK_RESULTS_DIR}/resumed.json"
printf "Validated artifacts: %s\n" "${CHECK_RESULTS_DIR}"
