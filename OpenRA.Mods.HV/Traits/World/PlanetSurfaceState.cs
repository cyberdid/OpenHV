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
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using OpenRA.Effects;
using OpenRA.Graphics;

namespace OpenRA.Mods.HV.Traits
{
	public enum PlanetCrustKind : byte
	{
		Oceanic,
		Continental,
		PlateBoundary
	}

	public enum PlanetTerrainKind : byte
	{
		DeepBasin,
		Shelf,
		Lowland,
		Highland,
		Mountain,
		Volcanic
	}

	public sealed class PlanetSurfaceSaveData
	{
		[FieldLoader.Require]
		public int SchemaVersion;

		[FieldLoader.Require]
		public int Generation;

		[FieldLoader.Require]
		public int TopologyHash;

		[FieldLoader.Require]
		public int GeologyPulseSequence;

		[FieldLoader.Require]
		public int GeologyHash;

		[FieldLoader.Require]
		public long ElevationBalanceErrorMeters;

		[FieldLoader.Require]
		public long MaterialBalanceErrorUnits;

		[FieldLoader.Require]
		public long CumulativeErodedMaterialUnits;

		[FieldLoader.Require]
		public long CumulativeMantleMaterialInputUnits;

		[FieldLoader.Require]
		public long CumulativeTectonicElevationChangeMeters;

		[FieldLoader.Require]
		public int ActiveVolcanicCellCount;

		[FieldLoader.Require]
		public string ElevationBrotliBase64;

		[FieldLoader.Require]
		public string SurfaceMaterialBrotliBase64;

		[FieldLoader.Require]
		public string LastGeologyChangeBrotliBase64;

		[FieldLoader.Require]
		public int HydrologyHash;

		[FieldLoader.Require]
		public string WaterDepthDeflateBase64;

		[FieldLoader.Require]
		public int ClimatePulseSequence;

		[FieldLoader.Require]
		public int ClimateHash;

		[FieldLoader.Require]
		public string TemperatureBrotliBase64;

		[FieldLoader.Require]
		public int AtmosphereHash;

		[FieldLoader.Require]
		public long TotalWaterMassUnits;

		[FieldLoader.Require]
		public int AtmosphereSubsteps;

		[FieldLoader.Require]
		public int AtmosphereStepSeconds;

		[FieldLoader.Require]
		public string EastWindBrotliBase64;

		[FieldLoader.Require]
		public string NorthWindBrotliBase64;

		[FieldLoader.Require]
		public string VaporWaterBrotliBase64;

		[FieldLoader.Require]
		public string CloudWaterBrotliBase64;

		[FieldLoader.Require]
		public string SurfaceWaterBrotliBase64;

		[FieldLoader.Require]
		public string PrecipitationBrotliBase64;

		[FieldLoader.Require]
		public int VerticalAtmosphereHash;

		[FieldLoader.Require]
		public int MeanLatentFluxMilliWattsPerSquareMeter;

		[FieldLoader.Require]
		public int LatentEnergyResidualMilliWattsPerSquareMeter;

		[FieldLoader.Require]
		public long CumulativeLatentEnergyMegaJoulesPerSquareMeter;

		[FieldLoader.Require]
		public string ColumnTemperatureBrotliBase64;

		[FieldLoader.Require]
		public string ColumnRelativeHumidityBrotliBase64;

		[FieldLoader.Require]
		public string ColumnEastWindBrotliBase64;

		[FieldLoader.Require]
		public string ColumnNorthWindBrotliBase64;

		[FieldLoader.Require]
		public string VerticalVelocityBrotliBase64;

		public int BiospherePulseSequence;

		public int BiosphereHash;

		public bool LifeOriginated;

		public int OriginLatitudeIndex;

		public int OriginLongitudeIndex;

		public string BiomassBrotliBase64;

		public string BiomassChurnBrotliBase64;

		public string ComplexityBrotliBase64;

		public string AbiogenesisProgressBrotliBase64;
	}

	/// <summary>
	/// Large equirectangular surface owned by the simulation. Cell arrays are
	/// represented in the synchronized state by deterministic digests so the
	/// normal 50 Hz RTS hash does not re-read every geological cell each tick.
	/// Any mutation must update its domain digest before returning.
	/// </summary>
	public sealed partial class PlanetSurfaceState : IEffect, ISync
	{
		const int SaveSchemaVersion = 6;
		const int LatitudeCount = 180;
		const int LongitudeCount = 360;
		const int ChunkLatitudeCount = 12;
		const int ChunkLongitudeCount = 12;

