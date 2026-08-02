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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.HV.Traits;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Widgets;

namespace OpenRA.Mods.HV.Widgets
{
	public enum PlanetOverlay
	{
		Terrain,
		Geology,
		Temperature,
		Pressure,
		Wind,
		Precipitation,
		VerticalMotion,
		Biomass,
		Population,
		Faction
	}

	/// <summary>
	/// Draws the authoritative native PlanetSurfaceState as a grid of cells.
	/// The old planet-state-v1 HTTP reader remains a migration fallback for
	/// biosphere/faction layers that have not joined the native runtime yet.
	/// <para>
	/// Built like RadarWidget rather than as ten thousand filled rectangles: one
	/// BGRA sheet written per fetch and drawn as a single scaled sprite. At
	/// 144x72 the per-rectangle version would issue a draw call per cell every
	/// frame for a picture that changes about once a second.
	/// </para>
	/// </summary>
	public class PlanetMapWidget : Widget
	{
		public string Url = "http://localhost:8791/api/planet";
		public PlanetOverlay Overlay = PlanetOverlay.Terrain;

		// planet-state-v1 biome codes. Index is the code the schema defines;
		// the colours are read-at-a-glance, not the tileset's.
		static readonly Color[] BiomeColors =
		[
			Color.FromArgb(0xE8, 0xF4, 0xFF),   // 0 ice
			Color.FromArgb(0x9F, 0xB0, 0xA8),   // 1 tundra
			Color.FromArgb(0xB9, 0xA1, 0x7E),   // 2 barrens
			Color.FromArgb(0x8C, 0x9E, 0x54),   // 3 steppe
			Color.FromArgb(0x3E, 0x7A, 0x3C),   // 4 growth
			Color.FromArgb(0x1C, 0x4A, 0x2E),   // 5 deep-growth
			Color.FromArgb(0x5A, 0x33, 0x28),   // 6 scorched
		];

		static readonly Color[] TerrainColors =
		[
			Color.FromArgb(0x08, 0x1C, 0x3A),   // deep basin
			Color.FromArgb(0x24, 0x60, 0x80),   // shelf
			Color.FromArgb(0x42, 0x7A, 0x3C),   // lowland
			Color.FromArgb(0x9B, 0x83, 0x52),   // highland
			Color.FromArgb(0xB2, 0xB2, 0xB5),   // mountain
			Color.FromArgb(0x9A, 0x37, 0x28),   // volcanic
		];

		public int LatitudeCells { get; private set; }
		public int LongitudeCells { get; private set; }
		public int Step { get; private set; }
		public string PlanetId { get; private set; }
		public string PlanetName { get; private set; }
		public string Error { get; private set; }
		public bool Fetching { get; private set; }

		public int[] Biome { get; private set; }
		public int[] Biomass { get; private set; }
		public int[] PopulationDensity { get; private set; }
		public int[] Faction { get; private set; }
		public int[] TemperatureK { get; private set; }
		public int[] ElevationMeters { get; private set; }
		public long[] SurfaceMaterialUnits { get; private set; }
		public int[] LastGeologyChangeMeters { get; private set; }
		public int[] PressurePascals { get; private set; }
		public int[] EastWindCentimetersPerSecond { get; private set; }
		public int[] NorthWindCentimetersPerSecond { get; private set; }
		public int[] PrecipitationTenthsMillimetersPerDay { get; private set; }
		public int[] VerticalVelocityMillimetersPerSecond { get; private set; }
		public List<(int Index, string Name, Color Colour)> Factions { get; } = [];
		public bool IsNative => nativePlanet != null;

		public int2? SelectedCell { get; private set; }
		public Action OnFetched;
		public Action OnSelectionChanged;

		Sheet sheet;
		Sprite sprite;
		byte[] data;
		bool dirty;
		PlanetState nativePlanet;

