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
using System.Text.Json;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.HV.Campaign;
using OpenRA.Mods.HV.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.HV.Widgets.Logic
{
	/// <summary>
	/// The campaign screen: one planet frame, an overlay picker and a cell
	/// inspector.
	/// <para>
	/// Native observer for the same UniverseState that the OpenHV world advances.
	/// The panel never issues simulation orders: the world lives autonomously.
	/// </para>
	/// </summary>
	public class PlanetLogic : ChromeLogic
	{
		readonly PlanetMapWidget map;

		[ObjectCreator.UseCtor]
		public PlanetLogic(Widget widget, World world, Action onExit)
		{
			map = widget.Get<PlanetMapWidget>("PLANET_MAP");
			map.OnSelectionChanged = () => lastAction = null;
			var universe = world.WorldActor.TraitOrDefault<UniverseState>();
			if (universe != null)
			{
				PlanetState activePlanet = null;
				foreach (var planet in universe.StarSystem.Planets)
					if (planet.Active)
					{
						activePlanet = planet;
						break;
					}

				map.Bind(activePlanet ?? universe.StarSystem.Planets[0]);
			}

			var status = widget.GetOrNull<LabelWidget>("PLANET_STATUS");
			var title = widget.GetOrNull<LabelWidget>("PLANET_TITLE");
			var cellInfo = widget.GetOrNull<LabelWidget>("CELL_INFO");

			if (title != null)
				title.GetText = () => map.PlanetName == null
					? "No planet loaded"
					: $"{map.PlanetName} - step {map.Step}";

			if (status != null)
				status.GetText = () =>
				{
					if (map.Fetching)
						return "Fetching...";
					if (map.Error != null)
						return map.Error;
					if (map.Biome == null)
						return "Start the campaign with: python3 sim_server.py";
					return map.IsNative
						? $"LIVE · native .NET · {map.LongitudeCells} x {map.LatitudeCells} cells"
						: $"legacy frame · {map.LongitudeCells} x {map.LatitudeCells} cells";
				};

			if (cellInfo != null)
				cellInfo.GetText = DescribeSelectionOrAction;

			var refresh = widget.GetOrNull<ButtonWidget>("REFRESH_BUTTON");
			if (refresh != null)
			{
				refresh.OnClick = () => map.Fetch();
				refresh.IsDisabled = () => map.Fetching;
			}

			foreach (var (button, overlay) in new (string, PlanetOverlay)[]
			{
				("TERRAIN_BUTTON", PlanetOverlay.Terrain),
				("TEMPERATURE_BUTTON", PlanetOverlay.Temperature),
				("PRESSURE_BUTTON", PlanetOverlay.Pressure),
				("WIND_BUTTON", PlanetOverlay.Wind),
				("PRECIPITATION_BUTTON", PlanetOverlay.Precipitation),
				("VERTICAL_BUTTON", PlanetOverlay.VerticalMotion),
			})
			{
				var b = widget.GetOrNull<ButtonWidget>(button);
				if (b == null)
					continue;

				var captured = overlay;
				b.OnClick = () => map.SetOverlay(captured);
				b.IsHighlighted = () => map.Overlay == captured;
			}

			var fight = widget.GetOrNull<ButtonWidget>("FIGHT_BUTTON");
			if (fight != null)
			{
				fight.OnClick = WriteBattleRequest;
				fight.IsVisible = () => !map.IsNative;
				fight.IsDisabled = () => map.Biome == null || !map.SelectedCell.HasValue ||
					!SelectedDefender().HasValue;
			}

			var back = widget.GetOrNull<ButtonWidget>("BACK_BUTTON");
			if (back != null)
				back.OnClick = () => { Ui.CloseWindow(); onExit(); };

			if (!map.IsNative)
				map.Fetch();
		}

		/// <summary>
		/// Writes the request rather than starting the match, because it cannot
		/// start one: Launch.Simulation is read by PanelLoadScreen once at process
		/// start, so a battle needs a process of its own. Pretending otherwise
		/// would mean a button that looks like it fights and does not.
		/// </summary>
		void WriteBattleRequest()
		{
			if (map.Biome == null || !map.SelectedCell.HasValue)
				return;

			var cell = map.SelectedCell.Value;
			var i = cell.Y * map.LongitudeCells + cell.X;
			var holder = map.Faction[i];
			var holderName = FactionName(holder);
			var defender = SelectedDefender();
			if (!defender.HasValue)
			{
				lastAction = "This cell is not on a contested faction border.";
				return;
			}

			var temperature = map.TemperatureK != null ? map.TemperatureK[i] : 288.0;

			var request = BattleRequestBuilder.Build(
				map.PlanetId, map.Step, cell.X, cell.Y,
				map.LatitudeCells, map.LongitudeCells,
				map.Biome[i], map.Biomass[i], map.PopulationDensity[i],
				holder, holderName, defender.Value, FactionName(defender.Value), temperature);

			try
			{
				var directory = Path.Combine(Platform.SupportDir, "battles");
				Directory.CreateDirectory(directory);
				var requestId = request["requestId"].GetValue<string>();
				var path = Path.Combine(directory, $"request-{requestId}.json");
				File.WriteAllText(path, request.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

				var mapName = request["map"]["name"].GetValue<string>();

				// The command replays this document rather than passing the cell,
				// because the campaign moves while you look at it: asking for the
				// same cell a minute from now yields a different seed, map and
				// army values than the ones just written down here.
				lastAction =
					$"Wrote request for ({cell.X}, {cell.Y})\n" +
					$"step {map.Step}, on {mapName}.\n\n" +
					$"Fight it with:\n./fight-cell.sh --request \\\n  \"{path}\"";
			}
			catch (Exception e)
			{
				lastAction = $"Could not write the request:\n{e.Message}";
			}
		}

		string lastAction;

		int? SelectedDefender()
		{
			if (!map.SelectedCell.HasValue || map.Faction == null)
				return null;
			var cell = map.SelectedCell.Value;
			return BattleRequestBuilder.SelectDefender(
				map.Faction, map.LongitudeCells, map.LatitudeCells, cell.X, cell.Y);
		}

		string FactionName(int index)
		{
			foreach (var faction in map.Factions)
				if (faction.Index == index)
					return faction.Name;
			return index < 0 ? "Unclaimed" : $"faction {index}";
		}

		string DescribeSelectionOrAction()
		{
			return lastAction ?? DescribeSelection();
		}

		string DescribeSelection()
		{
			if (map.Biome == null || !map.SelectedCell.HasValue)
				return "Click a cell.";

			var cell = map.SelectedCell.Value;
			var i = cell.Y * map.LongitudeCells + cell.X;
			if (map.IsNative)
			{
				var wind = ApproximateSpeed(
					map.EastWindCentimetersPerSecond[i], map.NorthWindCentimetersPerSecond[i]);
				string[] nativeLines =
				[
					$"cell ({cell.X}, {cell.Y})",
					$"terrain    {(PlanetTerrainKind)map.Biome[i]}",
					$"temperature {map.TemperatureK[i]} K",
					$"pressure   {map.PressurePascals[i] / 1000f:0.0} kPa",
					$"wind       {wind / 100f:0.0} m/s",
					$"rain       {map.PrecipitationTenthsMillimetersPerDay[i] / 10f:0.0} mm/day",
					$"vertical   {map.VerticalVelocityMillimetersPerSecond[i] / 1000f:+0.000;-0.000;0.000} m/s",
					"",
					"Autonomous world",
					"No player orders"
				];
				return string.Join("\n", nativeLines);
			}

			var biome = map.Biome[i];
			List<string> lines =
			[
				$"cell ({cell.X}, {cell.Y})",
				$"terrain    {BattleRequestBuilder.BiomeName(biome)}",
				$"biomass    {map.Biomass[i] / 255f:0.00} of capacity",
				$"population {map.PopulationDensity[i] / 255f:0.00} (log scale)",
			];

			var faction = map.Faction[i];
			if (faction < 0)
				lines.Add("holder     unclaimed");
			else
			{
				var name = FactionName(faction);
				lines.Add($"holder     {name}");
				var defender = SelectedDefender();
				lines.Add(defender.HasValue
					? $"opponent   {FactionName(defender.Value)}"
					: "battle     no contested border");
			}

			return string.Join("\n", lines);
		}

		static int ApproximateSpeed(int x, int y)
		{
			var absoluteX = Math.Abs(x);
			var absoluteY = Math.Abs(y);
			return Math.Max(absoluteX, absoluteY) + Math.Min(absoluteX, absoluteY) / 2;
		}
	}
}
