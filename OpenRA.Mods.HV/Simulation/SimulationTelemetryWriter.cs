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
using System.Text.Json.Serialization;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.HV
{
	public sealed class SimulationTelemetryWriter : IDisposable
	{
		const int SchemaVersion = 1;
		const int ShortageThreshold = 750;

		static readonly JsonSerializerOptions JsonOptions = new()
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		readonly SimulationConfig config;
		readonly StreamWriter telemetry;
		readonly StreamWriter events;
		readonly Dictionary<string, ObservedSettlement> observedSettlements = new(StringComparer.Ordinal);
		bool completed;

		public int LastSnapshotTick { get; private set; } = -1;
		public string TelemetryPath { get; }
		public string EventsPath { get; }

		public SimulationTelemetryWriter(SimulationConfig config)
		{
			this.config = config;
			var directory = Path.GetDirectoryName(config.ResultPath);
			if (string.IsNullOrEmpty(directory))
				directory = ".";

			Directory.CreateDirectory(directory);
			TelemetryPath = Path.Combine(directory, "telemetry.jsonl");
			EventsPath = Path.Combine(directory, "events.jsonl");
			telemetry = CreateWriter(TelemetryPath);
			events = CreateWriter(EventsPath);
		}

		public void Start(World world)
		{
			WriteEvent(new SimulationEventRecord
			{
				SchemaVersion = SchemaVersion,
				RecordType = "event",
				MatchId = config.MatchId,
				WorldTick = world.WorldTick,
				EventType = "match-start",
				ReasonCode = "simulation-started",
				MapUid = config.MapUid,
				RandomSeed = config.EffectiveRandomSeed
			});
			Capture(world);
		}

		public void Capture(World world)
		{
			if (completed || LastSnapshotTick == world.WorldTick)
				return;

			var players = world.Players
				.Where(player => player.IsBot && !player.NonCombatant)
				.OrderBy(player => player.ClientIndex)
				.Select(player =>
				{
					var stats = player.PlayerActor.TraitOrDefault<PlayerStatistics>();
					var resources = player.PlayerActor.TraitOrDefault<PlayerResources>();
					var power = player.PlayerActor.TraitOrDefault<PowerManager>();
					return new SimulationTelemetryPlayer
					{
						PlayerName = player.ResolvedPlayerName,
						Faction = player.Faction.InternalName,
						Outcome = player.WinState.ToString().ToLowerInvariant(),
						CashAndResources = resources?.GetCashAndResources() ?? 0,
						Earned = resources?.Earned ?? 0,
						Spent = resources?.Spent ?? 0,
						PowerProvided = power?.PowerProvided ?? 0,
						PowerDrained = power?.PowerDrained ?? 0,
						ArmyValue = stats?.ArmyValue ?? 0,
						AssetsValue = stats?.AssetsValue ?? 0,
						KillsValue = stats?.KillsCost ?? 0,
						DeathsValue = stats?.DeathsCost ?? 0,
						Civilization = SimulationCivilizationSnapshotBuilder.Build(world, player)
					};
				})
				.ToArray();

			WriteLine(telemetry, new SimulationTelemetryRecord
			{
				SchemaVersion = SchemaVersion,
				RecordType = "snapshot",
				MatchId = config.MatchId,
				WorldTick = world.WorldTick,
				SynchronizedStateHash = unchecked((uint)world.SyncHash()).ToString(
					"X8",
					CultureInfo.InvariantCulture),
				Players = players
			});

			foreach (var player in players)
				ObserveSettlements(world.WorldTick, player);

			LastSnapshotTick = world.WorldTick;
		}

		public void Complete(World world, SimulationEndReason endReason)
		{
			if (completed)
				return;

			Capture(world);
			WriteEvent(new SimulationEventRecord
			{
				SchemaVersion = SchemaVersion,
				RecordType = "event",
				MatchId = config.MatchId,
				WorldTick = world.WorldTick,
				EventType = "match-end",
				ReasonCode = endReason.ToIdentifier(),
				SynchronizedStateHash = unchecked((uint)world.SyncHash()).ToString(
					"X8",
					CultureInfo.InvariantCulture)
			});
			completed = true;
			Dispose();
		}

		void ObserveSettlements(int worldTick, SimulationTelemetryPlayer player)
		{
			foreach (var settlement in player.Civilization.Settlements)
			{
				if (!observedSettlements.TryGetValue(settlement.SettlementId, out var previous))
				{
					observedSettlements.Add(settlement.SettlementId, ObservedSettlement.From(settlement));
					WriteSettlementEvent(
						worldTick,
						player.PlayerName,
						settlement,
						"settlement-founded",
						"capital-created",
						settlement.Population,
						null);
					continue;
				}

				if (settlement.Population != previous.Population)
					WriteSettlementEvent(
						worldTick,
						player.PlayerName,
						settlement,
						"population-changed",
						settlement.Population > previous.Population ? "natural-growth" : "mortality",
						settlement.Population,
						previous.Population);

				ObserveNeed(
					worldTick,
					player.PlayerName,
					settlement,
					"food",
					settlement.FoodSatisfaction,
					previous.FoodShortage);
				ObserveNeed(
					worldTick,
					player.PlayerName,
					settlement,
					"housing",
					settlement.HousingSatisfaction,
					previous.HousingShortage);
				ObserveNeed(
					worldTick,
					player.PlayerName,
					settlement,
					"energy",
					settlement.EnergySatisfaction,
					previous.EnergyShortage);

				observedSettlements[settlement.SettlementId] = ObservedSettlement.From(settlement);
			}
		}

		void ObserveNeed(
			int worldTick,
			string playerName,
			SimulationSettlementResult settlement,
			string need,
			int satisfaction,
			bool previousShortage)
		{
			var shortage = satisfaction < ShortageThreshold;
			if (shortage == previousShortage)
				return;

			WriteSettlementEvent(
				worldTick,
				playerName,
				settlement,
				shortage ? "shortage-started" : "shortage-resolved",
				$"{need}-shortage",
				satisfaction,
				null);
		}

		void WriteSettlementEvent(
			int worldTick,
			string playerName,
			SimulationSettlementResult settlement,
			string eventType,
			string reasonCode,
			int value,
			int? previousValue)
		{
			WriteEvent(new SimulationEventRecord
			{
				SchemaVersion = SchemaVersion,
				RecordType = "event",
				MatchId = config.MatchId,
				WorldTick = worldTick,
				EventType = eventType,
				PlayerName = playerName,
				SettlementId = settlement.SettlementId,
				ReasonCode = reasonCode,
				Value = value,
				PreviousValue = previousValue
			});
		}

		void WriteEvent(SimulationEventRecord record)
		{
			WriteLine(events, record);
		}

		static StreamWriter CreateWriter(string path)
		{
			return new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
			{
				AutoFlush = true
			};
		}

		static void WriteLine<T>(StreamWriter writer, T value)
		{
			writer.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
		}

		public void Dispose()
		{
			telemetry.Dispose();
			events.Dispose();
		}

		sealed class ObservedSettlement
		{
			public int Population { get; init; }
			public bool FoodShortage { get; init; }
			public bool HousingShortage { get; init; }
			public bool EnergyShortage { get; init; }

			public static ObservedSettlement From(SimulationSettlementResult settlement)
			{
				return new ObservedSettlement
				{
					Population = settlement.Population,
					FoodShortage = settlement.FoodSatisfaction < ShortageThreshold,
					HousingShortage = settlement.HousingSatisfaction < ShortageThreshold,
					EnergyShortage = settlement.EnergySatisfaction < ShortageThreshold
				};
			}
		}
	}

	public sealed class SimulationTelemetryRecord
	{
		public int SchemaVersion { get; init; }
		public string RecordType { get; init; }
		public string MatchId { get; init; }
		public int WorldTick { get; init; }
		public string SynchronizedStateHash { get; init; }
		public SimulationTelemetryPlayer[] Players { get; init; }
	}

	public sealed class SimulationTelemetryPlayer
	{
		public string PlayerName { get; init; }
		public string Faction { get; init; }
		public string Outcome { get; init; }
		public int CashAndResources { get; init; }
		public int Earned { get; init; }
		public int Spent { get; init; }
		public int PowerProvided { get; init; }
		public int PowerDrained { get; init; }
		public int ArmyValue { get; init; }
		public int AssetsValue { get; init; }
		public int KillsValue { get; init; }
		public int DeathsValue { get; init; }
		public SimulationCivilizationResult Civilization { get; init; }
	}

	public sealed class SimulationEventRecord
	{
		public int SchemaVersion { get; init; }
		public string RecordType { get; init; }
		public string MatchId { get; init; }
		public int WorldTick { get; init; }
		public string EventType { get; init; }
		public string ReasonCode { get; init; }
		public string PlayerName { get; init; }
		public string SettlementId { get; init; }
		public int? Value { get; init; }
		public int? PreviousValue { get; init; }
		public string MapUid { get; init; }
		public int? RandomSeed { get; init; }
		public string SynchronizedStateHash { get; init; }
	}
}