		readonly PlanetDefinition planet;
		readonly short[] elevationMeters;
		readonly byte[] crust;
		readonly byte[] plate;
		readonly byte[] terrain;
		readonly ushort[] waterDepthMeters;
		readonly byte[] materialRichness;
		readonly ushort[] temperatureDeciKelvin;
		readonly ushort[] nextTemperatureDeciKelvin;
		readonly int[] pressurePascals;
		readonly ushort[] absorbedSolarWattsPerSquareMeter;

		[VerifySync]
		int generation = 1;

		[VerifySync]
		int topologyHash;

		[VerifySync]
		int hydrologyHash;

		[VerifySync]
		int climatePulseSequence;

		[VerifySync]
		int climateHash;

		public int LatitudeCells => LatitudeCount;
		public int LongitudeCells => LongitudeCount;
		public int CellCount => LatitudeCount * LongitudeCount;
		public int ChunkLatitudeCells => ChunkLatitudeCount;
		public int ChunkLongitudeCells => ChunkLongitudeCount;
		public int ChunkRows => LatitudeCount / ChunkLatitudeCount;
		public int ChunkColumns => LongitudeCount / ChunkLongitudeCount;
		public int ChunkCount => ChunkRows * ChunkColumns;
		public int Generation => generation;
		public int TopologyHash => topologyHash;
		public int HydrologyHash => hydrologyHash;
		public int ClimatePulseSequence => climatePulseSequence;
		public int ClimateHash => climateHash;
		public int MinimumElevationMeters { get; private set; }
		public int MaximumElevationMeters { get; private set; }
		public int LandCellCount { get; private set; }
		public int BasinCellCount { get; private set; }
		public int MinimumTemperatureMilliKelvin { get; private set; }
		public int MaximumTemperatureMilliKelvin { get; private set; }
		public int MeanTemperatureMilliKelvin { get; private set; }
		public int MinimumPressurePascals { get; private set; }
		public int MaximumPressurePascals { get; private set; }
		public int MeanPressurePascals { get; private set; }
		public int MeanAbsorbedSolarWattsPerSquareMeter { get; private set; }

		public PlanetSurfaceState(PlanetDefinition planet, PlanetPhysicalState physics)
		{
			this.planet = planet;
			elevationMeters = new short[CellCount];
			crust = new byte[CellCount];
			plate = new byte[CellCount];
			terrain = new byte[CellCount];
			waterDepthMeters = new ushort[CellCount];
			materialRichness = new byte[CellCount];
			temperatureDeciKelvin = new ushort[CellCount];
			nextTemperatureDeciKelvin = new ushort[CellCount];
			pressurePascals = new int[CellCount];
			absorbedSolarWattsPerSquareMeter = new ushort[CellCount];
			Generate();
			InitializeGeology();
			InitializeClimate(physics);
			InitializeAtmosphereAndHydrology(physics);
			InitializeVerticalAtmosphere();
			InitializeBiosphere();
		}

		public string CellId(int latitudeIndex, int longitudeIndex)
		{
			ValidateCell(latitudeIndex, longitudeIndex);
			return $"{planet.PlanetId}:cell:{latitudeIndex:D3}:{longitudeIndex:D3}";
		}

		public string ChunkId(int chunkLatitudeIndex, int chunkLongitudeIndex)
		{
			if (chunkLatitudeIndex < 0 || chunkLatitudeIndex >= ChunkRows)
				throw new ArgumentOutOfRangeException(nameof(chunkLatitudeIndex));
			if (chunkLongitudeIndex < 0 || chunkLongitudeIndex >= ChunkColumns)
				throw new ArgumentOutOfRangeException(nameof(chunkLongitudeIndex));

			return $"{planet.PlanetId}:chunk:{chunkLatitudeIndex:D2}:{chunkLongitudeIndex:D2}";
		}

		public int ElevationAt(int latitudeIndex, int longitudeIndex)
		{
			return elevationMeters[CellIndex(latitudeIndex, longitudeIndex)];
		}

		public PlanetCrustKind CrustAt(int latitudeIndex, int longitudeIndex)
		{
			return (PlanetCrustKind)crust[CellIndex(latitudeIndex, longitudeIndex)];
		}

		public PlanetTerrainKind TerrainAt(int latitudeIndex, int longitudeIndex)
		{
			return (PlanetTerrainKind)terrain[CellIndex(latitudeIndex, longitudeIndex)];
		}

		public int PlateAt(int latitudeIndex, int longitudeIndex)
		{
			return plate[CellIndex(latitudeIndex, longitudeIndex)];
		}

