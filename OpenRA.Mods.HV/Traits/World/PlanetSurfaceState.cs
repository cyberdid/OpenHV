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
		public int HydrologyHash;

		[FieldLoader.Require]
		public string WaterDepthDeflateBase64;
	}

	/// <summary>
	/// Large equirectangular surface owned by the simulation. Cell arrays are
	/// represented in the synchronized state by deterministic digests so the
	/// normal 50 Hz RTS hash does not re-read every geological cell each tick.
	/// Any mutation must update its domain digest before returning.
	/// </summary>
	public sealed class PlanetSurfaceState : IEffect, ISync
	{
		const int SaveSchemaVersion = 1;
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

		[VerifySync]
		int generation = 1;

		[VerifySync]
		int topologyHash;

		[VerifySync]
		int hydrologyHash;

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
		public int MinimumElevationMeters { get; private set; }
		public int MaximumElevationMeters { get; private set; }
		public int LandCellCount { get; private set; }
		public int BasinCellCount { get; private set; }

		public PlanetSurfaceState(PlanetDefinition planet)
		{
			this.planet = planet;
			elevationMeters = new short[CellCount];
			crust = new byte[CellCount];
			plate = new byte[CellCount];
			terrain = new byte[CellCount];
			waterDepthMeters = new ushort[CellCount];
			materialRichness = new byte[CellCount];
			Generate();
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

		internal PlanetSurfaceSaveData CreateSaveData() => new()
		{
			SchemaVersion = SaveSchemaVersion,
			Generation = generation,
			TopologyHash = topologyHash,
			HydrologyHash = hydrologyHash,
			WaterDepthDeflateBase64 = CompressWaterDepth()
		};

		internal void Restore(PlanetSurfaceSaveData data)
		{
			if (data.SchemaVersion != SaveSchemaVersion)
				throw new InvalidOperationException(
					$"Planet surface save schema {data.SchemaVersion} is not supported.");

			generation = data.Generation;
			if (topologyHash != data.TopologyHash)
				throw new InvalidOperationException(
					"Planet surface seed generated a different topology than the saved runtime.");

			RestoreWaterDepth(data.WaterDepthDeflateBase64);
			RecalculateDerivedState();
			if (hydrologyHash != data.HydrologyHash)
				throw new InvalidOperationException("Planet surface save payload failed its deterministic digest check.");
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

		void RecalculateDerivedState()
		{
			MinimumElevationMeters = int.MaxValue;
			MaximumElevationMeters = int.MinValue;
			LandCellCount = 0;
			BasinCellCount = 0;
			var topology = 2166136261u;
			var hydrology = 2166136261u;
			for (var i = 0; i < CellCount; i++)
			{
				var elevation = elevationMeters[i];
				MinimumElevationMeters = Math.Min(MinimumElevationMeters, elevation);
				MaximumElevationMeters = Math.Max(MaximumElevationMeters, elevation);
				if (elevation >= 0)
					LandCellCount++;
				else
					BasinCellCount++;

				topology = Mix(topology, unchecked((ushort)elevation));
				topology = Mix(topology, crust[i]);
				topology = Mix(topology, plate[i]);
				topology = Mix(topology, terrain[i]);
				topology = Mix(topology, materialRichness[i]);
				hydrology = Mix(hydrology, waterDepthMeters[i]);
			}

			topologyHash = unchecked((int)topology);
			hydrologyHash = unchecked((int)hydrology);
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

		string CompressWaterDepth()
		{
			var raw = new byte[waterDepthMeters.Length * 2];
			for (var i = 0; i < waterDepthMeters.Length; i++)
			{
				raw[i * 2] = (byte)waterDepthMeters[i];
				raw[i * 2 + 1] = (byte)(waterDepthMeters[i] >> 8);
			}

			using var output = new MemoryStream();
			using (var compressor = new DeflateStream(output, CompressionLevel.Optimal, true))
				compressor.Write(raw, 0, raw.Length);

			return Convert.ToBase64String(output.ToArray());
		}

		void RestoreWaterDepth(string encoded)
		{
			byte[] compressed;
			try
			{
				compressed = Convert.FromBase64String(encoded);
			}
			catch (FormatException e)
			{
				throw new InvalidOperationException("Saved planet hydrology is not valid base64.", e);
			}

			using var input = new MemoryStream(compressed);
			using var decompressor = new DeflateStream(input, CompressionMode.Decompress);
			using var output = new MemoryStream();
			decompressor.CopyTo(output);
			var raw = output.ToArray();
			if (raw.Length != waterDepthMeters.Length * 2)
				throw new InvalidOperationException(
					$"Saved planet hydrology contains {raw.Length} bytes; expected {waterDepthMeters.Length * 2}.");

			for (var i = 0; i < waterDepthMeters.Length; i++)
				waterDepthMeters[i] = (ushort)(raw[i * 2] | raw[i * 2 + 1] << 8);
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

		void IEffect.Tick(World world) { }

		IEnumerable<IRenderable> IEffect.Render(WorldRenderer renderer) { return []; }
	}
}
