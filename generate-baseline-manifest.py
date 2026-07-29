#!/usr/bin/env python3

"""Generate the balanced 112-match Universe civil/military baseline manifest."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
from typing import Any


MAPS = (
    ("coldrage", "coldrage"),
    ("doubles", "doubles"),
    ("tournament-island", "island"),
    ("abwinter", "winter"),
)
PROFILES = ("aggressor", "economist", "technologist", "fortress")
REPLICATES = 7
SEED_BASE = 820_000
VARIANTS = {
    "baseline": (
        "baseline-112-v1",
        "112-match held-out civil/military baseline: four maps, four "
        "profile-slot rotations, and seven deterministic seeds per cell.",
    ),
    "ai003-candidate": (
        "ai003-candidate-112-v1",
        "AI-003 opening/economy/technology planner candidate on the exact "
        "baseline-112-v1 map, slot, profile, and seed schedule.",
    ),
}


def build_manifest(variant: str = "baseline") -> dict[str, Any]:
    run_id, description = VARIANTS[variant]
    matches = []
    for map_index, (map_name, map_label) in enumerate(MAPS):
        for rotation in range(len(PROFILES)):
            bots = list(PROFILES[rotation:] + PROFILES[:rotation])
            for replicate in range(1, REPLICATES + 1):
                matches.append(
                    {
                        "id": (
                            f"baseline-{map_label}-r{rotation + 1}-"
                            f"s{replicate:02d}"
                        ),
                        "map": map_name,
                        "seed": (
                            SEED_BASE
                            + map_index * 1_000
                            + rotation * 100
                            + replicate
                        ),
                        "bots": bots,
                    }
                )

    return {
        "schemaVersion": 1,
        "runId": run_id,
        "description": description,
        "defaults": {
            "bots": list(PROFILES),
            "headless": True,
            "gameSpeed": "fastest",
            "maxWorldTicks": 12_000,
            "scenarioMode": "conflict",
            "observationHorizonTicks": 0,
            "stalemateWindowTicks": 2_000,
            "stalemateTerminates": False,
            "collapsePopulationThreshold": 250,
            "collapseStabilityThreshold": 250,
            "watchdogSeconds": 180,
            "telemetryIntervalTicks": 1_000,
            "civilizationProfile": "balanced",
            "tradeEnabled": True,
        },
        "runner": {
            "workers": 4,
            "maxInfrastructureRetries": 1,
            "processTimeoutGraceSeconds": 60,
            "successfulReplaySampleEvery": 28,
        },
        "matches": matches,
    }


def write_atomic(path: Path, document: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.{os.getpid()}.tmp")
    with temporary.open("w", encoding="utf-8") as stream:
        json.dump(document, stream, indent=2)
        stream.write("\n")
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary, path)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--variant",
        choices=tuple(VARIANTS),
        default="baseline",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("batch-manifests/baseline-112-v1.json"),
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="Fail if the output differs from the generated manifest.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    manifest = build_manifest(args.variant)
    output = args.output.resolve()
    if args.check:
        if not output.is_file():
            raise SystemExit(f"Generated manifest is missing: {output}")
        actual = json.loads(output.read_text(encoding="utf-8"))
        if actual != manifest:
            raise SystemExit(f"Generated manifest is stale: {output}")
        print(f"Manifest is current: {output}")
        return 0

    write_atomic(output, manifest)
    print(f"Wrote {len(manifest['matches'])} matches to {output}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
