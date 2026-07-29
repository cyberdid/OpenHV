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
		readonly Actor self;

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

		public CivilizationState(ActorInitializer init, CivilizationStateInfo info)
		{
			self = init.Self;
			Info = info;
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
			SelectAvailableTechnology();
			if (CurrentTechnologyIndex >= TechnologyNames.Length)
				return;

			var settlements = Settlements(self.World, self.Owner).ToArray();
			var required = TechnologyCosts[CurrentTechnologyIndex] - ResearchProgress;
			foreach (var actor in settlements)
			{
				var settlement = actor.Trait<SettlementCore>();
				var contribution = Math.Min(settlement.Knowledge, required);
				settlement.Knowledge -= contribution;
				ResearchProgress += contribution;
				required -= contribution;
				if (required == 0)
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
			EnergyDemand = infrastructure.Sum(i => Math.Max(0, i.EnergyUse));
			MaterialsDemand = infrastructure.Sum(i => Math.Max(0, i.MaterialsUse));
			FoodDemand = Math.Max(1, DivideRoundUp((long)Population * info.FoodConsumptionPerThousand, 1000));

			var availableFood = Food + FoodProduction;
			var availableMaterials = Materials + MaterialsProduction;
			var availableEnergy = Energy + EnergyProduction;
			FoodSatisfaction = Ratio(availableFood, FoodDemand);
			EnergySatisfaction = EnergyDemand == 0 ? 1000 : Ratio(availableEnergy, EnergyDemand);
			HousingSatisfaction = Ratio(Housing, Population);
			Employed = Math.Min(Adults, Jobs);
			EmploymentSatisfaction = Adults == 0 ? 1000 : Ratio(Employed, Adults);

			Food = ClampStock(availableFood - Math.Min(availableFood, FoodDemand), FoodStorage);
			Materials = ClampStock(
				availableMaterials - Math.Min(availableMaterials, MaterialsDemand),
				MaterialsStorage);
			Energy = ClampStock(availableEnergy - Math.Min(availableEnergy, EnergyDemand), EnergyStorage);
			Knowledge = Math.Max(0, Knowledge + KnowledgeProduction);

			Prosperity = (FoodSatisfaction + HousingSatisfaction + EnergySatisfaction + EmploymentSatisfaction) / 4;
			Stability = (Stability * 3 + Prosperity) / 4;
			MigrationPressure = Math.Max(0, 1000 - Prosperity);
			CivilPulseCount++;
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

		static int TradeSpecialization(Player owner)
		{
			return Math.Abs(owner.ClientIndex) % 3;
		}
	}
}
