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
    support_dir = Path(os.environ["OPENHV_SUPPORT_DIR"])
    replay_dir = support_dir / "Replays" / "hv" / "test"
    replay_dir.mkdir(parents=True)
    (support_dir / "Logs").mkdir()
    (support_dir / "Logs" / "client.log").write_text(
        f"Synthetic log for {map_name}.\n", encoding="utf-8"
    )
    (replay_dir / f"{map_name}.orarep").write_bytes(b"synthetic replay")
    telemetry_interval = int(os.environ["SIMULATION_TELEMETRY_INTERVAL_TICKS"])
    if telemetry_interval > 0:
        (result_path.parent / "telemetry.jsonl").write_text(
            json.dumps({
                "schemaVersion": 1,
                "recordType": "snapshot",
                "matchId": os.environ["SIMULATION_MATCH_ID"],
                "worldTick": 0,
                "syntheticProcessId": os.getpid(),
            }) + "\n",
            encoding="utf-8",
        )
        (result_path.parent / "events.jsonl").write_text(
            json.dumps({
                "schemaVersion": 1,
                "recordType": "event",
                "matchId": os.environ["SIMULATION_MATCH_ID"],
                "worldTick": 0,
                "eventType": "match-start",
                "reasonCode": "synthetic",
            }) + "\n",
            encoding="utf-8",
        )
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
    telemetry = telemetry_interval
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
    settlement = {
        "settlementId": "Multi0-1",
        "actorId": 1,
        "actorType": "base",
        "foundedTick": 0,
        "cellX": 1,
        "cellY": 1,
        "population": 1000,
        "children": 220,
        "adults": 650,
        "elders": 130,
        "workforce": 650,
        "employed": 650,
        "housing": 1200,
        "jobs": 700,
        "food": 600,
        "materials": 400,
        "energy": 300,
        "knowledge": 0,
        "foodStorage": 3000,
        "materialsStorage": 2000,
        "energyStorage": 1500,
        "foodProduction": 30,
        "materialsProduction": 12,
        "energyProduction": 10,
        "knowledgeProduction": 2,
        "foodDemand": 25,
        "materialsDemand": 2,
        "energyDemand": 8,
        "foodSatisfaction": 1000,
        "housingSatisfaction": 1000,
        "energySatisfaction": 1000,
        "employmentSatisfaction": 1000,
        "prosperity": 1000,
        "stability": 1000,
        "migrationPressure": 0,
        "lastPopulationDelta": 0,
        "civilPulseCount": 1,
        "demographicPulseCount": 0,
        "infrastructureCount": 1,
    }
    civilization = {
        "model": "living-factions-v1",
        "foundedTick": 0,
        "population": 1000,
        "children": 220,
        "adults": 650,
        "elders": 130,
        "workforce": 650,
        "employed": 650,
        "housing": 1200,
        "jobs": 700,
        "food": 600,
        "materials": 400,
        "energy": 300,
        "knowledge": 0,
        "foodProduction": 30,
        "materialsProduction": 12,
        "energyProduction": 10,
        "knowledgeProduction": 2,
        "prosperity": 1000,
        "stability": 1000,
        "migrationPressure": 0,
        "settlements": [settlement],
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
        "civilization": civilization,
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
            "scenarioMode": os.environ["SIMULATION_SCENARIO_MODE"],
            "observationHorizonTicks": int(
                os.environ["SIMULATION_OBSERVATION_HORIZON_TICKS"]
            ),
            "stalemateWindowTicks": int(
                os.environ["SIMULATION_STALEMATE_WINDOW_TICKS"]
            ),
            "stalemateTerminates": (
                os.environ["SIMULATION_STALEMATE_TERMINATES"].lower() == "true"
            ),
            "collapsePopulationThreshold": int(
                os.environ["SIMULATION_COLLAPSE_POPULATION_THRESHOLD"]
            ),
            "collapseStabilityThreshold": int(
                os.environ["SIMULATION_COLLAPSE_STABILITY_THRESHOLD"]
            ),
            "watchdogSeconds": watchdog,
            "telemetryIntervalTicks": telemetry,
            "civilizationProfile": os.environ["SIMULATION_CIVILIZATION_PROFILE"],
            "tradeEnabled": os.environ["SIMULATION_TRADE_ENABLED"].lower() == "true",
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
        "botRandomTotalCount": max_ticks,
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