		public override void Draw()
		{
			if (Biome == null)
				return;

			if (dirty)
			{
				Repaint();
				dirty = false;
			}

			if (sprite == null)
				return;

			var (origin, size) = MapRect();
			WidgetUtils.DrawSprite(sprite, origin, size);

			if (SelectedCell.HasValue)
			{
				var cw = size.X / LongitudeCells;
				var ch = size.Y / LatitudeCells;
				var tl = new float2(origin.X + SelectedCell.Value.X * cw, origin.Y + SelectedCell.Value.Y * ch);
				WidgetUtils.FillRectWithColor(
					new Rectangle((int)tl.X, (int)tl.Y, Math.Max(1, (int)cw), Math.Max(1, (int)ch)),
					Color.FromArgb(140, 255, 255, 255));
			}
		}

		/// <summary>Letterboxed 2:1, because an equirectangular frame stretched to
		/// fit a differently shaped panel puts cells where they are not.</summary>
		(float2 Origin, float2 Size) MapRect()
		{
			var bounds = RenderBounds;
			var aspect = (float)LongitudeCells / LatitudeCells;
			float width = bounds.Width;
			var height = width / aspect;
			if (height > bounds.Height)
			{
				height = bounds.Height;
				width = height * aspect;
			}

			return (new float2(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2),
				new float2(width, height));
		}

		public int2? CellAt(int2 screen)
		{
			if (Biome == null)
				return null;

			var (origin, size) = MapRect();
			var x = (int)((screen.X - origin.X) / (size.X / LongitudeCells));
			var y = (int)((screen.Y - origin.Y) / (size.Y / LatitudeCells));
			if (x < 0 || y < 0 || x >= LongitudeCells || y >= LatitudeCells)
				return null;

			return new int2(x, y);
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Event != MouseInputEvent.Down || mi.Button != MouseButton.Left)
				return false;

			var cell = CellAt(mi.Location);
			if (cell == null)
				return false;

			SelectedCell = cell;
			OnSelectionChanged?.Invoke();
			return true;
		}

		public void SetOverlay(PlanetOverlay overlay)
		{
			Overlay = overlay;
			dirty = true;
		}

		public void Bind(PlanetState planet)
		{
			nativePlanet = planet ?? throw new ArgumentNullException(nameof(planet));
			RefreshNative();
		}

		public override void Tick()
		{
			base.Tick();
			if (nativePlanet != null && nativePlanet.Surface.ClimatePulseSequence != Step)
				RefreshNative();
		}

		public void RefreshNative()
		{
			if (nativePlanet == null)
				return;

			var surface = nativePlanet.Surface;
			LatitudeCells = surface.LatitudeCells;
			LongitudeCells = surface.LongitudeCells;
			Step = surface.ClimatePulseSequence;
			PlanetId = nativePlanet.Definition.PlanetId;
			PlanetName = nativePlanet.Definition.Name;
			var count = surface.CellCount;
			Biome = new int[count];
			Biomass = new int[count];
			PopulationDensity = new int[count];
			Faction = new int[count];
			TemperatureK = new int[count];
			ElevationMeters = new int[count];
			SurfaceMaterialUnits = new long[count];
			LastGeologyChangeMeters = new int[count];
			PressurePascals = new int[count];
			EastWindCentimetersPerSecond = new int[count];
			NorthWindCentimetersPerSecond = new int[count];
			PrecipitationTenthsMillimetersPerDay = new int[count];
			VerticalVelocityMillimetersPerSecond = new int[count];
			for (var latitude = 0; latitude < LatitudeCells; latitude++)
				for (var longitude = 0; longitude < LongitudeCells; longitude++)
				{
					var index = latitude * LongitudeCells + longitude;
					Biome[index] = (int)surface.TerrainAt(latitude, longitude);
					Faction[index] = -1;
					TemperatureK[index] = surface.TemperatureMilliKelvinAt(latitude, longitude) / 1000;
					ElevationMeters[index] = surface.ElevationAt(latitude, longitude);
					SurfaceMaterialUnits[index] = surface.SurfaceMaterialUnitsAt(latitude, longitude);
					LastGeologyChangeMeters[index] = surface.LastGeologyChangeMetersAt(latitude, longitude);
					PressurePascals[index] = surface.PressurePascalsAt(latitude, longitude);
					EastWindCentimetersPerSecond[index] =
						surface.EastWindCentimetersPerSecondAt(latitude, longitude);
					NorthWindCentimetersPerSecond[index] =
						surface.NorthWindCentimetersPerSecondAt(latitude, longitude);
					PrecipitationTenthsMillimetersPerDay[index] =
						surface.PrecipitationTenthsMillimetersPerDayAt(latitude, longitude);
					VerticalVelocityMillimetersPerSecond[index] =
						surface.VerticalVelocityMillimetersPerSecondAt(latitude, longitude);
				}

			Error = null;
			dirty = true;
			OnFetched?.Invoke();
		}

