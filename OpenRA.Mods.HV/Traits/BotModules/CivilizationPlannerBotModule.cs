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
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.HV.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Turns the synchronized civilization plan into concrete economic and technology unit requests.")]
	public sealed class CivilizationPlannerBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Bot ticks between planner production requests.")]
		public readonly int DecisionInterval = 1000;

		public override object Create(ActorInitializer init)
		{
			return new CivilizationPlannerBotModule(init.Self, this);
		}
	}

	public sealed class CivilizationPlannerBotModule : ConditionalTrait<CivilizationPlannerBotModuleInfo>, IBotTick
	{
		readonly Actor self;
		readonly World world;
		IBotRequestUnitProduction[] productionRequesters;
		int decisionTicks;

		public CivilizationPlannerBotModule(Actor self, CivilizationPlannerBotModuleInfo info)
			: base(info)
		{
			this.self = self;
			world = self.World;
		}

		protected override void Created(Actor self)
		{
			productionRequesters = self.Owner.PlayerActor
				.TraitsImplementing<IBotRequestUnitProduction>()
				.ToArray();
		}

		protected override void TraitEnabled(Actor self)
		{
			decisionTicks = Math.Max(1, Info.DecisionInterval);
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (--decisionTicks > 0)
				return;

			decisionTicks = Math.Max(1, Info.DecisionInterval);
			var civilization = self.Owner.PlayerActor.TraitOrDefault<CivilizationState>();
			var requester = productionRequesters.FirstOrDefault(Exts.IsTraitEnabled);
			if (civilization == null || requester == null)
				return;

			var requested = civilization.Plan switch
			{
				CivilizationPlan.Opening => ExecuteOpening(bot, requester, civilization),
				CivilizationPlan.Economy => ExecuteEconomy(bot, requester, civilization),
				CivilizationPlan.Technology => ExecuteTechnology(bot, requester, civilization),
				CivilizationPlan.Recovery => ExecuteRecovery(bot, requester, civilization),
				_ => null
			};
			if (requested == null)
				return;

			civilization.RecordPlannerRequest(
				CivilizationState.PlannerActorCode(requested),
				world.WorldTick);
		}

		static string ExecuteOpening(
			IBot bot,
			IBotRequestUnitProduction requester,
			CivilizationState civilization)
		{
			return RequestWithinBudget(bot, requester, civilization, "miner", 1);
		}

		string ExecuteEconomy(
			IBot bot,
			IBotRequestUnitProduction requester,
			CivilizationState civilization)
		{
			var requestBudget = self.Owner.BotType switch
			{
				"economist" => 2,
				_ => 1
			};
			return RequestWithinBudget(bot, requester, civilization, "miner", requestBudget);
		}

		static string ExecuteTechnology(
			IBot bot,
			IBotRequestUnitProduction requester,
			CivilizationState civilization)
		{
			return RequestWithinBudget(bot, requester, civilization, "technician", 1) ??
				RequestWithinBudget(bot, requester, civilization, "observer", 1) ??
				RequestWithinBudget(bot, requester, civilization, "radartank", 1);
		}

		static string ExecuteRecovery(
			IBot bot,
			IBotRequestUnitProduction requester,
			CivilizationState civilization)
		{
			return RequestWithinBudget(bot, requester, civilization, "repairtank", 1) ??
				RequestWithinBudget(bot, requester, civilization, "miner", 2);
		}

		static string RequestWithinBudget(
			IBot bot,
			IBotRequestUnitProduction requester,
			CivilizationState civilization,
			string requestedActor,
			int requestBudget)
		{
			if (civilization.PlannerRequestsFor(requestedActor) >= requestBudget ||
				requester.RequestedProductionCount(bot, requestedActor) > 0)
				return null;

			requester.RequestUnitProduction(bot, requestedActor);
			return requestedActor;
		}
	}
}
