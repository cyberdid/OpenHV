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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.HV.Traits;

namespace OpenRA.Mods.HV
{
	public sealed class SimulationLifecycleMonitor
	{
		readonly SimulationConfig config;
		readonly HashSet<int> collapsedPlayerIndexes = [];
		readonly List<SimulationCollapsedFaction> collapsedFactions = [];
		ActivitySample previousSample;
		int lastCivilCollapseCheckTick = -250;
		bool initialized;

		public bool StalemateAdvisory { get; private set; }
		public int StalemateSinceTick { get; private set; }
		public int LastMeaningfulActivityTick { get; private set; }
		public int StalemateSequence { get; private set; }
		public bool AllFactionsCollapsed { get; private set; }

		public SimulationLifecycleMonitor(SimulationConfig config)
		{
			this.config = config;
		}

		public void Update(World world)
		{
			ObserveCollapses(world);
			var sample = ActivitySample.Build(world);
			if (!initialized)
			{
				initialized = true;
				previousSample = sample;
				LastMeaningfulActivityTick = world.WorldTick;
				return;
			}

			var activeWar = world.WorldActor.TraitOrDefault<DiplomacyManager>()?.Relations.Any(relation =>
				relation.State == DiplomaticRelationState.War) == true;
			if (sample != previousSample || activeWar || world.IsGameOver)
			{
				previousSample = sample;
				LastMeaningfulActivityTick = world.WorldTick;
				if (StalemateAdvisory)
				{
					StalemateAdvisory = false;
					StalemateSequence++;
				}

				return;
			}

			if (config.StalemateWindowTicks <= 0 ||
				world.WorldTick - LastMeaningfulActivityTick < config.StalemateWindowTicks ||
				StalemateAdvisory)
				return;

			StalemateAdvisory = true;
			StalemateSinceTick = world.WorldTick;
			StalemateSequence++;
		}

		public SimulationLifecycleResult BuildSnapshot()
		{
			return new SimulationLifecycleResult
			{
				ScenarioMode = config.ScenarioMode,
				ObservationHorizonTick = config.ObservationHorizonTicks,
				HardTickLimit = config.MaxWorldTicks,
				StalemateWindowTicks = config.StalemateWindowTicks,
				StalemateTerminates = config.StalemateTerminates,
				CollapsePopulationThreshold = config.CollapsePopulationThreshold,
				CollapseStabilityThreshold = config.CollapseStabilityThreshold,
				StalemateAdvisory = StalemateAdvisory,
				StalemateSinceTick = StalemateSinceTick,
				LastMeaningfulActivityTick = LastMeaningfulActivityTick,
				StalemateSequence = StalemateSequence,
				AllFactionsCollapsed = AllFactionsCollapsed,
				CollapsedFactions = collapsedFactions.ToArray()
			};
		}

		void ObserveCollapses(World world)
		{
			const int CivilCollapseCheckInterval = 250;
			var evaluateCivilCollapse =
				world.WorldTick - lastCivilCollapseCheckTick >= CivilCollapseCheckInterval;
			if (evaluateCivilCollapse)
				lastCivilCollapseCheckTick = world.WorldTick;

			foreach (var player in world.Players
				.Where(player => player.IsBot && !player.NonCombatant)
				.OrderBy(player => player.ClientIndex))
			{
				if (collapsedPlayerIndexes.Contains(player.ClientIndex))
					continue;

				var statistics = player.PlayerActor.TraitOrDefault<PlayerStatistics>();
				var engineLoss = player.WinState == WinState.Lost;
				if (!engineLoss && !evaluateCivilCollapse)
					continue;

				var (population, stability) = AggregateCivilization(world, player);
				var civilCollapse =
					evaluateCivilCollapse &&
					config.CollapsePopulationThreshold > 0 &&
					population <= config.CollapsePopulationThreshold &&
					stability <= config.CollapseStabilityThreshold;
				if (!engineLoss && !civilCollapse)
					continue;

				collapsedPlayerIndexes.Add(player.ClientIndex);
				collapsedFactions.Add(new SimulationCollapsedFaction
				{
					PlayerName = player.ResolvedPlayerName,
					BotType = player.BotType,
					Faction = player.Faction.InternalName,
					CollapseTick = world.WorldTick,
					Population = population,
					SurvivingAssetsValue = statistics?.AssetsValue ?? 0,
					ReasonCode = engineLoss ? "engine-loss" : "civil-threshold"
				});
			}

			var factions = world.Players
				.Where(player => player.IsBot && !player.NonCombatant)
				.ToArray();
			AllFactionsCollapsed = factions.Length > 0 &&
				factions.All(player => collapsedPlayerIndexes.Contains(player.ClientIndex));
		}

		static (int Population, int Stability) AggregateCivilization(World world, Player player)
		{
			var civilization = player.PlayerActor.TraitOrDefault<CivilizationState>();
			if (civilization == null)
				return (0, 0);

			var population = 0;
			long weightedStability = 0;
			foreach (var actor in civilization.Settlements(world, player))
			{
				var settlement = actor.Trait<SettlementCore>();
				population += settlement.Population;
				weightedStability += (long)settlement.Stability * settlement.Population;
			}

			return (population, population == 0 ? 0 : (int)(weightedStability / population));
		}

		readonly record struct ActivitySample(
			long Earned,
			long Spent,
			long Kills,
			long Deaths,
			long Army,
			long Assets,
			long Population,
			long Stocks,
			long Research,
			long Technologies,
			long TradeShipments)
		{
			public static ActivitySample Build(World world)
			{
				long earned = 0;
				long spent = 0;
				long kills = 0;
				long deaths = 0;
				long army = 0;
				long assets = 0;
				long population = 0;
				long stocks = 0;
				long research = 0;
				long technologies = 0;
				foreach (var player in world.Players.Where(player => player.IsBot && !player.NonCombatant))
				{
					var resources = player.PlayerActor.TraitOrDefault<PlayerResources>();
					var statistics = player.PlayerActor.TraitOrDefault<PlayerStatistics>();
					var civilization = player.PlayerActor.TraitOrDefault<CivilizationState>();
					earned += resources?.Earned ?? 0;
					spent += resources?.Spent ?? 0;
					kills += statistics?.KillsCost ?? 0;
					deaths += statistics?.DeathsCost ?? 0;
					army += statistics?.ArmyValue ?? 0;
					assets += statistics?.AssetsValue ?? 0;
					research += civilization?.ResearchProgress ?? 0;
					technologies += civilization?.CompletedTechnologyMask ?? 0;
					if (civilization == null)
						continue;

					foreach (var actor in civilization.Settlements(world, player))
					{
						var settlement = actor.Trait<SettlementCore>();
						population += settlement.Population;
						stocks += settlement.Food + settlement.Materials + settlement.Energy + settlement.Knowledge;
					}
				}

				var tradeShipments = world.WorldActor.TraitOrDefault<TradeManager>()?.Routes.Sum(route =>
					(long)route.ShipmentSequence) ?? 0;
				return new ActivitySample(
					earned,
					spent,
					kills,
					deaths,
					army,
					assets,
					population,
					stocks,
					research,
					technologies,
					tradeShipments);
			}
		}
	}
}
