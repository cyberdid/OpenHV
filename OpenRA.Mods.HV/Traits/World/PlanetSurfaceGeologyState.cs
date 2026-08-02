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
		const int MinimumGeologyElevationMeters = -16_000;
		const int MaximumGeologyElevationMeters = 16_000;
		const int MaterialUnitsPerRichness = 100;

		readonly uint[] surfaceMaterialUnits = new uint[LatitudeCount * LongitudeCount];
		readonly short[] lastGeologyChangeMeters = new short[LatitudeCount * LongitudeCount];
		readonly int[] geologyElevationDelta = new int[LatitudeCount * LongitudeCount];
		readonly long[] geologyMaterialDelta = new long[LatitudeCount * LongitudeCount];

		[VerifySync]
		int geologyPulseSequence;

		[VerifySync]
		int geologyHash;

		public int GeologyPulseSequence => geologyPulseSequence;
		public int GeologyHash => geologyHash;
		public int MeanElevationMeters { get; private set; }
		public int MeanAbsoluteGeologyChangeMilliMeters { get; private set; }
		public int ActiveVolcanicCellCount { get; private set; }
		public long TotalSurfaceMaterialUnits { get; private set; }
		public long CumulativeErodedMaterialUnits { get; private set; }
		public long CumulativeMantleMaterialInputUnits { get; private set; }
		public long CumulativeTectonicElevationChangeMeters { get; private set; }
		public long ElevationBalanceErrorMeters { get; private set; }
		public long MaterialBalanceErrorUnits { get; private set; }

		public long SurfaceMaterialUnitsAt(int latitudeIndex, int longitudeIndex)
		{
			return surfaceMaterialUnits[CellIndex(latitudeIndex, longitudeIndex)];
		}

		public int LastGeologyChangeMetersAt(int latitudeIndex, int longitudeIndex)
		{
			return lastGeologyChangeMeters[CellIndex(latitudeIndex, longitudeIndex)];
		}

		void InitializeGeology()
		{
			for (var i = 0; i < CellCount; i++)
				surfaceMaterialUnits[i] = checked((uint)(materialRichness[i] * MaterialUnitsPerRichness));

			RecalculateDerivedState();
		}

		void AdvanceGeology(PlanetPhysicalState physics)
		{
			geologyPulseSequence++;
			Array.Clear(geologyElevationDelta);
			Array.Clear(geologyMaterialDelta);
			Array.Clear(lastGeologyChangeMeters);
			var elevationBefore = SumElevation();
			var materialBefore = Sum(surfaceMaterialUnits);
			long expectedCrustChange = 0;
			long mantleMaterialInput = 0;
			long erodedMaterial = 0;
			var activeVolcanicCells = 0;
			var activity = Math.Clamp(physics.TectonicActivityPerMille, 0, 1000);

			for (var latitude = 0; latitude < LatitudeCount; latitude++)
				for (var longitude = 0; longitude < LongitudeCount; longitude++)
				{
					var cell = latitude * LongitudeCount + longitude;
					if (crust[cell] != (byte)PlanetCrustKind.PlateBoundary)
						continue;

					var stress = Positive(CoordinateHash(
						GeologySeed ^ geologyPulseSequence * 0x1F123BB5,
						latitude,
						longitude));
					if (stress % 8000 < activity)
					{
						var tectonicChange = (stress / 8000 % 4) switch
						{
							0 => 2,
							1 => 1,
							2 => -1,
							_ => 0
						};
						geologyElevationDelta[cell] += tectonicChange;
						expectedCrustChange += tectonicChange;
					}

					if (!IsVolcanicallyActive(latitude, longitude, activity))
						continue;

					geologyElevationDelta[cell] += 2;
					expectedCrustChange += 2;
					geologyMaterialDelta[cell] += 50;
					mantleMaterialInput += 50;
					activeVolcanicCells++;
				}

			for (var latitude = 0; latitude < LatitudeCount; latitude++)
				for (var longitude = 0; longitude < LongitudeCount; longitude++)
				{
					var source = latitude * LongitudeCount + longitude;
					var destination = LowestNeighbor(latitude, longitude);
					if (destination == source)
						continue;

					var slope = elevationMeters[source] - elevationMeters[destination];
					if (slope < 120 ||
						(precipitationTenthsMillimetersPerDay[source] < 100 && waterDepthMeters[source] == 0))
						continue;

					var erosionMeters = Math.Clamp(
						1 + slope / 2200 + precipitationTenthsMillimetersPerDay[source] / 3000 +
							waterDepthMeters[source] / 2500,
						1,
						4);
					geologyElevationDelta[source] -= erosionMeters;
					geologyElevationDelta[destination] += erosionMeters;

					var movedMaterial = Math.Min(
						surfaceMaterialUnits[source],
						erosionMeters * 20L);
					geologyMaterialDelta[source] -= movedMaterial;
					geologyMaterialDelta[destination] += movedMaterial;
					erodedMaterial += movedMaterial;
				}

			for (var i = 0; i < CellCount; i++)
			{
				var elevation = elevationMeters[i] + geologyElevationDelta[i];
				if (elevation < MinimumGeologyElevationMeters || elevation > MaximumGeologyElevationMeters)
					throw new InvalidOperationException(
						$"Planet geology exceeded its elevation domain at cell {i}: {elevation} m.");

				var material = surfaceMaterialUnits[i] + geologyMaterialDelta[i];
				if (material < 0 || material > uint.MaxValue)
					throw new InvalidOperationException(
						$"Planet geology exceeded its material reservoir at cell {i}: {material}.");

				elevationMeters[i] = checked((short)elevation);
				lastGeologyChangeMeters[i] = checked((short)geologyElevationDelta[i]);
				surfaceMaterialUnits[i] = checked((uint)material);
			}

			CumulativeErodedMaterialUnits = checked(CumulativeErodedMaterialUnits + erodedMaterial);
			CumulativeMantleMaterialInputUnits = checked(
				CumulativeMantleMaterialInputUnits + mantleMaterialInput);
			CumulativeTectonicElevationChangeMeters = checked(
				CumulativeTectonicElevationChangeMeters + expectedCrustChange);
			ActiveVolcanicCellCount = activeVolcanicCells;
			ElevationBalanceErrorMeters = SumElevation() - elevationBefore - expectedCrustChange;
			MaterialBalanceErrorUnits = Sum(surfaceMaterialUnits) - materialBefore - mantleMaterialInput;
			if (ElevationBalanceErrorMeters != 0 || MaterialBalanceErrorUnits != 0)
				throw new InvalidOperationException(
					$"Planet geology ledger failed: elevation={ElevationBalanceErrorMeters} m, " +
					$"material={MaterialBalanceErrorUnits} units.");

			ReclassifyGeology(activity);
			RecalculateDerivedState();
		}

		int LowestNeighbor(int latitude, int longitude)
		{
			var cell = latitude * LongitudeCount + longitude;
			var north = Math.Max(0, latitude - 1) * LongitudeCount + longitude;
			var south = Math.Min(LatitudeCount - 1, latitude + 1) * LongitudeCount + longitude;
			var west = latitude * LongitudeCount + (longitude + LongitudeCount - 1) % LongitudeCount;
			var east = latitude * LongitudeCount + (longitude + 1) % LongitudeCount;
			var lowest = cell;
			if (elevationMeters[north] < elevationMeters[lowest])
				lowest = north;
			if (elevationMeters[south] < elevationMeters[lowest])
				lowest = south;
			if (elevationMeters[west] < elevationMeters[lowest])
				lowest = west;
			if (elevationMeters[east] < elevationMeters[lowest])
				lowest = east;
			return lowest;
		}

		void ReclassifyGeology(int activity)
		{
			for (var latitude = 0; latitude < LatitudeCount; latitude++)
				for (var longitude = 0; longitude < LongitudeCount; longitude++)
				{
					var cell = latitude * LongitudeCount + longitude;
					var elevation = elevationMeters[cell];
					var volcanic = crust[cell] == (byte)PlanetCrustKind.PlateBoundary &&
						(elevation > 1800 || IsVolcanicallyActive(latitude, longitude, activity));
					terrain[cell] = (byte)(volcanic ? PlanetTerrainKind.Volcanic :
						elevation < -3000 ? PlanetTerrainKind.DeepBasin :
						elevation < 0 ? PlanetTerrainKind.Shelf :
						elevation < 900 ? PlanetTerrainKind.Lowland :
						elevation < 2200 ? PlanetTerrainKind.Highland : PlanetTerrainKind.Mountain);
					materialRichness[cell] = (byte)Math.Clamp(
						surfaceMaterialUnits[cell] / MaterialUnitsPerRichness,
						0,
						255);
				}
		}

		bool IsVolcanicallyActive(int latitude, int longitude, int activity)
		{
			if (geologyPulseSequence == 0 || activity <= 0)
				return false;

			var value = Positive(CoordinateHash(
				GeologySeed ^ geologyPulseSequence * 0x45D9F3B,
				latitude + 97,
				longitude + 193));
			return value % 16_000 < activity;
		}

		void RecalculateGeologySummaryAndHash()
		{
			long elevationTotal = 0;
			long absoluteChangeTotal = 0;
			long materialTotal = 0;
			var hash = 2166136261u;
			for (var i = 0; i < CellCount; i++)
			{
				elevationTotal += elevationMeters[i];
				absoluteChangeTotal += Math.Abs(lastGeologyChangeMeters[i]);
				materialTotal += surfaceMaterialUnits[i];
				hash = Mix(hash, unchecked((ushort)elevationMeters[i]));
				hash = Mix(hash, terrain[i]);
				hash = Mix(hash, materialRichness[i]);
				hash = Mix32(hash, unchecked((int)surfaceMaterialUnits[i]));
				hash = Mix(hash, unchecked((ushort)lastGeologyChangeMeters[i]));
			}

			hash = Mix32(hash, geologyPulseSequence);
			hash = MixLong(hash, CumulativeErodedMaterialUnits);
			hash = MixLong(hash, CumulativeMantleMaterialInputUnits);
			hash = MixLong(hash, CumulativeTectonicElevationChangeMeters);
			hash = Mix32(hash, ActiveVolcanicCellCount);
			hash = MixLong(hash, ElevationBalanceErrorMeters);
			hash = MixLong(hash, MaterialBalanceErrorUnits);
			geologyHash = unchecked((int)hash);
			MeanElevationMeters = checked((int)(elevationTotal / CellCount));
			MeanAbsoluteGeologyChangeMilliMeters = checked((int)(absoluteChangeTotal * 1000 / CellCount));
			TotalSurfaceMaterialUnits = materialTotal;
		}

		void RestoreGeology(PlanetSurfaceSaveData data, PlanetPhysicalState physics)
		{
			geologyPulseSequence = data.GeologyPulseSequence;
			ElevationBalanceErrorMeters = data.ElevationBalanceErrorMeters;
			MaterialBalanceErrorUnits = data.MaterialBalanceErrorUnits;
			CumulativeErodedMaterialUnits = data.CumulativeErodedMaterialUnits;
			CumulativeMantleMaterialInputUnits = data.CumulativeMantleMaterialInputUnits;
			CumulativeTectonicElevationChangeMeters = data.CumulativeTectonicElevationChangeMeters;
			ActiveVolcanicCellCount = data.ActiveVolcanicCellCount;
			RestoreShorts(data.ElevationBrotliBase64, elevationMeters, "geology elevation");
			RestoreDeltaUInts(data.SurfaceMaterialBrotliBase64, surfaceMaterialUnits, "surface material");
			RestoreShorts(data.LastGeologyChangeBrotliBase64, lastGeologyChangeMeters, "geology change");
			ReclassifyGeology(Math.Clamp(physics.TectonicActivityPerMille, 0, 1000));
		}

		long SumElevation()
		{
			long total = 0;
			for (var i = 0; i < elevationMeters.Length; i++)
				total += elevationMeters[i];
			return total;
		}

		int GeologySeed => unchecked(0x36D2B771 ^ planet.Index * 0x1F123BB5 ^
			planet.Physics.MassEarthMillionths ^ planet.Physics.RadiusKilometers << 8);
	}
}
