#!/usr/bin/env python3

"""Small process-compatible simulation stub for batch-runner integration tests."""

from __future__ import annotations

import datetime as dt
import hashlib
import json
import os
import sys
import time
from pathlib import Path


def timestamp() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")


def main() -> int:
    map_name = sys.argv[1]
    result_path = Path(os.environ["SIMULATION_RESULT"])
    if map_name == "invalid":
        print(
            "Exception of type `System.ArgumentException`: "
            "Unknown simulation bot type(s): does-not-exist.",
            file=sys.stderr,
        )
        return 7

    if map_name == "flaky":
        marker = result_path.parent / ".fake-flaky-completed"
        if not marker.exists():
            marker.write_text("first attempt failed\n", encoding="utf-8")
            print("Synthetic transient process failure.", file=sys.stderr)
            return 9

    if map_name == "interrupt-once":
        (result_path.parent / ".fake-child-pid").write_text(
            str(os.getpid()), encoding="utf-8"
        )
        marker = result_path.parent / ".fake-interrupt-started"
        if not marker.exists():
            marker.write_text("first attempt started\n", encoding="utf-8")
            time.sleep(30)

    if map_name == "hang":
        (result_path.parent / ".fake-child-pid").write_text(
            str(os.getpid()), encoding="utf-8"
        )
        time.sleep(30)

    started = timestamp()
    match_id = os.environ["SIMULATION_MATCH_ID"]
    bots = os.environ["SIMULATION_BOTS"].split(",")
    seed = int(os.environ["SIMULATION_SEED"])
    max_ticks = int(os.environ["SIMULATION_MAX_TICKS"])
    watchdog = int(os.environ["SIMULATION_WATCHDOG_SECONDS"])
    telemetry = int(os.environ["SIMULATION_TELEMETRY_INTERVAL_TICKS"])
    headless = os.environ["SIMULATION_HEADLESS"].lower() == "true"
    state_hash = hashlib.sha256(
        f"{map_name}:{seed}:{max_ticks}:{','.join(bots)}".encode()
    ).hexdigest()[:8].upper()
    player_config = {
        "slot": "Multi0",
        "playerName": "Fake AI",
        "botType": bots[0],
        "faction": "fake",
        "team": 0,
        "color": "AABBCC",
        "spawnPoint": 1,
        "homeCellX": 1,
        "homeCellY": 1,
    }
    player_result = {
        **player_config,
        "outcome": "undefined",
        "score": 100,
        "experience": 0,
        "killsValue": 0,
        "deathsValue": 0,
        "unitsKilled": 0,
        "unitsLost": 0,
        "buildingsKilled": 0,
        "buildingsLost": 0,
        "armyValue": 0,
        "assetsValue": 100,
        "cashAndResources": 0,
        "earned": 0,
        "spent": 0,
    }
    result = {
        "schemaVersion": 1,
        "build": {
            "engineVersion": "fake-engine",
            "modId": "hv",
            "modVersion": "test",
            "gitCommit": "fake-commit",
            "gitDirty": False,
            "executionMode": "headless" if headless else "graphical",
        },
        "config": {
            "schemaVersion": 1,
            "matchId": match_id,
            "mapRequest": map_name,
            "mapUid": f"fake-{map_name}",
            "mapTitle": map_name,
            "mapHash": f"fake-{map_name}",
            "botTypes": bots,
            "headless": headless,
            "deterministicSimulation": True,
            "gameSpeed": os.environ["SIMULATION_SPEED"],
            "gameTimestepMilliseconds": 20,
            "requestedRandomSeed": seed,
            "effectiveRandomSeed": seed,
            "maxWorldTicks": max_ticks,
            "watchdogSeconds": watchdog,
            "telemetryIntervalTicks": telemetry,
            "gitCommit": "fake-commit",
            "gitDirty": False,
            "resultPath": str(result_path),
            "players": [player_config],
        },
        "endReason": "world-tick-limit",
        "endDetail": f"Reached configured world tick limit {max_ticks}.",
        "startedUtc": started,
        "endedUtc": timestamp(),
        "worldTick": max_ticks,
        "simulatedSeconds": max_ticks * 0.02,
        "synchronizedStateHash": state_hash,
        "naturalWinners": [],
        "scoreLeader": {
            "playerName": "Fake AI",
            "botType": bots[0],
            "faction": "fake",
        },
        "players": [player_result],
    }
    result_path.parent.mkdir(parents=True, exist_ok=True)
    temporary = result_path.with_suffix(".tmp")
    with temporary.open("w", encoding="utf-8") as stream:
        json.dump(result, stream, indent=2)
        stream.write("\n")
    os.replace(temporary, result_path)
    print(f"Synthetic simulation result written to {result_path}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
