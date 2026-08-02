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
using System.IO;
using System.IO.Compression;

namespace OpenRA.Mods.HV.Traits
{
	public sealed partial class PlanetSurfaceState
	{
		const long WaterUnitsPerPartPerMillion = 1_000_000;
		const long WaterUnitsPerCubicKilometer = 500;
		const int SecondsPerDay = 86_400;
		const int MinimumAtmosphereStepSeconds = 900;
		const int MaximumAtmosphereStepSeconds = 21_600;
		const int MaximumAtmosphereSubsteps = SecondsPerDay / MinimumAtmosphereStepSeconds;

		readonly ushort[] latitudeAreaWeights = new ushort[LatitudeCount];
		readonly int[] dynamicPressurePascals = new int[LatitudeCount * LongitudeCount];
		readonly int[] nextDynamicPressurePascals = new int[LatitudeCount * LongitudeCount];
		readonly short[] eastWindCentimetersPerSecond = new short[LatitudeCount * LongitudeCount];
		readonly short[] northWindCentimetersPerSecond = new short[LatitudeCount * LongitudeCount];
		readonly short[] nextEastWindCentimetersPerSecond = new short[LatitudeCount * LongitudeCount];
		readonly short[] nextNorthWindCentimetersPerSecond = new short[LatitudeCount * LongitudeCount];
		readonly uint[] vaporWaterMassUnits = new uint[LatitudeCount * LongitudeCount];
		readonly uint[] cloudWaterMassUnits = new uint[LatitudeCount * LongitudeCount];
		readonly uint[] surfaceWaterMassUnits = new uint[LatitudeCount * LongitudeCount];
		readonly uint[] precipitationMassUnits = new uint[LatitudeCount * LongitudeCount];
		readonly uint[] dailyEvaporatedMassUnits = new uint[LatitudeCount * LongitudeCount];
		readonly uint[] dailyCondensedMassUnits = new uint[LatitudeCount * LongitudeCount];
		readonly ushort[] precipitationTenthsMillimetersPerDay = new ushort[LatitudeCount * LongitudeCount];
		readonly long[] transportDelta = new long[LatitudeCount * LongitudeCount];

		[VerifySync]
		int atmosphereHash;

		[VerifySync]
		int atmosphereSubsteps;

		[VerifySync]
		int atmosphereStepSeconds;

		int totalAreaWeight;

		public int AtmosphereHash => atmosphereHash;
		public long TotalWaterMassUnits { get; private set; }
		public long WaterBalanceErrorUnits { get; private set; }
		public int AtmosphereSubsteps => atmosphereSubsteps;
		public int AtmosphereStepSeconds => atmosphereStepSeconds;
		public int MeanWindCentimetersPerSecond { get; private set; }
		public int MaximumWindCentimetersPerSecond { get; private set; }
		public int MeanAtmosphericWaterPartsPerMillion { get; private set; }
		public int MeanCloudCoverPerMille { get; private set; }
		public int MeanPrecipitationTenthsMillimetersPerDay { get; private set; }
		public long SpatialSurfaceWaterCubicKilometers { get; private set; }

		public int EastWindCentimetersPerSecondAt(int latitudeIndex, int longitudeIndex)
		{
			return eastWindCentimetersPerSecond[CellIndex(latitudeIndex, longitudeIndex)];
		}

		public int NorthWindCentimetersPerSecondAt(int latitudeIndex, int longitudeIndex)
		{
			return northWindCentimetersPerSecond[CellIndex(latitudeIndex, longitudeIndex)];
		}

		public int CloudCoverPerMilleAt(int latitudeIndex, int longitudeIndex)
		{
			var index = CellIndex(latitudeIndex, longitudeIndex);
			var atmosphericWater = (long)vaporWaterMassUnits[index] + cloudWaterMassUnits[index];
			return atmosphericWater == 0 ? 0 : checked((int)(cloudWaterMassUnits[index] * 1000L / atmosphericWater));
		}