		public int MaterialRichnessAt(int latitudeIndex, int longitudeIndex)
		{
			return materialRichness[CellIndex(latitudeIndex, longitudeIndex)];
		}

		public int TemperatureMilliKelvinAt(int latitudeIndex, int longitudeIndex)
		{
			return temperatureDeciKelvin[CellIndex(latitudeIndex, longitudeIndex)] * 100;
		}

		public int PressurePascalsAt(int latitudeIndex, int longitudeIndex)
		{
			return pressurePascals[CellIndex(latitudeIndex, longitudeIndex)];
		}

		public int AbsorbedSolarWattsPerSquareMeterAt(int latitudeIndex, int longitudeIndex)
		{
			return absorbedSolarWattsPerSquareMeter[CellIndex(latitudeIndex, longitudeIndex)];
		}

		internal PlanetSurfaceSaveData CreateSaveData() => new()
		{
			SchemaVersion = SaveSchemaVersion,
			Generation = generation,
			TopologyHash = topologyHash,
			GeologyPulseSequence = geologyPulseSequence,
			GeologyHash = geologyHash,
			ElevationBalanceErrorMeters = ElevationBalanceErrorMeters,
			MaterialBalanceErrorUnits = MaterialBalanceErrorUnits,
			CumulativeErodedMaterialUnits = CumulativeErodedMaterialUnits,
			CumulativeMantleMaterialInputUnits = CumulativeMantleMaterialInputUnits,
			CumulativeTectonicElevationChangeMeters = CumulativeTectonicElevationChangeMeters,
			ActiveVolcanicCellCount = ActiveVolcanicCellCount,
			ElevationBrotliBase64 = geologyPulseSequence == 0 ? string.Empty : CompressShorts(elevationMeters),
			SurfaceMaterialBrotliBase64 = geologyPulseSequence == 0 ? string.Empty :
				CompressDeltaUInts(surfaceMaterialUnits),
			LastGeologyChangeBrotliBase64 = geologyPulseSequence == 0 ? string.Empty :
				CompressShorts(lastGeologyChangeMeters),
			HydrologyHash = hydrologyHash,
			WaterDepthDeflateBase64 = CompressUShorts(waterDepthMeters),
			ClimatePulseSequence = climatePulseSequence,
			ClimateHash = climateHash,
			TemperatureBrotliBase64 = climatePulseSequence == 0 ? string.Empty :
				CompressDeltaUShorts(temperatureDeciKelvin),
			AtmosphereHash = atmosphereHash,
			TotalWaterMassUnits = this.TotalWaterMassUnits,
			AtmosphereSubsteps = atmosphereSubsteps,
			AtmosphereStepSeconds = atmosphereStepSeconds,
			EastWindBrotliBase64 = climatePulseSequence == 0 ? string.Empty : CompressShorts(eastWindCentimetersPerSecond),
			NorthWindBrotliBase64 = climatePulseSequence == 0 ? string.Empty : CompressShorts(northWindCentimetersPerSecond),
			VaporWaterBrotliBase64 = climatePulseSequence == 0 ? string.Empty : CompressDeltaUInts(vaporWaterMassUnits),
			CloudWaterBrotliBase64 = climatePulseSequence == 0 ? string.Empty : CompressDeltaUInts(cloudWaterMassUnits),
			SurfaceWaterBrotliBase64 = climatePulseSequence == 0 ? string.Empty : CompressDeltaUInts(surfaceWaterMassUnits),
			PrecipitationBrotliBase64 = climatePulseSequence == 0 ? string.Empty :
				CompressDeltaUShorts(precipitationTenthsMillimetersPerDay),
			VerticalAtmosphereHash = verticalAtmosphereHash,
			MeanLatentFluxMilliWattsPerSquareMeter = MeanLatentFluxMilliWattsPerSquareMeter,
			LatentEnergyResidualMilliWattsPerSquareMeter = LatentEnergyResidualMilliWattsPerSquareMeter,
			CumulativeLatentEnergyMegaJoulesPerSquareMeter = CumulativeLatentEnergyMegaJoulesPerSquareMeter,
			ColumnTemperatureBrotliBase64 = climatePulseSequence == 0 ? string.Empty :
				CompressDeltaUShorts(columnTemperatureDeciKelvin),
			ColumnRelativeHumidityBrotliBase64 = climatePulseSequence == 0 ? string.Empty :
				CompressDeltaUShorts(columnRelativeHumidityPerMille),
			ColumnEastWindBrotliBase64 = climatePulseSequence == 0 ? string.Empty :
				CompressShorts(columnEastWindCentimetersPerSecond),
			ColumnNorthWindBrotliBase64 = climatePulseSequence == 0 ? string.Empty :
				CompressShorts(columnNorthWindCentimetersPerSecond),
			VerticalVelocityBrotliBase64 = climatePulseSequence == 0 ? string.Empty :
				CompressShorts(verticalVelocityMillimetersPerSecond),
			BiospherePulseSequence = biospherePulseSequence,
			BiosphereHash = biosphereHash,
			LifeOriginated = lifeOriginated,
			OriginLatitudeIndex = OriginLatitudeIndex,
			OriginLongitudeIndex = OriginLongitudeIndex,
			BiomassBrotliBase64 = biospherePulseSequence == 0 ? string.Empty :
				CompressDeltaUShorts(biomassPerMille),
			BiomassChurnBrotliBase64 = biospherePulseSequence == 0 ? string.Empty :
				CompressDeltaUShorts(biomassChurnPerMille),
			ComplexityBrotliBase64 = biospherePulseSequence == 0 ? string.Empty :
				CompressDeltaUInts(complexityMillionths),
			AbiogenesisProgressBrotliBase64 = biospherePulseSequence == 0 ? string.Empty :
				CompressDeltaUInts(abiogenesisProgressUnits)
		};

