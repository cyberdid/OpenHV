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
	/// <summary>
	/// Native zero-to-life layer. Climate determines instantaneous carrying
	/// capacity; sustained suitable chemistry accumulates abiogenesis progress;
	/// only an actually originated biosphere may grow, migrate, and become complex.
	/// All state is integer and deterministic.
	/// </summary>
	public sealed partial class PlanetSurfaceState
	{
		const int AbiogenesisThresholdUnits = 3_000;
		const int HabitableThresholdPerMille = 250;
		const int AbiogenesisThresholdPerMille = 450;

		ushort[] habitabilityPerMille;
		ushort[] biomassPerMille;
		ushort[] nextBiomassPerMille;
		ushort[] biomassChurnPerMille;
		uint[] complexityMillionths;
		uint[] abiogenesisProgressUnits;

		[VerifySync]
		int biospherePulseSequence;

		[VerifySync]
		int biosphereHash;

		[VerifySync]
		bool lifeOriginated;

		[VerifySync]
		int originLatitudeIndex = -1;

		[VerifySync]
		int originLongitudeIndex = -1;

		public int BiospherePulseSequence => biospherePulseSequence;
		public int BiosphereHash => biosphereHash;
		public bool LifeOriginated => lifeOriginated;
		public int OriginLatitudeIndex => originLatitudeIndex;
		public int OriginLongitudeIndex => originLongitudeIndex;
		public int MeanHabitabilityPerMille { get; private set; }
		public int HabitableCellCount { get; private set; }
		public int LivingCellCount { get; private set; }
		public int MeanBiomassPerMille { get; private set; }
		public int MeanComplexityMillionths { get; private set; }
		public int MaximumComplexityMillionths { get; private set; }
		public int MaximumAbiogenesisProgressUnits { get; private set; }

		public int HabitabilityPerMilleAt(int latitudeIndex, int longitudeIndex)
		{
			return habitabilityPerMille[CellIndex(latitudeIndex, longitudeIndex)];
		}

		public int BiomassPerMilleAt(int latitudeIndex, int longitudeIndex)
		{
			return biomassPerMille[CellIndex(latitudeIndex, longitudeIndex)];
		}

		public int ComplexityMillionthsAt(int latitudeIndex, int longitudeIndex)
		{
			return checked((int)complexityMillionths[CellIndex(latitudeIndex, longitudeIndex)]);
		}

		public int AbiogenesisProgressUnitsAt(int latitudeIndex, int longitudeIndex)
		{
			return checked((int)abiogenesisProgressUnits[CellIndex(latitudeIndex, longitudeIndex)]);
		}

		void InitializeBiosphere()
		{
			habitabilityPerMille = new ushort[CellCount];
			biomassPerMille = new ushort[CellCount];
			nextBiomassPerMille = new ushort[CellCount];
			biomassChurnPerMille = new ushort[CellCount];
			complexityMillionths = new uint[CellCount];
			abiogenesisProgressUnits = new uint[CellCount];
			RecalculateBiosphereSummaryAndHash();
		}

		internal bool AdvanceBiosphere()
		{
			biospherePulseSequence++;
			var originCandidate = -1;
			uint highestProgress = 0;

			for (var i = 0; i < CellCount; i++)
			{
				var habitability = CalculateHabitabilityPerMille(i);
				habitabilityPerMille[i] = (ushort)habitability;
				if (lifeOriginated)
					continue;
				if (habitability < AbiogenesisThresholdPerMille)
				{
					// Chemistry remembers near misses, but an intermittent oasis cannot
					// accumulate forever: hostile pulses erode the precursor stock.
					abiogenesisProgressUnits[i] = abiogenesisProgressUnits[i] * 49 / 50;
					continue;
				}

				// Rare chemistry is deterministic and spatial, not a random roll per
				// frame. Volcanic/mineral-rich wet cells accumulate fastest.
				var latitude = i / LongitudeCount;
				var longitude = i % LongitudeCount;
				var chemistry = 500 + Positive(CoordinateHash(
					unchecked(planet.Index * 0x45D9F3B + 0x2C1B3C6D), latitude, longitude)) % 501;
				var increment = (habitability - AbiogenesisThresholdPerMille + 1) * chemistry / 1000;
				if ((PlanetTerrainKind)terrain[i] == PlanetTerrainKind.Volcanic)
					increment += 25;
				increment += materialRichness[i] / 16;
				abiogenesisProgressUnits[i] = checked((uint)Math.Min(
					AbiogenesisThresholdUnits * 2L,
					abiogenesisProgressUnits[i] + Math.Max(1, increment)));
				if (abiogenesisProgressUnits[i] > highestProgress)
				{
					highestProgress = abiogenesisProgressUnits[i];
					originCandidate = i;
				}
			}

			var originatedThisPulse = false;
			if (!lifeOriginated && originCandidate >= 0 && highestProgress >= AbiogenesisThresholdUnits)
			{
				lifeOriginated = true;
				originatedThisPulse = true;
				originLatitudeIndex = originCandidate / LongitudeCount;
				originLongitudeIndex = originCandidate % LongitudeCount;
				biomassPerMille[originCandidate] = (ushort)Math.Max(
					10, habitabilityPerMille[originCandidate] / 40);
				complexityMillionths[originCandidate] = 1;
			}

			if (lifeOriginated)
				AdvanceLivingBiosphere();

			RecalculateBiosphereSummaryAndHash();
			return originatedThisPulse;
		}

		void AdvanceLivingBiosphere()
		{
			for (var latitude = 0; latitude < LatitudeCount; latitude++)
				for (var longitude = 0; longitude < LongitudeCount; longitude++)
				{
					var i = latitude * LongitudeCount + longitude;
					var biomass = biomassPerMille[i];
					var capacity = habitabilityPerMille[i];
					var north = Math.Max(0, latitude - 1) * LongitudeCount + longitude;
					var south = Math.Min(LatitudeCount - 1, latitude + 1) * LongitudeCount + longitude;
					var west = latitude * LongitudeCount + (longitude + LongitudeCount - 1) % LongitudeCount;
					var east = latitude * LongitudeCount + (longitude + 1) % LongitudeCount;
					var neighborMean = (biomassPerMille[north] + biomassPerMille[south] +
						biomassPerMille[west] + biomassPerMille[east]) / 4;

					int next;
					if (biomass < capacity)
					{
						var growth = (long)Math.Max(1, (int)biomass) * (capacity - biomass) * 40 / 1_000_000;
						var migration = capacity >= HabitableThresholdPerMille ? neighborMean / 40 : 0;
						next = checked((int)Math.Min(capacity, biomass + Math.Max(0, growth) + migration));
					}
					else
					{
						var starvation = Math.Max(1, (biomass - capacity + 24) / 25);
						next = Math.Max(0, biomass - starvation);
					}

					nextBiomassPerMille[i] = (ushort)Math.Clamp(next, 0, 1000);
				}

			for (var i = 0; i < CellCount; i++)
			{
				var change = Math.Abs(nextBiomassPerMille[i] - biomassPerMille[i]);
				var churn = biomassChurnPerMille[i] * 98 / 100 + change * 4 / 5;
				biomassChurnPerMille[i] = (ushort)Math.Clamp(churn, 0, 1000);
				biomassPerMille[i] = nextBiomassPerMille[i];

				var stability = 1000 - biomassChurnPerMille[i];
				var remaining = 1_000_000L - complexityMillionths[i];
				var complexityGain = 600L * biomassPerMille[i] * stability * remaining /
					1_000_000_000_000L;
				if (complexityGain > 0)
					complexityMillionths[i] = checked((uint)Math.Min(
						1_000_000L, complexityMillionths[i] + complexityGain));
			}
		}

		int CalculateHabitabilityPerMille(int index)
		{
			var deviationKelvin = Math.Abs(temperatureDeciKelvin[index] * 100 - 288_000) / 1000;
			var temperature = Math.Max(0, 1000 - deviationKelvin * deviationKelvin * 1000 / (75 * 75));
			var precipitation = precipitationTenthsMillimetersPerDay[index];
			var moisture = precipitation == 0 ? 0 : precipitation * 1000 / (precipitation + 40);
			var pressure = pressurePascals[index];
			var pressureScore = pressure <= 0 ? 0 : pressure < 50_000 ? pressure * 1000 / 50_000 :
				pressure <= 500_000 ? 1000 : Math.Max(0, 1000 - (pressure - 500_000) / 2000);
			var nutrients = 400 + materialRichness[index] * 600 / 255;
			return checked((int)((long)temperature * moisture * pressureScore * nutrients / 1_000_000_000));
		}

		void RestoreBiosphere(PlanetSurfaceSaveData data)
		{
			biospherePulseSequence = data.BiospherePulseSequence;
			lifeOriginated = data.LifeOriginated;
			originLatitudeIndex = data.OriginLatitudeIndex;
			originLongitudeIndex = data.OriginLongitudeIndex;
			if (lifeOriginated &&
				(originLatitudeIndex < 0 || originLatitudeIndex >= LatitudeCount ||
					originLongitudeIndex < 0 || originLongitudeIndex >= LongitudeCount))
				throw new InvalidOperationException("Saved biosphere has life but no valid origin cell.");
			if (!lifeOriginated && (originLatitudeIndex != -1 || originLongitudeIndex != -1))
				throw new InvalidOperationException("Saved lifeless biosphere unexpectedly names an origin cell.");
			if (biospherePulseSequence > 0)
			{
				RestoreDeltaUShorts(data.BiomassBrotliBase64, biomassPerMille, "biomass");
				RestoreDeltaUShorts(data.BiomassChurnBrotliBase64, biomassChurnPerMille, "biomass churn");
				RestoreDeltaUInts(data.ComplexityBrotliBase64, complexityMillionths, "complexity");
				RestoreDeltaUInts(
					data.AbiogenesisProgressBrotliBase64,
					abiogenesisProgressUnits,
					"abiogenesis progress");
			}

			RecalculateBiosphereSummaryAndHash();
			if (biosphereHash != data.BiosphereHash)
				throw new InvalidOperationException(
					$"Planet biosphere digest {unchecked((uint)biosphereHash):X8} does not match saved " +
					$"{unchecked((uint)data.BiosphereHash):X8}.");
		}

		void RecalculateBiosphereSummaryAndHash()
		{
			long habitabilityTotal = 0;
			long biomassTotal = 0;
			long complexityTotal = 0;
			var maximumComplexity = 0;
			var maximumProgress = 0;
			var habitableCells = 0;
			var livingCells = 0;
			var hash = 2166136261u;
			for (var i = 0; i < CellCount; i++)
			{
				// Habitability is derived from the restored physical grid and is never
				// serialized as a second authority. Recompute it here so construction,
				// advancement, and save restoration all close to the same digest.
				habitabilityPerMille[i] = (ushort)CalculateHabitabilityPerMille(i);
				habitabilityTotal += habitabilityPerMille[i];
				biomassTotal += biomassPerMille[i];
				complexityTotal += complexityMillionths[i];
				habitableCells += habitabilityPerMille[i] >= HabitableThresholdPerMille ? 1 : 0;
				livingCells += biomassPerMille[i] > 0 ? 1 : 0;
				maximumComplexity = Math.Max(maximumComplexity, checked((int)complexityMillionths[i]));
				maximumProgress = Math.Max(maximumProgress, checked((int)abiogenesisProgressUnits[i]));
				hash = Mix(hash, habitabilityPerMille[i]);
				hash = Mix(hash, biomassPerMille[i]);
				hash = Mix(hash, biomassChurnPerMille[i]);
				hash = Mix32(hash, checked((int)complexityMillionths[i]));
				hash = Mix32(hash, checked((int)abiogenesisProgressUnits[i]));
			}

			hash = Mix32(hash, biospherePulseSequence);
			hash = Mix(hash, lifeOriginated ? 1 : 0);
			hash = Mix32(hash, originLatitudeIndex);
			hash = Mix32(hash, originLongitudeIndex);
			MeanHabitabilityPerMille = checked((int)(habitabilityTotal / CellCount));
			HabitableCellCount = habitableCells;
			LivingCellCount = livingCells;
			MeanBiomassPerMille = checked((int)(biomassTotal / CellCount));
			MeanComplexityMillionths = checked((int)(complexityTotal / CellCount));
			MaximumComplexityMillionths = maximumComplexity;
			MaximumAbiogenesisProgressUnits = maximumProgress;
			biosphereHash = unchecked((int)hash);
		}
	}
}