		public int PrecipitationTenthsMillimetersPerDayAt(int latitudeIndex, int longitudeIndex)
		{
			return precipitationTenthsMillimetersPerDay[CellIndex(latitudeIndex, longitudeIndex)];
		}

		void InitializeAtmosphereAndHydrology(PlanetPhysicalState physics)
		{
			InitializeAreaWeights();
			DistributeMass(
				vaporWaterMassUnits,
				physics.AtmosphericWaterPartsPerMillion * WaterUnitsPerPartPerMillion);
			DistributeMass(
				surfaceWaterMassUnits,
				physics.SurfaceWaterCubicKilometers * WaterUnitsPerCubicKilometer);
			TotalWaterMassUnits = SumWaterMass();
			UpdateWaterDepthsAndPrecipitation();
			for (var i = 0; i < 3; i++)
				UpdateWindField(physics);

			RecalculateHydrologyAndAtmosphereSummary();
		}

		void AdvanceAtmosphereAndHydrology(PlanetPhysicalState physics, int macroDay)
		{
			ReconcileGlobalHydrology(physics);
			var waterBefore = TotalWaterMassUnits;
			Array.Clear(precipitationMassUnits);
			Array.Clear(precipitationTenthsMillimetersPerDay);
			Array.Clear(dailyEvaporatedMassUnits);
			Array.Clear(dailyCondensedMassUnits);

			var elapsedSeconds = 0;
			atmosphereSubsteps = 0;
			atmosphereStepSeconds = MaximumAtmosphereStepSeconds;
			while (elapsedSeconds < SecondsPerDay && atmosphereSubsteps < MaximumAtmosphereSubsteps)
			{
				UpdateWindField(physics);
				var stepSeconds = ComputeAtmosphereStepSeconds(SecondsPerDay - elapsedSeconds);
				atmosphereStepSeconds = Math.Min(atmosphereStepSeconds, stepSeconds);
				AdvectWater(vaporWaterMassUnits, stepSeconds);
				AdvectWater(cloudWaterMassUnits, stepSeconds);
				AdvanceWaterPhaseChanges(stepSeconds);
				elapsedSeconds += stepSeconds;
				atmosphereSubsteps++;
			}

			for (var pass = 0; pass < 4; pass++)
				AdvanceRunoff();

			AdvanceVerticalAtmosphereAndLatentEnergy(physics, macroDay);
			UpdateWaterDepthsAndPrecipitation();
			var waterAfter = SumWaterMass();
			WaterBalanceErrorUnits = waterAfter - waterBefore;
			if (WaterBalanceErrorUnits != 0)
				throw new InvalidOperationException(
					$"Planet hydrology changed total water by {WaterBalanceErrorUnits} mass units.");

			TotalWaterMassUnits = waterAfter;
			var atmosphericWater = Sum(vaporWaterMassUnits) + Sum(cloudWaterMassUnits);
			var surfaceWater = Sum(surfaceWaterMassUnits);
			physics.ApplySpatialHydrology(
				checked((int)(atmosphericWater / WaterUnitsPerPartPerMillion)),
				surfaceWater / WaterUnitsPerCubicKilometer,
				macroDay);
			RecalculateHydrologyAndAtmosphereSummary();
		}

		void InitializeAreaWeights()
		{
			totalAreaWeight = 0;
			for (var latitude = 0; latitude < LatitudeCount; latitude++)
			{
				var distanceFromEquator = Math.Abs(latitude * 2 - (LatitudeCount - 1)) * 1000 /
					(LatitudeCount - 1);
				latitudeAreaWeights[latitude] = (ushort)Math.Max(25, 1000 - distanceFromEquator * 975 / 1000);
				totalAreaWeight += latitudeAreaWeights[latitude] * LongitudeCount;
			}
		}

