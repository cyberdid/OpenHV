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


def planet_physics(
    *,
    planet_id: str,
    active: bool,
    mass: int,
    radius: int,
    atmospheric_water: int,
    surface_water: int,
):
    calibrated = {
        "planet-0001": (
            9762, 1020, 151_000, 380, 23_500, 18_000, 1340,
            380, 204, 390_547, 422_479, -127_728, 420_010,
            120_000, 0, 12, 920,
        ),
        "planet-0002": (
            9469, 1800, 210_000, 590, 12_000, 40_000, 750,
            380, 111, 258_440, 310_000, -206_240, 90_000,
            20_000, 0, 8, 500,
        ),
        "planet-0003": (
            9985, 780, 100_000, 210, 5000, 10_000, 2300,
            380, 352, 560_510, 700_000, -557_960, 1_500_000,
            450_000, 0, 15, 800,
        ),
    }[planet_id]
    return {
        "geologicalAgeYears": 2_000_000 if active else 0,
        "massEarthMillionths": mass,
        "radiusKilometers": radius,
        "surfaceGravityMilliMetersPerSecondSquared": calibrated[0],
        "rotationPeriodMinutes": calibrated[1],
        "orbitalDistanceMillionKilometers": calibrated[2],
        "orbitalPeriodDays": calibrated[3],
        "axialTiltMilliDegrees": calibrated[4],
        "orbitalEccentricityMillionths": calibrated[5],
        "stellarFluxWattsPerSquareMeter": calibrated[6],
        "bondAlbedoPerMille": calibrated[7],
        "absorbedSolarWattsPerSquareMeter": calibrated[8],
        "radiativeEquilibriumMilliKelvin": calibrated[9],
        "meanSurfaceTemperatureMilliKelvin": calibrated[10],
        "energyImbalanceMilliWattsPerSquareMeter": calibrated[11],
        "atmospherePressurePascals": calibrated[12],
        "carbonDioxidePartsPerMillion": calibrated[13],
        "atmosphericWaterPartsPerMillion": atmospheric_water,
        "surfaceWaterCubicKilometers": surface_water,
        "oceanCoveragePerMille": calibrated[14],
        "tectonicPlateCount": calibrated[15],
        "tectonicActivityPerMille": calibrated[16],
        "climatePulseSequence": 2 if active else 0,
    }


