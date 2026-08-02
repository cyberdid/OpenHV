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

namespace OpenRA.Mods.HV.Traits
{
	public sealed partial class PlanetSurfaceState
	{
		const int VerticalLevelCount = 8;
		const int VerticalLayerHeightMeters = 1400;
		const int MaximumColumnWindCentimetersPerSecond = 18_000;
		const int MaximumLatentFluxMilliWattsPerSquareMeter = 1_500_000;
		const int SurfaceHeatCapacityJoulesPerSquareMeterKelvin = 24_000_000;
		const int LowerAtmosphereHeatCapacityJoulesPerSquareMeterKelvin = 12_000_000;

		static readonly int[] PressureLevelPerMille = [980, 760, 590, 450, 340, 250, 180, 110];
		static readonly int[] HumidityLevelPerMille = [1000, 710, 500, 350, 250, 180, 125, 90];
		static readonly int[] WindLevelPerMille = [1000, 900, 840, 800, 820, 900, 1050, 1200];
		static readonly int[] JetShapePerMille = [1000, 1150, 1270, 1340, 1340, 1270, 1150, 1000];

		readonly ushort[] columnTemperatureDeciKelvin =
			new ushort[VerticalLevelCount * LatitudeCount * LongitudeCount];
		readonly ushort[] nextColumnTemperatureDeciKelvin =
			new ushort[VerticalLevelCount * LatitudeCount * LongitudeCount];
		readonly ushort[] columnRelativeHumidityPerMille =
			new ushort[VerticalLevelCount * LatitudeCount * LongitudeCount];
		readonly ushort[] nextColumnRelativeHumidityPerMille =
			new ushort[VerticalLevelCount * LatitudeCount * LongitudeCount];
		readonly short[] columnEastWindCentimetersPerSecond =
			new short[VerticalLevelCount * LatitudeCount * LongitudeCount];
		readonly short[] nextColumnEastWindCentimetersPerSecond =
			new short[VerticalLevelCount * LatitudeCount * LongitudeCount];
		readonly short[] columnNorthWindCentimetersPerSecond =
			new short[VerticalLevelCount * LatitudeCount * LongitudeCount];
		readonly short[] nextColumnNorthWindCentimetersPerSecond =
			new short[VerticalLevelCount * LatitudeCount * LongitudeCount];
		readonly short[] verticalVelocityMillimetersPerSecond =
			new short[LatitudeCount * LongitudeCount];
		readonly int[] bulkRichardsonMillionths = new int[LatitudeCount * LongitudeCount];

		[VerifySync]
		int verticalAtmosphereHash;

		public int VerticalLevels => VerticalLevelCount;
		public int VerticalAtmosphereHash => verticalAtmosphereHash;
		public int MeanLowerAtmosphereTemperatureMilliKelvin { get; private set; }
		public int MeanUpperAtmosphereTemperatureMilliKelvin { get; private set; }
		public int MeanLowerRelativeHumidityPerMille { get; private set; }
		public int MeanUpperRelativeHumidityPerMille { get; private set; }
		public int MeanVerticalShearMicrosPerSecond { get; private set; }
		public int MeanJetSpeedCentimetersPerSecond { get; private set; }
		public int MaximumJetSpeedCentimetersPerSecond { get; private set; }
		public int MeanVerticalVelocityMillimetersPerSecond { get; private set; }
		public int MeanBulkRichardsonMillionths { get; private set; }
		public int HadleyTemperatureIndexMilliKelvin { get; private set; }
		public int MeanLatentFluxMilliWattsPerSquareMeter { get; private set; }
		public int LatentEnergyResidualMilliWattsPerSquareMeter { get; private set; }
		public long CumulativeLatentEnergyMegaJoulesPerSquareMeter { get; private set; }

		public int ColumnTemperatureMilliKelvinAt(int level, int latitudeIndex, int longitudeIndex)
		{
			return columnTemperatureDeciKelvin[ColumnIndex(level, latitudeIndex, longitudeIndex)] * 100;
		}