		void DistributeMass(uint[] destination, long totalMass)
		{
			Array.Clear(destination);
			long distributed = 0;
			for (var latitude = 0; latitude < LatitudeCount; latitude++)
				for (var longitude = 0; longitude < LongitudeCount; longitude++)
				{
					var index = latitude * LongitudeCount + longitude;
					var share = totalMass * latitudeAreaWeights[latitude] / totalAreaWeight;
					destination[index] = checked((uint)share);
					distributed += share;
				}

			var remainder = totalMass - distributed;
			for (var i = 0; remainder > 0; i = (i + 1) % CellCount)
			{
				destination[i]++;
				remainder--;
			}
		}

		void ReconcileGlobalHydrology(PlanetPhysicalState physics)
		{
			var targetAtmospheric = Math.Clamp(
				physics.AtmosphericWaterPartsPerMillion * WaterUnitsPerPartPerMillion,
				0,
				TotalWaterMassUnits);
			var currentAtmospheric = Sum(vaporWaterMassUnits) + Sum(cloudWaterMassUnits);
			if (currentAtmospheric > targetAtmospheric)
			{
				var remaining = currentAtmospheric - targetAtmospheric;
				remaining -= TransferProportionally(
					vaporWaterMassUnits, surfaceWaterMassUnits, Math.Min(remaining, Sum(vaporWaterMassUnits)));
				if (remaining > 0)
					TransferProportionally(cloudWaterMassUnits, surfaceWaterMassUnits, remaining);
			}
			else if (currentAtmospheric < targetAtmospheric)
				TransferProportionally(
					surfaceWaterMassUnits, vaporWaterMassUnits, targetAtmospheric - currentAtmospheric);
		}

		static long TransferProportionally(uint[] source, uint[] destination, long requested)
		{
			var available = Sum(source);
			var amount = Math.Min(requested, available);
			if (amount <= 0 || available <= 0)
				return 0;

			long transferred = 0;
			for (var i = 0; i < source.Length; i++)
			{
				var take = source[i] * amount / available;
				if (take == 0)
					continue;

				source[i] -= checked((uint)take);
				destination[i] = checked(destination[i] + (uint)take);
				transferred += take;
			}

			for (var i = 0; transferred < amount; i = (i + 1) % source.Length)
				if (source[i] > 0)
				{
					source[i]--;
					destination[i] = checked(destination[i] + 1);
					transferred++;
				}

			return transferred;
		}

