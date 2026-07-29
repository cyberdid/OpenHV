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
				Employed = settlements.Sum(s => s.Employed),
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
				Employed = settlement.Employed,
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