		public int ColumnRelativeHumidityPerMilleAt(int level, int latitudeIndex, int longitudeIndex)
		{
			return columnRelativeHumidityPerMille[ColumnIndex(level, latitudeIndex, longitudeIndex)];
		}

		public int ColumnPressurePascalsAt(int level, int latitudeIndex, int longitudeIndex)
		{
			ValidateVerticalLevel(level);
			return pressurePascals[CellIndex(latitudeIndex, longitudeIndex)] * PressureLevelPerMille[level] / 1000;
		}

		public int ColumnEastWindCentimetersPerSecondAt(int level, int latitudeIndex, int longitudeIndex)
		{
			return columnEastWindCentimetersPerSecond[ColumnIndex(level, latitudeIndex, longitudeIndex)];
		}

		public int ColumnNorthWindCentimetersPerSecondAt(int level, int latitudeIndex, int longitudeIndex)
		{
			return columnNorthWindCentimetersPerSecond[ColumnIndex(level, latitudeIndex, longitudeIndex)];
		}

		public int VerticalVelocityMillimetersPerSecondAt(int latitudeIndex, int longitudeIndex)
		{
			return verticalVelocityMillimetersPerSecond[CellIndex(latitudeIndex, longitudeIndex)];
		}

		void InitializeVerticalAtmosphere()
		{
			for (var level = 0; level < VerticalLevelCount; level++)
				for (var latitude = 0; latitude < LatitudeCount; latitude++)
					for (var longitude = 0; longitude < LongitudeCount; longitude++)
					{
						var cell = latitude * LongitudeCount + longitude;
						var index = level * CellCount + cell;
						var surfaceHumidity = SurfaceRelativeHumidityPerMille(cell);
						columnTemperatureDeciKelvin[index] = (ushort)Math.Clamp(
							temperatureDeciKelvin[cell] - 60 - level * 54, 1400, 9000);
						columnRelativeHumidityPerMille[index] = (ushort)Math.Clamp(
							surfaceHumidity * HumidityLevelPerMille[level] / 1000, 0, 2000);
						columnEastWindCentimetersPerSecond[index] = (short)Math.Clamp(
							eastWindCentimetersPerSecond[cell] * WindLevelPerMille[level] / 1000,
							-MaximumColumnWindCentimetersPerSecond,
							MaximumColumnWindCentimetersPerSecond);
						columnNorthWindCentimetersPerSecond[index] = (short)Math.Clamp(
							northWindCentimetersPerSecond[cell] * WindLevelPerMille[level] / 1000,
							-MaximumColumnWindCentimetersPerSecond,
							MaximumColumnWindCentimetersPerSecond);
					}

			DiagnoseVerticalCirculation();
			RecalculateVerticalAtmosphereSummary();
		}

		void AdvanceVerticalAtmosphereAndLatentEnergy(PlanetPhysicalState physics, int macroDay)
		{
			ApplyLatentHeatPump();
			DiagnoseVerticalCirculation();
			AdvanceColumnProfiles();
			DiagnoseVerticalCirculation();
			RecalculateClimateSummaryAndHash();
			physics.ApplySpatialClimate(
				MeanTemperatureMilliKelvin,
				MeanLatentFluxMilliWattsPerSquareMeter,
				macroDay);
			RecalculateVerticalAtmosphereSummary();
		}

