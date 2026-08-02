#region Copyright & License Information
/*
 * Copyright 2026 The Universe Simulation Developers
 * This file is part of OpenHV, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Globalization;
using System.Linq;
using OpenRA.Mods.HV.Traits;

namespace OpenRA.Mods.HV
{
	public static class SimulationUniverseSnapshotBuilder
	{
		public static SimulationUniverseResult Build(World world)
		{
			var universe = world.WorldActor.TraitOrDefault<UniverseState>();
			if (universe == null)
				return null;

			return new SimulationUniverseResult
			{
				UniverseId = UniverseState.UniverseId,
				StarSystemId = UniverseState.StarSystemId,
				MacroDay = universe.MacroDay,
				MacroTickRemainder = universe.MacroTickRemainder,
				MacroEventSequence = universe.MacroEventSequence,
				TicksPerMacroDay = universe.TicksPerMacroDay,
				Planets = universe.StarSystem.Planets
					.OrderBy(planet => planet.Definition.Index)
					.Select(planet => new SimulationPlanetResult
					{
						PlanetId = planet.Definition.PlanetId,
						Name = planet.Definition.Name,
						Index = planet.Definition.Index,
						Active = planet.Active,
						LifecycleStage = LifecycleIdentifier(planet.LifecycleStage),
						NativeRaceId = null,
						Physics = PhysicsSnapshot(planet.Physics),
						Surface = SurfaceSnapshot(planet.Surface),
						Biosphere = BiosphereSnapshot(planet.Surface)
					})
					.ToArray()
			};
		}

		static SimulationPlanetBiosphereResult BiosphereSnapshot(PlanetSurfaceState surface)
		{
			return new SimulationPlanetBiosphereResult
			{
				PulseSequence = surface.BiospherePulseSequence,
				StateHash = unchecked((uint)surface.BiosphereHash).ToString("X8", CultureInfo.InvariantCulture),
				LifeOriginated = surface.LifeOriginated,
				OriginLatitudeIndex = surface.OriginLatitudeIndex,
				OriginLongitudeIndex = surface.OriginLongitudeIndex,
				MeanHabitabilityPerMille = surface.MeanHabitabilityPerMille,
				HabitableCellCount = surface.HabitableCellCount,
				LivingCellCount = surface.LivingCellCount,
				MeanBiomassPerMille = surface.MeanBiomassPerMille,
				MeanComplexityMillionths = surface.MeanComplexityMillionths,
				MaximumComplexityMillionths = surface.MaximumComplexityMillionths,
				MaximumAbiogenesisProgressUnits = surface.MaximumAbiogenesisProgressUnits
			};
		}

		static SimulationPlanetSurfaceResult SurfaceSnapshot(PlanetSurfaceState surface)
		{
			return new SimulationPlanetSurfaceResult
			{
				LatitudeCells = surface.LatitudeCells,
				LongitudeCells = surface.LongitudeCells,
				CellCount = surface.CellCount,
				ChunkLatitudeCells = surface.ChunkLatitudeCells,
				ChunkLongitudeCells = surface.ChunkLongitudeCells,
				ChunkRows = surface.ChunkRows,
				ChunkColumns = surface.ChunkColumns,
				ChunkCount = surface.ChunkCount,
				Generation = surface.Generation,
				TopologyHash = unchecked((uint)surface.TopologyHash).ToString("X8", CultureInfo.InvariantCulture),
				GeologyPulseSequence = surface.GeologyPulseSequence,
				GeologyHash = unchecked((uint)surface.GeologyHash).ToString("X8", CultureInfo.InvariantCulture),
				MeanElevationMeters = surface.MeanElevationMeters,
				MeanAbsoluteGeologyChangeMilliMeters = surface.MeanAbsoluteGeologyChangeMilliMeters,
				ActiveVolcanicCellCount = surface.ActiveVolcanicCellCount,
				TotalSurfaceMaterialUnits = surface.TotalSurfaceMaterialUnits,
				CumulativeErodedMaterialUnits = surface.CumulativeErodedMaterialUnits,
				CumulativeMantleMaterialInputUnits = surface.CumulativeMantleMaterialInputUnits,
				CumulativeTectonicElevationChangeMeters = surface.CumulativeTectonicElevationChangeMeters,
				ElevationBalanceErrorMeters = surface.ElevationBalanceErrorMeters,
				MaterialBalanceErrorUnits = surface.MaterialBalanceErrorUnits,
				HydrologyHash = unchecked((uint)surface.HydrologyHash).ToString("X8", CultureInfo.InvariantCulture),
				ClimatePulseSequence = surface.ClimatePulseSequence,
				ClimateHash = unchecked((uint)surface.ClimateHash).ToString("X8", CultureInfo.InvariantCulture),
				AtmosphereHash = unchecked((uint)surface.AtmosphereHash).ToString("X8", CultureInfo.InvariantCulture),
				MinimumElevationMeters = surface.MinimumElevationMeters,
				MaximumElevationMeters = surface.MaximumElevationMeters,
				LandCellCount = surface.LandCellCount,
				BasinCellCount = surface.BasinCellCount,
				MinimumTemperatureMilliKelvin = surface.MinimumTemperatureMilliKelvin,
				MaximumTemperatureMilliKelvin = surface.MaximumTemperatureMilliKelvin,
				MeanTemperatureMilliKelvin = surface.MeanTemperatureMilliKelvin,
				MinimumPressurePascals = surface.MinimumPressurePascals,
				MaximumPressurePascals = surface.MaximumPressurePascals,
				MeanPressurePascals = surface.MeanPressurePascals,
				MeanAbsorbedSolarWattsPerSquareMeter = surface.MeanAbsorbedSolarWattsPerSquareMeter,
				AtmosphereSubsteps = surface.AtmosphereSubsteps,
				AtmosphereStepSeconds = surface.AtmosphereStepSeconds,
				MeanWindCentimetersPerSecond = surface.MeanWindCentimetersPerSecond,
				MaximumWindCentimetersPerSecond = surface.MaximumWindCentimetersPerSecond,
				MeanAtmosphericWaterPartsPerMillion = surface.MeanAtmosphericWaterPartsPerMillion,
				MeanCloudCoverPerMille = surface.MeanCloudCoverPerMille,
				MeanPrecipitationTenthsMillimetersPerDay = surface.MeanPrecipitationTenthsMillimetersPerDay,
				SpatialSurfaceWaterCubicKilometers = surface.SpatialSurfaceWaterCubicKilometers,
				TotalWaterMassUnits = surface.TotalWaterMassUnits,
				WaterBalanceErrorUnits = surface.WaterBalanceErrorUnits,
				VerticalLevels = surface.VerticalLevels,
				VerticalAtmosphereHash = unchecked((uint)surface.VerticalAtmosphereHash)
					.ToString("X8", CultureInfo.InvariantCulture),
				MeanLowerAtmosphereTemperatureMilliKelvin = surface.MeanLowerAtmosphereTemperatureMilliKelvin,
				MeanUpperAtmosphereTemperatureMilliKelvin = surface.MeanUpperAtmosphereTemperatureMilliKelvin,
				MeanLowerRelativeHumidityPerMille = surface.MeanLowerRelativeHumidityPerMille,
				MeanUpperRelativeHumidityPerMille = surface.MeanUpperRelativeHumidityPerMille,
				MeanVerticalShearMicrosPerSecond = surface.MeanVerticalShearMicrosPerSecond,
				MeanJetSpeedCentimetersPerSecond = surface.MeanJetSpeedCentimetersPerSecond,
				MaximumJetSpeedCentimetersPerSecond = surface.MaximumJetSpeedCentimetersPerSecond,
				MeanVerticalVelocityMillimetersPerSecond = surface.MeanVerticalVelocityMillimetersPerSecond,
				MeanBulkRichardsonMillionths = surface.MeanBulkRichardsonMillionths,
				HadleyTemperatureIndexMilliKelvin = surface.HadleyTemperatureIndexMilliKelvin,
				MeanLatentFluxMilliWattsPerSquareMeter = surface.MeanLatentFluxMilliWattsPerSquareMeter,
				LatentEnergyResidualMilliWattsPerSquareMeter = surface.LatentEnergyResidualMilliWattsPerSquareMeter,
				CumulativeLatentEnergyMegaJoulesPerSquareMeter =
					surface.CumulativeLatentEnergyMegaJoulesPerSquareMeter,
				FirstCellId = surface.CellId(0, 0),
				LastCellId = surface.CellId(surface.LatitudeCells - 1, surface.LongitudeCells - 1),
				FirstChunkId = surface.ChunkId(0, 0),
				LastChunkId = surface.ChunkId(surface.ChunkRows - 1, surface.ChunkColumns - 1)
			};
		}

		static SimulationPlanetPhysicsResult PhysicsSnapshot(PlanetPhysicalState physics)
		{
			return new SimulationPlanetPhysicsResult
			{
				GeologicalAgeYears = physics.GeologicalAgeYears,
				MassEarthMillionths = physics.MassEarthMillionths,
				RadiusKilometers = physics.RadiusKilometers,
				SurfaceGravityMilliMetersPerSecondSquared = physics.SurfaceGravityMilliMetersPerSecondSquared,
				RotationPeriodMinutes = physics.RotationPeriodMinutes,
				OrbitalDistanceMillionKilometers = physics.OrbitalDistanceMillionKilometers,
				OrbitalPeriodDays = physics.OrbitalPeriodDays,
				AxialTiltMilliDegrees = physics.AxialTiltMilliDegrees,
				OrbitalEccentricityMillionths = physics.OrbitalEccentricityMillionths,
				StellarFluxWattsPerSquareMeter = physics.StellarFluxWattsPerSquareMeter,
				BondAlbedoPerMille = physics.BondAlbedoPerMille,
				AbsorbedSolarWattsPerSquareMeter = physics.AbsorbedSolarWattsPerSquareMeter,
				RadiativeEquilibriumMilliKelvin = physics.RadiativeEquilibriumMilliKelvin,
				MeanSurfaceTemperatureMilliKelvin = physics.MeanSurfaceTemperatureMilliKelvin,
				EnergyImbalanceMilliWattsPerSquareMeter = physics.EnergyImbalanceMilliWattsPerSquareMeter,
				AtmospherePressurePascals = physics.AtmospherePressurePascals,
				CarbonDioxidePartsPerMillion = physics.CarbonDioxidePartsPerMillion,
				AtmosphericWaterPartsPerMillion = physics.AtmosphericWaterPartsPerMillion,
				SurfaceWaterCubicKilometers = physics.SurfaceWaterCubicKilometers,
				OceanCoveragePerMille = physics.OceanCoveragePerMille,
				TectonicPlateCount = physics.TectonicPlateCount,
				TectonicActivityPerMille = physics.TectonicActivityPerMille,
				ClimatePulseSequence = physics.ClimatePulseSequence
			};
		}

		static string LifecycleIdentifier(PlanetLifecycleStage stage)
		{
			return stage switch
			{
				PlanetLifecycleStage.Lifeless => "lifeless",
				PlanetLifecycleStage.Biosphere => "biosphere",
				PlanetLifecycleStage.SapientLife => "sapient-life",
				PlanetLifecycleStage.Civilization => "civilization",
				PlanetLifecycleStage.Spacefaring => "spacefaring",
				_ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
			};
		}
	}
}
