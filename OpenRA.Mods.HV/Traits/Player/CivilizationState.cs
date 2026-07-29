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
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.HV.Traits
{
	public enum CivilizationStrategy
	{
		Development,
		Survival,
		Research,
		Trade,
		Mobilization,
		Recovery
	}

	[TraitLocation(SystemActors.Player)]
	[Desc("Stores deterministic civilization-level identity and exposes the player's settlements.")]
	public sealed class CivilizationStateInfo : TraitInfo
	{
		[Desc("Stable identifier for the first civilization model.")]
		public readonly string Model = "living-factions-v1";

		[Desc("Ticks between deterministic research spending decisions.")]
		public readonly int ResearchInterval = 250;

		public override object Create(ActorInitializer init) { return new CivilizationState(init, this); }
	}

	public sealed class CivilizationState : ITick, ISync
	{
		public static readonly string[] TechnologyNames =
		[
			"agricultural-systems",
			"energy-grid",
			"logistics",
			"civil-engineering",
			"research-networks"
		];

		static readonly int[] TechnologyCosts = [40, 60, 80, 100, 120];
		static readonly int[] TechnologyPrerequisites = [0, 0, 1 << 0, 1 << 2, 1 << 1];

		public readonly CivilizationStateInfo Info;
		readonly Player owner;

		[VerifySync]
		public readonly int FoundedTick;

		[VerifySync]
		int researchTicks;

		[VerifySync]
		public int CurrentTechnologyIndex;

		[VerifySync]
		public int ResearchProgress;

		[VerifySync]
		public int CompletedTechnologyMask;

		[VerifySync]
		int strategy;

		[VerifySync]
		public int StrategySequence;

		[VerifySync]
		public int StrategyTransitionTick;

		[VerifySync]
		public int SurvivalUtility;

		[VerifySync]
		public int ResearchUtility;

		[VerifySync]
		public int TradeUtility;

		[VerifySync]
		public int SecurityUtility;

		[VerifySync]
		public int RecoveryUtility;

		[VerifySync]
		public int WarUtility;

		[VerifySync]
		public int TradeDependency;

		[VerifySync]
		public int ActiveWars;

		[VerifySync]
		public int ResearchMaterialsSpent;

		[VerifySync]
		public int ResearchEnergySpent;

		public CivilizationStrategy Strategy
		{
			get => (CivilizationStrategy)strategy;
			set => strategy = (int)value;
		}

		public CivilizationState(ActorInitializer init, CivilizationStateInfo info)
		{
			Info = info;
			owner = init.Self.Owner;
			FoundedTick = init.Self.World.WorldTick;
		}

		public IEnumerable<Actor> Settlements(World world, Player owner)
		{
			return world.ActorsHavingTrait<SettlementCore>()
				.Where(actor => !actor.IsDead && actor.Owner == owner)
				.OrderBy(actor => actor.ActorID);
		}

		public string CurrentTechnology =>
			CurrentTechnologyIndex < TechnologyNames.Length ? TechnologyNames[CurrentTechnologyIndex] : null;

		public int CurrentTechnologyCost =>
			CurrentTechnologyIndex < TechnologyCosts.Length ? TechnologyCosts[CurrentTechnologyIndex] : 0;

		public string[] CompletedTechnologies =>
			TechnologyNames.Where((_, index) => HasTechnology(index)).ToArray();

		public bool HasTechnology(int index)
		{
			return (CompletedTechnologyMask & (1 << index)) != 0;
		}

		void ITick.Tick(Actor self)
		{
			if (++researchTicks < Math.Max(1, Info.ResearchInterval))
				return;

			researchTicks = 0;
			UpdateStrategy(self);
			SelectAvailableTechnology();
			if (CurrentTechnologyIndex >= TechnologyNames.Length)
				return;

			var settlements = Settlements(self.World, self.Owner).ToArray();
			var required = TechnologyCosts[CurrentTechnologyIndex] - ResearchProgress;
			var budget = Strategy switch
			{
				CivilizationStrategy.Survival => 1,
				CivilizationStrategy.Recovery => 1,
				CivilizationStrategy.Mobilization => Math.Max(1, required / 2),
				_ => required
			};
			foreach (var actor in settlements)
			{
				var settlement = actor.Trait<SettlementCore>();
				var contribution = Math.Min(
					Math.Min(settlement.Knowledge, required),
					Math.Min(budget, Math.Min(settlement.Materials * 4, settlement.Energy * 4)));
				if (contribution <= 0)
					continue;

				var materialsCost = DivideRoundUp(contribution, 4);
				var energyCost = DivideRoundUp(contribution, 4);
				settlement.Knowledge -= contribution;
				settlement.Materials -= materialsCost;
				settlement.Energy -= energyCost;
				ResearchProgress += contribution;
				ResearchMaterialsSpent += materialsCost;
				ResearchEnergySpent += energyCost;
				required -= contribution;
				budget -= contribution;
				if (required == 0 || budget == 0)
					break;
			}

			if (ResearchProgress < TechnologyCosts[CurrentTechnologyIndex])
				return;

			CompletedTechnologyMask |= 1 << CurrentTechnologyIndex;
			ResearchProgress = 0;
			SelectAvailableTechnology();
		}

		void SelectAvailableTechnology()
		{
			for (var index = 0; index < TechnologyNames.Length; index++)
			{
				if (HasTechnology(index))
					continue;

				if ((CompletedTechnologyMask & TechnologyPrerequisites[index]) ==
					TechnologyPrerequisites[index])
				{
					CurrentTechnologyIndex = index;
					return;
				}
			}

			CurrentTechnologyIndex = TechnologyNames.Length;
		}

		public int StrategicPressureAgainst(Player other, DiplomaticRelation relation)
		{
			if (other == null || relation == null || !Matches(relation, owner, other))
				return 0;

			if (owner.BotType == "steward")
				return 0;

			var baseline = owner.BotType switch
			{
				"rogue" => 450,
				"aggressor" => 450,
				"technologist" => 250,
				"economist" => 150,
				"fortress" => 100,
				_ => 0
			};
			var settlements = Settlements(owner.World, owner)
				.Select(actor => actor.Trait<SettlementCore>())
				.ToArray();
			var prosperity = settlements.Length == 0 ? 0 : (int)settlements.Average(s => s.Prosperity);
			var stability = settlements.Length == 0 ? 0 : (int)settlements.Average(s => s.Stability);
			var dependency = PairTradeDependency(owner.World, owner, other);
			var relativePower = RelativePower(owner, other);
			var researchCommitment = CurrentTechnologyCost == 0
				? 0
				: 100 + ResearchProgress * 200 / CurrentTechnologyCost;
			var casualtyAversion = Math.Min(250, settlements.Sum(s => s.WarCasualties));
			return Math.Clamp(
				baseline +
				(relativePower - 500) / 2 -
				(1000 - prosperity) / 2 -
				(1000 - stability) / 4 -
				dependency / 2 -
				researchCommitment / 4 -
				casualtyAversion,
				0,
				1000);
		}

		void UpdateStrategy(Actor self)
		{
			var settlements = Settlements(self.World, self.Owner)
				.Select(actor => actor.Trait<SettlementCore>())
				.ToArray();
			var prosperity = settlements.Length == 0 ? 0 : (int)settlements.Average(s => s.Prosperity);
			var stability = settlements.Length == 0 ? 0 : (int)settlements.Average(s => s.Stability);
			var food = settlements.Length == 0 ? 0 : (int)settlements.Average(s => s.FoodSatisfaction);
			ActiveWars = self.World.WorldActor.TraitOrDefault<DiplomacyManager>()?.Relations.Count(relation =>
				relation.State == DiplomaticRelationState.War &&
				(relation.PlayerA == self.Owner || relation.PlayerB == self.Owner)) ?? 0;
			TradeDependency = TotalTradeDependency(self.World, self.Owner);
			var opponents = self.World.Players
				.Where(player =>
					player != self.Owner &&
					player.Playable &&
					!player.NonCombatant &&
					!player.Spectating)
				.ToArray();
			var relativePower = opponents.Length == 0
				? 500
				: (int)opponents.Average(player => RelativePower(self.Owner, player));
			var personalityResearch = self.Owner.BotType == "technologist" ? 200 : 0;
			var personalityTrade = self.Owner.BotType == "economist" ? 200 : 0;
			SurvivalUtility = Math.Clamp(1000 - food + (1000 - prosperity) / 2, 0, 1000);
			ResearchUtility = Math.Clamp(
				700 - CompletedTechnologies.Length * 100 + personalityResearch -
				SurvivalUtility / 2 - ActiveWars * 150,
				0,
				1000);
			TradeUtility = Math.Clamp(SurvivalUtility + TradeDependency / 2 + personalityTrade, 0, 1000);
			SecurityUtility = Math.Clamp(
				ActiveWars * 400 + (1000 - stability) / 2 + Math.Max(0, 500 - relativePower),
				0,
				1000);
			RecoveryUtility = Math.Clamp(
				1000 - prosperity +
				1000 - stability +
				settlements.Sum(s => s.LastWarCasualties) * 20 +
				Math.Min(300, settlements.Sum(s => s.WarCasualties) * 10),
				0,
				1000);

			var diplomacy = self.World.WorldActor.TraitOrDefault<DiplomacyManager>();
			WarUtility = diplomacy?.Relations
				.Where(relation => relation.PlayerA == self.Owner || relation.PlayerB == self.Owner)
				.Select(relation =>
				{
					var other = relation.PlayerA == self.Owner ? relation.PlayerB : relation.PlayerA;
					return StrategicPressureAgainst(other, relation);
				})
				.DefaultIfEmpty(0)
				.Max() ?? 0;

			var next = ActiveWars > 0 && RecoveryUtility >= 700
				? CivilizationStrategy.Recovery
				: ActiveWars > 0
					? CivilizationStrategy.Mobilization
					: settlements.Sum(s => s.WarCasualties) > 0 && stability < 850
						? CivilizationStrategy.Recovery
						: SurvivalUtility >= 500
							? CivilizationStrategy.Survival
							: TradeUtility >= ResearchUtility && TradeUtility >= 600
								? CivilizationStrategy.Trade
								: ResearchUtility >= 500
									? CivilizationStrategy.Research
									: CivilizationStrategy.Development;
			if (Strategy == next)
				return;

			Strategy = next;
			StrategySequence++;
			StrategyTransitionTick = self.World.WorldTick;
		}

		static int TotalTradeDependency(World world, Player player)
		{
			var manager = world.WorldActor.TraitOrDefault<TradeManager>();
			if (manager == null)
				return 0;

			var incoming = 0;
			var outgoing = 0;
			foreach (var route in manager.Routes.Where(route =>
				route.Relation.PlayerA == player || route.Relation.PlayerB == player))
			{
				var isA = route.Relation.PlayerA == player;
				incoming += isA
					? route.FoodBToA + route.MaterialsBToA + route.EnergyBToA
					: route.FoodAToB + route.MaterialsAToB + route.EnergyAToB;
				outgoing += isA
					? route.FoodAToB + route.MaterialsAToB + route.EnergyAToB
					: route.FoodBToA + route.MaterialsBToA + route.EnergyBToA;
			}

			return Ratio(incoming, incoming + outgoing + 200);
		}

		static int PairTradeDependency(World world, Player player, Player other)
		{
			var route = world.WorldActor.TraitOrDefault<TradeManager>()?.Routes.FirstOrDefault(route =>
				(route.Relation.PlayerA == player && route.Relation.PlayerB == other) ||
				(route.Relation.PlayerA == other && route.Relation.PlayerB == player));
			if (route == null)
				return 0;

			var isA = route.Relation.PlayerA == player;
			var incoming = isA
				? route.FoodBToA + route.MaterialsBToA + route.EnergyBToA
				: route.FoodAToB + route.MaterialsAToB + route.EnergyAToB;
			var outgoing = isA
				? route.FoodAToB + route.MaterialsAToB + route.EnergyAToB
				: route.FoodBToA + route.MaterialsBToA + route.EnergyBToA;
			return Ratio(incoming, incoming + outgoing + 100);
		}

		static int RelativePower(Player player, Player other)
		{
			var own = player.PlayerActor.TraitOrDefault<PlayerStatistics>();
			var rival = other.PlayerActor.TraitOrDefault<PlayerStatistics>();
			var ownPower = (own?.ArmyValue ?? 0) + (own?.AssetsValue ?? 0) / 2;
			var rivalPower = (rival?.ArmyValue ?? 0) + (rival?.AssetsValue ?? 0) / 2;
			return Ratio(ownPower, ownPower + rivalPower);
		}

		static bool Matches(DiplomaticRelation relation, Player player, Player other)
		{
			return (relation.PlayerA == player && relation.PlayerB == other) ||
				(relation.PlayerA == other && relation.PlayerB == player);
		}

		static int Ratio(int numerator, int denominator)
		{
			if (denominator <= 0)
				return 500;

			return Math.Clamp((int)((long)Math.Max(0, numerator) * 1000 / denominator), 0, 1000);
		}

		static int DivideRoundUp(int value, int divisor)
		{
			return (value + divisor - 1) / divisor;
		}
	}

	[Desc("Marks a building as a deterministic contributor to nearby settlement capacity and production.")]
	public sealed class CivilInfrastructureInfo : ConditionalTraitInfo
	{
		[Desc("Housing capacity supplied by this building.")]
		public readonly int Housing;

		[Desc("Jobs supplied by this building.")]
		public readonly int Jobs;

		[Desc("Food produced on each civil pulse.")]
		public readonly int Food;

		[Desc("Materials produced on each civil pulse.")]
		public readonly int Materials;

		[Desc("Energy produced on each civil pulse.")]
		public readonly int Energy;

		[Desc("Knowledge produced on each civil pulse.")]
		public readonly int Knowledge;

		[Desc("Energy consumed on each civil pulse.")]
		public readonly int EnergyUse;

		[Desc("Materials consumed as maintenance on each civil pulse.")]
		public readonly int MaterialsUse;

		[Desc("Additional food storage capacity.")]
		public readonly int FoodStorage;

		[Desc("Additional materials storage capacity.")]
		public readonly int MaterialsStorage;

		[Desc("Additional energy storage capacity.")]
		public readonly int EnergyStorage;

		[Desc("Abstract bilateral trade throughput supplied by this building.")]
		public readonly int TradeCapacity;

		public override object Create(ActorInitializer init) { return new CivilInfrastructure(this); }
	}

	public sealed class CivilInfrastructure : ConditionalTrait<CivilInfrastructureInfo>
	{
		public CivilInfrastructure(CivilInfrastructureInfo info)
			: base(info) { }

		public bool IsActive => !IsTraitDisabled;
	}

	[Desc("Models a settlement's synchronized population, capacity, stocks, production, and needs.")]
	public sealed class SettlementCoreInfo : TraitInfo
	{
		[Desc("Ticks between economic and needs updates.")]
		public readonly int CivilPulseInterval = 250;

		[Desc("Ticks between demographic updates.")]
		public readonly int DemographicInterval = 3000;

		[Desc("Initial population.")]
		public readonly int InitialPopulation = 1000;

		[Desc("Initial food stock.")]
		public readonly int InitialFood = 600;

		[Desc("Initial materials stock.")]
		public readonly int InitialMaterials = 400;

		[Desc("Initial energy stock.")]
		public readonly int InitialEnergy = 300;

		[Desc("Food consumed per one thousand residents on each civil pulse.")]
		public readonly int FoodConsumptionPerThousand = 25;

		[Desc("Births per one thousand residents on each demographic pulse at high needs satisfaction.")]
		public readonly int BirthsPerThousand = 8;

		[Desc("Baseline deaths per one thousand residents on each demographic pulse.")]
		public readonly int DeathsPerThousand = 2;

		[Desc("Maximum additional deaths per one thousand residents during total food shortage.")]
		public readonly int ShortageDeathsPerThousand = 20;

		public override object Create(ActorInitializer init) { return new SettlementCore(init, this); }
	}

	public sealed class SettlementCore : ITick, ISync
	{
		readonly SettlementCoreInfo info;
		readonly Actor self;

		[VerifySync]
		int civilTicks;

		[VerifySync]
		int demographicTicks;

		[VerifySync]
		public int Population;

		[VerifySync]
		public int Children;

		[VerifySync]
		public int Adults;

		[VerifySync]
		public int Elders;

		[VerifySync]
		public int Food;

		[VerifySync]
		public int Materials;

		[VerifySync]
		public int Energy;

		[VerifySync]
		public int Knowledge;

		[VerifySync]
		public int Housing;

		[VerifySync]
		public int Jobs;

		[VerifySync]
		public int Employed;

		[VerifySync]
		public int AvailableWorkforce;

		[VerifySync]
		public int Mobilized;

		[VerifySync]
		public int ActiveWars;

		[VerifySync]
		public int WarCasualties;

		[VerifySync]
		public int LastWarCasualties;

		[VerifySync]
		public int LastMilitaryDeathsValue;

		[VerifySync]
		public int FoodStorage;

		[VerifySync]
		public int MaterialsStorage;

		[VerifySync]
		public int EnergyStorage;

		[VerifySync]
		public int FoodProduction;

		[VerifySync]
		public int MaterialsProduction;

		[VerifySync]
		public int EnergyProduction;

		[VerifySync]
		public int KnowledgeProduction;

		[VerifySync]
		public int FoodDemand;

		[VerifySync]
		public int EnergyDemand;

		[VerifySync]
		public int MaterialsDemand;

		[VerifySync]
		public int FoodSatisfaction;

		[VerifySync]
		public int HousingSatisfaction;

		[VerifySync]
		public int EnergySatisfaction;

		[VerifySync]
		public int EmploymentSatisfaction;

		[VerifySync]
		public int Prosperity;

		[VerifySync]
		public int Stability;

		[VerifySync]
		public int MigrationPressure;

		[VerifySync]
		public int LastPopulationDelta;

		[VerifySync]
		public int CivilPulseCount;

		[VerifySync]
		public int DemographicPulseCount;

		[VerifySync]
		public int InfrastructureCount;

		[VerifySync]
		public readonly int FoundedTick;

		public SettlementCore(ActorInitializer init, SettlementCoreInfo info)
		{
			self = init.Self;
			this.info = info;
			FoundedTick = self.World.WorldTick;
			Population = Math.Max(1, info.InitialPopulation);
			Children = Population * 22 / 100;
			Elders = Population * 13 / 100;
			Adults = Population - Children - Elders;
			var profile = self.World.WorldActor.TraitOrDefault<CivilizationScenario>()?.Profile ??
				CivilizationScenarioInfo.Balanced;
			Food = profile == CivilizationScenarioInfo.Scarcity
				? Math.Max(0, info.InitialFood / 4)
				: Math.Max(0, info.InitialFood);
			Materials = Math.Max(0, info.InitialMaterials);
			Energy = Math.Max(0, info.InitialEnergy);
			if (profile == CivilizationScenarioInfo.Trade)
			{
				switch (TradeSpecialization(self.Owner))
				{
					case 0:
						Food = 1200;
						Materials = 80;
						Energy = 60;
						break;
					case 1:
						Food = 80;
						Materials = 1200;
						Energy = 60;
						break;
					default:
						Food = 80;
						Materials = 80;
						Energy = 1000;
						break;
				}
			}

			FoodSatisfaction = 1000;
			HousingSatisfaction = 1000;
			EnergySatisfaction = 1000;
			EmploymentSatisfaction = 1000;
			Prosperity = 1000;
			Stability = 1000;
		}

		void ITick.Tick(Actor self)
		{
			if (++civilTicks >= Math.Max(1, info.CivilPulseInterval))
			{
				civilTicks = 0;
				RunCivilPulse();
			}

			if (++demographicTicks >= Math.Max(1, info.DemographicInterval))
			{
				demographicTicks = 0;
				RunDemographicPulse();
			}
		}

		void RunCivilPulse()
		{
			var infrastructure = self.World.ActorsHavingTrait<CivilInfrastructure>()
				.Where(actor => !actor.IsDead && actor.Owner == self.Owner && actor.Trait<CivilInfrastructure>().IsActive)
				.Where(IsClosestSettlement)
				.OrderBy(actor => actor.ActorID)
				.Select(actor => actor.Info.TraitInfo<CivilInfrastructureInfo>())
				.ToArray();

			InfrastructureCount = infrastructure.Length;
			Housing = infrastructure.Sum(i => Math.Max(0, i.Housing));
			Jobs = infrastructure.Sum(i => Math.Max(0, i.Jobs));
			FoodStorage = infrastructure.Sum(i => Math.Max(0, i.FoodStorage));
			MaterialsStorage = infrastructure.Sum(i => Math.Max(0, i.MaterialsStorage));
			EnergyStorage = infrastructure.Sum(i => Math.Max(0, i.EnergyStorage));
			var profile = self.World.WorldActor.TraitOrDefault<CivilizationScenario>()?.Profile ??
				CivilizationScenarioInfo.Balanced;
			FoodProduction = infrastructure.Sum(i => Math.Max(0, i.Food));
			if (profile == CivilizationScenarioInfo.Scarcity)
				FoodProduction /= 4;
			MaterialsProduction = infrastructure.Sum(i => Math.Max(0, i.Materials));
			EnergyProduction = infrastructure.Sum(i => Math.Max(0, i.Energy));
			KnowledgeProduction = infrastructure.Sum(i => Math.Max(0, i.Knowledge));
			if (profile == CivilizationScenarioInfo.Trade)
			{
				switch (TradeSpecialization(self.Owner))
				{
					case 0:
						FoodProduction *= 2;
						MaterialsProduction /= 4;
						EnergyProduction /= 4;
						break;
					case 1:
						FoodProduction /= 4;
						MaterialsProduction *= 2;
						EnergyProduction /= 4;
						break;
					default:
						FoodProduction /= 4;
						MaterialsProduction /= 4;
						EnergyProduction *= 2;
						break;
				}
			}

			var civilization = self.Owner.PlayerActor.TraitOrDefault<CivilizationState>();
			if (civilization?.HasTechnology(0) == true)
				FoodProduction = ApplyPercentage(FoodProduction, 125);
			if (civilization?.HasTechnology(1) == true)
				EnergyProduction = ApplyPercentage(EnergyProduction, 120);
			if (civilization?.HasTechnology(2) == true)
			{
				FoodStorage = ApplyPercentage(FoodStorage, 125);
				MaterialsStorage = ApplyPercentage(MaterialsStorage, 125);
				EnergyStorage = ApplyPercentage(EnergyStorage, 125);
			}

			if (civilization?.HasTechnology(3) == true)
				Housing = ApplyPercentage(Housing, 120);
			if (civilization?.HasTechnology(4) == true)
				KnowledgeProduction = ApplyPercentage(KnowledgeProduction, 125);
			UpdateWarCosts();
			var laborModifier = Adults == 0 ? 1000 : Ratio(AvailableWorkforce, Adults);
			FoodProduction = ApplyPerMille(FoodProduction, laborModifier);
			MaterialsProduction = ApplyPerMille(MaterialsProduction, laborModifier);
			EnergyProduction = ApplyPerMille(EnergyProduction, laborModifier);
			KnowledgeProduction = ApplyPerMille(KnowledgeProduction, laborModifier);
			EnergyDemand = infrastructure.Sum(i => Math.Max(0, i.EnergyUse));
			MaterialsDemand = infrastructure.Sum(i => Math.Max(0, i.MaterialsUse));
			FoodDemand = Math.Max(1, DivideRoundUp((long)Population * info.FoodConsumptionPerThousand, 1000));

			var availableFood = Food + FoodProduction;
			var availableMaterials = Materials + MaterialsProduction;
			var availableEnergy = Energy + EnergyProduction;
			FoodSatisfaction = Ratio(availableFood, FoodDemand);
			EnergySatisfaction = EnergyDemand == 0 ? 1000 : Ratio(availableEnergy, EnergyDemand);
			HousingSatisfaction = Ratio(Housing, Population);
			Employed = Math.Min(AvailableWorkforce, Jobs);
			EmploymentSatisfaction = Adults == 0 ? 1000 : Ratio(Employed, Adults);

			Food = ClampStock(availableFood - Math.Min(availableFood, FoodDemand), FoodStorage);
			Materials = ClampStock(
				availableMaterials - Math.Min(availableMaterials, MaterialsDemand),
				MaterialsStorage);
			Energy = ClampStock(availableEnergy - Math.Min(availableEnergy, EnergyDemand), EnergyStorage);
			Knowledge = Math.Max(0, Knowledge + KnowledgeProduction);

			Prosperity = (FoodSatisfaction + HousingSatisfaction + EnergySatisfaction + EmploymentSatisfaction) / 4;
			var stabilityTarget = Math.Clamp(
				Prosperity - ActiveWars * 100 - LastWarCasualties * 10,
				0,
				1000);
			Stability = (Stability * 3 + stabilityTarget) / 4;
			MigrationPressure = Math.Max(0, 1000 - Math.Min(Prosperity, Stability));
			CivilPulseCount++;
		}

		void UpdateWarCosts()
		{
			var statistics = self.Owner.PlayerActor.TraitOrDefault<PlayerStatistics>();
			var deathsValue = statistics?.DeathsCost ?? 0;
			var primarySettlement = self.Owner.PlayerActor
				.TraitOrDefault<CivilizationState>()?
				.Settlements(self.World, self.Owner)
				.FirstOrDefault();
			LastWarCasualties = 0;
			if (primarySettlement == self)
			{
				var newDeathsValue = Math.Max(0, deathsValue - LastMilitaryDeathsValue);
				LastWarCasualties = Math.Min(Adults, newDeathsValue / 500);
				Adults -= LastWarCasualties;
				WarCasualties += LastWarCasualties;
				Population = Math.Max(1, Children + Adults + Elders);
			}

			LastMilitaryDeathsValue = deathsValue;
			ActiveWars = self.World.WorldActor.TraitOrDefault<DiplomacyManager>()?.Relations.Count(relation =>
				relation.State == DiplomaticRelationState.War &&
				(relation.PlayerA == self.Owner || relation.PlayerB == self.Owner)) ?? 0;
			var armyValue = statistics?.ArmyValue ?? 0;
			var mobilizationDivisor = ActiveWars > 0 ? 80 : 250;
			Mobilized = Math.Min(Adults / 3, armyValue / mobilizationDivisor);
			AvailableWorkforce = Math.Max(0, Adults - Mobilized);
		}

		void RunDemographicPulse()
		{
			var births = Prosperity >= 750
				? Math.Max(1, (int)((long)Population * info.BirthsPerThousand / 1000))
				: 0;
			var baselineDeaths = Math.Max(1, (int)((long)Population * info.DeathsPerThousand / 1000));
			var shortageDeaths = (int)((long)Population *
				Math.Max(0, 1000 - FoodSatisfaction) *
				info.ShortageDeathsPerThousand / 1000000);
			var deaths = Math.Min(Population - 1, baselineDeaths + shortageDeaths);
			var childDeaths = Math.Min(Children, deaths / 3);
			var elderDeaths = Math.Min(Elders, deaths - childDeaths);
			var adultDeaths = deaths - childDeaths - elderDeaths;
			if (adultDeaths > Adults)
			{
				elderDeaths += adultDeaths - Adults;
				adultDeaths = Adults;
			}

			var childrenToAdults = Math.Min(Children - childDeaths, Math.Max(1, Children / 200));
			var adultsToElders = Math.Min(Adults - adultDeaths, Math.Max(1, Adults / 600));
			Children = Children - childDeaths - childrenToAdults + births;
			Adults = Adults - adultDeaths + childrenToAdults - adultsToElders;
			Elders = Elders - elderDeaths + adultsToElders;
			var previousPopulation = Population;
			Population = Math.Max(1, Children + Adults + Elders);
			LastPopulationDelta = Population - previousPopulation;
			DemographicPulseCount++;
		}

		bool IsClosestSettlement(Actor infrastructure)
		{
			var closest = self.World.ActorsHavingTrait<SettlementCore>()
				.Where(actor => !actor.IsDead && actor.Owner == self.Owner)
				.OrderBy(actor => DistanceSquared(actor.Location, infrastructure.Location))
				.ThenBy(actor => actor.ActorID)
				.FirstOrDefault();
			return closest == self;
		}

		static long DistanceSquared(CPos a, CPos b)
		{
			var dx = (long)a.X - b.X;
			var dy = (long)a.Y - b.Y;
			return dx * dx + dy * dy;
		}

		static int DivideRoundUp(long value, int divisor)
		{
			return checked((int)((value + divisor - 1) / divisor));
		}

		static int Ratio(int numerator, int denominator)
		{
			if (denominator <= 0)
				return 1000;

			return Math.Clamp((int)((long)Math.Max(0, numerator) * 1000 / denominator), 0, 1000);
		}

		static int ClampStock(int value, int capacity)
		{
			if (capacity <= 0)
				return 0;

			return Math.Clamp(value, 0, capacity);
		}

		static int ApplyPercentage(int value, int percentage)
		{
			return (int)((long)value * percentage / 100);
		}

		static int ApplyPerMille(int value, int perMille)
		{
			return (int)((long)value * perMille / 1000);
		}

		static int TradeSpecialization(Player owner)
		{
			return Math.Abs(owner.ClientIndex) % 3;
		}
	}
}
