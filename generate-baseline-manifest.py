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
# Matches resolve naturally around tick 60,000: a four-seed probe on the current
# build produced one natural victory at 59,309 and two or three eliminations in
# the rest, where the same probe before the economy and diplomacy work produced
# one elimination and no endings. The 12,000-tick schedule therefore asks why a
# match has not finished after a fifth of it, which is why seven candidates in a
# row could not move the tick-ceiling rate.
LONG_MATCH_TICKS = 60_000

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
    "ai003-candidate-v2": (
        "ai003-candidate-112-v2",
        "AI-003 corrected planner candidate on the exact baseline-112-v1 "
        "map, slot, profile, and seed schedule.",
    ),
    "ai003-candidate-v3": (
        "ai003-candidate-112-v3",
        "AI-003 consistent military-value candidate on the exact "
        "baseline-112-v1 map, slot, profile, and seed schedule.",
    ),
    "ai004-candidate": (
        "ai004-candidate-112-v1",
        "AI-004 target-scoring, retreat, and regroup candidate on the exact "
        "baseline-112-v1 map, slot, profile, and seed schedule.",
    ),
    "baseline-v2": (
        "baseline-112-v2",
        "Baseline re-measured on the same schedule at the commit immediately "
        "before AI-005, so the candidate differs by the regroup change alone.",
    ),
    "ai005-candidate": (
        "ai005-candidate-112-v1",
        "AI-005 regroup locality and early re-engagement candidate on the "
        "exact baseline-112-v1 map, slot, profile, and seed schedule.",
    ),
    "econ001-candidate": (
        "econ001-candidate-112-v1",
        "ECON-001 levelling trade candidate on the exact baseline-112-v1 map, "
        "slot, profile, and seed schedule.",
    ),
    "econ002-candidate": (
        "econ002-candidate-112-v1",
        "ECON-002 reachable technology tree candidate on the exact "
        "baseline-112-v1 map, slot, profile, and seed schedule.",
    ),
    "dip001-candidate": (
        "dip001-candidate-112-v1",
        "DIP-001 strategic interval candidate on the exact baseline-112-v1 "
        "map, slot, profile, and seed schedule.",
    ),
    "baseline-v3": (
        "baseline-112-v3",
        "Baseline re-measured after the art work, which reaches the simulation "
        "because the swarm is a selectable random faction.",
    ),
    "dip002-candidate": (
        "dip002-candidate-112-v1",
        "DIP-002 compressed disposition candidate on the exact baseline-112-v1 "
        "map, slot, profile, and seed schedule.",
    ),
    "dip003-candidate": (
        "dip003-candidate-112-v1",
        "DIP-003 undivided relative power candidate on the exact "
        "baseline-112-v1 map, slot, profile, and seed schedule.",
    ),
    "dip004-candidate": (
        "dip004-candidate-112-v1",
        "DIP-004 symmetric trade restraint candidate on the exact "
        "baseline-112-v1 map, slot, profile, and seed schedule.",
    ),
    "baseline-long": (
        "baseline-long-112-v1",
        "The baseline schedule run to 60,000 ticks, long enough for matches to "
        "reach a natural end, so the ending rate becomes a live metric.",
    ),
    "dip005b-candidate": (
        "dip005b-candidate-112-v1",
        "DIP-005b belligerence baseline halved to 100, bracketing the constant "
        "that made the world decisive at 200 but moved three score-lead rates.",
    ),
    "dip005-candidate": (
        "dip005-candidate-112-v1",
        "DIP-005 belligerence baseline candidate on the long schedule, which "
        "is the only one where the ending rate this candidate targets is a "
        "live metric at all.",
    ),
}

# Candidates aimed at how a match ends have to be measured on the schedule
# where matches can end. Keep this beside VARIANTS so adding one is a single
# decision rather than two conditions further down.
LONG_SCHEDULE_VARIANTS = frozenset(
    {"baseline-long", "dip005-candidate", "dip005b-candidate"}
)


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
            "maxWorldTicks": (
                LONG_MATCH_TICKS
                if variant in LONG_SCHEDULE_VARIANTS
                else 12_000
            ),
            "scenarioMode": "conflict",
            "observationHorizonTicks": 0,
            "stalemateWindowTicks": 2_000,
            "stalemateTerminates": False,
            "collapsePopulationThreshold": 250,
            "collapseStabilityThreshold": 250,
            "watchdogSeconds": (
                900 if variant in LONG_SCHEDULE_VARIANTS else 180
            ),
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
