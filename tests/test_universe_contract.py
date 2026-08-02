#!/usr/bin/env python3

"""Contract tests for the synchronized Universe root added by UNI-001."""

from __future__ import annotations

import json
import unittest
from pathlib import Path

from jsonschema import Draft202012Validator
from referencing import Registry, Resource


PROJECT_DIR = Path(__file__).resolve().parents[1]
RESULT_SCHEMA_PATH = PROJECT_DIR / "schemas" / "simulation-result-v1.schema.json"
TELEMETRY_SCHEMA_PATH = (
    PROJECT_DIR / "schemas" / "simulation-telemetry-v1.schema.json"
)
CHECKPOINT_SCHEMA_PATH = (
    PROJECT_DIR / "schemas" / "universe-checkpoint-v1.schema.json"
)


def read_json(path: Path):
    with path.open(encoding="utf-8") as stream:
        return json.load(stream)


def universe_snapshot():
    return {
        "universeId": "universe-0001",
        "starSystemId": "tyranthos-system",
        "macroDay": 2,
        "macroTickRemainder": 0,
        "macroEventSequence": 2,
        "ticksPerMacroDay": 250,
        "planets": [
            {
                "planetId": "planet-0001",
                "name": "Tyranthos",
                "index": 0,
                "active": True,
                "lifecycleStage": "lifeless",
                "nativeRaceId": None,
            },
            {
                "planetId": "planet-0002",
                "name": "Planet II",
                "index": 1,
                "active": False,
                "lifecycleStage": "lifeless",
                "nativeRaceId": None,
            },
            {
                "planetId": "planet-0003",
                "name": "Planet III",
                "index": 2,
                "active": False,
                "lifecycleStage": "lifeless",
                "nativeRaceId": None,
            },
        ],
    }


class UniverseContractTests(unittest.TestCase):
    def setUp(self) -> None:
        self.result_schema = read_json(RESULT_SCHEMA_PATH)
        Draft202012Validator.check_schema(self.result_schema)
        universe_schema = dict(self.result_schema["$defs"]["universe"])
        universe_schema["$schema"] = self.result_schema["$schema"]
        universe_schema["$defs"] = self.result_schema["$defs"]
        self.universe_validator = Draft202012Validator(universe_schema)

    def test_initial_universe_has_one_active_lifeless_planet(self) -> None:
        snapshot = universe_snapshot()
        self.assertTrue(self.universe_validator.is_valid(snapshot))
        self.assertEqual(sum(planet["active"] for planet in snapshot["planets"]), 1)
        self.assertTrue(
            all(planet["lifecycleStage"] == "lifeless" for planet in snapshot["planets"])
        )

    def test_exactly_three_planet_slots_are_required(self) -> None:
        snapshot = universe_snapshot()
        snapshot["planets"] = snapshot["planets"][:2]
        self.assertFalse(self.universe_validator.is_valid(snapshot))

    def test_only_one_planet_is_active_in_the_initial_contract(self) -> None:
        snapshot = universe_snapshot()
        snapshot["planets"][1]["active"] = True
        self.assertFalse(self.universe_validator.is_valid(snapshot))

    def test_unassigned_native_race_may_be_omitted_from_jsonl(self) -> None:
        snapshot = universe_snapshot()
        for planet in snapshot["planets"]:
            del planet["nativeRaceId"]
        self.assertTrue(self.universe_validator.is_valid(snapshot))

    def test_result_and_telemetry_schemas_publish_the_same_universe_contract(self) -> None:
        telemetry_schema = read_json(TELEMETRY_SCHEMA_PATH)
        Draft202012Validator.check_schema(telemetry_schema)
        self.assertEqual(
            self.result_schema["properties"]["universe"],
            {"$ref": "#/$defs/universe"},
        )
        self.assertEqual(
            telemetry_schema["properties"]["universe"],
            {"$ref": "simulation-result-v1.schema.json#/$defs/universe"},
        )

    def test_checkpoint_manifest_uses_the_result_universe_contract(self) -> None:
        checkpoint_schema = read_json(CHECKPOINT_SCHEMA_PATH)
        Draft202012Validator.check_schema(checkpoint_schema)
        self.assertEqual(checkpoint_schema["properties"]["schemaVersion"], {"const": 1})
        self.assertEqual(
            checkpoint_schema["properties"]["universe"],
            {"$ref": "simulation-result-v1.schema.json#/$defs/universe"},
        )

        registry = Registry().with_resource(
            self.result_schema["$id"], Resource.from_contents(self.result_schema)
        )
        checkpoint_validator = Draft202012Validator(
            checkpoint_schema, registry=registry
        )
        manifest = {
            "schemaVersion": 1,
            "checkpointName": "universe-split.orasav",
            "worldTick": 250,
            "synchronizedStateHash": "3A94CA09",
            "universe": universe_snapshot(),
        }
        checkpoint_validator.validate(manifest)


if __name__ == "__main__":
    unittest.main(verbosity=2)