		void UpdateWindField(PlanetPhysicalState physics)
		{
			var meanTemperatureDeciKelvin = MeanTemperatureMilliKelvin / 100;
			for (var i = 0; i < CellCount; i++)
				dynamicPressurePascals[i] = MeanPressurePascals -
					(temperatureDeciKelvin[i] - meanTemperatureDeciKelvin) * 18 +
					(pressurePascals[i] - MeanPressurePascals) / 20;

			// Winds respond to synoptic pressure, not every one-cell terrain
			// anomaly. Three conservative smoothing passes mirror the oracle.
			for (var pass = 0; pass < 3; pass++)
			{
				for (var latitude = 0; latitude < LatitudeCount; latitude++)
					for (var longitude = 0; longitude < LongitudeCount; longitude++)
					{
						var index = latitude * LongitudeCount + longitude;
						var north = Math.Max(0, latitude - 1) * LongitudeCount + longitude;
						var south = Math.Min(LatitudeCount - 1, latitude + 1) * LongitudeCount + longitude;
						var west = latitude * LongitudeCount +
							(longitude + LongitudeCount - 1) % LongitudeCount;
						var east = latitude * LongitudeCount + (longitude + 1) % LongitudeCount;
						var neighborMean = (dynamicPressurePascals[north] + dynamicPressurePascals[south] +
							dynamicPressurePascals[west] + dynamicPressurePascals[east]) / 4;
						nextDynamicPressurePascals[index] = dynamicPressurePascals[index] +
							(neighborMean - dynamicPressurePascals[index]) / 4;
					}

				Array.Copy(nextDynamicPressurePascals, dynamicPressurePascals, CellCount);
			}

			for (var latitude = 0; latitude < LatitudeCount; latitude++)
				for (var longitude = 0; longitude < LongitudeCount; longitude++)
				{
					var index = latitude * LongitudeCount + longitude;
					var north = Math.Max(0, latitude - 1) * LongitudeCount + longitude;
					var south = Math.Min(LatitudeCount - 1, latitude + 1) * LongitudeCount + longitude;
					var west = latitude * LongitudeCount + (longitude + LongitudeCount - 1) % LongitudeCount;
					var east = latitude * LongitudeCount + (longitude + 1) % LongitudeCount;
					var signedLatitude = (LatitudeCount - 1 - latitude * 2) * 1000 / (LatitudeCount - 1);
					var coriolis = signedLatitude * 1440 / Math.Max(1, physics.RotationPeriodMinutes);
					if (Math.Abs(coriolis) < 80)
						coriolis = coriolis < 0 ? -80 : 80;

					var northGradient = (dynamicPressurePascals[south] - dynamicPressurePascals[north]) / 2;
					var eastGradient = (dynamicPressurePascals[east] - dynamicPressurePascals[west]) / 2;
					var targetEast = Math.Clamp(-northGradient * 5500 / coriolis, -9500, 9500);
					var targetNorth = Math.Clamp(eastGradient * 5500 / coriolis, -9500, 9500);
					var eastNeighborMean = (eastWindCentimetersPerSecond[north] +
						eastWindCentimetersPerSecond[south] + eastWindCentimetersPerSecond[west] +
						eastWindCentimetersPerSecond[east]) / 4;
					var northNeighborMean = (northWindCentimetersPerSecond[north] +
						northWindCentimetersPerSecond[south] + northWindCentimetersPerSecond[west] +
						northWindCentimetersPerSecond[east]) / 4;
					var nextEast = eastWindCentimetersPerSecond[index] +
						(targetEast - eastWindCentimetersPerSecond[index]) / 4 +
						(eastNeighborMean - eastWindCentimetersPerSecond[index]) / 12;
					var nextNorth = northWindCentimetersPerSecond[index] +
						(targetNorth - northWindCentimetersPerSecond[index]) / 4 +
						(northNeighborMean - northWindCentimetersPerSecond[index]) / 12;
					if (Math.Abs(signedLatitude) >= 890)
					{
						nextEast = nextEast * 3 / 5;
						nextNorth = nextNorth * 3 / 5;
					}

					nextEastWindCentimetersPerSecond[index] = (short)Math.Clamp(nextEast, -9500, 9500);
					nextNorthWindCentimetersPerSecond[index] = (short)Math.Clamp(nextNorth, -9500, 9500);
				}

			Array.Copy(nextEastWindCentimetersPerSecond, eastWindCentimetersPerSecond, CellCount);
			Array.Copy(nextNorthWindCentimetersPerSecond, northWindCentimetersPerSecond, CellCount);
		}

		int ComputeAtmosphereStepSeconds(int remainingSeconds)
		{
			var northSpacingMeters = NorthCellSpacingMeters();
			var step = MaximumAtmosphereStepSeconds;
			for (var latitude = 0; latitude < LatitudeCount; latitude++)
			{
				var signedLatitude = (LatitudeCount - 1 - latitude * 2) * 1000 / (LatitudeCount - 1);
				if (Math.Abs(signedLatitude) > 933)
					continue;

				var eastSpacingMeters = EastCellSpacingMeters(latitude);
				for (var longitude = 0; longitude < LongitudeCount; longitude++)
				{
					var index = latitude * LongitudeCount + longitude;
					var eastSpeed = Math.Abs(eastWindCentimetersPerSecond[index]);
					var northSpeed = Math.Abs(northWindCentimetersPerSecond[index]);
					if (eastSpeed > 0)
						step = Math.Min(step, eastSpacingMeters * 45 / eastSpeed);
					if (northSpeed > 0)
						step = Math.Min(step, northSpacingMeters * 45 / northSpeed);
				}
			}

			return Math.Min(remainingSeconds, Math.Clamp(
				step, MinimumAtmosphereStepSeconds, MaximumAtmosphereStepSeconds));
		}

