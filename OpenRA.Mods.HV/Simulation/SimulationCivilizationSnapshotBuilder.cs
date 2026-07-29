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
using System.Linq;
using OpenRA.Mods.HV.Traits;

namespace OpenRA.Mods.HV
{
	public static class SimulationCivilizationSnapshotBuilder
	{
		public static SimulationCivilizationResult Build(World world, Player player)
		{
			var civilization = player.PlayerActor.TraitOrDefault<CivilizationState>();
			var settlements = civilization?.Settlements(world, player)
				.Select(actor => BuildSettlement(player, actor, actor.Trait<SettlementCore>()))
				.ToArray() ?? [];

			return new SimulationCivilizationResult
			{
				Model = civilization?.Info.Model ?? "unavailable",
				FoundedTick = civilization?.FoundedTick ?? 0,
				Population = settlements.Sum(s => s.Population),
				Children = settlements.Sum(s => s.Children),
				Adults = settlements.Sum(s => s.Adults),
				Elders = settlements.Sum(s => s.Elders),
				Workforce = settlements.Sum(s => s.Workforce),
				AvailableWorkforce = settlements.Sum(s => s.AvailableWorkforce),
				Employed = settlements.Sum(s => s.Employed),
				Mobilized = settlements.Sum(s => s.Mobilized),
				ActiveWars = civilization?.ActiveWars ?? 0,
				WarCasualties = settlements.Sum(s => s.WarCasualties),
				LastWarCasualties = settlements.Sum(s => s.LastWarCasualties),
				Housing = settlements.Sum(s => s.Housing),
				Jobs = settlements.Sum(s => s.Jobs),
				Food = settlements.Sum(s => s.Food),
				Materials = settlements.Sum(s => s.Materials),
				Energy = settlements.Sum(s => s.Energy),
				Knowledge = settlements.Sum(s => s.Knowledge),
				FoodProduction = settlements.Sum(s => s.FoodProduction),
				MaterialsProduction = settlements.Sum(s => s.MaterialsProduction),
				EnergyProduction = settlements.Sum(s => s.EnergyProduction),
				KnowledgeProduction = settlements.Sum(s => s.KnowledgeProduction),
				Prosperity = WeightedAverage(settlements, s => s.Prosperity),
				Stability = WeightedAverage(settlements, s => s.Stability),
				MigrationPressure = WeightedAverage(settlements, s => s.MigrationPressure),
				CompletedTechnologies = civilization?.CompletedTechnologies ?? [],
				CurrentTechnology = civilization?.CurrentTechnology,
				ResearchProgress = civilization?.ResearchProgress ?? 0,
				ResearchCost = civilization?.CurrentTechnologyCost ?? 0,
				ResearchMaterialsSpent = civilization?.ResearchMaterialsSpent ?? 0,
				ResearchEnergySpent = civilization?.ResearchEnergySpent ?? 0,
				Strategy = StrategyIdentifier(civilization?.Strategy ?? CivilizationStrategy.Development),
				StrategySequence = civilization?.StrategySequence ?? 0,
				StrategyTransitionTick = civilization?.StrategyTransitionTick ?? 0,
				Plan = PlanIdentifier(civilization?.Plan ?? CivilizationPlan.Opening),
				PlanReason = PlanReasonIdentifier(
					civilization?.PlanReason ?? CivilizationPlanReason.OpeningWindow),
				PlanSequence = civilization?.PlanSequence ?? 0,
				PlanTransitionTick = civilization?.PlanTransitionTick ?? 0,
				PlannerRequestSequence = civilization?.PlannerRequestSequence ?? 0,
				LastPlannerRequestTick = civilization?.LastPlannerRequestTick ?? 0,
				LastPlannerRequestActor = civilization?.LastPlannerRequestActor,
				SurvivalUtility = civilization?.SurvivalUtility ?? 0,
				ResearchUtility = civilization?.ResearchUtility ?? 0,
				TradeUtility = civilization?.TradeUtility ?? 0,
				SecurityUtility = civilization?.SecurityUtility ?? 0,
				RecoveryUtility = civilization?.RecoveryUtility ?? 0,
				WarUtility = civilization?.WarUtility ?? 0,
				TradeDependency = civilization?.TradeDependency ?? 0,
				Settlements = settlements
			};
		}

