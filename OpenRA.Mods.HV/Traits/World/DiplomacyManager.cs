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
	public sealed class DiplomacyManagerInfo : TraitInfo
	{
		[Desc("Ticks between strategic relationship decisions.")]
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
					relation));
			relation.GrievanceB = Math.Min(
				1000,
				relation.GrievanceB + StrategicPressure(
					relation.PlayerB,
					relation.PlayerA,
					relation));
			relation.Trust = Math.Min(1000, relation.Trust + 25);
			if (relation.GrievanceA + relation.GrievanceB >= info.WarGrievanceThreshold)
				Transition(relation, DiplomaticRelationState.War, DiplomacyReason.StrategicRivalry);
		}

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
				? "notification-diplomacy-war"
				: "notification-diplomacy-peace";
			TextNotificationsManager.AddSystemLine(
				FluentProvider.GetMessage(
					message,
					"first", relation.PlayerA.PlayerName,
					"second", relation.PlayerB.PlayerName,
					"reason", FluentProvider.GetMessage(ReasonKey(reason))));
		}

		static string ReasonKey(DiplomacyReason reason)
		{
			return reason switch
			{
				DiplomacyReason.InitialNeutrality => "diplomacy-reason-initial-neutrality",
				DiplomacyReason.StrategicRivalry => "diplomacy-reason-strategic-rivalry",
				DiplomacyReason.WarExhaustion => "diplomacy-reason-war-exhaustion",
				DiplomacyReason.DefensiveResponse => "diplomacy-reason-defensive-response",
				_ => "diplomacy-reason-faction-collapse"
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
			DiplomaticRelation relation)
		{
			return player.PlayerActor
				.TraitOrDefault<CivilizationState>()?
				.StrategicPressureAgainst(other, relation) ?? 0;
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
