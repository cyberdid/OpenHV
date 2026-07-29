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

namespace OpenRA.Mods.HV
{
	public enum SimulationEndReason
	{
		NaturalVictory,
		FactionCollapse,
		ObservationHorizon,
		WorldTickLimit,
		Stalemate,
		InvalidConfiguration,
		Desync,
		Crash,
		ExternalCancel,
		WatchdogTimeout
	}

	public static class SimulationEndReasonExts
	{
		public static string ToIdentifier(this SimulationEndReason reason)
		{
			return reason switch
			{
				SimulationEndReason.NaturalVictory => "natural-victory",
				SimulationEndReason.FactionCollapse => "faction-collapse",
				SimulationEndReason.ObservationHorizon => "observation-horizon",
				SimulationEndReason.WorldTickLimit => "world-tick-limit",
				SimulationEndReason.Stalemate => "stalemate",
				SimulationEndReason.InvalidConfiguration => "invalid-configuration",
				SimulationEndReason.Desync => "desync",
				SimulationEndReason.Crash => "crash",
				SimulationEndReason.ExternalCancel => "external-cancel",
				SimulationEndReason.WatchdogTimeout => "watchdog-timeout",
				_ => "unknown"
			};
		}
	}
}