		Color ColorFor(int i)
		{
			switch (Overlay)
			{
				case PlanetOverlay.Geology:
				{
					if (LastGeologyChangeMeters == null || SurfaceMaterialUnits == null)
					{
						var biome = Biome[i];
						return biome >= 0 && biome < BiomeColors.Length ?
							BiomeColors[biome] : Color.FromArgb(255, 255, 0, 255);
					}

					var change = LastGeologyChangeMeters[i];
					var strength = Math.Clamp(Math.Abs(change) / 5f, 0f, 1f);
					if (change > 0)
						return Color.FromArgb(255, (int)(90 + 165 * strength), (int)(42 + 120 * strength), 24);
					if (change < 0)
						return Color.FromArgb(255, 20, (int)(60 + 100 * strength), (int)(95 + 160 * strength));

					var material = Math.Clamp(SurfaceMaterialUnits[i] / 25_500f, 0f, 1f);
					return Color.FromArgb(255, (int)(30 + 130 * material), (int)(32 + 95 * material), 38);
				}

				case PlanetOverlay.Temperature:
				{
					var v = Math.Clamp((TemperatureK[i] - 140) / 760f, 0f, 1f);
					return HeatColor(v);
				}

				case PlanetOverlay.Pressure:
				{
					var v = Math.Clamp(PressurePascals[i] / 2_500_000f, 0f, 1f);
					return Color.FromArgb(255, (int)(20 + 100 * v), (int)(45 + 180 * v), (int)(80 + 175 * v));
				}

				case PlanetOverlay.Wind:
				{
					var speed = ApproximateSpeed(
						EastWindCentimetersPerSecond[i], NorthWindCentimetersPerSecond[i]);
					return HeatColor(Math.Clamp(speed / 15_000f, 0f, 1f));
				}

				case PlanetOverlay.Precipitation:
				{
					var v = Math.Clamp(PrecipitationTenthsMillimetersPerDay[i] / 5000f, 0f, 1f);
					return Color.FromArgb(255, (int)(18 + 110 * v), (int)(25 + 180 * v), (int)(50 + 205 * v));
				}

				case PlanetOverlay.VerticalMotion:
				{
					var velocity = VerticalVelocityMillimetersPerSecond[i];
					var strength = Math.Clamp(Math.Abs(velocity) / 900f, 0f, 1f);
					return velocity >= 0
						? Color.FromArgb(255, (int)(30 + 225 * strength), (int)(40 + 170 * strength), 50)
						: Color.FromArgb(255, 35, (int)(50 + 100 * strength), (int)(70 + 185 * strength));
				}

				case PlanetOverlay.Biomass:
				{
					// Against standing capacity, so a stripped cell reads as
					// stripped rather than as merely dim.
					var v = Biomass[i] / 255f;
					return Color.FromArgb(255, (int)(40 + 30 * v), (int)(20 + 200 * v), (int)(40 + 60 * v));
				}

				case PlanetOverlay.Population:
				{
					// The document already carries this on a log scale: density
					// spans six orders of magnitude and linear would render a
					// founder brood and a peak swarm as the same black.
					var v = PopulationDensity[i] / 255f;
					return Color.FromArgb(255, (int)(30 + 225 * v), (int)(20 + 40 * v), (int)(60 + 80 * v));
				}

				case PlanetOverlay.Faction:
				{
					var f = Faction[i];
					foreach (var faction in Factions)
						if (faction.Index == f)
							return faction.Colour;
					return Color.FromArgb(255, 24, 24, 28);
				}

				default:
				{
					var b = Biome[i];
					var colors = IsNative ? TerrainColors : BiomeColors;
					return b >= 0 && b < colors.Length ? colors[b] : Color.FromArgb(255, 255, 0, 255);
				}
			}
		}