		void AdvectWater(uint[] field, int stepSeconds)
		{
			Array.Clear(transportDelta);
			var northSpacingMeters = NorthCellSpacingMeters();
			for (var latitude = 0; latitude < LatitudeCount; latitude++)
			{
				var eastSpacingMeters = EastCellSpacingMeters(latitude);
				for (var longitude = 0; longitude < LongitudeCount; longitude++)
				{
					var index = latitude * LongitudeCount + longitude;
					TransferAlongWind(
						field, index, eastWindCentimetersPerSecond[index], stepSeconds,
						eastSpacingMeters, latitude,
						(longitude + (eastWindCentimetersPerSecond[index] >= 0 ? 1 : LongitudeCount - 1)) %
							LongitudeCount);

					var destinationLatitude = Math.Clamp(
						latitude + (northWindCentimetersPerSecond[index] >= 0 ? -1 : 1),
						0,
						LatitudeCount - 1);
					TransferAlongWind(
						field, index, northWindCentimetersPerSecond[index], stepSeconds,
						northSpacingMeters, destinationLatitude, longitude);
				}
			}

			ApplyTransportDelta(field);
		}

		void TransferAlongWind(
			uint[] field,
			int source,
			int speedCentimetersPerSecond,
			int stepSeconds,
			int spacingMeters,
			int destinationLatitude,
			int destinationLongitude)
		{
			if (speedCentimetersPerSecond == 0)
				return;

			var amount = Math.Min(
				field[source] * 45L / 100,
				field[source] * Math.Abs(speedCentimetersPerSecond) * stepSeconds /
					(Math.Max(1, spacingMeters) * 100L));
			if (amount <= 0)
				return;

			var destination = destinationLatitude * LongitudeCount + destinationLongitude;
			if (destination == source)
				return;

			var sourceAvailable = field[source] + Math.Min(0, transportDelta[source]);
			var destinationCapacity = uint.MaxValue - field[destination] -
				Math.Max(0, transportDelta[destination]);
			amount = Math.Min(amount, Math.Max(0, sourceAvailable));
			amount = Math.Min(amount, Math.Max(0, destinationCapacity));
			if (amount == 0)
				return;

			transportDelta[source] -= amount;
			transportDelta[destination] += amount;
		}

		void ApplyTransportDelta(uint[] field)
		{
			for (var i = 0; i < CellCount; i++)
			{
				var updated = field[i] + transportDelta[i];
				if (updated < 0 || updated > uint.MaxValue)
					throw new InvalidOperationException("Atmospheric transport exceeded its fixed-point reservoir.");

				field[i] = (uint)updated;
			}
		}

