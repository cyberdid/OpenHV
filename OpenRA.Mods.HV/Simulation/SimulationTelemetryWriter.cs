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
		readonly SimulationLifecycleMonitor lifecycle;
		readonly StreamWriter telemetry;
		readonly StreamWriter events;
		readonly Dictionary<string, ObservedSettlement> observedSettlements = new(StringComparer.Ordinal);
		readonly Dictionary<string, HashSet<string>> observedTechnologies = new(StringComparer.Ordinal);
		readonly Dictionary<string, int> observedStrategySequences = new(StringComparer.Ordinal);
		readonly Dictionary<string, int> observedPlanSequences = new(StringComparer.Ordinal);
		readonly Dictionary<string, int> observedPlannerRequestSequences = new(StringComparer.Ordinal);
		readonly Dictionary<string, int> observedCombatDecisionSequences = new(StringComparer.Ordinal);
		readonly Dictionary<string, int> observedDiplomacySequences = new(StringComparer.Ordinal);
		readonly Dictionary<string, int> observedTradeStatusSequences = new(StringComparer.Ordinal);
		readonly Dictionary<string, int> observedTradeShipmentSequences = new(StringComparer.Ordinal);
		readonly HashSet<string> observedCollapsedFactions = new(StringComparer.Ordinal);
		int observedStalemateSequence;
		bool completed;

		public int LastSnapshotTick { get; private set; } = -1;
		public string TelemetryPath { get; }
		public string EventsPath { get; }

		public SimulationTelemetryWriter(SimulationConfig config, SimulationLifecycleMonitor lifecycle)
		{
			this.config = config;
			this.lifecycle = lifecycle;
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
			var diplomacy = SimulationDiplomacySnapshotBuilder.Build(world);
			var tradeRoutes = SimulationTradeSnapshotBuilder.Build(world);
			var lifecycleSnapshot = lifecycle.BuildSnapshot();

			WriteLine(telemetry, new SimulationTelemetryRecord
			{
				SchemaVersion = SchemaVersion,
				RecordType = "snapshot",
				MatchId = config.MatchId,
				WorldTick = world.WorldTick,
				SynchronizedStateHash = unchecked((uint)world.SyncHash()).ToString(
					"X8",
					CultureInfo.InvariantCulture),
				Players = players,
				Diplomacy = diplomacy,
				TradeRoutes = tradeRoutes,
				Lifecycle = lifecycleSnapshot
			});

			foreach (var player in players)
			{
				ObserveSettlements(world.WorldTick, player);
				ObserveTechnologies(world.WorldTick, player);
				ObserveStrategy(world.WorldTick, player);
				ObservePlan(world.WorldTick, player);
				ObserveCombatDecision(world.WorldTick, player);
			}

			ObserveDiplomacy(world.WorldTick, diplomacy);
			ObserveTrade(world.WorldTick, tradeRoutes);
			ObserveLifecycle(world.WorldTick, lifecycleSnapshot);

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

		void ObserveTechnologies(int worldTick, SimulationTelemetryPlayer player)
		{
			if (!observedTechnologies.TryGetValue(player.PlayerName, out var previous))
			{
				observedTechnologies.Add(
					player.PlayerName,
					player.Civilization.CompletedTechnologies.ToHashSet(StringComparer.Ordinal));
				return;
			}

			foreach (var technology in player.Civilization.CompletedTechnologies.Where(t => !previous.Contains(t)))
			{
				WriteEvent(new SimulationEventRecord
				{
					SchemaVersion = SchemaVersion,
					RecordType = "event",
					MatchId = config.MatchId,
					WorldTick = worldTick,
					EventType = "technology-completed",
					ReasonCode = technology,
					PlayerName = player.PlayerName,
					Value = player.Civilization.CompletedTechnologies.Length
				});
				previous.Add(technology);
			}
		}

		void ObserveStrategy(int worldTick, SimulationTelemetryPlayer player)
		{
			if (observedStrategySequences.TryGetValue(player.PlayerName, out var sequence) &&
				sequence == player.Civilization.StrategySequence)
				return;

			observedStrategySequences[player.PlayerName] = player.Civilization.StrategySequence;
			WriteEvent(new SimulationEventRecord
			{
				SchemaVersion = SchemaVersion,
				RecordType = "event",
				MatchId = config.MatchId,
				WorldTick = worldTick,
				EventType = "strategy-transition",
				ReasonCode = "utility-selection",
				PlayerName = player.PlayerName,
				Strategy = player.Civilization.Strategy,
				Value = player.Civilization.WarUtility,
				PreviousValue = player.Civilization.StrategySequence
			});
		}

		void ObservePlan(int worldTick, SimulationTelemetryPlayer player)
		{
			if (!observedPlanSequences.TryGetValue(player.PlayerName, out var planSequence) ||
				planSequence != player.Civilization.PlanSequence)
			{
				observedPlanSequences[player.PlayerName] = player.Civilization.PlanSequence;
				WriteEvent(new SimulationEventRecord
				{
					SchemaVersion = SchemaVersion,
					RecordType = "event",
					MatchId = config.MatchId,
					WorldTick = worldTick,
					EventType = "planner-transition",
					ReasonCode = player.Civilization.PlanReason,
					PlayerName = player.PlayerName,
					Plan = player.Civilization.Plan,
					Value = player.Civilization.PlanSequence,
					PreviousValue = player.Civilization.PlanTransitionTick
				});
			}

			if (player.Civilization.PlannerRequestSequence <= 0 ||
				(observedPlannerRequestSequences.TryGetValue(
					player.PlayerName,
					out var requestSequence) &&
					requestSequence == player.Civilization.PlannerRequestSequence))
				return;

			observedPlannerRequestSequences[player.PlayerName] =
				player.Civilization.PlannerRequestSequence;
			WriteEvent(new SimulationEventRecord
			{
				SchemaVersion = SchemaVersion,
				RecordType = "event",
				MatchId = config.MatchId,
				WorldTick = worldTick,
				EventType = "planner-request",
				ReasonCode = player.Civilization.PlanReason,
				PlayerName = player.PlayerName,
				Plan = player.Civilization.Plan,
				ActorType = player.Civilization.LastPlannerRequestActor,
				Value = player.Civilization.PlannerRequestSequence,
				PreviousValue = player.Civilization.LastPlannerRequestTick
			});
		}

		void ObserveCombatDecision(int worldTick, SimulationTelemetryPlayer player)
		{
			if (player.Civilization.CombatDecisionSequence <= 0 ||
				(observedCombatDecisionSequences.TryGetValue(
					player.PlayerName,
					out var sequence) &&
					sequence == player.Civilization.CombatDecisionSequence))
				return;

			observedCombatDecisionSequences[player.PlayerName] =
				player.Civilization.CombatDecisionSequence;
			WriteEvent(new SimulationEventRecord
			{
				SchemaVersion = SchemaVersion,
				RecordType = "event",
				MatchId = config.MatchId,
				WorldTick = worldTick,
				EventType = "combat-decision",
				ReasonCode = player.Civilization.LastCombatDecisionReason,
				PlayerName = player.PlayerName,
				CombatDecision = player.Civilization.LastCombatDecision,
				SquadType = player.Civilization.LastCombatSquadType,
				ActorId = player.Civilization.LastCombatTargetActorId,
				UnitCount = player.Civilization.LastCombatUnitCount,
				Value = player.Civilization.LastCombatOwnValue,
				PreviousValue = player.Civilization.LastCombatEnemyValue
			});
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

		void ObserveDiplomacy(int worldTick, SimulationDiplomaticRelation[] relations)
		{
			foreach (var relation in relations)
			{
				if (observedDiplomacySequences.TryGetValue(relation.RelationId, out var sequence) &&
					sequence == relation.TransitionSequence)
					continue;

				observedDiplomacySequences[relation.RelationId] = relation.TransitionSequence;
				WriteEvent(new SimulationEventRecord
				{
					SchemaVersion = SchemaVersion,
					RecordType = "event",
					MatchId = config.MatchId,
					WorldTick = worldTick,
					EventType = "diplomacy-transition",
					ReasonCode = relation.ReasonCode,
					PlayerName = relation.PlayerA,
					OtherPlayerName = relation.PlayerB,
					RelationId = relation.RelationId,
					RelationState = relation.State,
					Value = relation.Trust
				});
			}
		}

		void ObserveTrade(int worldTick, SimulationTradeRoute[] routes)
		{
			foreach (var route in routes)
			{
				if (!observedTradeStatusSequences.TryGetValue(route.RouteId, out var statusSequence) ||
					statusSequence != route.StatusSequence)
				{
					observedTradeStatusSequences[route.RouteId] = route.StatusSequence;
					WriteEvent(new SimulationEventRecord
					{
						SchemaVersion = SchemaVersion,
						RecordType = "event",
						MatchId = config.MatchId,
						WorldTick = worldTick,
						EventType = "trade-route-state",
						ReasonCode = route.StatusReason,
						PlayerName = route.PlayerA,
						OtherPlayerName = route.PlayerB,
						TradeRouteId = route.RouteId,
						RouteStatus = route.Status,
						Value = route.Risk
					});
				}

				if (route.ShipmentSequence <= 0 ||
					(observedTradeShipmentSequences.TryGetValue(route.RouteId, out var shipmentSequence) &&
						shipmentSequence == route.ShipmentSequence))
					continue;

				observedTradeShipmentSequences[route.RouteId] = route.ShipmentSequence;
				var importer = route.LastExporter == route.PlayerA ? route.PlayerB : route.PlayerA;
				WriteEvent(new SimulationEventRecord
				{
					SchemaVersion = SchemaVersion,
					RecordType = "event",
					MatchId = config.MatchId,
					WorldTick = worldTick,
					EventType = "trade-shipment",
					ReasonCode = "stock-surplus-demand",
					PlayerName = route.LastExporter,
					OtherPlayerName = importer,
					TradeRouteId = route.RouteId,
					RouteStatus = route.Status,
					ResourceType = route.LastResource,
					Amount = route.LastAmount,
					Value = route.ShipmentSequence
				});
			}
		}

		void ObserveLifecycle(int worldTick, SimulationLifecycleResult snapshot)
		{
			foreach (var collapse in snapshot.CollapsedFactions)
			{
				if (!observedCollapsedFactions.Add(collapse.PlayerName))
					continue;

				WriteEvent(new SimulationEventRecord
				{
					SchemaVersion = SchemaVersion,
					RecordType = "event",
					MatchId = config.MatchId,
					WorldTick = worldTick,
					EventType = "faction-collapsed",
					ReasonCode = collapse.ReasonCode,
					PlayerName = collapse.PlayerName,
					Value = collapse.Population,
					PreviousValue = collapse.SurvivingAssetsValue
				});
			}

			if (snapshot.StalemateSequence <= 0 ||
				snapshot.StalemateSequence == observedStalemateSequence)
				return;

			observedStalemateSequence = snapshot.StalemateSequence;
			WriteEvent(new SimulationEventRecord
			{
				SchemaVersion = SchemaVersion,
				RecordType = "event",
				MatchId = config.MatchId,
				WorldTick = worldTick,
				EventType = snapshot.StalemateAdvisory ? "stalemate-advisory" : "stalemate-cleared",
				ReasonCode = snapshot.StalemateAdvisory
					? "no-meaningful-progress-window"
					: "meaningful-progress-resumed",
				Value = snapshot.LastMeaningfulActivityTick,
				PreviousValue = snapshot.StalemateSinceTick
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
		public SimulationDiplomaticRelation[] Diplomacy { get; init; }
		public SimulationTradeRoute[] TradeRoutes { get; init; }
		public SimulationLifecycleResult Lifecycle { get; init; }
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
		public string OtherPlayerName { get; init; }
		public string RelationId { get; init; }
		public string RelationState { get; init; }
		public string TradeRouteId { get; init; }
		public string RouteStatus { get; init; }
		public string ResourceType { get; init; }
		public int? Amount { get; init; }
		public string Strategy { get; init; }
		public string Plan { get; init; }
		public string ActorType { get; init; }
		public string CombatDecision { get; init; }
		public string SquadType { get; init; }
		public int? ActorId { get; init; }
		public int? UnitCount { get; init; }
	}
}