		void ApplyLatentHeatPump()
		{
			long fluxTotal = 0;
			long residualTotal = 0;
			for (var latitude = 0; latitude < LatitudeCount; latitude++)
			{
				var area = CellAreaSquareKilometers(latitude);
				var areaWeight = latitudeAreaWeights[latitude];
				for (var longitude = 0; longitude < LongitudeCount; longitude++)
				{
					var cell = latitude * LongitudeCount + longitude;
					var condensationFlux = LatentFlux(dailyCondensedMassUnits[cell], area);
					var evaporationFlux = LatentFlux(dailyEvaporatedMassUnits[cell], area);
					var netFlux = condensationFlux - evaporationFlux;
					var diagnosedFlux = LatentFlux(dailyCondensedMassUnits[cell], area) -
						LatentFlux(dailyEvaporatedMassUnits[cell], area);
					fluxTotal += netFlux * (long)areaWeight;
					residualTotal += (netFlux - diagnosedFlux) * (long)areaWeight;

					var surfaceCoolingDeciKelvin = checked((int)Math.Min(
						800L,
						evaporationFlux * 864L / SurfaceHeatCapacityJoulesPerSquareMeterKelvin));
					temperatureDeciKelvin[cell] = (ushort)Math.Clamp(
						temperatureDeciKelvin[cell] - surfaceCoolingDeciKelvin, 1000, 9000);

					var lowerIndex = cell;
					var lowerHeatingDeciKelvin = checked((int)Math.Min(
						800L,
						condensationFlux * 864L / LowerAtmosphereHeatCapacityJoulesPerSquareMeterKelvin));
					columnTemperatureDeciKelvin[lowerIndex] = (ushort)Math.Clamp(
						columnTemperatureDeciKelvin[lowerIndex] + lowerHeatingDeciKelvin, 1400, 9000);
				}
			}

			var divisor = Math.Max(1, totalAreaWeight);
			MeanLatentFluxMilliWattsPerSquareMeter = checked((int)(fluxTotal / divisor));
			LatentEnergyResidualMilliWattsPerSquareMeter = checked((int)(residualTotal / divisor));
			if (LatentEnergyResidualMilliWattsPerSquareMeter != 0)
				throw new InvalidOperationException(
					$"Planet latent-energy ledger residual is {LatentEnergyResidualMilliWattsPerSquareMeter} mW/m².");

			CumulativeLatentEnergyMegaJoulesPerSquareMeter = checked(
				CumulativeLatentEnergyMegaJoulesPerSquareMeter +
				MeanLatentFluxMilliWattsPerSquareMeter * 86_400L / 1_000_000_000L);
		}

		static int LatentFlux(uint massUnits, long areaSquareKilometers)
		{
			// One fixed water unit is 0.002 km³. At 1000 kg/m³ its
			// 2.5 MJ/kg phase energy becomes 57,870,370 mW/m² over one day.
			return checked((int)Math.Min(
				MaximumLatentFluxMilliWattsPerSquareMeter,
				massUnits * 57_870_370L / Math.Max(1, areaSquareKilometers)));
		}

		void DiagnoseVerticalCirculation()
		{
			for (var latitude = 0; latitude < LatitudeCount; latitude++)
				for (var longitude = 0; longitude < LongitudeCount; longitude++)
				{
					var cell = latitude * LongitudeCount + longitude;
					var north = Math.Max(0, latitude - 1) * LongitudeCount + longitude;
					var south = Math.Min(LatitudeCount - 1, latitude + 1) * LongitudeCount + longitude;
					var west = latitude * LongitudeCount + (longitude + LongitudeCount - 1) % LongitudeCount;
					var east = latitude * LongitudeCount + (longitude + 1) % LongitudeCount;
					var convergence = eastWindCentimetersPerSecond[west] - eastWindCentimetersPerSecond[east] +
						northWindCentimetersPerSecond[south] - northWindCentimetersPerSecond[north];
					var lowerTemperature = columnTemperatureDeciKelvin[cell];
					var instability = temperatureDeciKelvin[cell] - lowerTemperature - 60;
					verticalVelocityMillimetersPerSecond[cell] = (short)Math.Clamp(
						convergence / 30 + instability * 2, -700, 900);

					var levelOne = CellCount + cell;
					var windDifference = ApproximateVectorMagnitude(
						columnEastWindCentimetersPerSecond[levelOne] - columnEastWindCentimetersPerSecond[cell],
						columnNorthWindCentimetersPerSecond[levelOne] - columnNorthWindCentimetersPerSecond[cell]);
					var lapse = columnTemperatureDeciKelvin[cell] - columnTemperatureDeciKelvin[levelOne];
					bulkRichardsonMillionths[cell] = Math.Clamp(
						(lapse - 20) * 5_000_000 / Math.Max(100, windDifference),
						-2_000_000,
						12_000_000);
				}
		}

