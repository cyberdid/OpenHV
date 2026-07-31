#!/usr/bin/env python3

"""The campaign/tactical contract.

A campaign layer asks for a battle with a battle-request-v1 document and gets
a simulation-result-v1 document back. There is deliberately no second result
schema: the existing one already carries losses, kills, surviving army and the
synchronized hash, and a second format would only drift from it.

These tests keep the schema honest. A schema nobody validates against is
decoration, and this project has already lost 112 matches to an undeclared
property slipping into a document.
"""

from __future__ import annotations

import json
import unittest
from pathlib import Path

from jsonschema import Draft202012Validator, FormatChecker


PROJECT_DIR = Path(__file__).resolve().parents[1]
SCHEMA_PATH = PROJECT_DIR / "schemas" / "battle-request-v1.schema.json"
EXAMPLE_PATH = PROJECT_DIR / "schemas" / "examples" / "battle-request-v1-example.json"


def read_json(path: Path):
    with path.open(encoding="utf-8") as stream:
        return json.load(stream)


class BattleContractTests(unittest.TestCase):
    def setUp(self) -> None:
        self.schema = read_json(SCHEMA_PATH)
        Draft202012Validator.check_schema(self.schema)
        self.validator = Draft202012Validator(
            self.schema, format_checker=FormatChecker()
        )
        self.example = read_json(EXAMPLE_PATH)

    def test_example_satisfies_the_schema(self) -> None:
        errors = sorted(self.validator.iter_errors(self.example), key=str)
        self.assertEqual(errors, [], f"Example is invalid: {errors[:2]}")

    def test_undeclared_properties_are_rejected(self) -> None:
        # The config.factions incident: a field the simulation started emitting
        # but the schema never declared failed every match in a 112-run batch.
        # additionalProperties must stay closed so the failure is loud and early.
        document = dict(self.example)
        document["unexpectedField"] = 1
        self.assertFalse(self.validator.is_valid(document))

    def test_a_battle_needs_at_least_two_sides(self) -> None:
        document = json.loads(json.dumps(self.example))
        document["participants"] = document["participants"][:1]
        self.assertFalse(self.validator.is_valid(document))

    def test_map_is_named_or_generated_but_never_both(self) -> None:
        named = json.loads(json.dumps(self.example))
        named["map"] = {"name": "coldrage"}
        self.assertTrue(self.validator.is_valid(named))

        both = json.loads(json.dumps(self.example))
        both["map"] = {
            "name": "coldrage",
            "generator": {"tileset": "temperate", "sizeX": 98, "sizeY": 98},
        }
        self.assertFalse(
            self.validator.is_valid(both),
            "A request must not be ambiguous about what will be played",
        )

        neither = json.loads(json.dumps(self.example))
        neither["map"] = {}
        self.assertFalse(self.validator.is_valid(neither))

    def test_determinism_block_is_required(self) -> None:
        # Same request, same hash. Without a seed and a tick ceiling the reply
        # cannot be reproduced and the campaign cannot be replayed.
        document = json.loads(json.dumps(self.example))
        del document["determinism"]
        self.assertFalse(self.validator.is_valid(document))

        document = json.loads(json.dumps(self.example))
        del document["determinism"]["seed"]
        self.assertFalse(self.validator.is_valid(document))

    def test_the_reply_format_is_the_existing_result_schema(self) -> None:
        # Guards the decision itself: if someone adds battle-result-v1, this
        # fails and they have to argue for the second format on purpose.
        result_schema = PROJECT_DIR / "schemas" / "simulation-result-v1.schema.json"
        self.assertTrue(result_schema.is_file())
        self.assertFalse(
            (PROJECT_DIR / "schemas" / "battle-result-v1.schema.json").exists(),
            "The battle reply is a simulation-result-v1 document by design",
        )
        result = read_json(result_schema)
        for field in ("players", "naturalWinners", "endReason", "synchronizedStateHash"):
            self.assertIn(
                field,
                result["required"],
                f"A campaign cannot apply an outcome without {field}",
            )


if __name__ == "__main__":
    unittest.main(verbosity=2)
