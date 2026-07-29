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
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.HV
{
	public sealed class SimulationConfig
	{
		public const int CurrentSchemaVersion = 1;
		public const int DefaultMaxWorldTicks = 1500;
		public const int DefaultWatchdogSeconds = 120;

		public int SchemaVersion { get; init; } = CurrentSchemaVersion;
		public string MatchId { get; init; }
		public string MapRequest { get; init; }
		public string MapUid { get; init; }
		public string MapTitle { get; init; }
		public string MapHash { get; init; }
		public string[] BotTypes { get; init; }
		public bool Headless { get; init; }
		public bool DeterministicSimulation { get; init; }
		public string GameSpeed { get; init; }
		public int GameTimestepMilliseconds { get; init; }
		public int? RequestedRandomSeed { get; init; }
		public int EffectiveRandomSeed { get; set; }
		public int MaxWorldTicks { get; init; }
		public int WatchdogSeconds { get; init; }
		public int TelemetryIntervalTicks { get; init; }
		public string CivilizationProfile { get; init; }
		public bool TradeEnabled { get; init; }
		public string GitCommit { get; init; }
		public bool GitDirty { get; init; }
		public string ResultPath { get; init; }
		public SimulationPlayerConfig[] Players { get; set; } = [];

		public static SimulationConfig Parse(Arguments args, MapPreview map)
		{
			if (map.Status != MapStatus.Available)
				throw new ArgumentException($"Simulation map '{map.Uid}' is not available.");

			var botType = args.GetValue("Launch.SimulationBot", "rogue");
			var botTypes = args.GetValue("Launch.SimulationBots", botType)
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			if (botTypes.Length == 0)
				throw new ArgumentException("Launch.SimulationBots must specify at least one bot type.");

			var availableBotTypes = new HashSet<string>(
				map.PlayerActorInfo.TraitInfos<IBotInfo>().Select(info => info.Type),
				StringComparer.Ordinal);
			var unknownBotTypes = botTypes.Where(type => !availableBotTypes.Contains(type)).Distinct().ToArray();
			if (unknownBotTypes.Length > 0)
				throw new ArgumentException(
					$"Unknown simulation bot type(s): {string.Join(", ", unknownBotTypes)}. " +
					$"Available types: {string.Join(", ", availableBotTypes.OrderBy(type => type))}.");

			var gameSpeed = args.GetValue("Launch.SimulationSpeed", "fastest");
			var gameSpeeds = Game.ModData.GetOrCreate<GameSpeeds>();
			if (!gameSpeeds.Speeds.TryGetValue(gameSpeed, out var speed))
				throw new ArgumentException(
					$"Unknown simulation speed '{gameSpeed}'. " +
					$"Available speeds: {string.Join(", ", gameSpeeds.Speeds.Keys.OrderBy(name => name))}.");

			var randomSeed = ParseOptionalInt(args, "Launch.SimulationSeed");
			var maxWorldTicks = ParseOptionalNonNegativeInt(args, "Launch.SimulationMaxTicks");
			var legacyDurationSeconds = ParseOptionalNonNegativeInt(args, "Launch.SimulationDuration");
			if (!maxWorldTicks.HasValue && legacyDurationSeconds > 0)
			{
				var ticks = (long)legacyDurationSeconds.Value * 1000 + speed.Timestep - 1;
				maxWorldTicks = checked((int)(ticks / speed.Timestep));
			}

			maxWorldTicks ??= DefaultMaxWorldTicks;
			if (maxWorldTicks <= 0)
				throw new ArgumentException("Launch.SimulationMaxTicks must be greater than zero.");

			var watchdogSeconds =
				ParseOptionalNonNegativeInt(args, "Launch.SimulationWatchdogSeconds") ?? DefaultWatchdogSeconds;
			var telemetryIntervalTicks =
				ParseOptionalNonNegativeInt(args, "Launch.SimulationTelemetryIntervalTicks") ?? 0;
			var civilizationProfile = args.GetValue(
				"Launch.SimulationCivilizationProfile",
				Traits.CivilizationScenarioInfo.Balanced);
			if (civilizationProfile != Traits.CivilizationScenarioInfo.Balanced &&
				civilizationProfile != Traits.CivilizationScenarioInfo.Scarcity &&
				civilizationProfile != Traits.CivilizationScenarioInfo.Trade)
				throw new ArgumentException(
					"Launch.SimulationCivilizationProfile must be 'balanced', 'scarcity', or 'trade', " +
					$"but was '{civilizationProfile}'.");
			var tradeEnabledText = args.GetValue("Launch.SimulationTradeEnabled", "true");
			if (!bool.TryParse(tradeEnabledText, out var tradeEnabled))
				throw new ArgumentException(
					$"Launch.SimulationTradeEnabled must be 'true' or 'false', but was '{tradeEnabledText}'.");

			var gitDirtyText = args.GetValue("Launch.SimulationGitDirty", "false");
			if (!bool.TryParse(gitDirtyText, out var gitDirty))
				throw new ArgumentException(
					$"Launch.SimulationGitDirty must be 'true' or 'false', but was '{gitDirtyText}'.");

			var headlessText = args.GetValue("Engine.Headless", "false");
			if (!bool.TryParse(headlessText, out var headless))
				throw new ArgumentException(
					$"Engine.Headless must be 'true' or 'false', but was '{headlessText}'.");

			var deterministicSimulationText = args.GetValue("Engine.DeterministicSimulation", "false");
			if (!bool.TryParse(deterministicSimulationText, out var deterministicSimulation))
				throw new ArgumentException(
					"Engine.DeterministicSimulation must be 'true' or 'false', " +
					$"but was '{deterministicSimulationText}'.");

			var matchId = args.GetValue("Launch.SimulationMatchId", "simulation");
			if (string.IsNullOrWhiteSpace(matchId))
				throw new ArgumentException("Launch.SimulationMatchId must not be empty.");

			var gitCommit = args.GetValue("Launch.SimulationGitCommit", "unknown");
			if (string.IsNullOrWhiteSpace(gitCommit))
				throw new ArgumentException("Launch.SimulationGitCommit must not be empty.");

			return new SimulationConfig
			{
				MatchId = matchId,
				MapRequest = args.GetValue("Launch.Map", map.Uid),
				MapUid = map.Uid,
				MapTitle = map.Title,
				MapHash = map.Uid,
				BotTypes = botTypes,
				Headless = headless,
				DeterministicSimulation = deterministicSimulation,
				GameSpeed = gameSpeed,
				GameTimestepMilliseconds = speed.Timestep,
				RequestedRandomSeed = randomSeed,
				MaxWorldTicks = maxWorldTicks.Value,
				WatchdogSeconds = watchdogSeconds,
				TelemetryIntervalTicks = telemetryIntervalTicks,
				CivilizationProfile = civilizationProfile,
				TradeEnabled = tradeEnabled,
				GitCommit = gitCommit,
				GitDirty = gitDirty,
				ResultPath = args.GetValue("Launch.SimulationResult", "")
			};
		}

		static int? ParseOptionalInt(Arguments args, string key)
		{
			var value = args.GetValue(key, "");
			if (string.IsNullOrWhiteSpace(value))
				return null;

			if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
				throw new ArgumentException($"{key} must be a 32-bit integer, but was '{value}'.");

			return parsed;
		}

		static int? ParseOptionalNonNegativeInt(Arguments args, string key)
		{
			var parsed = ParseOptionalInt(args, key);
			if (parsed < 0)
				throw new ArgumentException($"{key} must not be negative.");

			return parsed;
		}
	}

	public sealed class SimulationPlayerConfig
	{
		public string Slot { get; init; }
		public string PlayerName { get; init; }
		public string BotType { get; init; }
		public string Faction { get; init; }
		public int Team { get; init; }
		public string Color { get; init; }
		public int SpawnPoint { get; init; }
		public int HomeCellX { get; init; }
		public int HomeCellY { get; init; }
	}
}
