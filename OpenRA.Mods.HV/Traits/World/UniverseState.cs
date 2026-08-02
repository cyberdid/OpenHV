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
using OpenRA.Traits;

namespace OpenRA.Mods.HV.Traits
{
	public enum PlanetLifecycleStage
	{
		Lifeless,
		Biosphere,
		SapientLife,
		Civilization,
		Spacefaring
	}

	public sealed class PlanetSlotDefinition
	{
		public int Index { get; }
		public string PlanetId { get; }
		public string Name { get; }

		public PlanetSlotDefinition(int index, string planetId, string name)
		{
			Index = index;
			PlanetId = planetId;
			Name = name;
		}
	}

	[TraitLocation(SystemActors.World)]
	[Desc("Owns the synchronized Universe identity, three planetary slots, and the slow simulation clock.")]
	public sealed class UniverseStateInfo : TraitInfo
	{
		[Desc("OpenRA world ticks in one Universe macro day.")]
		public readonly int TicksPerMacroDay = 250;

		public override object Create(ActorInitializer init) { return new UniverseState(this); }
	}

	/// <summary>
	/// The authoritative root of the long-running simulation. Stable identifiers and
	/// slot definitions are immutable data; all mutable values that can affect the
	/// simulation are integers included in OpenRA's synchronization hash.
	/// </summary>
	public sealed class UniverseState : ITick, ISync
	{
		public const string UniverseId = "universe-0001";
		public const string StarSystemId = "tyranthos-system";

		static readonly PlanetSlotDefinition[] PlanetDefinitions =
		[
			new(0, "planet-0001", "Tyranthos"),
			new(1, "planet-0002", "Planet II"),
			new(2, "planet-0003", "Planet III")
		];

		readonly UniverseStateInfo info;

		[VerifySync]
		int macroDay;

		[VerifySync]
		int macroTickRemainder;

		[VerifySync]
		int activePlanetMask = 1;

		[VerifySync]
		int planet0LifecycleStage = (int)PlanetLifecycleStage.Lifeless;

		[VerifySync]
		int planet1LifecycleStage = (int)PlanetLifecycleStage.Lifeless;

		[VerifySync]
		int planet2LifecycleStage = (int)PlanetLifecycleStage.Lifeless;

		public int MacroDay => macroDay;
		public int MacroTickRemainder => macroTickRemainder;
		public int TicksPerMacroDay => Math.Max(1, info.TicksPerMacroDay);
		public static ReadOnlySpan<PlanetSlotDefinition> Planets => PlanetDefinitions;

		public UniverseState(UniverseStateInfo info)
		{
			this.info = info;
		}

		public bool IsPlanetActive(int index)
		{
			ValidatePlanetIndex(index);
			return (activePlanetMask & 1 << index) != 0;
		}

		public PlanetLifecycleStage LifecycleStage(int index)
		{
			ValidatePlanetIndex(index);
			return (PlanetLifecycleStage)(index switch
			{
				0 => planet0LifecycleStage,
				1 => planet1LifecycleStage,
				_ => planet2LifecycleStage
			});
		}

		void ITick.Tick(Actor self)
		{
			// Ordinary OpenHV matches retain their original behavior. Universe time is
			// authoritative only for explicitly deterministic simulation sessions.
			if (!Game.IsDeterministicSimulation)
				return;

			macroTickRemainder++;
			if (macroTickRemainder < TicksPerMacroDay)
				return;

			macroTickRemainder = 0;
			macroDay++;
		}

		static void ValidatePlanetIndex(int index)
		{
			if ((uint)index >= (uint)PlanetDefinitions.Length)
				throw new ArgumentOutOfRangeException(nameof(index));
		}
	}
}
