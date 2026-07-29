#!/usr/bin/env python3

"""Compare a clean candidate batch with the exact Universe baseline schedule."""

from __future__ import annotations

import argparse
import importlib.util
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any


PROJECT_DIR = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location(
    "analyze_baseline", PROJECT_DIR / "analyze-baseline.py"
)
assert spec is not None and spec.loader is not None
analysis = importlib.util.module_from_spec(spec)
spec.loader.exec_module(analysis)

COMPARISON_FIELDS = (
    "scoreLead",
    "collapsed",
    "score",
    "population",
    "prosperity",
    "stability",
    "armyValue",
    "killsValue",
    "deathsValue",
    "completedTechnologies",
    "researchInputSpent",
    "mobilized",
    "availableWorkforce",
    "warCasualties",
    "activeWars",
    "plannerRequests",
    "targetSelections",
    "retreats",
    "regroups",
    "reengagements",
)


def index_rows(rows: list[dict[str, Any]]) -> dict[tuple[str, str], dict[str, Any]]:
    indexed = {(row["matchId"], row["profile"]): row for row in rows}
    if len(indexed) != len(rows):
        raise ValueError("Duplicate match/profile observations")
    return indexed


def compare_rows(
    baseline_rows: list[dict[str, Any]],
    candidate_rows: list[dict[str, Any]],
) -> dict[str, Any]:
    baseline = index_rows(baseline_rows)
    candidate = index_rows(candidate_rows)
    if baseline.keys() != candidate.keys():
        raise ValueError("Baseline and candidate observations do not align")

    differences: dict[str, dict[str, list[float]]] = defaultdict(
        lambda: defaultdict(list)
    )
    identity_mismatches = []
    for key in sorted(baseline):
        before = baseline[key]
        after = candidate[key]
        for field in ("mapUid", "seed", "faction", "spawnPoint"):
            if before[field] != after[field]:
                identity_mismatches.append(
                    {
                        "matchId": key[0],
                        "profile": key[1],
                        "field": field,
                        "baseline": before[field],
                        "candidate": after[field],
                    }
                )
        for field in COMPARISON_FIELDS:
            differences[key[1]][field].append(after[field] - before[field])

    if identity_mismatches:
        raise ValueError(
            "Candidate changed paired map/seed/faction/spawn identity: "
            f"{identity_mismatches[:3]}"
        )

    by_profile = {}
    for profile_index, profile in enumerate(analysis.PROFILES):
        before = [row for row in baseline_rows if row["profile"] == profile]
        after = [row for row in candidate_rows if row["profile"] == profile]
        by_profile[profile] = {
            "n": len(after),
            "baselineFinalPlans": dict(
                sorted(Counter(row["plan"] for row in before).items())
            ),
            "candidateFinalPlans": dict(
                sorted(Counter(row["plan"] for row in after).items())
            ),
            "candidatePlanReasons": dict(
                sorted(Counter(row["planReason"] for row in after).items())
            ),
            "baselineFinalCombatDecisions": dict(
                sorted(Counter(row["combatDecision"] for row in before).items())
            ),
            "candidateFinalCombatDecisions": dict(
                sorted(Counter(row["combatDecision"] for row in after).items())
            ),
            "candidateCombatDecisionReasons": dict(
                sorted(
                    Counter(
                        row["combatDecisionReason"] for row in after
                    ).items()
                )
            ),
            "candidateMinusBaseline": {
                field: analysis.summarize_numeric(
                    differences[profile][field],
                    analysis.BOOTSTRAP_SEED
                    + 10_000
                    + profile_index * 100
                    + field_index,
                )
                for field_index, field in enumerate(COMPARISON_FIELDS)
            },
        }

    return {
        "schemaVersion": 1,
        "pairedObservations": len(candidate_rows),
        "fields": list(COMPARISON_FIELDS),
        "byProfile": by_profile,
    }


def compare_runs(baseline_dir: Path, candidate_dir: Path) -> dict[str, Any]:
    baseline_report, baseline_matches, baseline_rows = analysis.analyze(
        baseline_dir
    )
    candidate_report, candidate_matches, candidate_rows = analysis.analyze(
        candidate_dir
    )
    if [row["matchId"] for row in baseline_matches] != [
        row["matchId"] for row in candidate_matches
    ]:
        raise ValueError("Resolved match IDs differ")

    comparison = compare_rows(baseline_rows, candidate_rows)
    comparison.update(
        {
            "baselineRunId": baseline_report["runId"],
            "baselineCommit": baseline_report["gitCommit"],
            "candidateRunId": candidate_report["runId"],
            "candidateCommit": candidate_report["gitCommit"],
            "matchCount": candidate_report["matchCount"],
            "baselineEndReasons": baseline_report["endReasons"],
            "candidateEndReasons": candidate_report["endReasons"],
            "baselineCollapsedFactions": baseline_report[
                "collapsedFactions"
            ],
            "candidateCollapsedFactions": candidate_report[
                "collapsedFactions"
            ],
        }
    )
    return comparison


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("baseline_dir", type=Path)
    parser.add_argument("candidate_dir", type=Path)
    parser.add_argument(
        "--output",
        type=Path,
        help="Default: <candidate-dir>/analysis/candidate-comparison.json",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    baseline_dir = args.baseline_dir.expanduser().resolve()
    candidate_dir = args.candidate_dir.expanduser().resolve()
    output = (
        args.output.expanduser().resolve()
        if args.output
        else candidate_dir / "analysis" / "candidate-comparison.json"
    )
    comparison = compare_runs(baseline_dir, candidate_dir)
    analysis.write_json_atomic(output, comparison)
    print(
        f"Compared {comparison['matchCount']} paired matches into {output}."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