		static int ApproximateSpeed(int x, int y)
		{
			var absoluteX = Math.Abs(x);
			var absoluteY = Math.Abs(y);
			return Math.Max(absoluteX, absoluteY) + Math.Min(absoluteX, absoluteY) / 2;
		}

		static Color HeatColor(float value)
		{
			var v = Math.Clamp(value, 0f, 1f);
			return v < 0.5f
				? Color.FromArgb(255, (int)(20 + 120 * v), (int)(50 + 340 * v), (int)(120 + 270 * v))
				: Color.FromArgb(255, (int)(-115 + 370 * v), (int)(300 - 260 * v), (int)(300 - 270 * v));
		}

		void Repaint()
		{
			if (Biome == null)
				return;

			if (sheet == null || sheet.Size.Width < LongitudeCells || sheet.Size.Height < LatitudeCells)
			{
				sheet?.Dispose();
				sheet = new Sheet(SheetType.BGRA, new Size(LongitudeCells, LatitudeCells).NextPowerOf2());
				data = sheet.GetData();
				sprite = new Sprite(sheet, new Rectangle(0, 0, LongitudeCells, LatitudeCells), TextureChannel.RGBA);
			}

			// Bytes rather than a uint* cast: RadarWidget can use pointers because
			// the engine assembly sets AllowUnsafeBlocks, and this one does not.
			// Writing BGRA by hand is worth more than turning unsafe on across the
			// whole mod assembly for one loop that runs once a fetch.
			var stride = sheet.Size.Width;
			for (var y = 0; y < LatitudeCells; y++)
			{
				for (var x = 0; x < LongitudeCells; x++)
				{
					var c = ColorFor(y * LongitudeCells + x);
					var o = 4 * (y * stride + x);
					data[o + 0] = c.B;
					data[o + 1] = c.G;
					data[o + 2] = c.R;
					data[o + 3] = c.A;
				}
			}

			sheet.CommitBufferedData();
		}

