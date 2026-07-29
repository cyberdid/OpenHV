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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using OpenRA.Mods.Common.Traits;

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
			SimulationConfig config,
			SimulationLifecycleResult lifecycle,
			SimulationEndReason endReason,
			string endDetail,
			DateTime startedUtc)
		{
			var players = world.Players
				.Where(player => player.IsBot && !player.NonCombatant)
				.Select(player =>
				{
					var stats = player.PlayerActor.TraitOrDefault<PlayerStatistics>();
					var resources = player.PlayerActor.TraitOrDefault<PlayerResources>();
					var client = world.LobbyInfo.ClientWithIndex(player.ClientIndex);
					var score = (stats?.KillsCost ?? 0) -
						(stats?.DeathsCost ?? 0) +
						(stats?.ArmyValue ?? 0) +
						(stats?.AssetsValue ?? 0) +
						(resources?.GetCashAndResources() ?? 0) +
						(stats?.Experience ?? 0) * 100;

					return new SimulationPlayerResult
					{
						PlayerName = player.ResolvedPlayerName,
						Slot = client?.Slot,
						BotType = player.BotType,
						Faction = player.Faction.InternalName,
						Team = client?.Team ?? 0,
						Color = player.Color.ToString(),
						SpawnPoint = player.SpawnPoint,
						HomeCellX = player.HomeLocation.X,
						HomeCellY = player.HomeLocation.Y,
						Outcome = player.WinState.ToString().ToLowerInvariant(),
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
						Spent = resources?.Spent ?? 0,
						Civilization = SimulationCivilizationSnapshotBuilder.Build(world, player)
					};
				})
				.OrderByDescending(player => player.Outcome == WinState.Won.ToString().ToLowerInvariant())
				.ThenByDescending(player => player.Score)
				.ThenBy(player => player.PlayerName, StringComparer.Ordinal)
				.ToArray();

			config.Players = players
				.OrderBy(player => player.Slot, StringComparer.Ordinal)
				.Select(player => new SimulationPlayerConfig
				{
					Slot = player.Slot,
					PlayerName = player.PlayerName,
					BotType = player.BotType,
					Faction = player.Faction,
					Team = player.Team,
					Color = player.Color,
					SpawnPoint = player.SpawnPoint,
					HomeCellX = player.HomeCellX,
					HomeCellY = player.HomeCellY
				})
				.ToArray();

			var naturalWinners = players
				.Where(player => player.Outcome == WinState.Won.ToString().ToLowerInvariant())
				.Select(ToLeader)
				.ToArray();
			var scoreLeader = players
				.OrderByDescending(player => player.Score)
				.ThenBy(player => player.PlayerName, StringComparer.Ordinal)
				.FirstOrDefault();
			var result = new SimulationResult
			{
				SchemaVersion = SimulationConfig.CurrentSchemaVersion,
				Build = new SimulationBuildMetadata
				{
					EngineVersion = Game.EngineVersion,
					ModId = Game.ModData.Manifest.Id,
					ModVersion = Game.ModData.Manifest.Metadata.Version,
					GitCommit = config.GitCommit,
					GitDirty = config.GitDirty,
					ExecutionMode = Game.IsHeadless ? "headless" : "graphical"
				},
				Config = config,
				EndReason = endReason.ToIdentifier(),
				EndDetail = endDetail,
				StartedUtc = startedUtc,
				EndedUtc = DateTime.UtcNow,
				WorldTick = world.WorldTick,
				SimulatedSeconds = world.WorldTick * world.Timestep / 1000d,
				SynchronizedStateHash = unchecked((uint)world.SyncHash()).ToString(
					"X8",
					CultureInfo.InvariantCulture),
				NaturalWinners = naturalWinners,
				ScoreLeader = scoreLeader != null ? ToLeader(scoreLeader) : null,
				Players = players,
				Diplomacy = SimulationDiplomacySnapshotBuilder.Build(world),
				TradeRoutes = SimulationTradeSnapshotBuilder.Build(world),
				Lifecycle = lifecycle
			};

			var directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directory))
				Directory.CreateDirectory(directory);

			var temporaryPath = path + $".tmp-{Environment.ProcessId}-{Guid.NewGuid():N}";
			try
			{
				File.WriteAllText(temporaryPath, JsonSerializer.Serialize(result, JsonOptions));
				File.Move(temporaryPath, path, true);
			}
			finally
			{
				if (File.Exists(temporaryPath))
					File.Delete(temporaryPath);
			}

			Console.WriteLine($"Simulation result written to {path}.");
		}

		static SimulationLeader ToLeader(SimulationPlayerResult player)
		{
			return new SimulationLeader
			{
				PlayerName = player.PlayerName,
				BotType = player.BotType,
				Faction = player.Faction
			};
		}
	}
}