		internal void Restore(PlanetSurfaceSaveData data, PlanetPhysicalState physics, int macroDay)
		{
			if (data.SchemaVersion < 5 || data.SchemaVersion > SaveSchemaVersion)
				throw new InvalidOperationException(
					$"Planet surface save schema {data.SchemaVersion} is not supported; expected 5–{SaveSchemaVersion}.");

			generation = data.Generation;
			if (topologyHash != data.TopologyHash)
				throw new InvalidOperationException(
					"Planet surface seed generated a different topology than the saved runtime.");

			if (data.GeologyPulseSequence > 0)
				RestoreGeology(data, physics);

			RestoreUShorts(data.WaterDepthDeflateBase64, waterDepthMeters, "hydrology");
			climatePulseSequence = data.ClimatePulseSequence;
			if (climatePulseSequence > 0)
			{
				RestoreDeltaUShorts(data.TemperatureBrotliBase64, temperatureDeciKelvin, "temperature");
				RestoreAtmosphereAndHydrology(data);
				RestoreVerticalAtmosphere(data);
			}

			RecalculateClimateDerivedState(physics, climatePulseSequence == 0 ? 0 : macroDay);
			if (data.SchemaVersion >= 6)
				RestoreBiosphere(data);
			else
				InitializeBiosphere();
			RecalculateDerivedState();
			if (geologyHash != data.GeologyHash)
				throw new InvalidOperationException(
					$"Planet geology digest {unchecked((uint)geologyHash):X8} does not match saved {unchecked((uint)data.GeologyHash):X8}.");
			if (hydrologyHash != data.HydrologyHash)
				throw new InvalidOperationException("Planet surface save payload failed its deterministic digest check.");
			if (climateHash != data.ClimateHash)
				throw new InvalidOperationException(
					$"Planet surface climate digest {unchecked((uint)climateHash):X8} does not match saved {unchecked((uint)data.ClimateHash):X8}.");
			if (atmosphereHash != data.AtmosphereHash)
				throw new InvalidOperationException(
					$"Planet atmosphere digest {unchecked((uint)atmosphereHash):X8} does not match saved {unchecked((uint)data.AtmosphereHash):X8}.");
			if (verticalAtmosphereHash != data.VerticalAtmosphereHash)
				throw new InvalidOperationException(
					$"Planet vertical-atmosphere digest {unchecked((uint)verticalAtmosphereHash):X8} does not match saved {unchecked((uint)data.VerticalAtmosphereHash):X8}.");
			if (TotalWaterMassUnits != data.TotalWaterMassUnits)
				throw new InvalidOperationException(
					$"Planet water mass {TotalWaterMassUnits} does not match saved {data.TotalWaterMassUnits}.");
		}