		void AdvanceWaterPhaseChanges(int stepSeconds)
		{
			for (var i = 0; i < CellCount; i++)
			{
				var saturation = SaturationVaporMassUnits(i);
				if (vaporWaterMassUnits[i] > saturation)
				{
					var excess = vaporWaterMassUnits[i] - saturation;
					var condensed = Math.Min(vaporWaterMassUnits[i],
						checked((uint)Math.Max(1, excess * stepSeconds / (SecondsPerDay * 180L))));
					vaporWaterMassUnits[i] -= condensed;
					cloudWaterMassUnits[i] = checked(cloudWaterMassUnits[i] + condensed);
					dailyCondensedMassUnits[i] = checked(dailyCondensedMassUnits[i] + condensed);
				}

				var cloudThreshold = Math.Max(1u, saturation / 20);
				if (cloudWaterMassUnits[i] > cloudThreshold)
				{
					var precipitated = checked((uint)Math.Max(1,
						(cloudWaterMassUnits[i] - cloudThreshold) * stepSeconds /
						(SecondsPerDay * 30L)));
					precipitated = Math.Min(precipitated, cloudWaterMassUnits[i]);
					cloudWaterMassUnits[i] -= precipitated;
					surfaceWaterMassUnits[i] = checked(surfaceWaterMassUnits[i] + precipitated);
					precipitationMassUnits[i] = checked(precipitationMassUnits[i] + precipitated);
				}

				if (surfaceWaterMassUnits[i] == 0 || vaporWaterMassUnits[i] >= saturation)
					continue;

				var windSpeed = ApproximateWindSpeed(i);
				var deficit = saturation - vaporWaterMassUnits[i];
				var evaporated = checked((uint)Math.Min(
					surfaceWaterMassUnits[i],
					deficit * stepSeconds * (1000 + windSpeed) / (SecondsPerDay * 4000L)));
				surfaceWaterMassUnits[i] -= evaporated;
				vaporWaterMassUnits[i] = checked(vaporWaterMassUnits[i] + evaporated);
				dailyEvaporatedMassUnits[i] = checked(dailyEvaporatedMassUnits[i] + evaporated);
			}
		}

		uint SaturationVaporMassUnits(int index)
		{
			var latitude = index / LongitudeCount;
			var capacityPartsPerMillion = Math.Clamp(
				(temperatureDeciKelvin[index] - 2200) * 400,
				10_000,
				1_000_000);
			capacityPartsPerMillion = checked((int)Math.Clamp(
				(long)capacityPartsPerMillion * MeanPressurePascals / Math.Max(100, pressurePascals[index]),
				10_000,
				1_000_000));
			return checked((uint)(
				capacityPartsPerMillion * WaterUnitsPerPartPerMillion * latitudeAreaWeights[latitude] /
				totalAreaWeight));
		}

		void AdvanceRunoff()
		{
			Array.Clear(transportDelta);
			for (var latitude = 0; latitude < LatitudeCount; latitude++)
				for (var longitude = 0; longitude < LongitudeCount; longitude++)
				{
					var index = latitude * LongitudeCount + longitude;
					if (elevationMeters[index] < 0)
						continue;

					var areaSquareKilometers = CellAreaSquareKilometers(latitude);
					var retention = checked((uint)Math.Max(1, areaSquareKilometers / 20));
					if (surfaceWaterMassUnits[index] <= retention)
						continue;

					var north = Math.Max(0, latitude - 1) * LongitudeCount + longitude;
					var south = Math.Min(LatitudeCount - 1, latitude + 1) * LongitudeCount + longitude;
					var west = latitude * LongitudeCount + (longitude + LongitudeCount - 1) % LongitudeCount;
					var east = latitude * LongitudeCount + (longitude + 1) % LongitudeCount;
					var destination = index;
					if (elevationMeters[north] < elevationMeters[destination])
						destination = north;
					if (elevationMeters[south] < elevationMeters[destination])
						destination = south;
					if (elevationMeters[west] < elevationMeters[destination])
						destination = west;
					if (elevationMeters[east] < elevationMeters[destination])
						destination = east;

					if (destination == index)
						continue;

					var runoff = (surfaceWaterMassUnits[index] - retention) / 4;
					transportDelta[index] -= runoff;
					transportDelta[destination] += runoff;
				}

			ApplyTransportDelta(surfaceWaterMassUnits);
		}

		void UpdateWaterDepthsAndPrecipitation()
		{
			for (var latitude = 0; latitude < LatitudeCount; latitude++)
			{
				var areaSquareKilometers = CellAreaSquareKilometers(latitude);
				for (var longitude = 0; longitude < LongitudeCount; longitude++)
				{
					var index = latitude * LongitudeCount + longitude;
					waterDepthMeters[index] = (ushort)Math.Clamp(
						(long)surfaceWaterMassUnits[index] * 2 / areaSquareKilometers,
						0,
						ushort.MaxValue);
					precipitationTenthsMillimetersPerDay[index] = (ushort)Math.Clamp(
						(long)precipitationMassUnits[index] * 20_000 / areaSquareKilometers,
						0,
						ushort.MaxValue);
				}
			}
		}

