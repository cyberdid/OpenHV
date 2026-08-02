#!/bin/sh

set -e

PROJECT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
DOTNET_DIR="${DOTNET_DIR:-${PROJECT_DIR}/../.dotnet}"
CHECK_PULSES="${CHECK_PULSES:-10}"
CHECK_RESULTS_DIR="${CHECK_RESULTS_DIR:-$(mktemp -d /tmp/universe-physics-golden.XXXXXX)}"
RESULT_PATH="${CHECK_RESULTS_DIR}/planet-physics-golden.json"

mkdir -p "${CHECK_RESULTS_DIR}"
PATH="${DOTNET_DIR}:${PATH}" "${PROJECT_DIR}/utility.sh" \
	--planet-physics-golden "${RESULT_PATH}" "${CHECK_PULSES}"

python3 - "${PROJECT_DIR}/schemas/planet-physics-golden-v1.schema.json" "${RESULT_PATH}" <<'PY'
import json
import sys

from jsonschema import Draft202012Validator

with open(sys.argv[1], encoding="utf-8") as stream:
    schema = json.load(stream)
with open(sys.argv[2], encoding="utf-8") as stream:
    payload = json.load(stream)

Draft202012Validator.check_schema(schema)
Draft202012Validator(schema).validate(payload)

assert payload["schemaVersion"] == 1
assert payload["grid"] == {"latitudeCells": 180, "longitudeCells": 360}
scenarios = {row["name"]: row for row in payload["scenarios"]}
assert set(scenarios) == {
    "baseline", "high-greenhouse", "thin-atmosphere",
    "fast-rotation", "high-stellar-flux",
}

baseline = scenarios["baseline"]
greenhouse = scenarios["high-greenhouse"]
thin = scenarios["thin-atmosphere"]
fast = scenarios["fast-rotation"]
stellar = scenarios["high-stellar-flux"]

assert greenhouse["meanSurfaceTemperatureMilliKelvin"] > baseline["meanSurfaceTemperatureMilliKelvin"] + 500
assert greenhouse["radiativeEquilibriumMilliKelvin"] > baseline["radiativeEquilibriumMilliKelvin"]
assert thin["meanPressurePascals"] < baseline["meanPressurePascals"] * 7 // 10
assert fast["rossbyMillionths"] < baseline["rossbyMillionths"]
assert stellar["absorbedSolarWattsPerSquareMeter"] > baseline["absorbedSolarWattsPerSquareMeter"]
assert stellar["meanSurfaceTemperatureMilliKelvin"] > baseline["meanSurfaceTemperatureMilliKelvin"]

for row in scenarios.values():
    assert 100_000 <= row["meanSurfaceTemperatureMilliKelvin"] <= 900_000
    assert row["meanPressurePascals"] > 0
    assert 0 < row["meanWindCentimetersPerSecond"] < 10_000
    assert abs(row["meanLatentFluxMilliWattsPerSquareMeter"]) <= 1_500_000
    assert 0 <= row["meanVerticalVelocityMillimetersPerSecond"] <= 900
    assert row["waterBalanceErrorUnits"] == 0
    assert row["latentEnergyResidualMilliWattsPerSquareMeter"] == 0
    assert row["elevationBalanceErrorMeters"] == 0
    assert row["materialBalanceErrorUnits"] == 0

assert len({row["climateHash"] for row in scenarios.values()}) == len(scenarios)
assert len({row["geologyHash"] for row in scenarios.values()}) == len(scenarios)
PY

printf "Planet physics golden forcing check passed across %s scenarios and %s pulses.\n" 5 "${CHECK_PULSES}"
printf "Validated artifact: %s\n" "${RESULT_PATH}"
