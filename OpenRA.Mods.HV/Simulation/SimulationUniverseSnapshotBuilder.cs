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
						Physics = PhysicsSnapshot(planet.Physics)
					})
					.ToArray()
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