		void AdvanceColumnProfiles()
		{
			for (var level = 0; level < VerticalLevelCount; level++)
				for (var latitude = 0; latitude < LatitudeCount; latitude++)
					for (var longitude = 0; longitude < LongitudeCount; longitude++)
					{
						var cell = latitude * LongitudeCount + longitude;
						var index = level * CellCount + cell;
						var below = Math.Max(0, level - 1) * CellCount + cell;
						var above = Math.Min(VerticalLevelCount - 1, level + 1) * CellCount + cell;
						var mix = Math.Clamp(1000 - Math.Max(0, bulkRichardsonMillionths[cell]) / 1500, 100, 1000);
						var targetTemperature = Math.Clamp(
							temperatureDeciKelvin[cell] - 60 - level * 54, 1400, 9000);
						var mixedTemperature = columnTemperatureDeciKelvin[index] +
							(targetTemperature - columnTemperatureDeciKelvin[index]) / 7 +
							((columnTemperatureDeciKelvin[below] + columnTemperatureDeciKelvin[above]) / 2 -
								columnTemperatureDeciKelvin[index]) * mix / 12_000;
						nextColumnTemperatureDeciKelvin[index] = (ushort)Math.Clamp(
							mixedTemperature, 1400, 9000);

						var targetHumidity = SurfaceRelativeHumidityPerMille(cell) *
							HumidityLevelPerMille[level] / 1000;
						var mixedHumidity = columnRelativeHumidityPerMille[index] +
							(targetHumidity - columnRelativeHumidityPerMille[index]) / 8 +
							((columnRelativeHumidityPerMille[below] + columnRelativeHumidityPerMille[above]) / 2 -
								columnRelativeHumidityPerMille[index]) * mix / 15_000;
						nextColumnRelativeHumidityPerMille[index] = (ushort)Math.Clamp(
							mixedHumidity, 0, 2000);

						var north = Math.Max(0, latitude - 1) * LongitudeCount + longitude;
						var south = Math.Min(LatitudeCount - 1, latitude + 1) * LongitudeCount + longitude;
						var thermalWind = -(temperatureDeciKelvin[south] - temperatureDeciKelvin[north]) * 140;
						var targetEast = eastWindCentimetersPerSecond[cell] * WindLevelPerMille[level] / 1000 +
							thermalWind * level * JetShapePerMille[level] /
							(Math.Max(1, VerticalLevelCount - 1) * 1000);
						var targetNorth = northWindCentimetersPerSecond[cell] * WindLevelPerMille[level] / 1000;
						var mixedEast = columnEastWindCentimetersPerSecond[index] +
							(targetEast - columnEastWindCentimetersPerSecond[index]) / 4 +
							((columnEastWindCentimetersPerSecond[below] + columnEastWindCentimetersPerSecond[above]) / 2 -
								columnEastWindCentimetersPerSecond[index]) * mix / 12_000;
						var mixedNorth = columnNorthWindCentimetersPerSecond[index] +
							(targetNorth - columnNorthWindCentimetersPerSecond[index]) / 4 +
							((columnNorthWindCentimetersPerSecond[below] + columnNorthWindCentimetersPerSecond[above]) / 2 -
								columnNorthWindCentimetersPerSecond[index]) * mix / 12_000;
						nextColumnEastWindCentimetersPerSecond[index] = (short)Math.Clamp(
							mixedEast, -MaximumColumnWindCentimetersPerSecond, MaximumColumnWindCentimetersPerSecond);
						nextColumnNorthWindCentimetersPerSecond[index] = (short)Math.Clamp(
							mixedNorth, -MaximumColumnWindCentimetersPerSecond, MaximumColumnWindCentimetersPerSecond);
					}

			Array.Copy(nextColumnTemperatureDeciKelvin, columnTemperatureDeciKelvin, columnTemperatureDeciKelvin.Length);
			Array.Copy(nextColumnRelativeHumidityPerMille, columnRelativeHumidityPerMille,
				columnRelativeHumidityPerMille.Length);
			Array.Copy(nextColumnEastWindCentimetersPerSecond, columnEastWindCentimetersPerSecond,
				columnEastWindCentimetersPerSecond.Length);
			Array.Copy(nextColumnNorthWindCentimetersPerSecond, columnNorthWindCentimetersPerSecond,
				columnNorthWindCentimetersPerSecond.Length);

			for (var cell = 0; cell < CellCount; cell++)
			{
				columnEastWindCentimetersPerSecond[cell] = eastWindCentimetersPerSecond[cell];
				columnNorthWindCentimetersPerSecond[cell] = northWindCentimetersPerSecond[cell];
				for (var level = 1; level < VerticalLevelCount; level++)
				{
					var index = level * CellCount + cell;
					var below = index - CellCount;
					columnTemperatureDeciKelvin[index] = (ushort)Math.Min(
						columnTemperatureDeciKelvin[index],
						Math.Max(1400, columnTemperatureDeciKelvin[below] - 2));
				}
			}
		}