		/// <summary>Fetch off the UI thread. The panel stays responsive while the
		/// simulation is thinking, and a server that is not running produces a
		/// message rather than a freeze.</summary>
		public void Fetch()
		{
			if (nativePlanet != null)
			{
				RefreshNative();
				return;
			}

			if (Fetching)
				return;

			Fetching = true;
			Error = null;
			var url = Url;

			Task.Run(() =>
			{
				string body;
				try
				{
					using var client = HttpClientFactory.Create();
					using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(10));
					body = client.GetStringAsync(url, cancel.Token).GetAwaiter().GetResult();
				}
				catch (Exception e)
				{
					Game.RunAfterTick(() =>
					{
						Error = $"Could not reach {url}: {e.Message}";
						Fetching = false;
						OnFetched?.Invoke();
					});
					return;
				}

				try
				{
					var parsed = Parse(body);
					Game.RunAfterTick(() =>
					{
						Apply(parsed);
						Fetching = false;
						OnFetched?.Invoke();
					});
				}
				catch (Exception e)
				{
					Game.RunAfterTick(() =>
					{
						Error = e.Message;
						Fetching = false;
						OnFetched?.Invoke();
					});
				}
			});
		}

		sealed class Frame
		{
			public int LatitudeCells;
			public int LongitudeCells;
			public int Step;
			public string PlanetId;
			public string PlanetName;
			public int[] Biome;
			public int[] Biomass;
			public int[] Population;
			public int[] Faction;
			public int[] TemperatureK;
			public List<(int, string, Color)> Factions = [];
		}

		static int[] ReadInts(JsonElement parent, string name, int expected)
		{
			if (!parent.TryGetProperty(name, out var element))
				throw new InvalidOperationException($"planet-state is missing cells.{name}.");

			var result = new int[element.GetArrayLength()];
			var i = 0;
			foreach (var v in element.EnumerateArray())
				result[i++] = v.GetInt32();

			// A short array would draw a torn map rather than fail, and a torn map
			// looks like an art problem for as long as it takes to think of
			// looking here.
			if (result.Length != expected)
				throw new InvalidOperationException($"cells.{name} has {result.Length} entries, expected {expected}.");

			return result;
		}

		static Frame Parse(string body)
		{
			using var document = JsonDocument.Parse(body);
			var root = document.RootElement;

			var version = root.GetProperty("schemaVersion").GetInt32();
			if (version != 1)
				throw new InvalidOperationException($"Unsupported planet-state schema version {version}.");

			var grid = root.GetProperty("grid");
			var frame = new Frame
			{
				LatitudeCells = grid.GetProperty("latitudeCells").GetInt32(),
				LongitudeCells = grid.GetProperty("longitudeCells").GetInt32(),
				Step = root.GetProperty("step").GetInt32(),
				PlanetId = root.GetProperty("planetId").GetString(),
				PlanetName = root.TryGetProperty("planetName", out var n) ? n.GetString() : "planet",
			};

			var expected = frame.LatitudeCells * frame.LongitudeCells;
			var cells = root.GetProperty("cells");
			frame.Biome = ReadInts(cells, "biome", expected);
			frame.Biomass = ReadInts(cells, "biomass", expected);
			frame.Population = ReadInts(cells, "population", expected);
			frame.Faction = ReadInts(cells, "faction", expected);

			// Optional in the schema: a frame without the overlay still makes a valid
			// battle request, it just falls back to a standard surface temperature.
			if (cells.TryGetProperty("temperatureK", out var temps))
			{
				var values = new int[temps.GetArrayLength()];
				var k = 0;
				foreach (var v in temps.EnumerateArray())
					values[k++] = v.GetInt32();
				if (values.Length == expected)
					frame.TemperatureK = values;
			}

			foreach (var f in root.GetProperty("factions").EnumerateArray())
			{
				var hex = f.GetProperty("colour").GetString();
				var colour = Color.FromArgb(
					255,
					Convert.ToInt32(hex[..2], 16),
					Convert.ToInt32(hex[2..4], 16),
					Convert.ToInt32(hex[4..6], 16));
				frame.Factions.Add((f.GetProperty("index").GetInt32(), f.GetProperty("name").GetString(), colour));
			}

			return frame;
		}

		void Apply(Frame f)
		{
			LatitudeCells = f.LatitudeCells;
			LongitudeCells = f.LongitudeCells;
			Step = f.Step;
			PlanetId = f.PlanetId;
			PlanetName = f.PlanetName;
			Biome = f.Biome;
			Biomass = f.Biomass;
			PopulationDensity = f.Population;
			Faction = f.Faction;
			TemperatureK = f.TemperatureK;

			Factions.Clear();
			Factions.AddRange(f.Factions);

			if (SelectedCell.HasValue &&
				(SelectedCell.Value.X >= LongitudeCells || SelectedCell.Value.Y >= LatitudeCells))
				SelectedCell = null;

			dirty = true;
		}

		public override void Removed()
		{
			base.Removed();
			sheet?.Dispose();
			sheet = null;
			sprite = null;
		}
	}
}