		void Generate()
		{
			var plateCount = Math.Clamp(planet.Physics.TectonicPlateCount, 1, 255);
			var centerLatitude = new int[plateCount];
			var centerLongitude = new int[plateCount];
			var continental = new bool[plateCount];
			var seed = unchecked(0x51ED270B ^ planet.Index * 0x1F123BB5 ^
				planet.Physics.MassEarthMillionths ^ planet.Physics.RadiusKilometers << 8);

			for (var p = 0; p < plateCount; p++)
			{
				centerLatitude[p] = Positive(CoordinateHash(seed, p, 11)) % LatitudeCount;
				centerLongitude[p] = Positive(CoordinateHash(seed, p, 29)) % LongitudeCount;
				continental[p] = (CoordinateHash(seed, p, 47) & 3) != 0;
			}

			for (var latitude = 0; latitude < LatitudeCount; latitude++)
			{
				var longitudeScale = 1000 - Math.Abs(latitude * 2 - (LatitudeCount - 1)) * 650 /
					(LatitudeCount - 1);
				for (var longitude = 0; longitude < LongitudeCount; longitude++)
				{
					var bestPlate = 0;
					var bestDistance = int.MaxValue;
					for (var p = 0; p < plateCount; p++)
					{
						var latitudeDelta = latitude - centerLatitude[p];
						var longitudeDelta = Math.Abs(longitude - centerLongitude[p]);
						longitudeDelta = Math.Min(longitudeDelta, LongitudeCount - longitudeDelta);
						var scaledLongitudeDelta = longitudeDelta * longitudeScale / 1000;
						var distance = latitudeDelta * latitudeDelta +
							scaledLongitudeDelta * scaledLongitudeDelta;
						if (distance < bestDistance)
						{
							bestDistance = distance;
							bestPlate = p;
						}
					}

					var index = latitude * LongitudeCount + longitude;
					plate[index] = (byte)bestPlate;
					crust[index] = (byte)(continental[bestPlate] ?
						PlanetCrustKind.Continental : PlanetCrustKind.Oceanic);
					var noise = Positive(CoordinateHash(seed, latitude, longitude)) % 2401 - 1200;
					elevationMeters[index] = checked((short)Math.Clamp(
						(continental[bestPlate] ? 700 : -3800) + noise, short.MinValue, short.MaxValue));
				}
			}

			for (var latitude = 0; latitude < LatitudeCount; latitude++)
				for (var longitude = 0; longitude < LongitudeCount; longitude++)
				{
					var index = latitude * LongitudeCount + longitude;
					var east = latitude * LongitudeCount + (longitude + 1) % LongitudeCount;
					var south = Math.Min(latitude + 1, LatitudeCount - 1) * LongitudeCount + longitude;
					var boundary = plate[index] != plate[east] || plate[index] != plate[south];
					if (boundary)
					{
						crust[index] = (byte)PlanetCrustKind.PlateBoundary;
						var uplift = (CoordinateHash(seed, latitude + 71, longitude + 113) & 1) == 0 ? 1800 : -700;
						elevationMeters[index] = checked((short)Math.Clamp(
							elevationMeters[index] + uplift, short.MinValue, short.MaxValue));
					}

					var elevation = elevationMeters[index];
					terrain[index] = (byte)(boundary && elevation > 1800 ? PlanetTerrainKind.Volcanic :
						elevation < -3000 ? PlanetTerrainKind.DeepBasin :
						elevation < 0 ? PlanetTerrainKind.Shelf :
						elevation < 900 ? PlanetTerrainKind.Lowland :
						elevation < 2200 ? PlanetTerrainKind.Highland : PlanetTerrainKind.Mountain);
					materialRichness[index] = (byte)Math.Clamp(
						30 + Positive(CoordinateHash(seed ^ 0x6D2B79F5, latitude, longitude)) % 151 +
						(boundary ? 45 : 0), 0, 255);
				}

			RecalculateDerivedState();
		}

		void InitializeClimate(PlanetPhysicalState physics)
		{
			var globalAbsorbed = physics.AbsorbedSolarWattsPerSquareMeter;
			for (var latitude = 0; latitude < LatitudeCount; latitude++)
				for (var longitude = 0; longitude < LongitudeCount; longitude++)
				{
					var index = latitude * LongitudeCount + longitude;
					var factor = BaseInsolationFactor(latitude);
					var absorbed = globalAbsorbed * factor / 1000;
					absorbedSolarWattsPerSquareMeter[index] = (ushort)Math.Clamp(absorbed, 0, ushort.MaxValue);
					var lapseDeciKelvin = elevationMeters[index] * 65 / 1000;
					var temperature = physics.MeanSurfaceTemperatureMilliKelvin / 100 +
						(absorbed - globalAbsorbed) * 2 - lapseDeciKelvin;
					temperatureDeciKelvin[index] = (ushort)Math.Clamp(temperature, 1000, 9000);
					pressurePascals[index] = LocalPressure(
						physics.AtmospherePressurePascals, elevationMeters[index]);
				}

			// Derived radiation must use the same orbital/axial-tilt model as
			// checkpoint restoration, including the day-zero seasonal phase.
			RecalculateClimateDerivedState(physics, 0);
		}