		int SurfaceRelativeHumidityPerMille(int cell)
		{
			var saturation = Math.Max(1u, SaturationVaporMassUnits(cell));
			return checked((int)Math.Clamp(vaporWaterMassUnits[cell] * 1000L / saturation, 0, 2000));
		}

		void RecalculateVerticalAtmosphereSummary()
		{
			long lowerTemperatureTotal = 0;
			long upperTemperatureTotal = 0;
			long lowerHumidityTotal = 0;
			long upperHumidityTotal = 0;
			long shearTotal = 0;
			long jetTotal = 0;
			var maximumJet = 0;
			long verticalVelocityTotal = 0;
			long richardsonTotal = 0;
			long equatorialTemperatureTotal = 0;
			long equatorialWeight = 0;
			long midLatitudeTemperatureTotal = 0;
			long midLatitudeWeight = 0;
			var hash = 2166136261u;
			for (var cell = 0; cell < CellCount; cell++)
			{
				var latitude = cell / LongitudeCount;
				var weight = latitudeAreaWeights[latitude];
				var lower = cell;
				var levelOne = CellCount + cell;
				var upper = (VerticalLevelCount - 1) * CellCount + cell;
				var shear = ApproximateVectorMagnitude(
					columnEastWindCentimetersPerSecond[levelOne] - columnEastWindCentimetersPerSecond[lower],
					columnNorthWindCentimetersPerSecond[levelOne] - columnNorthWindCentimetersPerSecond[lower]) *
					10_000 / VerticalLayerHeightMeters;
				var jet = ApproximateVectorMagnitude(
					columnEastWindCentimetersPerSecond[upper],
					columnNorthWindCentimetersPerSecond[upper]);
				lowerTemperatureTotal += columnTemperatureDeciKelvin[lower] * (long)weight;
				upperTemperatureTotal += columnTemperatureDeciKelvin[upper] * (long)weight;
				lowerHumidityTotal += columnRelativeHumidityPerMille[lower] * (long)weight;
				upperHumidityTotal += columnRelativeHumidityPerMille[upper] * (long)weight;
				shearTotal += shear * (long)weight;
				jetTotal += jet * (long)weight;
				maximumJet = Math.Max(maximumJet, jet);
				verticalVelocityTotal += Math.Abs(verticalVelocityMillimetersPerSecond[cell]) * (long)weight;
				richardsonTotal += bulkRichardsonMillionths[cell] * (long)weight;

				var absoluteLatitude = Math.Abs(latitude * 2 - (LatitudeCount - 1)) * 90 /
					(LatitudeCount - 1);
				if (absoluteLatitude <= 20)
				{
					equatorialTemperatureTotal += temperatureDeciKelvin[cell] * (long)weight;
					equatorialWeight += weight;
				}
				else if (absoluteLatitude >= 35 && absoluteLatitude <= 60)
				{
					midLatitudeTemperatureTotal += temperatureDeciKelvin[cell] * (long)weight;
					midLatitudeWeight += weight;
				}
			}

			for (var i = 0; i < columnTemperatureDeciKelvin.Length; i++)
			{
				hash = Mix(hash, columnTemperatureDeciKelvin[i]);
				hash = Mix(hash, columnRelativeHumidityPerMille[i]);
				hash = Mix(hash, unchecked((ushort)columnEastWindCentimetersPerSecond[i]));
				hash = Mix(hash, unchecked((ushort)columnNorthWindCentimetersPerSecond[i]));
			}

			for (var i = 0; i < CellCount; i++)
			{
				hash = Mix(hash, unchecked((ushort)verticalVelocityMillimetersPerSecond[i]));
				hash = Mix32(hash, bulkRichardsonMillionths[i]);
			}

			hash = Mix32(hash, MeanLatentFluxMilliWattsPerSquareMeter);
			hash = Mix32(hash, LatentEnergyResidualMilliWattsPerSquareMeter);
			hash = MixLong(hash, CumulativeLatentEnergyMegaJoulesPerSquareMeter);
			verticalAtmosphereHash = unchecked((int)hash);

			var divisor = Math.Max(1, totalAreaWeight);
			MeanLowerAtmosphereTemperatureMilliKelvin = checked((int)(lowerTemperatureTotal * 100 / divisor));
			MeanUpperAtmosphereTemperatureMilliKelvin = checked((int)(upperTemperatureTotal * 100 / divisor));
			MeanLowerRelativeHumidityPerMille = checked((int)(lowerHumidityTotal / divisor));
			MeanUpperRelativeHumidityPerMille = checked((int)(upperHumidityTotal / divisor));
			MeanVerticalShearMicrosPerSecond = checked((int)(shearTotal / divisor));
			MeanJetSpeedCentimetersPerSecond = checked((int)(jetTotal / divisor));
			MaximumJetSpeedCentimetersPerSecond = maximumJet;
			MeanVerticalVelocityMillimetersPerSecond = checked((int)(verticalVelocityTotal / divisor));
			MeanBulkRichardsonMillionths = checked((int)(richardsonTotal / divisor));
			HadleyTemperatureIndexMilliKelvin = checked((int)(
				(equatorialTemperatureTotal / Math.Max(1, equatorialWeight) -
				midLatitudeTemperatureTotal / Math.Max(1, midLatitudeWeight)) * 100));
		}

