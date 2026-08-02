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
	public static class SimulationUniverseSnapshotBuilder
	{
		public static SimulationUniverseResult Build(World world)
		{
			var universe = world.WorldActor.TraitOrDefault<UniverseState>();
			if (universe == null)
				return null;

			return new SimulationUniverseResult
			{
				UniverseId = UniverseState.UniverseId,
				StarSystemId = UniverseState.StarSystemId,
				MacroDay = universe.MacroDay,
				MacroTickRemainder = universe.MacroTickRemainder,
				TicksPerMacroDay = universe.TicksPerMacroDay,
				Planets = UniverseState.Planets.ToArray()
					.OrderBy(planet => planet.Index)
					.Select(planet => new SimulationPlanetResult
					{
						PlanetId = planet.PlanetId,
						Name = planet.Name,
						Index = planet.Index,
						Active = universe.IsPlanetActive(planet.Index),
						LifecycleStage = LifecycleIdentifier(universe.LifecycleStage(planet.Index)),
						NativeRaceId = null
					})
					.ToArray()
			};
		}

		static string LifecycleIdentifier(PlanetLifecycleStage stage)
		{
			return stage switch
			{
				PlanetLifecycleStage.Lifeless => "lifeless",
				PlanetLifecycleStage.Biosphere => "biosphere",
				PlanetLifecycleStage.SapientLife => "sapient-life",
				PlanetLifecycleStage.Civilization => "civilization",
				PlanetLifecycleStage.Spacefaring => "spacefaring",
				_ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
			};
		}
	}
}