		int NorthCellSpacingMeters()
		{
			return Math.Max(1500, planet.Physics.RadiusKilometers * 17_453 / 1000);
		}

		int EastCellSpacingMeters(int latitude)
		{
			return Math.Max(1500, NorthCellSpacingMeters() * latitudeAreaWeights[latitude] / 1000);
		}

		long CellAreaSquareKilometers(int latitude)
		{
			var radius = (long)planet.Physics.RadiusKilometers;
			var surfaceAreaSquareKilometers = 12_566_372L * radius * radius / 1_000_000;
			return Math.Max(1, surfaceAreaSquareKilometers * latitudeAreaWeights[latitude] / totalAreaWeight);
		}

		int ApproximateWindSpeed(int index)
		{
			var east = Math.Abs(eastWindCentimetersPerSecond[index]);
			var north = Math.Abs(northWindCentimetersPerSecond[index]);
			return Math.Max(east, north) + Math.Min(east, north) / 2;
		}

		void RecalculateHydrologyAndAtmosphereSummary()
		{
			long windTotal = 0;
			var maximumWind = 0;
			long cloudCoverTotal = 0;
			long precipitationTotal = 0;
			var hydrology = 2166136261u;
			var atmosphere = 2166136261u;
			for (var i = 0; i < CellCount; i++)
			{
				var latitude = i / LongitudeCount;
				var areaWeight = latitudeAreaWeights[latitude];
				var wind = ApproximateWindSpeed(i);
				windTotal += wind * (long)areaWeight;
				maximumWind = Math.Max(maximumWind, wind);
				var atmosphericWater = (long)vaporWaterMassUnits[i] + cloudWaterMassUnits[i];
				cloudCoverTotal += (atmosphericWater == 0 ? 0 :
					cloudWaterMassUnits[i] * 1000L / atmosphericWater) * areaWeight;
				precipitationTotal += precipitationTenthsMillimetersPerDay[i] * (long)areaWeight;

				hydrology = Mix32(hydrology, unchecked((int)vaporWaterMassUnits[i]));
				hydrology = Mix32(hydrology, unchecked((int)cloudWaterMassUnits[i]));
				hydrology = Mix32(hydrology, unchecked((int)surfaceWaterMassUnits[i]));
				hydrology = Mix(hydrology, waterDepthMeters[i]);
				atmosphere = Mix(atmosphere, unchecked((ushort)eastWindCentimetersPerSecond[i]));
				atmosphere = Mix(atmosphere, unchecked((ushort)northWindCentimetersPerSecond[i]));
				atmosphere = Mix(atmosphere, precipitationTenthsMillimetersPerDay[i]);
			}

			hydrology = MixLong(hydrology, TotalWaterMassUnits);
			hydrology = MixLong(hydrology, WaterBalanceErrorUnits);
			atmosphere = Mix32(atmosphere, atmosphereSubsteps);
			atmosphere = Mix32(atmosphere, atmosphereStepSeconds);
			hydrologyHash = unchecked((int)hydrology);
			atmosphereHash = unchecked((int)atmosphere);
			var areaDivisor = Math.Max(1, totalAreaWeight);
			MeanWindCentimetersPerSecond = checked((int)(windTotal / areaDivisor));
			MaximumWindCentimetersPerSecond = maximumWind;
			var atmosphericWaterMass = Sum(vaporWaterMassUnits) + Sum(cloudWaterMassUnits);
			MeanAtmosphericWaterPartsPerMillion = checked((int)(
				atmosphericWaterMass / WaterUnitsPerPartPerMillion));
			MeanCloudCoverPerMille = checked((int)(cloudCoverTotal / areaDivisor));
			MeanPrecipitationTenthsMillimetersPerDay = checked((int)(
				precipitationTotal / areaDivisor));
			SpatialSurfaceWaterCubicKilometers = Sum(surfaceWaterMassUnits) / WaterUnitsPerCubicKilometer;
		}

