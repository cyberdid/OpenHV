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
using System.Linq;
using OpenRA.Effects;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Lint;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.HV.Traits
{
	public enum DiplomaticRelationState
	{
		Neutral,
		War,
		Alliance
	}

	public enum DiplomacyReason
	{
		InitialNeutrality,
		StrategicRivalry,
		WarExhaustion,
		DefensiveResponse,
		FactionCollapse
	}

	[TraitLocation(SystemActors.World)]
	[Desc("Owns synchronized bilateral diplomacy and applies it to OpenRA player targeting masks.")]
	[IncludeStaticFluentReferences(typeof(DiplomacyManager))]
	public sealed class DiplomacyManagerInfo : TraitInfo
	{
		[Desc("Ticks between strategic relationship decisions.")]
		// DIP-001 measured what this controls: at 5000 ticks a 12,000-tick match
		// runs the whole diplomatic system twice, and two samples cannot
		// accumulate grievance, so only the profile with the largest constant
		// baseline ever reaches the war threshold. Lowering it to 1000 made
		// every pairing fight - and made every war shorter, because exhaustion
		// accrues a flat amount per update. Wars became universal and brief,
		// collapses fell from 21 to 3 and the Economist lost 0.13 of its
		// score-lead rate. Any v2 has to scale the exhaustion increment with
		// this interval before changing it.
		public readonly int StrategicInterval = 5000;

		[Desc("Combined grievance required before an AI pair enters war.")]
		public readonly int WarGrievanceThreshold = 400;

		[Desc("War exhaustion required before both AIs accept peace.")]
		public readonly int PeaceExhaustionThreshold = 800;

		[Desc("Minimum ticks a declared war lasts before peace is considered.")]
		public readonly int MinimumWarDuration = 5000;

		[Desc("Ticks after peace before the same pair may declare war again.")]
		public readonly int PeaceCooldown = 10000;

		public override object Create(ActorInitializer init) { return new DiplomacyManager(init.Self, this); }
	}

	public sealed class DiplomacyManager : IWorldLoaded, ITick
	{
		readonly Actor self;
		readonly DiplomacyManagerInfo info;
		readonly List<DiplomaticRelation> relations = [];
		int strategicTicks;

		public IReadOnlyList<DiplomaticRelation> Relations => relations;

		public DiplomacyManager(Actor self, DiplomacyManagerInfo info)
		{
			this.self = self;
			this.info = info;
		}

		void IWorldLoaded.WorldLoaded(World world, WorldRenderer worldRenderer)
		{
			if (!Game.IsDeterministicSimulation)
				return;

			var players = ActivePlayers(world).ToArray();
			if (players.Length > 30)
				throw new InvalidOperationException("Dynamic diplomacy supports at most 30 active factions.");

			for (var i = 0; i < players.Length; i++)
			{
				for (var j = i + 1; j < players.Length; j++)
				{
					var relation = new DiplomaticRelation(players[i], players[j], i, j);
					relations.Add(relation);
					world.Add(relation);
					ApplyRelationship(relation);
				}
			}
		}

		void ITick.Tick(Actor self)
		{
			if (++strategicTicks < Math.Max(1, info.StrategicInterval))
				return;

			strategicTicks = 0;
			foreach (var relation in relations)
				UpdateRelation(relation);
		}

		void UpdateRelation(DiplomaticRelation relation)
		{
			if (relation.PlayerA.WinState == WinState.Lost || relation.PlayerB.WinState == WinState.Lost)
			{
				if (relation.State == DiplomaticRelationState.War)
					Transition(relation, DiplomaticRelationState.Neutral, DiplomacyReason.FactionCollapse);
				return;
			}

			if (relation.State == DiplomaticRelationState.War)
			{
				var deathsA = relation.PlayerA.PlayerActor.TraitOrDefault<PlayerStatistics>()?.DeathsCost ?? 0;
				var deathsB = relation.PlayerB.PlayerActor.TraitOrDefault<PlayerStatistics>()?.DeathsCost ?? 0;
				var newLosses = Math.Max(0, deathsA - relation.LastDeathsA) +
					Math.Max(0, deathsB - relation.LastDeathsB);
				relation.LastDeathsA = deathsA;
				relation.LastDeathsB = deathsB;
				relation.WarExhaustion = Math.Min(1000, relation.WarExhaustion + 250 + newLosses / 10);
				relation.Trust = Math.Max(0, relation.Trust - 150);

				if (self.World.WorldTick - relation.WarStartedTick >= info.MinimumWarDuration &&
					relation.WarExhaustion >= info.PeaceExhaustionThreshold)
					Transition(relation, DiplomaticRelationState.Neutral, DiplomacyReason.WarExhaustion);
				return;
			}

			if (self.World.WorldTick < relation.PeaceCooldownUntil)
				return;

			relation.GrievanceA = Math.Min(
				1000,
				relation.GrievanceA + StrategicPressure(
					relation.PlayerA,
					relation.PlayerB,
					relation,
					out var carriedA));
			relation.PressureTermA = (int)carriedA;
			relation.GrievanceB = Math.Min(
				1000,
				relation.GrievanceB + StrategicPressure(
					relation.PlayerB,
					relation.PlayerA,
					relation,
					out var carriedB));
			relation.PressureTermB = (int)carriedB;
			relation.Trust = Math.Min(1000, relation.Trust + 25);
			if (relation.GrievanceA + relation.GrievanceB >= info.WarGrievanceThreshold)
				Transition(relation, DiplomaticRelationState.War, DiplomacyReason.StrategicRivalry);
		}

		[FluentReference]
		const string WarLine = "notification-diplomacy-war";

		[FluentReference]
		const string PeaceLine = "notification-diplomacy-peace";

		[FluentReference]
		const string ReasonInitialNeutrality = "diplomacy-reason-initial-neutrality";

		[FluentReference]
		const string ReasonStrategicRivalry = "diplomacy-reason-strategic-rivalry";

		[FluentReference]
		const string ReasonWarExhaustion = "diplomacy-reason-war-exhaustion";

		[FluentReference]
		const string ReasonDefensiveResponse = "diplomacy-reason-defensive-response";

		[FluentReference]
		const string ReasonFactionCollapse = "diplomacy-reason-faction-collapse";

		/// <summary>
		/// Put the change on screen. A civil system nobody can see while the match
		/// runs is indistinguishable from one that is not running at all, and the
		/// result file is not something a viewer reads. Display only: nothing here
		/// touches synchronized state.
		/// </summary>
		static void Announce(
			DiplomaticRelation relation,
			DiplomaticRelationState state,
			DiplomacyReason reason)
		{
			var message = state == DiplomaticRelationState.War
				? WarLine
				: PeaceLine;
			TextNotificationsManager.AddSystemLine(
				FluentProvider.GetMessage(
					message,
					"first", relation.PlayerA.ResolvedPlayerName,
					"second", relation.PlayerB.ResolvedPlayerName,
					"reason", FluentProvider.GetMessage(ReasonKey(reason))));
		}

		static string ReasonKey(DiplomacyReason reason)
		{
			return reason switch
			{
				DiplomacyReason.InitialNeutrality => ReasonInitialNeutrality,
				DiplomacyReason.StrategicRivalry => ReasonStrategicRivalry,
				DiplomacyReason.WarExhaustion => ReasonWarExhaustion,
				DiplomacyReason.DefensiveResponse => ReasonDefensiveResponse,
				_ => ReasonFactionCollapse
			};
		}

		void Transition(
			DiplomaticRelation relation,
			DiplomaticRelationState state,
			DiplomacyReason reason)
		{
			if (relation.State == state)
				return;

			relation.State = state;
			relation.TransitionTick = self.World.WorldTick;
			relation.Reason = reason;
			relation.TransitionSequence++;
			Announce(relation, state, reason);
			if (state == DiplomaticRelationState.War)
			{
				relation.WarStartedTick = self.World.WorldTick;
				relation.WarExhaustion = 0;
				relation.LastDeathsA =
					relation.PlayerA.PlayerActor.TraitOrDefault<PlayerStatistics>()?.DeathsCost ?? 0;
				relation.LastDeathsB =
					relation.PlayerB.PlayerActor.TraitOrDefault<PlayerStatistics>()?.DeathsCost ?? 0;
			}
			else
			{
				relation.GrievanceA = 0;
				relation.GrievanceB = 0;
				relation.PeaceCooldownUntil = self.World.WorldTick + info.PeaceCooldown;
			}

			ApplyRelationship(relation);
		}

		static void ApplyRelationship(DiplomaticRelation relation)
		{
			var a = relation.PlayerA;
			var b = relation.PlayerB;
			a.AlliedPlayersMask = a.AlliedPlayersMask.Except(b.PlayerMask);
			b.AlliedPlayersMask = b.AlliedPlayersMask.Except(a.PlayerMask);
			a.EnemyPlayersMask = a.EnemyPlayersMask.Except(b.PlayerMask);
			b.EnemyPlayersMask = b.EnemyPlayersMask.Except(a.PlayerMask);

			if (relation.State == DiplomaticRelationState.War)
			{
				a.EnemyPlayersMask = a.EnemyPlayersMask.Union(b.PlayerMask);
				b.EnemyPlayersMask = b.EnemyPlayersMask.Union(a.PlayerMask);
			}
			else if (relation.State == DiplomaticRelationState.Alliance)
			{
				a.AlliedPlayersMask = a.AlliedPlayersMask.Union(b.PlayerMask);
				b.AlliedPlayersMask = b.AlliedPlayersMask.Union(a.PlayerMask);
			}
		}

		static int StrategicPressure(
			Player player,
			Player other,
			DiplomaticRelation relation,
			out PressureTerm carried)
		{
			carried = PressureTerm.None;
			var civilization = player.PlayerActor.TraitOrDefault<CivilizationState>();
			return civilization?.StrategicPressureAgainst(other, relation, out carried) ?? 0;
		}

		static IEnumerable<Player> ActivePlayers(World world)
		{
			return world.Players
				.Where(player => player.Playable && !player.NonCombatant && !player.Spectating)
				.OrderBy(player => player.ClientIndex);
		}
	}

	public sealed class DiplomaticRelation : IEffect, ISync
	{
		public readonly Player PlayerA;
		public readonly Player PlayerB;

		[VerifySync]
		public readonly int PlayerAIndex;

		[VerifySync]
		public readonly int PlayerBIndex;

		[VerifySync]
		int state;

		[VerifySync]
		int reason;

		[VerifySync]
		public int GrievanceA;

		[VerifySync]
		public int GrievanceB;

		/// <summary>
		/// Which named term carried each side's pressure when it was last weighed.
		/// A single grievance number says how badly somebody wants a war; this says
		/// what part of the world is doing the wanting.
		///
		/// Deliberately not marked for sync. Both are derived inside the same
		/// synchronized pass from inputs that are already hashed, so they cannot
		/// diverge on their own - and hashing them would move the state hash for
		/// an instrument that changes no decision, which would cost a fresh
		/// baseline every time a term is renamed.
		/// </summary>
		public int PressureTermA;

		public int PressureTermB;

		[VerifySync]
		public int Trust = 500;

		[VerifySync]
		public int WarExhaustion;

		[VerifySync]
		public int WarStartedTick;

		[VerifySync]
		public int PeaceCooldownUntil;

		[VerifySync]
		public int TransitionTick;

		[VerifySync]
		public int TransitionSequence;

		[VerifySync]
		public int LastDeathsA;

		[VerifySync]
		public int LastDeathsB;

		public DiplomaticRelationState State
		{
			get => (DiplomaticRelationState)state;
			set => state = (int)value;
		}

		public DiplomacyReason Reason
		{
			get => (DiplomacyReason)reason;
			set => reason = (int)value;
		}

		public DiplomaticRelation(Player playerA, Player playerB, int playerAIndex, int playerBIndex)
		{
			PlayerA = playerA;
			PlayerB = playerB;
			PlayerAIndex = playerAIndex;
			PlayerBIndex = playerBIndex;
			State = DiplomaticRelationState.Neutral;
			Reason = DiplomacyReason.InitialNeutrality;
		}

		void IEffect.Tick(World world) { }

		IEnumerable<IRenderable> IEffect.Render(WorldRenderer renderer)
		{
			return [];
		}
	}
}
