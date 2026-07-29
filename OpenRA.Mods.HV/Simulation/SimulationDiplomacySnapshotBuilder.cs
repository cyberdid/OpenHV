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

using System.Linq;
using OpenRA.Mods.HV.Traits;

namespace OpenRA.Mods.HV
{
	public static class SimulationDiplomacySnapshotBuilder
	{
		public static SimulationDiplomaticRelation[] Build(World world)
		{
			var manager = world.WorldActor.TraitOrDefault<DiplomacyManager>();
			return manager?.Relations
				.OrderBy(relation => relation.PlayerAIndex)
				.ThenBy(relation => relation.PlayerBIndex)
				.Select(relation => new SimulationDiplomaticRelation
				{
					RelationId = $"{relation.PlayerA.InternalName}:{relation.PlayerB.InternalName}",
					PlayerA = relation.PlayerA.ResolvedPlayerName,
					PlayerB = relation.PlayerB.ResolvedPlayerName,
					State = StateIdentifier(relation.State),
					GrievanceA = relation.GrievanceA,
					GrievanceB = relation.GrievanceB,
					Trust = relation.Trust,
					WarExhaustion = relation.WarExhaustion,
					WarStartedTick = relation.WarStartedTick,
					PeaceCooldownUntil = relation.PeaceCooldownUntil,
					TransitionTick = relation.TransitionTick,
					TransitionSequence = relation.TransitionSequence,
					ReasonCode = ReasonIdentifier(relation.Reason)
				})
				.ToArray() ?? [];
		}

		static string StateIdentifier(DiplomaticRelationState state)
		{
			return state switch
			{
				DiplomaticRelationState.Neutral => "neutral",
				DiplomaticRelationState.War => "war",
				DiplomaticRelationState.Alliance => "alliance",
				_ => "unknown"
			};
		}

		static string ReasonIdentifier(DiplomacyReason reason)
		{
			return reason switch
			{
				DiplomacyReason.InitialNeutrality => "initial-neutrality",
				DiplomacyReason.StrategicRivalry => "strategic-rivalry",
				DiplomacyReason.WarExhaustion => "war-exhaustion",
				DiplomacyReason.DefensiveResponse => "defensive-response",
				DiplomacyReason.FactionCollapse => "faction-collapse",
				_ => "unknown"
			};
		}
	}
}