		void RestoreAtmosphereAndHydrology(PlanetSurfaceSaveData data)
		{
			TotalWaterMassUnits = data.TotalWaterMassUnits;
			atmosphereSubsteps = data.AtmosphereSubsteps;
			atmosphereStepSeconds = data.AtmosphereStepSeconds;
			RestoreShorts(data.EastWindBrotliBase64, eastWindCentimetersPerSecond, "east wind");
			RestoreShorts(data.NorthWindBrotliBase64, northWindCentimetersPerSecond, "north wind");
			RestoreDeltaUInts(data.VaporWaterBrotliBase64, vaporWaterMassUnits, "vapor water");
			RestoreDeltaUInts(data.CloudWaterBrotliBase64, cloudWaterMassUnits, "cloud water");
			RestoreDeltaUInts(data.SurfaceWaterBrotliBase64, surfaceWaterMassUnits, "surface water");
			RestoreDeltaUShorts(
				data.PrecipitationBrotliBase64,
				precipitationTenthsMillimetersPerDay,
				"precipitation");
		}

		static string CompressShorts(short[] values)
		{
			var encoded = new ushort[values.Length];
			for (var i = 0; i < values.Length; i++)
				encoded[i] = unchecked((ushort)values[i]);

			return CompressDeltaUShorts(encoded);
		}

		static void RestoreShorts(string encoded, short[] destination, string fieldName)
		{
			var values = new ushort[destination.Length];
			RestoreDeltaUShorts(encoded, values, fieldName);
			for (var i = 0; i < destination.Length; i++)
				destination[i] = unchecked((short)values[i]);
		}

		static string CompressDeltaUInts(uint[] values)
		{
			var raw = new byte[values.Length * 4];
			var previous = 0u;
			for (var i = 0; i < values.Length; i++)
			{
				var delta = unchecked(values[i] - previous);
				previous = values[i];
				raw[i * 4] = (byte)delta;
				raw[i * 4 + 1] = (byte)(delta >> 8);
				raw[i * 4 + 2] = (byte)(delta >> 16);
				raw[i * 4 + 3] = (byte)(delta >> 24);
			}

			using var output = new MemoryStream();
			using (var compressor = new BrotliStream(output, CompressionLevel.Optimal, true))
				compressor.Write(raw, 0, raw.Length);

			return Convert.ToBase64String(output.ToArray());
		}

		static void RestoreDeltaUInts(string encoded, uint[] destination, string fieldName)
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
			if (raw.Length != destination.Length * 4)
				throw new InvalidOperationException(
					$"Saved planet {fieldName} contains {raw.Length} bytes; expected {destination.Length * 4}.");

			var previous = 0u;
			for (var i = 0; i < destination.Length; i++)
			{
				var delta = (uint)(raw[i * 4] | raw[i * 4 + 1] << 8 |
					raw[i * 4 + 2] << 16 | raw[i * 4 + 3] << 24);
				destination[i] = unchecked(previous + delta);
				previous = destination[i];
			}
		}

		static long Sum(uint[] values)
		{
			long total = 0;
			for (var i = 0; i < values.Length; i++)
				total += values[i];
			return total;
		}

		long SumWaterMass()
		{
			return Sum(vaporWaterMassUnits) + Sum(cloudWaterMassUnits) + Sum(surfaceWaterMassUnits);
		}

		static uint MixLong(uint hash, long value)
		{
			hash = Mix32(hash, unchecked((int)value));
			return Mix32(hash, unchecked((int)(value >> 32)));
		}
	}
}
