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
using System.IO;
using System.Linq;
using System.Text.Json;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.HV
{
	public static class SimulationResultWriter
	{
		static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = true
		};

		public static void Write(
			string path,
			World world,
			string mapTitle,
			int? randomSeed,
			bool timedOut,
			DateTime startedUtc)
		{
			var players = world.Players
				.Where(player => player.IsBot && !player.NonCombatant)
				.Select(player =>
				{
					var stats = player.PlayerActor.TraitOrDefault<PlayerStatistics>();
					var resources = player.PlayerActor.TraitOrDefault<PlayerResources>();
					var score = (stats?.KillsCost ?? 0) -
						(stats?.DeathsCost ?? 0) +
						(stats?.ArmyValue ?? 0) +
						(stats?.AssetsValue ?? 0) +
						(resources?.GetCashAndResources() ?? 0) +
						(stats?.Experience ?? 0) * 100;

					return new
					{
						Name = player.ResolvedPlayerName,
						BotType = player.BotType,
						Faction = player.Faction.InternalName,
						Outcome = player.WinState.ToString(),
						Score = score,
						Experience = stats?.Experience ?? 0,
						KillsValue = stats?.KillsCost ?? 0,
						DeathsValue = stats?.DeathsCost ?? 0,
						UnitsKilled = stats?.UnitsKilled ?? 0,
						UnitsLost = stats?.UnitsDead ?? 0,
						BuildingsKilled = stats?.BuildingsKilled ?? 0,
						BuildingsLost = stats?.BuildingsDead ?? 0,
						ArmyValue = stats?.ArmyValue ?? 0,
						AssetsValue = stats?.AssetsValue ?? 0,
						CashAndResources = resources?.GetCashAndResources() ?? 0,
						Earned = resources?.Earned ?? 0,
						Spent = resources?.Spent ?? 0
					};
				})
				.OrderByDescending(player => player.Outcome == WinState.Won.ToString())
				.ThenByDescending(player => player.Score)
				.ToArray();

			var winner = players.FirstOrDefault();
			var result = new
			{
				Map = mapTitle,
				RandomSeed = randomSeed,
				TimedOut = timedOut,
				StartedUtc = startedUtc,
				EndedUtc = DateTime.UtcNow,
				WorldTick = world.WorldTick,
				SimulatedSeconds = world.WorldTick * world.Timestep / 1000d,
				Winner = winner?.BotType,
				Players = players
			};

			var directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directory))
				Directory.CreateDirectory(directory);

			File.WriteAllText(path, JsonSerializer.Serialize(result, JsonOptions));
			Console.WriteLine($"Simulation result written to {path}.");
		}
	}
}
