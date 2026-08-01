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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using OpenRA.Mods.HV.Campaign;
using OpenRA.Support;

namespace OpenRA.Mods.HV.UtilityCommands
{
	/// <summary>
	/// Turns one cell of the campaign planet into a battle-request-v1 document.
	///
	/// This is the campaign asking for a battle, and it is the half of the bridge
	/// that had never been written: the planet could be read and drawn, but
	/// nothing turned a contested cell into a match anyone could play.
	///
	/// Writes the document rather than starting the match. Launch.Simulation is
	/// read once by PanelLoadScreen at process start, so a match cannot begin
	/// inside a process that is already running - fight-cell.sh takes this
	/// document and starts one.
	/// </summary>
	sealed class BattleFromPlanet : IUtilityCommand
	{
		string IUtilityCommand.Name => "--battle-from-planet";

		bool IUtilityCommand.ValidateArguments(string[] args)
		{
			return args.Length is >= 3 and <= 5;
		}

		[Desc("X Y [URL] [OUT]", "Build a battle-request-v1 document for one planet cell.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			var x = int.Parse(args[1], CultureInfo.InvariantCulture);
			var y = int.Parse(args[2], CultureInfo.InvariantCulture);
			var url = args.Length >= 4 ? args[3] : "http://localhost:8791/api/planet";
			var outPath = args.Length >= 5 ? args[4] : null;

			string body;
			try
			{
				using var client = HttpClientFactory.Create();
				using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(10));
				body = client.GetStringAsync(url, cancel.Token).GetAwaiter().GetResult();
			}
			catch (Exception e)
			{
				Console.WriteLine($"Could not reach {url}: {e.Message}");
				Console.WriteLine("Start the campaign with: python3 sim_server.py");
				Environment.Exit(1);
				return;
			}

			using var document = JsonDocument.Parse(body);
			var root = document.RootElement;

			if (root.GetProperty("schemaVersion").GetInt32() != 1)
			{
				Console.WriteLine("Unsupported planet-state schema version.");
				Environment.Exit(1);
				return;
			}

			var grid = root.GetProperty("grid");
			var latitudeCells = grid.GetProperty("latitudeCells").GetInt32();
			var longitudeCells = grid.GetProperty("longitudeCells").GetInt32();

			if (x < 0 || x >= longitudeCells || y < 0 || y >= latitudeCells)
			{
				Console.WriteLine($"Cell ({x},{y}) is outside the {longitudeCells}x{latitudeCells} grid.");
				Environment.Exit(1);
				return;
			}

			var index = y * longitudeCells + x;
			var cells = root.GetProperty("cells");
			var biome = Read(cells, "biome", index);
			var biomass = Read(cells, "biomass", index);
			var population = Read(cells, "population", index);
			var holders = cells.GetProperty("faction").EnumerateArray()
				.Select(value => value.GetInt32()).ToArray();
			if (holders.Length != latitudeCells * longitudeCells)
				throw new InvalidOperationException(
					$"cells.faction has {holders.Length} entries, expected {latitudeCells * longitudeCells}.");
			var holder = holders[index];

			var step = root.GetProperty("step").GetInt32();
			var planetId = root.GetProperty("planetId").GetString();

			var factionNames = new Dictionary<int, string>();
			foreach (var f in root.GetProperty("factions").EnumerateArray())
				factionNames[f.GetProperty("index").GetInt32()] = f.GetProperty("name").GetString();

			var holderName = holder >= 0 && factionNames.TryGetValue(holder, out var hn)
				? hn : "Unclaimed";
			var defender = BattleRequestBuilder.SelectDefender(
				holders, longitudeCells, latitudeCells, x, y);
			if (!defender.HasValue || !factionNames.TryGetValue(defender.Value, out var defenderName))
			{
				Console.WriteLine($"Cell ({x},{y}) is not on a contested faction border.");
				Environment.Exit(1);
				return;
			}

			// temperatureK is optional in planet-state-v1. Falling back to 288 K
			// keeps the request valid rather than refusing to build one when the
			// campaign is serving a frame without the overlay.
			var temperature = TryRead(cells, "temperatureK", index, out var t) ? t : 288.0;

			var request = BattleRequestBuilder.Build(
				planetId, step, x, y, latitudeCells, longitudeCells,
				biome, biomass, population,
				holder, holderName, defender.Value, defenderName, temperature);

			var json = request.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

			if (outPath != null)
			{
				Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
				File.WriteAllText(outPath, json);
				Console.WriteLine($"Wrote {outPath}");
			}
			else
				Console.WriteLine(json);
		}

		static int Read(JsonElement cells, string name, int index)
		{
			var array = cells.GetProperty(name);
			if (index >= array.GetArrayLength())
				throw new InvalidOperationException($"cells.{name} is shorter than the grid.");

			return array[index].GetInt32();
		}

		static bool TryRead(JsonElement cells, string name, int index, out double value)
		{
			value = 0;
			if (!cells.TryGetProperty(name, out var array) || index >= array.GetArrayLength())
				return false;

			value = array[index].GetDouble();
			return true;
		}
	}
}
