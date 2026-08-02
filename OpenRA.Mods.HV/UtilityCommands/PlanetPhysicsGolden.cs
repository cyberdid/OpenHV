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
using System.IO;
using System.Text.Json;
using OpenRA.Mods.HV.Traits;

namespace OpenRA.Mods.HV.UtilityCommands
{
	sealed class PlanetPhysicsGolden : IUtilityCommand
	{
		static readonly JsonSerializerOptions SerializerOptions = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = true
		};

		string IUtilityCommand.Name => "--planet-physics-golden";

		bool IUtilityCommand.ValidateArguments(string[] args)
		{
			return args.Length is 2 or 3;
		}

		[Desc("OUTPUT.json", "[PULSES]", "Run isolated deterministic C# planet forcing scenarios.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			var pulses = args.Length == 3 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 10;
			if (pulses < 2 || pulses > 200)
				throw new ArgumentOutOfRangeException(nameof(args), "PULSES must be between 2 and 200.");

			var scenarios = new[]
			{
				Run("baseline", pulses),
				Run("high-greenhouse", pulses, carbonDioxide: 220_000),
				Run("thin-atmosphere", pulses, atmospherePressure: 210_000),
				Run("fast-rotation", pulses, rotationMinutes: 510),
				Run("high-stellar-flux", pulses, stellarFlux: 1590)
			};
			var payload = new
			{
				schemaVersion = 1,
				pulses,
				grid = new { latitudeCells = 180, longitudeCells = 360 },
				scenarios
			};
			var json = JsonSerializer.Serialize(payload, SerializerOptions);
			File.WriteAllText(args[1], json + Environment.NewLine);
			Console.WriteLine($"Planet physics golden scenarios written to {args[1]}.");
		}

		static object Run(
			string name,
			int pulses,
			int rotationMinutes = 1020,
			int stellarFlux = 1340,
			int atmospherePressure = 420_000,
			int carbonDioxide = 120_000)
		{
			var physicsDefinition = new PlanetPhysicsDefinition(
				1_020_000,
				6450,
				rotationMinutes,
				151_000,
				380,
				23_500,
				18_000,
				stellarFlux,
				12,
				420_000,
				atmospherePressure,
				carbonDioxide,
				650_000,
				920);
			var definition = new PlanetDefinition(0, $"golden-{name}", name, physicsDefinition);
			var physics = new PlanetPhysicalState(physicsDefinition);
			var surface = new PlanetSurfaceState(definition, physics);
			for (var pulse = 1; pulse <= pulses; pulse++)
			{
				physics.AdvanceClimate(pulse);
				surface.AdvanceClimate(physics, pulse);
			}

			return new
			{
				name,
				physics.MeanSurfaceTemperatureMilliKelvin,
				physics.RadiativeEquilibriumMilliKelvin,
				physics.AbsorbedSolarWattsPerSquareMeter,
				physics.AtmospherePressurePascals,
				physics.RotationPeriodMinutes,
				surface.MeanPressurePascals,
				surface.MeanWindCentimetersPerSecond,
				rossbyMillionths = RossbyMillionths(
					surface.MeanWindCentimetersPerSecond,
					physics.RotationPeriodMinutes,
					physics.RadiusKilometers),
				surface.MeanLatentFluxMilliWattsPerSquareMeter,
				surface.MeanVerticalVelocityMillimetersPerSecond,
				surface.WaterBalanceErrorUnits,
				surface.LatentEnergyResidualMilliWattsPerSquareMeter,
				surface.ElevationBalanceErrorMeters,
				surface.MaterialBalanceErrorUnits,
				surface.ClimateHash,
				surface.GeologyHash
			};
		}

		static long RossbyMillionths(int windCentimetersPerSecond, int rotationMinutes, int radiusKilometers)
		{
			return windCentimetersPerSecond * (long)rotationMinutes * 60 * 1_000_000 /
				(1_256_637L * radiusKilometers);
		}
	}
}