		internal void AdvanceClimate(PlanetPhysicalState physics, int macroDay)
		{
			AdvanceGeology(physics);
			climatePulseSequence++;
			var orbit = Math.Max(1, physics.OrbitalPeriodDays);
			var orbitalDay = Math.Abs(macroDay % orbit);
			var half = Math.Max(1, orbit / 2);
			var season = orbitalDay <= half ? orbitalDay * 2000 / half - 1000 :
				1000 - (orbitalDay - half) * 2000 / Math.Max(1, orbit - half);
			var tiltScale = Math.Clamp(physics.AxialTiltMilliDegrees * 1000 / 45_000, 0, 1000);
			var globalAbsorbed = physics.AbsorbedSolarWattsPerSquareMeter;

			for (var chunkLatitude = 0; chunkLatitude < ChunkRows; chunkLatitude++)
				for (var chunkLongitude = 0; chunkLongitude < ChunkColumns; chunkLongitude++)
					for (var localLatitude = 0; localLatitude < ChunkLatitudeCount; localLatitude++)
						for (var localLongitude = 0; localLongitude < ChunkLongitudeCount; localLongitude++)
						{
							var latitude = chunkLatitude * ChunkLatitudeCount + localLatitude;
							var longitude = chunkLongitude * ChunkLongitudeCount + localLongitude;
							var index = latitude * LongitudeCount + longitude;
							var signedLatitude = (LatitudeCount - 1 - latitude * 2) * 1000 / (LatitudeCount - 1);
							var seasonalAdjustment = signedLatitude * season * tiltScale / 2_000_000;
							var factor = Math.Clamp(BaseInsolationFactor(latitude) + seasonalAdjustment, 50, 2100);
							var absorbed = globalAbsorbed * factor / 1000;
							absorbedSolarWattsPerSquareMeter[index] = (ushort)Math.Clamp(absorbed, 0, ushort.MaxValue);

							var lapseDeciKelvin = elevationMeters[index] * 65 / 1000;
							var target = physics.RadiativeEquilibriumMilliKelvin / 100 +
								(absorbed - globalAbsorbed) * 2 - lapseDeciKelvin;
							var north = Math.Max(0, latitude - 1) * LongitudeCount + longitude;
							var south = Math.Min(LatitudeCount - 1, latitude + 1) * LongitudeCount + longitude;
							var west = latitude * LongitudeCount + (longitude + LongitudeCount - 1) % LongitudeCount;
							var east = latitude * LongitudeCount + (longitude + 1) % LongitudeCount;
							var neighborMean = (temperatureDeciKelvin[north] + temperatureDeciKelvin[south] +
								temperatureDeciKelvin[west] + temperatureDeciKelvin[east]) / 4;
							var current = temperatureDeciKelvin[index];
							var next = current + (target - current) / 12 + (neighborMean - current) / 20;
							nextTemperatureDeciKelvin[index] = (ushort)Math.Clamp(next, 1000, 9000);
						}

			Array.Copy(nextTemperatureDeciKelvin, temperatureDeciKelvin, CellCount);
			RecalculateClimateDerivedState(physics, macroDay);
			AdvanceAtmosphereAndHydrology(physics, macroDay);
			RecalculateClimateDerivedState(physics, macroDay);
		}

		void RecalculateClimateDerivedState(PlanetPhysicalState physics, int macroDay)
		{
			var orbit = Math.Max(1, physics.OrbitalPeriodDays);
			var orbitalDay = Math.Abs(macroDay % orbit);
			var half = Math.Max(1, orbit / 2);
			var season = orbitalDay <= half ? orbitalDay * 2000 / half - 1000 :
				1000 - (orbitalDay - half) * 2000 / Math.Max(1, orbit - half);
			var tiltScale = Math.Clamp(physics.AxialTiltMilliDegrees * 1000 / 45_000, 0, 1000);
			var globalAbsorbed = physics.AbsorbedSolarWattsPerSquareMeter;
			for (var latitude = 0; latitude < LatitudeCount; latitude++)
				for (var longitude = 0; longitude < LongitudeCount; longitude++)
				{
					var index = latitude * LongitudeCount + longitude;
					var signedLatitude = (LatitudeCount - 1 - latitude * 2) * 1000 / (LatitudeCount - 1);
					var seasonalAdjustment = signedLatitude * season * tiltScale / 2_000_000;
					var factor = Math.Clamp(BaseInsolationFactor(latitude) + seasonalAdjustment, 50, 2100);
					absorbedSolarWattsPerSquareMeter[index] = (ushort)Math.Clamp(
						globalAbsorbed * factor / 1000, 0, ushort.MaxValue);
					pressurePascals[index] = LocalPressure(physics.AtmospherePressurePascals, elevationMeters[index]);
				}

			RecalculateClimateSummaryAndHash();
		}

