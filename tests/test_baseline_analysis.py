#!/usr/bin/env python3

from __future__ import annotations

import importlib.util
import unittest
from collections import Counter
from pathlib import Path


PROJECT_DIR = Path(__file__).resolve().parents[1]


def load_script(name: str, filename: str):
    spec = importlib.util.spec_from_file_location(name, PROJECT_DIR / filename)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


generator = load_script(
    "generate_baseline_manifest", "generate-baseline-manifest.py"
)
analysis = load_script("analyze_baseline", "analyze-baseline.py")
comparison = load_script("compare_candidate", "compare-candidate.py")


class BaselineSuiteTests(unittest.TestCase):
    def test_manifest_balances_profiles_across_map_slots(self) -> None:
        manifest = generator.build_manifest()
        matches = manifest["matches"]
        self.assertEqual(len(matches), 112)
        self.assertEqual(len({match["id"] for match in matches}), 112)
        self.assertEqual(len({match["seed"] for match in matches}), 112)
        candidate = generator.build_manifest("ai003-candidate")
        self.assertEqual(candidate["matches"], matches)
        self.assertEqual(candidate["defaults"], manifest["defaults"])
        candidate_v2 = generator.build_manifest("ai003-candidate-v2")
        self.assertEqual(candidate_v2["matches"], matches)
        self.assertEqual(candidate_v2["defaults"], manifest["defaults"])
        candidate_v3 = generator.build_manifest("ai003-candidate-v3")
        self.assertEqual(candidate_v3["matches"], matches)
        self.assertEqual(candidate_v3["defaults"], manifest["defaults"])

        map_counts = Counter(match["map"] for match in matches)
        self.assertEqual(set(map_counts.values()), {28})
        for map_name in map_counts:
            map_matches = [match for match in matches if match["map"] == map_name]
            for slot in range(4):
                slot_counts = Counter(match["bots"][slot] for match in map_matches)
                self.assertEqual(
                    slot_counts,
                    Counter({profile: 7 for profile in generator.PROFILES}),
                )

    def test_trade_allocation_reconciles_both_directions(self) -> None:
        totals = analysis.trade_totals(
            {
                "tradeRoutes": [
                    {
                        "playerA": "A",
                        "playerB": "B",
                        "foodAToB": 10,
                        "materialsAToB": 20,
                        "energyAToB": 30,
                        "foodBToA": 4,
                        "materialsBToA": 5,
                        "energyBToA": 6,
                    }
                ]
            }
        )
        self.assertEqual(
            totals["A"], {"tradeImported": 15, "tradeExported": 60}
        )
        self.assertEqual(
            totals["B"], {"tradeImported": 60, "tradeExported": 15}
        )

    def test_profile_aggregate_is_deterministic(self) -> None:
        rows = []
        for index in range(8):
            row = {
                field: index * 10 + field_index
                for field_index, field in enumerate(analysis.PLAYER_FIELDS)
            }
            row.update(
                {
                    "naturalWin": int(index == 0),
                    "scoreLead": int(index < 2),
                    "collapsed": int(index == 7),
                    "strategy": "research" if index % 2 == 0 else "mobilization",
                    "plan": "technology" if index % 2 == 0 else "economy",
                    "planReason": (
                        "technologist-doctrine"
                        if index % 2 == 0
                        else "economist-doctrine"
                    ),
                }
            )
            rows.append(row)

        first = analysis.aggregate_group(rows, 123)
        second = analysis.aggregate_group(rows, 123)
        self.assertEqual(first, second)
        self.assertEqual(first["n"], 8)
        self.assertEqual(first["naturalWins"], 1)
        self.assertEqual(first["scoreLeads"], 2)
        self.assertEqual(first["collapses"], 1)
        self.assertEqual(
            first["strategies"], {"mobilization": 4, "research": 4}
        )
        self.assertEqual(first["plans"], {"economy": 4, "technology": 4})
        self.assertLess(
            first["metrics"]["population"]["meanBootstrap95"][0],
            first["metrics"]["population"]["meanBootstrap95"][1],
        )

    def test_paired_differences_preserve_direction(self) -> None:
        rows = []
        for match_index in range(4):
            for profile_index, profile in enumerate(analysis.PROFILES):
                row = {
                    "matchId": f"match-{match_index}",
                    "profile": profile,
                }
                for field in analysis.PAIRED_FIELDS:
                    row[field] = match_index * 10 + profile_index
                rows.append(row)

        report = analysis.paired_profile_differences(rows, 321)
        aggressor_minus_fortress = report["aggressor-minus-fortress"]
        self.assertEqual(aggressor_minus_fortress["n"], 4)
        for metric in aggressor_minus_fortress["metrics"].values():
            self.assertEqual(metric["mean"], -3)
            self.assertEqual(metric["meanBootstrap95"], [-3.0, -3.0])

    def test_candidate_comparison_is_paired_by_match_and_profile(self) -> None:
        baseline = []
        candidate = []
        for profile_index, profile in enumerate(analysis.PROFILES):
            before = {
                "matchId": "match-1",
                "profile": profile,
                "mapUid": "map",
                "seed": 42,
                "faction": "sc",
                "spawnPoint": profile_index + 1,
                "plan": "unavailable",
                "planReason": "unavailable",
            }
            for field in comparison.COMPARISON_FIELDS:
                before[field] = profile_index
            after = dict(before)
            after["plan"] = "technology"
            after["planReason"] = "technologist-doctrine"
            for field in comparison.COMPARISON_FIELDS:
                after[field] = before[field] + 2
            baseline.append(before)
            candidate.append(after)

        report = comparison.compare_rows(baseline, candidate)
        self.assertEqual(report["pairedObservations"], 4)
        for profile in analysis.PROFILES:
            metrics = report["byProfile"][profile]["candidateMinusBaseline"]
            for metric in metrics.values():
                self.assertEqual(metric["mean"], 2)


if __name__ == "__main__":
    unittest.main()