		static SimulationSettlementResult BuildSettlement(Player player, Actor actor, SettlementCore settlement)
		{
			return new SimulationSettlementResult
			{
				SettlementId = $"{player.InternalName}-{actor.ActorID}",
				ActorId = actor.ActorID,
				ActorType = actor.Info.Name,
				FoundedTick = settlement.FoundedTick,
				CellX = actor.Location.X,
				CellY = actor.Location.Y,
				Population = settlement.Population,
				Children = settlement.Children,
				Adults = settlement.Adults,
				Elders = settlement.Elders,
				Workforce = settlement.Adults,
				AvailableWorkforce = settlement.AvailableWorkforce,
				Employed = settlement.Employed,
				Mobilized = settlement.Mobilized,
				ActiveWars = settlement.ActiveWars,
				WarCasualties = settlement.WarCasualties,
				LastWarCasualties = settlement.LastWarCasualties,
				Housing = settlement.Housing,
				Jobs = settlement.Jobs,
				Food = settlement.Food,
				Materials = settlement.Materials,
				Energy = settlement.Energy,
				Knowledge = settlement.Knowledge,
				FoodStorage = settlement.FoodStorage,
				MaterialsStorage = settlement.MaterialsStorage,
				EnergyStorage = settlement.EnergyStorage,
				FoodProduction = settlement.FoodProduction,
				MaterialsProduction = settlement.MaterialsProduction,
				EnergyProduction = settlement.EnergyProduction,
				KnowledgeProduction = settlement.KnowledgeProduction,
				FoodDemand = settlement.FoodDemand,
				MaterialsDemand = settlement.MaterialsDemand,
				EnergyDemand = settlement.EnergyDemand,
				FoodSatisfaction = settlement.FoodSatisfaction,
				HousingSatisfaction = settlement.HousingSatisfaction,
				EnergySatisfaction = settlement.EnergySatisfaction,
				EmploymentSatisfaction = settlement.EmploymentSatisfaction,
				Prosperity = settlement.Prosperity,
				Stability = settlement.Stability,
				MigrationPressure = settlement.MigrationPressure,
				LastPopulationDelta = settlement.LastPopulationDelta,
				CivilPulseCount = settlement.CivilPulseCount,
				DemographicPulseCount = settlement.DemographicPulseCount,
				InfrastructureCount = settlement.InfrastructureCount
			};
		}

		static string StrategyIdentifier(CivilizationStrategy strategy)
		{
			return strategy switch
			{
				CivilizationStrategy.Development => "development",
				CivilizationStrategy.Survival => "survival",
				CivilizationStrategy.Research => "research",
				CivilizationStrategy.Trade => "trade",
				CivilizationStrategy.Mobilization => "mobilization",
				CivilizationStrategy.Recovery => "recovery",
				_ => "unknown"
			};
		}

		static string PlanIdentifier(CivilizationPlan plan)
		{
			return plan switch
			{
				CivilizationPlan.Opening => "opening",
				CivilizationPlan.Economy => "economy",
				CivilizationPlan.Technology => "technology",
				CivilizationPlan.Recovery => "recovery",
				_ => "unknown"
			};
		}

		static string PlanReasonIdentifier(CivilizationPlanReason reason)
		{
			return reason switch
			{
				CivilizationPlanReason.OpeningWindow => "opening-window",
				CivilizationPlanReason.CrisisRecovery => "crisis-recovery",
				CivilizationPlanReason.TechnologistDoctrine => "technologist-doctrine",
				CivilizationPlanReason.EconomistDoctrine => "economist-doctrine",
				CivilizationPlanReason.ResearchStrategy => "research-strategy",
				CivilizationPlanReason.DevelopmentDoctrine => "development-doctrine",
				_ => "unknown"
			};
		}

		static int WeightedAverage(
			SimulationSettlementResult[] settlements,
			Func<SimulationSettlementResult, int> selector)
		{
			var population = settlements.Sum(s => (long)s.Population);
			if (population == 0)
				return 0;

			return (int)(settlements.Sum(s => (long)selector(s) * s.Population) / population);
		}
	}
}
