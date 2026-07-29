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
		public SimulationDiplomaticRelation[] Diplomacy { get; init; }
		public SimulationTradeRoute[] TradeRoutes { get; init; }
	}

	public sealed class SimulationBuildMetadata
	{
		public string EngineVersion { get; init; }
		public string ModId { get; init; }
		public string ModVersion { get; init; }
		public string GitCommit { get; init; }
		public bool GitDirty { get; init; }
		public string ExecutionMode { get; init; }
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
		public SimulationCivilizationResult Civilization { get; init; }
	}

	public sealed class SimulationCivilizationResult
	{
		public string Model { get; init; }
		public int FoundedTick { get; init; }
		public int Population { get; init; }
		public int Children { get; init; }
		public int Adults { get; init; }
		public int Elders { get; init; }
		public int Workforce { get; init; }
		public int Employed { get; init; }
		public int Housing { get; init; }
		public int Jobs { get; init; }
		public int Food { get; init; }
		public int Materials { get; init; }
		public int Energy { get; init; }
		public int Knowledge { get; init; }
		public int FoodProduction { get; init; }
		public int MaterialsProduction { get; init; }
		public int EnergyProduction { get; init; }
		public int KnowledgeProduction { get; init; }
		public int Prosperity { get; init; }
		public int Stability { get; init; }
		public int MigrationPressure { get; init; }
		public string[] CompletedTechnologies { get; init; }
		public string CurrentTechnology { get; init; }
		public int ResearchProgress { get; init; }
		public int ResearchCost { get; init; }
		public SimulationSettlementResult[] Settlements { get; init; }
	}

	public sealed class SimulationSettlementResult
	{
		public string SettlementId { get; init; }
		public uint ActorId { get; init; }
		public string ActorType { get; init; }
		public int FoundedTick { get; init; }
		public int CellX { get; init; }
		public int CellY { get; init; }
		public int Population { get; init; }
		public int Children { get; init; }
		public int Adults { get; init; }
		public int Elders { get; init; }
		public int Workforce { get; init; }
		public int Employed { get; init; }
		public int Housing { get; init; }
		public int Jobs { get; init; }
		public int Food { get; init; }
		public int Materials { get; init; }
		public int Energy { get; init; }
		public int Knowledge { get; init; }
		public int FoodStorage { get; init; }
		public int MaterialsStorage { get; init; }
		public int EnergyStorage { get; init; }
		public int FoodProduction { get; init; }
		public int MaterialsProduction { get; init; }
		public int EnergyProduction { get; init; }
		public int KnowledgeProduction { get; init; }
		public int FoodDemand { get; init; }
		public int MaterialsDemand { get; init; }
		public int EnergyDemand { get; init; }
		public int FoodSatisfaction { get; init; }
		public int HousingSatisfaction { get; init; }
		public int EnergySatisfaction { get; init; }
		public int EmploymentSatisfaction { get; init; }
		public int Prosperity { get; init; }
		public int Stability { get; init; }
		public int MigrationPressure { get; init; }
		public int LastPopulationDelta { get; init; }
		public int CivilPulseCount { get; init; }
		public int DemographicPulseCount { get; init; }
		public int InfrastructureCount { get; init; }
	}

	public sealed class SimulationDiplomaticRelation
	{
		public string RelationId { get; init; }
		public string PlayerA { get; init; }
		public string PlayerB { get; init; }
		public string State { get; init; }
		public int GrievanceA { get; init; }
		public int GrievanceB { get; init; }
		public int Trust { get; init; }
		public int WarExhaustion { get; init; }
		public int WarStartedTick { get; init; }
		public int PeaceCooldownUntil { get; init; }
		public int TransitionTick { get; init; }
		public int TransitionSequence { get; init; }
		public string ReasonCode { get; init; }
	}

	public sealed class SimulationTradeRoute
	{
		public string RouteId { get; init; }
		public string PlayerA { get; init; }
		public string PlayerB { get; init; }
		public string Status { get; init; }
		public string StatusReason { get; init; }
		public int DistanceCells { get; init; }
		public int Capacity { get; init; }
		public int Risk { get; init; }
		public int StatusSequence { get; init; }
		public int ShipmentSequence { get; init; }
		public int LastTradeTick { get; init; }
		public string LastResource { get; init; }
		public int LastAmount { get; init; }
		public string LastExporter { get; init; }
		public int FoodAToB { get; init; }
		public int FoodBToA { get; init; }
		public int MaterialsAToB { get; init; }
		public int MaterialsBToA { get; init; }
		public int EnergyAToB { get; init; }
		public int EnergyBToA { get; init; }
	}
}
