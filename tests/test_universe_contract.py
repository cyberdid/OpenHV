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


def planet_physics(*, active: bool, mass: int, radius: int):
    return {
        "geologicalAgeYears": 2_000_000 if active else 0,
        "massEarthMillionths": mass,
        "radiusKilometers": radius,
        "surfaceGravityMilliMetersPerSecondSquared": 9810,
        "rotationPeriodMinutes": 1020,
        "orbitalDistanceMillionKilometers": 151_000,
        "orbitalPeriodDays": 380,
        "axialTiltMilliDegrees": 23_500,
        "orbitalEccentricityMillionths": 18_000,
        "stellarFluxWattsPerSquareMeter": 1340,
        "bondAlbedoPerMille": 380,
        "absorbedSolarWattsPerSquareMeter": 204,
        "radiativeEquilibriumMilliKelvin": 395_000,
        "meanSurfaceTemperatureMilliKelvin": 414_000,
        "energyImbalanceMilliWattsPerSquareMeter": -76_000,
        "atmospherePressurePascals": 420_018,
        "carbonDioxidePartsPerMillion": 120_000,
        "atmosphericWaterPartsPerMillion": 650_000,
        "surfaceWaterCubicKilometers": 0,
        "oceanCoveragePerMille": 0,
        "tectonicPlateCount": 12,
        "tectonicActivityPerMille": 920,
        "climatePulseSequence": 2 if active else 0,
    }


def planet_surface(
    planet_id: str, topology_hash: str, land_cells: int, basin_cells: int
):
    climate = {
        "planet-0001": (
            2, "E8F85B93", 361_500, 480_600, 422_493,
            249_490, 682_949, 472_214, 203,
        ),
        "planet-0002": (
            0, "1F2D5FAF", 269_300, 362_000, 319_357,
            53_460, 146_340, 104_525, 110,
        ),
        "planet-0003": (
            0, "57FCD0FB", 623_300, 788_700, 701_943,
            891_000, 2_440_500, 1_557_526, 350,
        ),
    }[planet_id]
    return {
        "latitudeCells": 180,
        "longitudeCells": 360,
        "cellCount": 64_800,
        "chunkLatitudeCells": 12,
        "chunkLongitudeCells": 12,
        "chunkRows": 15,
        "chunkColumns": 30,
        "chunkCount": 450,
        "generation": 1,
        "topologyHash": topology_hash,
        "hydrologyHash": "5EBF32C5",
        "climatePulseSequence": climate[0],
        "climateHash": climate[1],
        "minimumElevationMeters": -5700,
        "maximumElevationMeters": 3700,
        "landCellCount": land_cells,
        "basinCellCount": basin_cells,
        "minimumTemperatureMilliKelvin": climate[2],
        "maximumTemperatureMilliKelvin": climate[3],
        "meanTemperatureMilliKelvin": climate[4],
        "minimumPressurePascals": climate[5],
        "maximumPressurePascals": climate[6],
        "meanPressurePascals": climate[7],
        "meanAbsorbedSolarWattsPerSquareMeter": climate[8],
        "firstCellId": f"{planet_id}:cell:000:000",
        "lastCellId": f"{planet_id}:cell:179:359",
        "firstChunkId": f"{planet_id}:chunk:00:00",
        "lastChunkId": f"{planet_id}:chunk:14:29",
    }


def universe_snapshot():
    return {
        "universeId": "universe-0001",
        "starSystemId": "tyranthos-system",
        "macroDay": 2,
        "macroTickRemainder": 0,
        "macroEventSequence": 4,
        "ticksPerMacroDay": 250,
        "planets": [
            {
                "planetId": "planet-0001",
                "name": "Tyranthos",
                "index": 0,
                "active": True,
                "lifecycleStage": "lifeless",
                "nativeRaceId": None,
                "physics": planet_physics(active=True, mass=1_020_000, radius=6450),
                "surface": planet_surface("planet-0001", "4EFA647D", 30137, 34663),
            },
            {
                "planetId": "planet-0002",
                "name": "Planet II",
                "index": 1,
                "active": False,
                "lifecycleStage": "lifeless",
                "nativeRaceId": None,
                "physics": planet_physics(active=False, mass=800_000, radius=5800),
                "surface": planet_surface("planet-0002", "E3E824C2", 26424, 38376),
            },
            {
                "planetId": "planet-0003",
                "name": "Planet III",
                "index": 2,
                "active": False,
                "lifecycleStage": "lifeless",
                "nativeRaceId": None,
                "physics": planet_physics(active=False, mass=1_300_000, radius=7200),
                "surface": planet_surface("planet-0003", "18E26992", 39087, 25713),
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

    def test_active_planet_advances_physics_without_life(self) -> None:
        snapshot = universe_snapshot()
        active = snapshot["planets"][0]
        self.assertEqual(active["lifecycleStage"], "lifeless")
        self.assertEqual(active["physics"]["geologicalAgeYears"], 2_000_000)
        self.assertEqual(active["physics"]["climatePulseSequence"], 2)
        self.assertGreater(active["physics"]["atmospherePressurePascals"], 0)

    def test_planet_surface_is_large_chunked_and_has_stable_ids(self) -> None:
        surface = universe_snapshot()["planets"][0]["surface"]
        self.assertEqual(surface["longitudeCells"], 2 * surface["latitudeCells"])
        self.assertEqual(surface["cellCount"], 180 * 360)
        self.assertEqual(surface["chunkCount"], 15 * 30)
        self.assertEqual(surface["firstCellId"], "planet-0001:cell:000:000")
        self.assertEqual(surface["lastChunkId"], "planet-0001:chunk:14:29")
        self.assertEqual(
            surface["landCellCount"] + surface["basinCellCount"],
            surface["cellCount"],
        )

    def test_three_planet_seeds_generate_distinct_topologies(self) -> None:
        hashes = {
            planet["surface"]["topologyHash"]
            for planet in universe_snapshot()["planets"]
        }
        self.assertEqual(len(hashes), 3)

    def test_spatial_climate_advances_only_on_the_active_planet(self) -> None:
        planets = universe_snapshot()["planets"]
        self.assertEqual(planets[0]["surface"]["climatePulseSequence"], 2)
        self.assertEqual(
            [planet["surface"]["climatePulseSequence"] for planet in planets[1:]],
            [0, 0],
        )
        for planet in planets:
            surface = planet["surface"]
            self.assertLessEqual(
                surface["minimumTemperatureMilliKelvin"],
                surface["meanTemperatureMilliKelvin"],
            )
            self.assertLessEqual(
                surface["meanTemperatureMilliKelvin"],
                surface["maximumTemperatureMilliKelvin"],
            )

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