		void RecalculateClimateSummaryAndHash()
		{
			var minimumTemperature = int.MaxValue;
			var maximumTemperature = int.MinValue;
			var minimumPressure = int.MaxValue;
			var maximumPressure = int.MinValue;
			long temperatureTotal = 0;
			long pressureTotal = 0;
			long absorbedTotal = 0;
			var hash = 2166136261u;
			for (var i = 0; i < CellCount; i++)
			{
				minimumTemperature = Math.Min(minimumTemperature, temperatureDeciKelvin[i]);
				maximumTemperature = Math.Max(maximumTemperature, temperatureDeciKelvin[i]);
				minimumPressure = Math.Min(minimumPressure, pressurePascals[i]);
				maximumPressure = Math.Max(maximumPressure, pressurePascals[i]);
				temperatureTotal += temperatureDeciKelvin[i];
				pressureTotal += pressurePascals[i];
				absorbedTotal += absorbedSolarWattsPerSquareMeter[i];
				hash = Mix(hash, temperatureDeciKelvin[i]);
				hash = Mix32(hash, pressurePascals[i]);
				hash = Mix(hash, absorbedSolarWattsPerSquareMeter[i]);
			}

			MinimumTemperatureMilliKelvin = minimumTemperature * 100;
			MaximumTemperatureMilliKelvin = maximumTemperature * 100;
			MeanTemperatureMilliKelvin = checked((int)(temperatureTotal * 100 / CellCount));
			MinimumPressurePascals = minimumPressure;
			MaximumPressurePascals = maximumPressure;
			MeanPressurePascals = checked((int)(pressureTotal / CellCount));
			MeanAbsorbedSolarWattsPerSquareMeter = checked((int)(absorbedTotal / CellCount));
			climateHash = unchecked((int)hash);
		}

		static int BaseInsolationFactor(int latitude)
		{
			var distanceFromEquator = Math.Abs(latitude * 2 - (LatitudeCount - 1)) * 1000 /
				(LatitudeCount - 1);
			return 1750 - distanceFromEquator * 1500 / 1000;
		}

		static int LocalPressure(int globalPressure, int elevationMeters)
		{
			var factor = Math.Clamp(1000 - elevationMeters * 110 / 1000, 250, 2000);
			return Math.Max(100, checked((int)((long)globalPressure * factor / 1000)));
		}

		void RecalculateDerivedState()
		{
			MinimumElevationMeters = int.MaxValue;
			MaximumElevationMeters = int.MinValue;
			LandCellCount = 0;
			BasinCellCount = 0;
			var topology = 2166136261u;
			for (var i = 0; i < CellCount; i++)
			{
				var elevation = elevationMeters[i];
				MinimumElevationMeters = Math.Min(MinimumElevationMeters, elevation);
				MaximumElevationMeters = Math.Max(MaximumElevationMeters, elevation);
				if (elevation >= 0)
					LandCellCount++;
				else
					BasinCellCount++;

				topology = Mix(topology, crust[i]);
				topology = Mix(topology, plate[i]);
			}

			topologyHash = unchecked((int)topology);
			RecalculateGeologySummaryAndHash();
			RecalculateHydrologyAndAtmosphereSummary();
			RecalculateVerticalAtmosphereSummary();
		}

		static int CellIndex(int latitudeIndex, int longitudeIndex)
		{
			ValidateCell(latitudeIndex, longitudeIndex);
			return latitudeIndex * LongitudeCount + longitudeIndex;
		}

		static void ValidateCell(int latitudeIndex, int longitudeIndex)
		{
			if (latitudeIndex < 0 || latitudeIndex >= LatitudeCount)
				throw new ArgumentOutOfRangeException(nameof(latitudeIndex));
			if (longitudeIndex < 0 || longitudeIndex >= LongitudeCount)
				throw new ArgumentOutOfRangeException(nameof(longitudeIndex));
		}

		static string CompressUShorts(ushort[] values)
		{
			var raw = new byte[values.Length * 2];
			for (var i = 0; i < values.Length; i++)
			{
				raw[i * 2] = (byte)values[i];
				raw[i * 2 + 1] = (byte)(values[i] >> 8);
			}

			using var output = new MemoryStream();
			using (var compressor = new DeflateStream(output, CompressionLevel.Optimal, true))
				compressor.Write(raw, 0, raw.Length);

			return Convert.ToBase64String(output.ToArray());
		}

