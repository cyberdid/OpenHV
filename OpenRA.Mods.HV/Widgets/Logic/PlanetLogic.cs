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
using OpenRA.Widgets;

namespace OpenRA.Mods.HV.Widgets.Logic
{
	/// <summary>
	/// The campaign screen: one planet frame, an overlay picker and a cell
	/// inspector.
	///
	/// This is the half of the bridge the player can see. The other half - the
	/// simulation - runs as a separate process on a clock 4,320,000 times slower
	/// than a battle tick, which is why the planet is fetched rather than
	/// stepped in here.
	/// </summary>
	public class PlanetLogic : ChromeLogic
	{
		readonly PlanetMapWidget map;

		[ObjectCreator.UseCtor]
		public PlanetLogic(Widget widget, Action onExit)
		{
			map = widget.Get<PlanetMapWidget>("PLANET_MAP");
			map.OnSelectionChanged = () => lastAction = null;

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
					return $"{map.LongitudeCells} x {map.LatitudeCells} cells";
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
				("BIOME_BUTTON", PlanetOverlay.Biome),
				("BIOMASS_BUTTON", PlanetOverlay.Biomass),
				("POPULATION_BUTTON", PlanetOverlay.Population),
				("FACTION_BUTTON", PlanetOverlay.Faction),
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
				fight.IsDisabled = () => map.Biome == null || !map.SelectedCell.HasValue;
			}

			var back = widget.GetOrNull<ButtonWidget>("BACK_BUTTON");
			if (back != null)
				back.OnClick = () => { Ui.CloseWindow(); onExit(); };

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
			var i = (cell.Y * map.LongitudeCells) + cell.X;
			var holder = map.Faction[i];
			var holderName = holder >= 0 && holder < map.Factions.Count
				? map.Factions[holder].Name : "Unclaimed";

			var temperature = map.TemperatureK != null ? map.TemperatureK[i] : 288.0;

			var request = BattleRequestBuilder.Build(
				map.PlanetId, map.Step, cell.X, cell.Y,
				map.LatitudeCells, map.LongitudeCells,
				map.Biome[i], map.Biomass[i], map.PopulationDensity[i],
				holder, holderName, temperature);

			try
			{
				var directory = Path.Combine(Platform.SupportDir, "battles");
				Directory.CreateDirectory(directory);
				var path = Path.Combine(directory, $"request-{cell.X}-{cell.Y}.json");
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

			var biome = map.Biome[i];
			var lines = new List<string>
			{
				$"cell ({cell.X}, {cell.Y})",
				$"terrain    {BattleRequestBuilder.BiomeName(biome)}",
				$"biomass    {map.Biomass[i] / 255f:0.00} of capacity",
				$"population {map.PopulationDensity[i] / 255f:0.00} (log scale)",
			};

			var faction = map.Faction[i];
			if (faction < 0)
				lines.Add("holder     unclaimed");
			else
			{
				var name = faction < map.Factions.Count ? map.Factions[faction].Name : $"faction {faction}";
				lines.Add($"holder     {name}");
			}

			return string.Join("\n", lines);
		}
	}
}
