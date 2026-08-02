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
		public int BotRandomTotalCount { get; init; }
		public SimulationLeader[] NaturalWinners { get; init; }
		public SimulationLeader ScoreLeader { get; init; }
		public SimulationPlayerResult[] Players { get; init; }
		public SimulationDiplomaticRelation[] Diplomacy { get; init; }
		public SimulationTradeRoute[] TradeRoutes { get; init; }
		public SimulationLifecycleResult Lifecycle { get; init; }
		public SimulationUniverseResult Universe { get; init; }
	}

	public sealed class SimulationUniverseResult
	{
		public string UniverseId { get; init; }
		public string StarSystemId { get; init; }
		public int MacroDay { get; init; }
		public int MacroTickRemainder { get; init; }
		public int MacroEventSequence { get; init; }
		public int TicksPerMacroDay { get; init; }
		public SimulationPlanetResult[] Planets { get; init; }
	}

	public sealed class SimulationPlanetResult
	{
		public string PlanetId { get; init; }
		public string Name { get; init; }
		public int Index { get; init; }
		public bool Active { get; init; }
		public string LifecycleStage { get; init; }
		public string NativeRaceId { get; init; }
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
		public int AvailableWorkforce { get; init; }
		public int Employed { get; init; }
		public int Mobilized { get; init; }
		public int ActiveWars { get; init; }
		public int WarCasualties { get; init; }
		public int LastWarCasualties { get; init; }
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
		public int ResearchMaterialsSpent { get; init; }
		public int ResearchEnergySpent { get; init; }
		public string Strategy { get; init; }
		public int StrategySequence { get; init; }
		public int StrategyTransitionTick { get; init; }
		public string Plan { get; init; }
		public string PlanReason { get; init; }
		public int PlanSequence { get; init; }
		public int PlanTransitionTick { get; init; }
		public int PlannerRequestSequence { get; init; }
		public int LastPlannerRequestTick { get; init; }
		public string LastPlannerRequestActor { get; init; }
		public int CombatDecisionSequence { get; init; }
		public int LastCombatDecisionTick { get; init; }
		public string LastCombatDecision { get; init; }
		public string LastCombatDecisionReason { get; init; }
		public string LastCombatSquadType { get; init; }
		public int LastCombatUnitCount { get; init; }
		public int LastCombatTargetActorId { get; init; }
		public int LastCombatOwnValue { get; init; }
		public int LastCombatEnemyValue { get; init; }
		public int TargetSelectionCount { get; init; }
		public int RetreatCount { get; init; }
		public int RegroupCount { get; init; }

		public int IdleBaseUnits { get; init; }

		public int CommittedUnits { get; init; }
		public int ReengageCount { get; init; }
		public int SurvivalUtility { get; init; }
		public int ResearchUtility { get; init; }
		public int TradeUtility { get; init; }
		public int SecurityUtility { get; init; }
		public int RecoveryUtility { get; init; }
		public int WarUtility { get; init; }
		public int TradeDependency { get; init; }
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
		public int AvailableWorkforce { get; init; }
		public int Employed { get; init; }
		public int Mobilized { get; init; }
		public int ActiveWars { get; init; }
		public int WarCasualties { get; init; }
		public int LastWarCasualties { get; init; }
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

		public string PressureTermA { get; init; }

		public string PressureTermB { get; init; }
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

	public sealed class SimulationLifecycleResult
	{
		public string ScenarioMode { get; init; }
		public int ObservationHorizonTick { get; init; }
		public int HardTickLimit { get; init; }
		public int StalemateWindowTicks { get; init; }
		public bool StalemateTerminates { get; init; }
		public int CollapsePopulationThreshold { get; init; }
		public int CollapseStabilityThreshold { get; init; }
		public bool StalemateAdvisory { get; init; }
		public int StalemateSinceTick { get; init; }
		public int LastMeaningfulActivityTick { get; init; }
		public int StalemateSequence { get; init; }
		public bool AllFactionsCollapsed { get; init; }
		public SimulationCollapsedFaction[] CollapsedFactions { get; init; }
	}

	public sealed class SimulationCollapsedFaction
	{
		public string PlayerName { get; init; }
		public string BotType { get; init; }
		public string Faction { get; init; }
		public int CollapseTick { get; init; }
		public int Population { get; init; }
		public int SurvivingAssetsValue { get; init; }
		public string ReasonCode { get; init; }
	}
}