		static string CompressDeltaUShorts(ushort[] values)
		{
			var deltas = new ushort[values.Length];
			if (values.Length > 0)
				deltas[0] = values[0];
			for (var i = 1; i < values.Length; i++)
				deltas[i] = unchecked((ushort)(short)(values[i] - values[i - 1]));

			var raw = EncodeUShorts(deltas);
			using var output = new MemoryStream();
			using (var compressor = new BrotliStream(output, CompressionLevel.Optimal, true))
				compressor.Write(raw, 0, raw.Length);

			return Convert.ToBase64String(output.ToArray());
		}

		static void RestoreUShorts(string encoded, ushort[] destination, string fieldName)
		{
			byte[] compressed;
			try
			{
				compressed = Convert.FromBase64String(encoded);
			}
			catch (FormatException e)
			{
				throw new InvalidOperationException($"Saved planet {fieldName} is not valid base64.", e);
			}

			using var input = new MemoryStream(compressed);
			using var decompressor = new DeflateStream(input, CompressionMode.Decompress);
			using var output = new MemoryStream();
			decompressor.CopyTo(output);
			var raw = output.ToArray();
			if (raw.Length != destination.Length * 2)
				throw new InvalidOperationException(
					$"Saved planet {fieldName} contains {raw.Length} bytes; expected {destination.Length * 2}.");

			for (var i = 0; i < destination.Length; i++)
				destination[i] = (ushort)(raw[i * 2] | raw[i * 2 + 1] << 8);
		}

		static void RestoreDeltaUShorts(string encoded, ushort[] destination, string fieldName)
		{
			byte[] compressed;
			try
			{
				compressed = Convert.FromBase64String(encoded);
			}
			catch (FormatException e)
			{
				throw new InvalidOperationException($"Saved planet {fieldName} is not valid base64.", e);
			}

			using var input = new MemoryStream(compressed);
			using var decompressor = new BrotliStream(input, CompressionMode.Decompress);
			using var output = new MemoryStream();
			decompressor.CopyTo(output);
			var raw = output.ToArray();
			if (raw.Length != destination.Length * 2)
				throw new InvalidOperationException(
					$"Saved planet {fieldName} contains {raw.Length} bytes; expected {destination.Length * 2}.");

			var deltas = DecodeUShorts(raw, destination.Length);
			if (destination.Length == 0)
				return;

			destination[0] = deltas[0];
			for (var i = 1; i < destination.Length; i++)
				destination[i] = unchecked((ushort)(destination[i - 1] + (short)deltas[i]));
		}

		static byte[] EncodeUShorts(ushort[] values)
		{
			var raw = new byte[values.Length * 2];
			for (var i = 0; i < values.Length; i++)
			{
				raw[i * 2] = (byte)values[i];
				raw[i * 2 + 1] = (byte)(values[i] >> 8);
			}

			return raw;
		}

		static ushort[] DecodeUShorts(byte[] raw, int count)
		{
			var result = new ushort[count];
			for (var i = 0; i < count; i++)
				result[i] = (ushort)(raw[i * 2] | raw[i * 2 + 1] << 8);
			return result;
		}

		static int Positive(int value)
		{
			return value == int.MinValue ? int.MaxValue : Math.Abs(value);
		}

		static int CoordinateHash(int seed, int y, int x)
		{
			unchecked
			{
				var value = (uint)seed;
				value ^= (uint)y * 0x9E3779B9u;
				value = (value ^ (value >> 16)) * 0x85EBCA6Bu;
				value ^= (uint)x * 0xC2B2AE35u;
				value = (value ^ (value >> 13)) * 0x27D4EB2Du;
				return (int)(value ^ (value >> 15));
			}
		}

		static uint Mix(uint hash, int value)
		{
			unchecked
			{
				hash = (hash ^ (byte)value) * 16777619u;
				hash = (hash ^ (byte)(value >> 8)) * 16777619u;
				return hash;
			}
		}

		static uint Mix32(uint hash, int value)
		{
			unchecked
			{
				hash = Mix(hash, value);
				hash = (hash ^ (byte)(value >> 16)) * 16777619u;
				hash = (hash ^ (byte)(value >> 24)) * 16777619u;
				return hash;
			}
		}

		void IEffect.Tick(World world) { }

		IEnumerable<IRenderable> IEffect.Render(WorldRenderer renderer) { return []; }
	}
}
