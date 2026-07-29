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

namespace OpenRA.Mods.HV
{
	public sealed class SimulationResult
	{
		public int SchemaVersion { get; init; }
		public SimulationBuildMetadata Build { get; init; }
		public SimulationConfig Config { get; init; }
		public string EndReason { get; init; }
		public string EndDetail { get; init; }
		public DateTime StartedUtc { get; init; }
		public DateTime EndedUtc { get; init; }
		public int WorldTick { get; init; }
		public double SimulatedSeconds { get; init; }
		public string SynchronizedStateHash { get; init; }
		public SimulationLeader[] NaturalWinners { get; init; }
		public SimulationLeader ScoreLeader { get; init; }
		public SimulationPlayerResult[] Players { get; init; }
	}

	public sealed class SimulationBuildMetadata
	{
		public string EngineVersion { get; init; }
		public string ModId { get; init; }
		public string ModVersion { get; init; }
		public string GitCommit { get; init; }
		public bool GitDirty { get; init; }
	}

	public sealed class SimulationLeader
	{
		public string PlayerName { get; init; }
		public string BotType { get; init; }
		public string Faction { get; init; }
	}

	public sealed class SimulationPlayerResult
	{
		public string PlayerName { get; init; }
		public string Slot { get; init; }
		public string BotType { get; init; }
		public string Faction { get; init; }
		public int Team { get; init; }
		public string Color { get; init; }
		public int SpawnPoint { get; init; }
		public int HomeCellX { get; init; }
		public int HomeCellY { get; init; }
		public string Outcome { get; init; }
		public int Score { get; init; }
		public int Experience { get; init; }
		public int KillsValue { get; init; }
		public int DeathsValue { get; init; }
		public int UnitsKilled { get; init; }
		public int UnitsLost { get; init; }
		public int BuildingsKilled { get; init; }
		public int BuildingsLost { get; init; }
		public int ArmyValue { get; init; }
		public int AssetsValue { get; init; }
		public int CashAndResources { get; init; }
		public int Earned { get; init; }
		public int Spent { get; init; }
	}
}