		void RestoreVerticalAtmosphere(PlanetSurfaceSaveData data)
		{
			MeanLatentFluxMilliWattsPerSquareMeter = data.MeanLatentFluxMilliWattsPerSquareMeter;
			LatentEnergyResidualMilliWattsPerSquareMeter = data.LatentEnergyResidualMilliWattsPerSquareMeter;
			CumulativeLatentEnergyMegaJoulesPerSquareMeter = data.CumulativeLatentEnergyMegaJoulesPerSquareMeter;
			RestoreDeltaUShorts(
				data.ColumnTemperatureBrotliBase64,
				columnTemperatureDeciKelvin,
				"vertical temperature");
			RestoreDeltaUShorts(
				data.ColumnRelativeHumidityBrotliBase64,
				columnRelativeHumidityPerMille,
				"vertical relative humidity");
			RestoreShorts(
				data.ColumnEastWindBrotliBase64,
				columnEastWindCentimetersPerSecond,
				"vertical east wind");
			RestoreShorts(
				data.ColumnNorthWindBrotliBase64,
				columnNorthWindCentimetersPerSecond,
				"vertical north wind");
			RestoreShorts(
				data.VerticalVelocityBrotliBase64,
				verticalVelocityMillimetersPerSecond,
				"vertical velocity");
			DiagnoseVerticalCirculation();
		}

		static int ApproximateVectorMagnitude(int x, int y)
		{
			var absoluteX = Math.Abs(x);
			var absoluteY = Math.Abs(y);
			return Math.Max(absoluteX, absoluteY) + Math.Min(absoluteX, absoluteY) / 2;
		}

		static int ColumnIndex(int level, int latitudeIndex, int longitudeIndex)
		{
			ValidateVerticalLevel(level);
			return level * LatitudeCount * LongitudeCount + CellIndex(latitudeIndex, longitudeIndex);
		}

		static void ValidateVerticalLevel(int level)
		{
			if (level < 0 || level >= VerticalLevelCount)
				throw new ArgumentOutOfRangeException(nameof(level));
		}
	}
}
