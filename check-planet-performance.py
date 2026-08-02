#!/usr/bin/env python3

import json
import os
from pathlib import Path
import resource
import subprocess
import sys
import time

from jsonschema import Draft202012Validator, FormatChecker


def main() -> int:
    project = Path(__file__).resolve().parent
    ticks = int(os.environ.get("CHECK_MAX_TICKS", "2500"))
    elapsed_budget = float(os.environ.get("CHECK_ELAPSED_BUDGET_SECONDS", "30"))
    rss_budget_mib = int(os.environ.get("CHECK_RSS_BUDGET_MIB", "1536"))
    save_budget_mib = int(os.environ.get("CHECK_SAVE_BUDGET_MIB", "16"))
    if ticks < 500 or ticks % 250:
        raise ValueError("CHECK_MAX_TICKS must be at least 500 and divisible by 250")

    output_value = os.environ.get("CHECK_RESULTS_DIR")
    if not output_value:
        output_value = subprocess.check_output(
            ["mktemp", "-d", "/tmp/universe-planet-performance.XXXXXX"],
            text=True,
        ).strip()
    output_dir = Path(output_value)
    output_dir.mkdir(parents=True, exist_ok=True)
    support_dir = output_dir / "support"
    result_path = output_dir / "result.json"
    log_path = output_dir / "simulation.log"
    checkpoint_tick = ticks - 250
    checkpoint_name = "planet-performance.orasav"

    env = os.environ.copy()
    env.update({
        "SIMULATION_HEADLESS": "true",
        "SIMULATION_BOTS": "aggressor,economist,aggressor,economist",
        "SIMULATION_SEED": "424242",
        "SIMULATION_MAX_TICKS": str(ticks),
        "SIMULATION_WATCHDOG_SECONDS": "120",
        "SIMULATION_CHECKPOINT_WORLD_TICK": str(checkpoint_tick),
        "SIMULATION_CHECKPOINT_NAME": checkpoint_name,
        "SIMULATION_RESULT": str(result_path),
        "OPENHV_SUPPORT_DIR": str(support_dir),
    })

    started = time.monotonic()
    with log_path.open("w", encoding="utf-8") as log:
        completed = subprocess.run(
            [str(project / "run-simulation.sh"), "coldrage"],
            cwd=project,
            env=env,
            stdout=log,
            stderr=subprocess.STDOUT,
            check=False,
        )
    elapsed = time.monotonic() - started
    if completed.returncode:
        sys.stderr.write(log_path.read_text(encoding="utf-8"))
        return completed.returncode

    raw_rss = resource.getrusage(resource.RUSAGE_CHILDREN).ru_maxrss
    rss_mib = raw_rss / (1024 * 1024) if sys.platform == "darwin" else raw_rss / 1024
    with (project / "schemas/simulation-result-v1.schema.json").open(encoding="utf-8") as stream:
        schema = json.load(stream)
    with result_path.open(encoding="utf-8") as stream:
        result = json.load(stream)
    Draft202012Validator(schema, format_checker=FormatChecker()).validate(result)

    active = next(planet for planet in result["universe"]["planets"] if planet["active"])
    surface = active["surface"]
    assert surface["cellCount"] == 64_800
    assert surface["chunkCount"] == 450
    assert surface["climatePulseSequence"] == ticks // 250
    assert surface["geologyPulseSequence"] == surface["climatePulseSequence"]
    for field in (
        "waterBalanceErrorUnits",
        "latentEnergyResidualMilliWattsPerSquareMeter",
        "elevationBalanceErrorMeters",
        "materialBalanceErrorUnits",
    ):
        assert surface[field] == 0

    saves = list((support_dir / "Saves").rglob(checkpoint_name))
    assert len(saves) == 1
    save_bytes = saves[0].stat().st_size
    save_budget_bytes = save_budget_mib * 1024 * 1024
    assert save_bytes <= save_budget_bytes, (save_bytes, save_budget_bytes)
    assert elapsed <= elapsed_budget, (elapsed, elapsed_budget)
    assert rss_mib <= rss_budget_mib, (rss_mib, rss_budget_mib)

    report = {
        "schemaVersion": 1,
        "worldTicks": ticks,
        "climatePulses": surface["climatePulseSequence"],
        "cellCount": surface["cellCount"],
        "chunkCount": surface["chunkCount"],
        "elapsedSeconds": round(elapsed, 3),
        "maximumResidentSetMiB": round(rss_mib, 3),
        "checkpointBytes": save_bytes,
        "budgets": {
            "elapsedSeconds": elapsed_budget,
            "maximumResidentSetMiB": rss_budget_mib,
            "checkpointBytes": save_budget_bytes,
        },
        "synchronizedStateHash": result["synchronizedStateHash"],
        "geologyHash": surface["geologyHash"],
        "climateHash": surface["climateHash"],
    }
    report_path = output_dir / "performance.json"
    report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(
        f"Planet performance check passed: {elapsed:.3f}s, {rss_mib:.1f} MiB RSS, "
        f"{save_bytes / 1024 / 1024:.2f} MiB checkpoint."
    )
    print(f"Validated artifacts: {output_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
