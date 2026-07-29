#!/usr/bin/env python3

"""Analyze a completed Universe civil/military baseline batch."""

from __future__ import annotations

import argparse
import csv
import json
import math
import os
import random
import statistics
from collections import Counter, defaultdict
from itertools import combinations
from pathlib import Path
from typing import Any, Iterable


PROFILES = ("aggressor", "economist", "technologist", "fortress")
BOOTSTRAP_SAMPLES = 2_000
BOOTSTRAP_SEED = 20260729
PLAYER_FIELDS = (
    "score",
    "armyValue",
    "assetsValue",
    "killsValue",
    "deathsValue",
    "population",
    "prosperity",
    "stability",
    "foodSatisfaction",
    "energySatisfaction",
    "completedTechnologies",
    "researchInputSpent",
    "tradeDependency",
    "tradeImported",
    "tradeExported",
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
PAIRED_FIELDS = (
    "scoreLead",
    "score",
    "population",
    "prosperity",
    "stability",
    "armyValue",
    "killsValue",
    "deathsValue",
    "warCasualties",
    "activeWars",
    "targetSelections",
    "retreats",
)


def read_json(path: Path) -> Any:
    with path.open(encoding="utf-8") as stream:
        return json.load(stream)


def percentile(values: list[float], probability: float) -> float:
    ordered = sorted(values)
    if len(ordered) == 1:
        return ordered[0]
    position = probability * (len(ordered) - 1)
    lower = math.floor(position)
    upper = math.ceil(position)
    if lower == upper:
        return ordered[lower]
    fraction = position - lower
    return ordered[lower] * (1 - fraction) + ordered[upper] * fraction


def bootstrap_mean_interval(values: list[float], seed: int) -> list[float]:
    if not values:
        return [0.0, 0.0]
    if len(values) == 1:
        return [values[0], values[0]]
    rng = random.Random(seed)
    means = []
    for _ in range(BOOTSTRAP_SAMPLES):
        means.append(
            statistics.fmean(rng.choice(values) for _ in range(len(values)))
        )
    return [
        round(percentile(means, 0.025), 3),
        round(percentile(means, 0.975), 3),
    ]


def wilson_interval(successes: int, total: int) -> list[float]:
    if total == 0:
        return [0.0, 0.0]
    z = 1.959963984540054
    proportion = successes / total
    denominator = 1 + z * z / total
    center = (proportion + z * z / (2 * total)) / denominator
    margin = (
        z
        * math.sqrt(
            proportion * (1 - proportion) / total
            + z * z / (4 * total * total)
        )
        / denominator
    )
    return [round(center - margin, 4), round(center + margin, 4)]


def summarize_numeric(values: Iterable[int | float], seed: int) -> dict[str, Any]:
    numeric = [float(value) for value in values]
    if not numeric:
        return {
            "n": 0,
            "mean": 0,
            "median": 0,
            "p10": 0,
            "p90": 0,
            "meanBootstrap95": [0, 0],
        }
    return {
        "n": len(numeric),
        "mean": round(statistics.fmean(numeric), 3),
        "median": round(statistics.median(numeric), 3),
        "p10": round(percentile(numeric, 0.1), 3),
        "p90": round(percentile(numeric, 0.9), 3),
        "meanBootstrap95": bootstrap_mean_interval(numeric, seed),
    }


def trade_totals(result: dict[str, Any]) -> dict[str, dict[str, int]]:
    totals: dict[str, dict[str, int]] = defaultdict(
        lambda: {"tradeImported": 0, "tradeExported": 0}
    )
    for route in result.get("tradeRoutes", []):
        player_a = route["playerA"]
        player_b = route["playerB"]
        a_to_b = sum(
            route[field]
            for field in ("foodAToB", "materialsAToB", "energyAToB")
        )
        b_to_a = sum(
            route[field]
            for field in ("foodBToA", "materialsBToA", "energyBToA")
        )
        totals[player_a]["tradeExported"] += a_to_b
        totals[player_a]["tradeImported"] += b_to_a
        totals[player_b]["tradeExported"] += b_to_a
        totals[player_b]["tradeImported"] += a_to_b
    return totals


def relation_totals(result: dict[str, Any]) -> dict[str, Counter[str]]:
    totals: dict[str, Counter[str]] = defaultdict(Counter)
    for relation in result.get("diplomacy", []):
        totals[relation["playerA"]][relation["state"]] += 1
        totals[relation["playerB"]][relation["state"]] += 1
    return totals


def combat_label(civilization: dict[str, Any], field: str) -> str:
    if field not in civilization:
        return "unavailable"
    return civilization[field] or "none"


def player_rows(result: dict[str, Any], match_id: str) -> list[dict[str, Any]]:
    natural_winners = {
        (winner["playerName"], winner["botType"])
        for winner in result["naturalWinners"]
    }
    score_leader = result.get("scoreLeader")
    collapsed = {
        faction["playerName"]
        for faction in (result.get("lifecycle") or {}).get(
            "collapsedFactions", []
        )
    }
    trades = trade_totals(result)
    relations = relation_totals(result)
    rows = []
    for player in result["players"]:
        civilization = player["civilization"]
        settlements = civilization["settlements"]
        population = sum(settlement["population"] for settlement in settlements)
        weighted = max(population, 1)

        def weighted_mean(field: str) -> float:
            return sum(
                settlement[field] * settlement["population"]
                for settlement in settlements
            ) / weighted

        player_name = player["playerName"]
        row = {
            "matchId": match_id,
            "mapUid": result["config"]["mapUid"],
            "mapTitle": result["config"]["mapTitle"],
            "seed": result["config"]["effectiveRandomSeed"],
            "worldTick": result["worldTick"],
            "endReason": result["endReason"],
            "profile": player["botType"],
            "faction": player["faction"],
            "spawnPoint": player["spawnPoint"],
            "naturalWin": int(
                (player_name, player["botType"]) in natural_winners
            ),
            "scoreLead": int(
                score_leader is not None
                and score_leader["playerName"] == player_name
            ),
            "collapsed": int(player_name in collapsed),
            "strategy": civilization.get("strategy", "unavailable"),
            "plan": civilization.get("plan", "unavailable"),
            "planReason": civilization.get("planReason", "unavailable"),
            "score": player["score"],
            "armyValue": player["armyValue"],
            "assetsValue": player["assetsValue"],
            "killsValue": player["killsValue"],
            "deathsValue": player["deathsValue"],
            "population": population,
            "prosperity": round(weighted_mean("prosperity"), 3),
            "stability": round(weighted_mean("stability"), 3),
            "foodSatisfaction": round(
                weighted_mean("foodSatisfaction"), 3
            ),
            "energySatisfaction": round(
                weighted_mean("energySatisfaction"), 3
            ),
            "completedTechnologies": len(
                civilization.get("completedTechnologies", [])
            ),
            "researchInputSpent": (
                civilization.get("researchMaterialsSpent", 0)
                + civilization.get("researchEnergySpent", 0)
            ),
            "tradeDependency": civilization.get("tradeDependency", 0),
            "tradeImported": trades[player_name]["tradeImported"],
            "tradeExported": trades[player_name]["tradeExported"],
            "mobilized": civilization.get("mobilized", 0),
            "availableWorkforce": civilization.get(
                "availableWorkforce", 0
            ),
            "warCasualties": civilization.get("warCasualties", 0),
            "activeWars": relations[player_name]["war"],
            "plannerRequests": civilization.get("plannerRequestSequence", 0),
            # "unavailable" means a pre-AI-004 artifact without the field at
            # all; "none" means a current artifact whose faction never
            # commanded a squad. Both stay sortable strings so decision
            # distributions never mix null with real labels.
            "combatDecision": combat_label(
                civilization, "lastCombatDecision"
            ),
            "combatDecisionReason": combat_label(
                civilization, "lastCombatDecisionReason"
            ),
            "targetSelections": civilization.get("targetSelectionCount", 0),
            "retreats": civilization.get("retreatCount", 0),
            "regroups": civilization.get("regroupCount", 0),
            "reengagements": civilization.get("reengageCount", 0),
        }
        rows.append(row)
    return rows


def aggregate_group(rows: list[dict[str, Any]], seed: int) -> dict[str, Any]:
    natural_wins = sum(row["naturalWin"] for row in rows)
    score_leads = sum(row["scoreLead"] for row in rows)
    collapses = sum(row["collapsed"] for row in rows)
    return {
        "n": len(rows),
        "naturalWins": natural_wins,
        "naturalWinWilson95": wilson_interval(natural_wins, len(rows)),
        "scoreLeads": score_leads,
        "scoreLeadWilson95": wilson_interval(score_leads, len(rows)),
        "collapses": collapses,
        "strategies": dict(sorted(Counter(row["strategy"] for row in rows).items())),
        "plans": dict(sorted(Counter(row["plan"] for row in rows).items())),
        "planReasons": dict(
            sorted(Counter(row["planReason"] for row in rows).items())
        ),
        "combatDecisions": dict(
            sorted(Counter(row["combatDecision"] for row in rows).items())
        ),
        "combatDecisionReasons": dict(
            sorted(
                Counter(row["combatDecisionReason"] for row in rows).items()
            )
        ),
        "metrics": {
            field: summarize_numeric(
                (row[field] for row in rows),
                seed + field_index,
            )
            for field_index, field in enumerate(PLAYER_FIELDS)
        },
    }


def paired_profile_differences(
    rows: list[dict[str, Any]], seed: int
) -> dict[str, Any]:
    by_match: dict[str, dict[str, dict[str, Any]]] = defaultdict(dict)
    for row in rows:
        by_match[row["matchId"]][row["profile"]] = row

    report = {}
    for pair_index, (left, right) in enumerate(combinations(PROFILES, 2)):
        pairs = [
            (match[left], match[right])
            for match in by_match.values()
            if left in match and right in match
        ]
        report[f"{left}-minus-{right}"] = {
            "n": len(pairs),
            "metrics": {
                field: summarize_numeric(
                    (
                        left_row[field] - right_row[field]
                        for left_row, right_row in pairs
                    ),
                    seed + pair_index * 100 + field_index,
                )
                for field_index, field in enumerate(PAIRED_FIELDS)
            },
        }
    return report


def write_json_atomic(path: Path, document: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.{os.getpid()}.tmp")
    with temporary.open("w", encoding="utf-8") as stream:
        json.dump(document, stream, indent=2, ensure_ascii=False)
        stream.write("\n")
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary, path)


def write_csv_atomic(path: Path, rows: list[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.{os.getpid()}.tmp")
    with temporary.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary, path)


def analyze(run_dir: Path) -> tuple[dict[str, Any], list[dict[str, Any]], list[dict[str, Any]]]:
    summary = read_json(run_dir / "summary.json")
    if summary["counts"] != {"completed": summary["matchCount"]}:
        raise ValueError(f"Batch is incomplete: {summary['counts']}")
    schedule = read_json(run_dir / "resolved-manifest.json")
    expected = {match["id"]: match for match in schedule["matches"]}

    matches = []
    players = []
    commits = set()
    dirty_values = set()
    for match_id, requested in sorted(expected.items()):
        result_path = run_dir / "matches" / match_id / "result.json"
        if not result_path.is_file():
            raise ValueError(f"Missing result: {result_path}")
        result = read_json(result_path)
        commits.add(result["build"]["gitCommit"])
        dirty_values.add(result["build"]["gitDirty"])
        rows = player_rows(result, match_id)
        profiles = Counter(row["profile"] for row in rows)
        if profiles != Counter(PROFILES):
            raise ValueError(f"{match_id} has unexpected profiles: {profiles}")
        players.extend(rows)
        matches.append(
            {
                "matchId": match_id,
                "mapUid": result["config"]["mapUid"],
                "mapTitle": result["config"]["mapTitle"],
                "seed": result["config"]["effectiveRandomSeed"],
                "worldTick": result["worldTick"],
                "endReason": result["endReason"],
                "naturalWinnerCount": len(result["naturalWinners"]),
                "scoreLeaderProfile": (
                    result["scoreLeader"]["botType"]
                    if result.get("scoreLeader")
                    else ""
                ),
                "collapsedFactions": len(
                    (result.get("lifecycle") or {}).get(
                        "collapsedFactions", []
                    )
                ),
                "warRelations": sum(
                    relation["state"] == "war"
                    for relation in result.get("diplomacy", [])
                ),
                "tradeVolume": sum(
                    route[field]
                    for route in result.get("tradeRoutes", [])
                    for field in (
                        "foodAToB",
                        "foodBToA",
                        "materialsAToB",
                        "materialsBToA",
                        "energyAToB",
                        "energyBToA",
                    )
                ),
            }
        )

    if len(commits) != 1 or dirty_values != {False}:
        raise ValueError(
            f"Baseline must use one clean commit; commits={commits}, "
            f"dirty={dirty_values}"
        )

    by_profile = {}
    for index, profile in enumerate(PROFILES):
        group = [row for row in players if row["profile"] == profile]
        by_profile[profile] = aggregate_group(group, BOOTSTRAP_SEED + index * 100)

    by_profile_map = {}
    map_titles = sorted({row["mapTitle"] for row in players})
    for profile_index, profile in enumerate(PROFILES):
        by_profile_map[profile] = {}
        for map_index, map_title in enumerate(map_titles):
            group = [
                row
                for row in players
                if row["profile"] == profile and row["mapTitle"] == map_title
            ]
            by_profile_map[profile][map_title] = aggregate_group(
                group,
                BOOTSTRAP_SEED + profile_index * 100 + map_index * 10,
            )

    by_profile_faction = {}
    factions = sorted({row["faction"] for row in players})
    for profile_index, profile in enumerate(PROFILES):
        by_profile_faction[profile] = {}
        for faction_index, faction in enumerate(factions):
            group = [
                row
                for row in players
                if row["profile"] == profile and row["faction"] == faction
            ]
            by_profile_faction[profile][faction] = aggregate_group(
                group,
                BOOTSTRAP_SEED
                + 1_000
                + profile_index * 100
                + faction_index * 10,
            )

    by_profile_spawn = {}
    spawn_points = sorted({row["spawnPoint"] for row in players})
    for profile_index, profile in enumerate(PROFILES):
        by_profile_spawn[profile] = {}
        for spawn_index, spawn_point in enumerate(spawn_points):
            group = [
                row
                for row in players
                if row["profile"] == profile
                and row["spawnPoint"] == spawn_point
            ]
            by_profile_spawn[profile][str(spawn_point)] = aggregate_group(
                group,
                BOOTSTRAP_SEED
                + 2_000
                + profile_index * 100
                + spawn_index * 10,
            )

    report = {
        "schemaVersion": 1,
        "runId": schedule["runId"],
        "scheduleHash": schedule["scheduleHash"],
        "gitCommit": next(iter(commits)),
        "gitDirty": False,
        "matchCount": len(matches),
        "playerObservations": len(players),
        "maps": dict(sorted(Counter(row["mapTitle"] for row in matches).items())),
        "endReasons": dict(sorted(Counter(row["endReason"] for row in matches).items())),
        "worldTicks": summarize_numeric(
            (row["worldTick"] for row in matches), BOOTSTRAP_SEED
        ),
        "warRelations": summarize_numeric(
            (row["warRelations"] for row in matches), BOOTSTRAP_SEED + 1
        ),
        "tradeVolume": summarize_numeric(
            (row["tradeVolume"] for row in matches), BOOTSTRAP_SEED + 2
        ),
        "collapsedFactions": sum(row["collapsedFactions"] for row in matches),
        "byProfile": by_profile,
        "byProfileMap": by_profile_map,
        "byProfileFaction": by_profile_faction,
        "byProfileSpawn": by_profile_spawn,
        "pairedProfileDifferences": paired_profile_differences(
            players, BOOTSTRAP_SEED + 3_000
        ),
        "profileFactionCounts": {
            profile: dict(
                sorted(
                    Counter(
                        row["faction"]
                        for row in players
                        if row["profile"] == profile
                    ).items()
                )
            )
            for profile in PROFILES
        },
        "profileSpawnCounts": {
            profile: dict(
                sorted(
                    Counter(
                        str(row["spawnPoint"])
                        for row in players
                        if row["profile"] == profile
                    ).items()
                )
            )
            for profile in PROFILES
        },
        "method": {
            "bootstrapSamples": BOOTSTRAP_SAMPLES,
            "bootstrapSeed": BOOTSTRAP_SEED,
            "binaryInterval": "Wilson score 95%",
            "continuousInterval": "deterministic nonparametric bootstrap 95% for mean",
        },
    }
    return report, matches, players


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run_dir", type=Path)
    parser.add_argument(
        "--output-dir",
        type=Path,
        help="Default: <run-dir>/analysis",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    run_dir = args.run_dir.expanduser().resolve()
    output_dir = (
        args.output_dir.expanduser().resolve()
        if args.output_dir
        else run_dir / "analysis"
    )
    report, matches, players = analyze(run_dir)
    write_json_atomic(output_dir / "baseline-report.json", report)
    write_csv_atomic(output_dir / "baseline-match-observations.csv", matches)
    write_csv_atomic(output_dir / "baseline-player-observations.csv", players)
    print(
        f"Analyzed {report['matchCount']} matches and "
        f"{report['playerObservations']} player observations into {output_dir}."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
