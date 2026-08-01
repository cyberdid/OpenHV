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
using OpenRA.Mods.Common.Widgets;
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
		static readonly string[] BiomeNames =
		{
			"ice", "tundra", "barrens", "steppe", "growth", "deep-growth", "scorched"
		};

		readonly PlanetMapWidget map;

		[ObjectCreator.UseCtor]
		public PlanetLogic(Widget widget, Action onExit)
		{
			map = widget.Get<PlanetMapWidget>("PLANET_MAP");

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
				cellInfo.GetText = () => DescribeSelection();

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

			var back = widget.GetOrNull<ButtonWidget>("BACK_BUTTON");
			if (back != null)
				back.OnClick = () => { Ui.CloseWindow(); onExit(); };

			map.Fetch();
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
				$"terrain    {(biome >= 0 && biome < BiomeNames.Length ? BiomeNames[biome] : $"class-{biome}")}",
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