def planet_surface(
    planet_id: str, topology_hash: str, land_cells: int, basin_cells: int
):
    climate = {
        "planet-0001": (
            2, "31583E59", 361_000, 480_600, 422_479,
            249_485, 682_936, 472_205, 203,
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
    hydrology = {
        "planet-0001": (
            "A094A659", "1FEF2C7A", 96, 900, 5523, 14_175,
            649_972, 19, 762, 54_426, 650_000_000_000,
        ),
        "planet-0002": (
            "CD6E19D0", "B169A632", 0, 0, 3468, 8238,
            80_000, 0, 0, 0, 80_000_000_000,
        ),
        "planet-0003": (
            "5FF2509B", "3F8018AF", 0, 0, 6348, 8238,
            300_000, 0, 0, 0, 300_000_000_000,
        ),
    }[planet_id]
    column = {
        "planet-0001": (
            "DE588D81", 432_832, 391_607, 645, 58, 11_939,
            6521, 22_315, 246, 437_749, 23_000, 206_246, 58,
        ),
        "planet-0002": (
            "2F4B3EB3", 321_656, 283_856, 192, 16, 2480,
            4161, 9885, 161, 772_800, 20_300, 0, 0,
        ),
        "planet-0003": (
            "86A5D4D8", 714_470, 676_670, 331, 29, 4537,
            7617, 9885, 285, 322_897, 43_600, 0, 0,
        ),
    }[planet_id]
    geology = {
        "planet-0001": (
            2, "E04AD283", -1130, 25, 128, 691_731_050,
            12_060, 13_550, 800,
        ),
        "planet-0002": (
            0, "E1EA635A", -1468, 0, 0, 686_935_600,
            0, 0, 0,
        ),
        "planet-0003": (
            0, "ECAF3ECE", -347, 0, 0, 689_733_900,
            0, 0, 0,
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
        "geologyPulseSequence": geology[0],
        "geologyHash": geology[1],
        "meanElevationMeters": geology[2],
        "meanAbsoluteGeologyChangeMilliMeters": geology[3],
        "activeVolcanicCellCount": geology[4],
        "totalSurfaceMaterialUnits": geology[5],
        "cumulativeErodedMaterialUnits": geology[6],
        "cumulativeMantleMaterialInputUnits": geology[7],
        "cumulativeTectonicElevationChangeMeters": geology[8],
        "elevationBalanceErrorMeters": 0,
        "materialBalanceErrorUnits": 0,
        "hydrologyHash": hydrology[0],
        "climatePulseSequence": climate[0],
        "climateHash": climate[1],
        "atmosphereHash": hydrology[1],
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
        "atmosphereSubsteps": hydrology[2],
        "atmosphereStepSeconds": hydrology[3],
        "meanWindCentimetersPerSecond": hydrology[4],
        "maximumWindCentimetersPerSecond": hydrology[5],
        "meanAtmosphericWaterPartsPerMillion": hydrology[6],
        "meanCloudCoverPerMille": hydrology[7],
        "meanPrecipitationTenthsMillimetersPerDay": hydrology[8],
        "spatialSurfaceWaterCubicKilometers": hydrology[9],
        "totalWaterMassUnits": hydrology[10],
        "waterBalanceErrorUnits": 0,
        "verticalLevels": 8,
        "verticalAtmosphereHash": column[0],
        "meanLowerAtmosphereTemperatureMilliKelvin": column[1],
        "meanUpperAtmosphereTemperatureMilliKelvin": column[2],
        "meanLowerRelativeHumidityPerMille": column[3],
        "meanUpperRelativeHumidityPerMille": column[4],
        "meanVerticalShearMicrosPerSecond": column[5],
        "meanJetSpeedCentimetersPerSecond": column[6],
        "maximumJetSpeedCentimetersPerSecond": column[7],
        "meanVerticalVelocityMillimetersPerSecond": column[8],
        "meanBulkRichardsonMillionths": column[9],
        "hadleyTemperatureIndexMilliKelvin": column[10],
        "meanLatentFluxMilliWattsPerSquareMeter": column[11],
        "latentEnergyResidualMilliWattsPerSquareMeter": 0,
        "cumulativeLatentEnergyMegaJoulesPerSquareMeter": column[12],
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
                "physics": planet_physics(
                    planet_id="planet-0001",
                    active=True,
                    mass=1_020_000,
                    radius=6450,
                    atmospheric_water=649_972,
                    surface_water=54_426,
                ),
                "surface": planet_surface("planet-0001", "72DD4BA0", 30137, 34663),
            },
            {
                "planetId": "planet-0002",
                "name": "Planet II",
                "index": 1,
                "active": False,
                "lifecycleStage": "lifeless",
                "nativeRaceId": None,
                "physics": planet_physics(
                    planet_id="planet-0002",
                    active=False,
                    mass=800_000,
                    radius=5800,
                    atmospheric_water=80_000,
                    surface_water=0,
                ),
                "surface": planet_surface("planet-0002", "027DAEF1", 26424, 38376),
            },
            {
                "planetId": "planet-0003",
                "name": "Planet III",
                "index": 2,
                "active": False,
                "lifecycleStage": "lifeless",
                "nativeRaceId": None,
                "physics": planet_physics(
                    planet_id="planet-0003",
                    active=False,
                    mass=1_300_000,
                    radius=7200,
                    atmospheric_water=300_000,
                    surface_water=0,
                ),
                "surface": planet_surface("planet-0003", "BDEA3634", 39087, 25713),
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

    def test_geology_evolves_only_on_active_planet_and_closes_both_ledgers(self) -> None:
        planets = universe_snapshot()["planets"]
        active = planets[0]["surface"]
        self.assertEqual(active["geologyPulseSequence"], 2)
        self.assertGreater(active["meanAbsoluteGeologyChangeMilliMeters"], 0)
        self.assertGreater(active["activeVolcanicCellCount"], 0)
        self.assertGreater(active["cumulativeErodedMaterialUnits"], 0)
        self.assertGreater(active["cumulativeMantleMaterialInputUnits"], 0)
        self.assertEqual(active["elevationBalanceErrorMeters"], 0)
        self.assertEqual(active["materialBalanceErrorUnits"], 0)
        self.assertEqual(
            [planet["surface"]["geologyPulseSequence"] for planet in planets[1:]],
            [0, 0],
        )

    def test_geology_and_climate_share_the_same_surface_clock(self) -> None:
        surface = universe_snapshot()["planets"][0]["surface"]
        self.assertEqual(
            surface["geologyPulseSequence"],
            surface["climatePulseSequence"],
        )

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

    def test_active_atmosphere_advances_with_cfl_bounded_substeps(self) -> None:
        planets = universe_snapshot()["planets"]
        active_surface = planets[0]["surface"]
        self.assertGreater(active_surface["atmosphereSubsteps"], 0)
        self.assertGreaterEqual(active_surface["atmosphereStepSeconds"], 900)
        self.assertLessEqual(active_surface["atmosphereStepSeconds"], 21_600)
        self.assertLess(active_surface["meanWindCentimetersPerSecond"], 10_000)
        self.assertEqual(
            [planet["surface"]["atmosphereSubsteps"] for planet in planets[1:]],
            [0, 0],
        )

    def test_spatial_hydrology_conserves_and_reconciles_all_water(self) -> None:
        active = universe_snapshot()["planets"][0]
        surface = active["surface"]
        self.assertEqual(surface["waterBalanceErrorUnits"], 0)
        self.assertEqual(surface["totalWaterMassUnits"], 650_000_000_000)
        self.assertEqual(
            surface["meanAtmosphericWaterPartsPerMillion"],
            active["physics"]["atmosphericWaterPartsPerMillion"],
        )
        self.assertEqual(
            surface["spatialSurfaceWaterCubicKilometers"],
            active["physics"]["surfaceWaterCubicKilometers"],
        )

    def test_vertical_atmosphere_is_stratified_and_bounded(self) -> None:
        surface = universe_snapshot()["planets"][0]["surface"]
        self.assertEqual(surface["verticalLevels"], 8)
        self.assertLess(
            surface["meanUpperAtmosphereTemperatureMilliKelvin"],
            surface["meanLowerAtmosphereTemperatureMilliKelvin"],
        )
        self.assertLess(
            surface["meanUpperRelativeHumidityPerMille"],
            surface["meanLowerRelativeHumidityPerMille"],
        )
        self.assertGreater(surface["meanVerticalShearMicrosPerSecond"], 0)
        self.assertGreater(surface["meanJetSpeedCentimetersPerSecond"], 0)
        self.assertLessEqual(surface["maximumJetSpeedCentimetersPerSecond"], 27_000)
        self.assertLessEqual(surface["meanVerticalVelocityMillimetersPerSecond"], 900)
        self.assertGreaterEqual(surface["meanBulkRichardsonMillionths"], -2_000_000)
        self.assertLessEqual(surface["meanBulkRichardsonMillionths"], 12_000_000)

    def test_latent_heat_closes_and_spatial_temperature_drives_global_physics(self) -> None:
        active = universe_snapshot()["planets"][0]
        surface = active["surface"]
        self.assertEqual(surface["latentEnergyResidualMilliWattsPerSquareMeter"], 0)
        self.assertNotEqual(surface["meanLatentFluxMilliWattsPerSquareMeter"], 0)
        self.assertEqual(
            surface["meanTemperatureMilliKelvin"],
            active["physics"]["meanSurfaceTemperatureMilliKelvin"],
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
