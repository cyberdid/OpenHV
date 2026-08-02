#!/usr/bin/env python3

import json
import os
from pathlib import Path
import subprocess
import sys

from jsonschema import Draft202012Validator, FormatChecker


def run(project: Path, output_dir: Path, ticks: int) -> dict:
    result_path = output_dir / f"result-{ticks}.json"
    log_path = output_dir / f"simulation-{ticks}.log"
    support_dir = output_dir / f"support-{ticks}"
    env = os.environ.copy()
    env.update({
        "SIMULATION_HEADLESS": "true",
        "SIMULATION_BOTS": "aggressor,economist,aggressor,economist",
        "SIMULATION_SEED": "424242",
        "SIMULATION_MAX_TICKS": str(ticks),
        "SIMULATION_WATCHDOG_SECONDS": "240",
        "SIMULATION_RESULT": str(result_path),
        "OPENHV_SUPPORT_DIR": str(support_dir),
    })
    with log_path.open("w", encoding="utf-8") as log:
        completed = subprocess.run(
            [str(project / "run-simulation.sh"), "coldrage"],
            cwd=project,
            env=env,
            stdout=log,
            stderr=subprocess.STDOUT,
            check=False,
        )
    if completed.returncode:
        sys.stderr.write(log_path.read_text(encoding="utf-8"))
        raise SystemExit(completed.returncode)
    return json.loads(result_path.read_text(encoding="utf-8"))


def active_planet(result: dict) -> dict:
    return next(planet for planet in result["universe"]["planets"] if planet["active"])


def main() -> int:
    project = Path(__file__).resolve().parent
    output_value = os.environ.get("CHECK_RESULTS_DIR")
    if not output_value:
        output_value = subprocess.check_output(
            ["mktemp", "-d", "/tmp/universe-biosphere-emergence.XXXXXX"],
            text=True,
        ).strip()
    output_dir = Path(output_value)
    output_dir.mkdir(parents=True, exist_ok=True)

    early_ticks = int(os.environ.get("CHECK_EARLY_TICKS", "500"))
    emergence_ticks = int(os.environ.get("CHECK_EMERGENCE_TICKS", "10000"))
    if early_ticks < 250 or early_ticks % 250:
        raise ValueError("CHECK_EARLY_TICKS must be a positive multiple of 250")
    if emergence_ticks <= early_ticks or emergence_ticks % 250:
        raise ValueError("CHECK_EMERGENCE_TICKS must be a larger multiple of 250")

    early_result = run(project, output_dir, early_ticks)
    emergence_result = run(project, output_dir, emergence_ticks)
    schema = json.loads(
        (project / "schemas/simulation-result-v1.schema.json").read_text(encoding="utf-8")
    )
    validator = Draft202012Validator(schema, format_checker=FormatChecker())
    validator.validate(early_result)
    validator.validate(emergence_result)

    early = active_planet(early_result)
    assert early["lifecycleStage"] == "lifeless"
    assert early["biosphere"]["pulseSequence"] == early_ticks // 250
    assert early["biosphere"]["lifeOriginated"] is False
    assert early["biosphere"]["livingCellCount"] == 0
    assert early["biosphere"]["meanBiomassPerMille"] == 0
    assert early["biosphere"]["originLatitudeIndex"] == -1
    assert early["biosphere"]["originLongitudeIndex"] == -1

    emerged = active_planet(emergence_result)
    biosphere = emerged["biosphere"]
    assert emerged["lifecycleStage"] == "biosphere"
    assert biosphere["pulseSequence"] == emergence_ticks // 250
    assert biosphere["lifeOriginated"] is True
    assert 0 <= biosphere["originLatitudeIndex"] < emerged["surface"]["latitudeCells"]
    assert 0 <= biosphere["originLongitudeIndex"] < emerged["surface"]["longitudeCells"]
    assert biosphere["habitableCellCount"] > 0
    assert biosphere["livingCellCount"] > 0
    assert biosphere["maximumComplexityMillionths"] > 0
    assert biosphere["maximumAbiogenesisProgressUnits"] >= 3000
    assert emergence_result["universe"]["macroEventSequence"] == 2 * biosphere["pulseSequence"] + 1
    for planet in emergence_result["universe"]["planets"]:
        if planet["active"]:
            continue
        assert planet["lifecycleStage"] == "lifeless"
        assert planet["biosphere"]["pulseSequence"] == 0
        assert planet["biosphere"]["lifeOriginated"] is False

    report = {
        "schemaVersion": 1,
        "early": {
            "worldTicks": early_ticks,
            "stage": early["lifecycleStage"],
            "biosphereHash": early["biosphere"]["stateHash"],
            "livingCellCount": early["biosphere"]["livingCellCount"],
        },
        "emergence": {
            "worldTicks": emergence_ticks,
            "stage": emerged["lifecycleStage"],
            "biosphereHash": biosphere["stateHash"],
            "originLatitudeIndex": biosphere["originLatitudeIndex"],
            "originLongitudeIndex": biosphere["originLongitudeIndex"],
            "habitableCellCount": biosphere["habitableCellCount"],
            "livingCellCount": biosphere["livingCellCount"],
            "maximumComplexityMillionths": biosphere["maximumComplexityMillionths"],
        },
    }
    (output_dir / "biosphere-emergence.json").write_text(
        json.dumps(report, indent=2) + "\n", encoding="utf-8"
    )
    print(
        "Biosphere emergence check passed: zero biomass at "
        f"{early_ticks} ticks; autonomous origin at cell "
        f"({biosphere['originLongitudeIndex']}, {biosphere['originLatitudeIndex']}) "
        f"by {emergence_ticks} ticks."
    )
    print(f"Validated artifacts: {output_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
