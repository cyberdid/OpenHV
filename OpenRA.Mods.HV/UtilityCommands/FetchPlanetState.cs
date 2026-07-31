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
using System.Linq;
using System.Text.Json;
using System.Threading;
using OpenRA.Support;

namespace OpenRA.Mods.HV.UtilityCommands
{
	/// <summary>
	/// Reads one planet frame from the campaign simulation and prints what came
	/// back.
	///
	/// This is the whole bridge between the two halves, with no graphics in the
	/// way: the campaign is floating-point Python and the battle is integer
	/// lockstep, and they meet over HTTP carrying planet-state-v1 one way and
	/// battle-request-v1 the other. Nothing served here enters a match - a
	/// battle is set up once from a request document and is deterministic from
	/// that point - so this channel can never desynchronise anything.
	///
	/// Verifiable without a renderer on purpose. If this prints a biome
	/// histogram that matches what the simulation says it produced, the bridge
	/// works, and everything after it is drawing.
	/// </summary>
	sealed class FetchPlanetState : IUtilityCommand
	{
		string IUtilityCommand.Name => "--fetch-planet";

		bool IUtilityCommand.ValidateArguments(string[] args)
		{
			return args.Length is 1 or 2;
		}

		static readonly string[] BiomeNames =
		[
			"ice", "tundra", "barrens", "steppe", "growth", "deep-growth", "scorched"
		];

		[Desc("[URL]", "Fetch a planet frame from the campaign simulation (default http://localhost:8765/api/planet).")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			var url = args.Length == 2 ? args[1] : "http://localhost:8765/api/planet";

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

			var version = root.GetProperty("schemaVersion").GetInt32();
			if (version != 1)
			{
				Console.WriteLine($"Unsupported planet-state schema version {version}.");
				Environment.Exit(1);
				return;
			}

			var grid = root.GetProperty("grid");
			var latitudeCells = grid.GetProperty("latitudeCells").GetInt32();
			var longitudeCells = grid.GetProperty("longitudeCells").GetInt32();
			var expected = latitudeCells * longitudeCells;

			var cells = root.GetProperty("cells");
			var biome = cells.GetProperty("biome").EnumerateArray().Select(v => v.GetInt32()).ToArray();
			var biomass = cells.GetProperty("biomass").EnumerateArray().Select(v => v.GetInt32()).ToArray();
			var faction = cells.GetProperty("faction").EnumerateArray().Select(v => v.GetInt32()).ToArray();

			// Length is the one thing worth checking hard. A short array would
			// draw a torn map rather than fail, and a torn map looks like an art
			// problem for as long as it takes to think of looking here.
			foreach (var (name, array) in new[]
			{
				("biome", biome), ("biomass", biomass), ("faction", faction),
			})
			{
				if (array.Length != expected)
				{
					Console.WriteLine($"cells.{name} has {array.Length} entries, expected {expected}.");
					Environment.Exit(1);
					return;
				}
			}

			Console.WriteLine($"{root.GetProperty("planetName").GetString()} at step {root.GetProperty("step").GetInt32()}");
			Console.WriteLine($"  grid       {longitudeCells} x {latitudeCells} = {expected} cells");

			var histogram = new Dictionary<int, int>();
			foreach (var value in biome)
				histogram[value] = histogram.GetValueOrDefault(value) + 1;

			Console.WriteLine("  terrain");
			foreach (var (code, count) in histogram.OrderBy(entry => entry.Key))
			{
				var label = code < BiomeNames.Length ? BiomeNames[code] : $"class-{code}";
				Console.WriteLine($"    {label,-12} {count,6}  {100.0 * count / expected,5:0.0}%");
			}

			Console.WriteLine($"  biomass    mean {biomass.Average() / 255.0,5:0.000} of standing capacity");

			Console.WriteLine("  fleets");
			foreach (var fleet in root.GetProperty("factions").EnumerateArray())
			{
				Console.WriteLine(
					$"    {fleet.GetProperty("name").GetString(),-12} " +
					$"#{fleet.GetProperty("colour").GetString()} " +
					$"{fleet.GetProperty("cells").GetInt32(),6} cells");
			}

			if (root.TryGetProperty("race", out var race))
			{
				Console.WriteLine(
					$"  race       woke at step {race.GetProperty("emergedStep").GetInt32()} " +
					$"in cell ({race.GetProperty("cradleLatitudeIndex").GetInt32()}," +
					$"{race.GetProperty("cradleLongitudeIndex").GetInt32()})");
				Console.WriteLine(
					$"             appetite {race.GetProperty("appetite").GetDouble():0.00} " +
					$"reciprocity {race.GetProperty("reciprocity").GetDouble():0.00} " +
					$"expectation {race.GetProperty("expectation").GetDouble():0.00}");
			}
			else
				Console.WriteLine("  race       nothing has woken yet");
		}
	}
}
