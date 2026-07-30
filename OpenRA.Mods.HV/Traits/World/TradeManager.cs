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
using OpenRA.Traits;

namespace OpenRA.Mods.HV.Traits
{
	public enum TradeResource
	{
		None,
		Food,
		Materials,
		Energy
	}

	public enum TradeRouteStatus
	{
		Active,
		Suspended
	}

	public enum TradeRouteReason
	{
		InitialAgreement,
		WarSuspension,
		CapacityUnavailable,
		Resumed
	}

	[TraitLocation(SystemActors.World)]
	[Desc("Runs synchronized, stock-backed bilateral trade between non-hostile living factions.")]
	[IncludeStaticFluentReferences(typeof(TradeManager))]
	public sealed class TradeManagerInfo : TraitInfo, ILobbyOptions
	{
		public const string OptionId = "tradeenabled";

		[FluentReference]
		public readonly string CheckboxLabel = "options-living-trade.label";

		[FluentReference]
		public readonly string CheckboxDescription = "options-living-trade.description";

		public readonly bool CheckboxEnabled = true;
		public readonly bool CheckboxLocked;
		public readonly bool CheckboxVisible = true;
		public readonly int CheckboxDisplayOrder = 10;

		[Desc("Ticks between route shipment decisions.")]
		public readonly int TradeInterval = 250;

		[Desc("Civil demand pulses each settlement tries to retain before exporting.")]
		public readonly int ReservePulses = 8;

		[Desc("Route risk added per map cell of capital-to-capital distance.")]
		public readonly int DistanceRiskPerCell = 2;

		[Desc("Route risk added for each third-party war involving either partner.")]
		public readonly int ThirdPartyWarRisk = 150;

		[Desc("Maximum non-bilateral-war route risk on the 0-1000 scale.")]
		public readonly int MaximumOperationalRisk = 750;

		IEnumerable<LobbyOption> ILobbyOptions.LobbyOptions(MapPreview map)
		{
			yield return new LobbyBooleanOption(
				map,
				OptionId,
				CheckboxLabel,
				CheckboxDescription,
				CheckboxVisible,
				CheckboxDisplayOrder,
				CheckboxEnabled,
				CheckboxLocked);
		}

		public override object Create(ActorInitializer init) { return new TradeManager(init.Self, this); }
	}

	public sealed class TradeManager : INotifyCreated, IWorldLoaded, ITick, ISync
	{
		readonly Actor self;
		readonly TradeManagerInfo info;
		readonly List<TradeRoute> routes = [];
		DiplomacyManager diplomacy;
		int tradeTicks;

		[VerifySync]
		public bool Enabled;

		public IReadOnlyList<TradeRoute> Routes => routes;

		public TradeManager(Actor self, TradeManagerInfo info)
		{
			this.self = self;
			this.info = info;
			Enabled = info.CheckboxEnabled;
		}

		void INotifyCreated.Created(Actor self)
		{
			Enabled = self.World.LobbyInfo.GlobalSettings.OptionOrDefault(
				TradeManagerInfo.OptionId,
				info.CheckboxEnabled);
		}

		void IWorldLoaded.WorldLoaded(World world, WorldRenderer worldRenderer)
		{
			if (!Game.IsDeterministicSimulation || !Enabled)
				return;

			diplomacy = world.WorldActor.Trait<DiplomacyManager>();
			foreach (var relation in diplomacy.Relations)
			{
				var route = new TradeRoute(relation);
				routes.Add(route);
				world.Add(route);
			}
		}

		void ITick.Tick(Actor self)
		{
			if (!Enabled)
				return;

			if (++tradeTicks < Math.Max(1, info.TradeInterval))
				return;

			tradeTicks = 0;
			foreach (var route in routes)
				UpdateRoute(route);
		}

		void UpdateRoute(TradeRoute route)
		{
			if (route.Relation.State == DiplomaticRelationState.War)
			{
				route.Capacity = 0;
				route.Risk = 1000;
				SetStatus(route, TradeRouteStatus.Suspended, TradeRouteReason.WarSuspension);
				return;
			}

			var settlementA = PrimarySettlement(route.Relation.PlayerA);
			var settlementB = PrimarySettlement(route.Relation.PlayerB);
			if (settlementA == null || settlementB == null)
			{
				route.Capacity = 0;
				SetStatus(route, TradeRouteStatus.Suspended, TradeRouteReason.CapacityUnavailable);
				return;
			}

			var coreA = settlementA.Trait<SettlementCore>();
			var coreB = settlementB.Trait<SettlementCore>();
			route.DistanceCells = ManhattanDistance(settlementA.Location, settlementB.Location);
			route.Risk = OperationalRisk(route);
			var endpointCapacity = Math.Min(
				EndpointCapacity(route.Relation.PlayerA, settlementA),
				EndpointCapacity(route.Relation.PlayerB, settlementB));
			route.Capacity = endpointCapacity * (1000 - route.Risk) / 1000;
			if (route.Capacity <= 0)
			{
				SetStatus(route, TradeRouteStatus.Suspended, TradeRouteReason.CapacityUnavailable);
				return;
			}

			if (route.Status == TradeRouteStatus.Suspended)
				SetStatus(route, TradeRouteStatus.Active, TradeRouteReason.Resumed);

			var shipment = BestShipment(coreA, coreB, route.Capacity);
			if (shipment.Amount <= 0)
				return;

			var source = shipment.ExporterIndex == 0 ? coreA : coreB;
			var destination = shipment.ExporterIndex == 0 ? coreB : coreA;
			SetStock(source, shipment.Resource, GetStock(source, shipment.Resource) - shipment.Amount);
			SetStock(destination, shipment.Resource, GetStock(destination, shipment.Resource) + shipment.Amount);
			route.RecordShipment(shipment.Resource, shipment.Amount, shipment.ExporterIndex, self.World.WorldTick);
		}

		Shipment BestShipment(SettlementCore coreA, SettlementCore coreB, int capacity)
		{
			Shipment best = default;
			foreach (var resource in new[] { TradeResource.Food, TradeResource.Materials, TradeResource.Energy })
			{
				ConsiderShipment(ref best, coreA, coreB, resource, 0, capacity);
				ConsiderShipment(ref best, coreB, coreA, resource, 1, capacity);
			}

			return best;
		}

		void ConsiderShipment(
			ref Shipment best,
			SettlementCore source,
			SettlementCore destination,
			TradeResource resource,
			int exporterIndex,
			int capacity)
		{
			var sourceStock = GetStock(source, resource);
			var destinationStock = GetStock(destination, resource);
			var sourceReserve = ReserveTarget(source, resource);
			var surplus = Math.Max(0, sourceStock - sourceReserve);

			// Trade levels holdings; it does not only relieve emergencies. Buying
			// solely to climb back to an absolute floor means nothing moves unless
			// somebody is close to running out, and across the 112-match baseline
			// nobody ever was: 672 routes carried zero goods. Half the gap, so a
			// single shipment cannot overshoot into the exporter being the poorer
			// of the two.
			var deficit = Math.Max(0, (sourceStock - destinationStock) / 2);
			var freeStorage = Math.Max(0, GetStorage(destination, resource) - destinationStock);
			var amount = Math.Min(capacity, Math.Min(surplus, Math.Min(deficit, freeStorage)));
			if (amount > best.Amount)
				best = new Shipment(resource, amount, exporterIndex);
		}

		int ReserveTarget(SettlementCore settlement, TradeResource resource)
		{
			var demand = resource switch
			{
				TradeResource.Food => settlement.FoodDemand,
				TradeResource.Materials => settlement.MaterialsDemand,
				TradeResource.Energy => settlement.EnergyDemand,
				_ => 0
			};
			var minimumReserve = resource == TradeResource.Food ? 200 : 160;
			return Math.Max(
				demand * Math.Max(1, info.ReservePulses),
				Math.Min(GetStorage(settlement, resource), minimumReserve));
		}

		int OperationalRisk(TradeRoute route)
		{
			var thirdPartyWars = diplomacy.Relations.Count(relation =>
				relation != route.Relation &&
				relation.State == DiplomaticRelationState.War &&
				(relation.PlayerA == route.Relation.PlayerA ||
					relation.PlayerB == route.Relation.PlayerA ||
					relation.PlayerA == route.Relation.PlayerB ||
					relation.PlayerB == route.Relation.PlayerB));
			return Math.Min(
				Math.Max(0, info.MaximumOperationalRisk),
				route.DistanceCells * Math.Max(0, info.DistanceRiskPerCell) +
				thirdPartyWars * Math.Max(0, info.ThirdPartyWarRisk));
		}

		static Actor PrimarySettlement(Player player)
		{
			return player.World.ActorsHavingTrait<SettlementCore>()
				.Where(actor => !actor.IsDead && actor.Owner == player)
				.OrderBy(actor => actor.ActorID)
				.FirstOrDefault();
		}

		static int EndpointCapacity(Player player, Actor settlement)
		{
			return player.World.ActorsHavingTrait<CivilInfrastructure>()
				.Where(actor =>
					!actor.IsDead &&
					actor.Owner == player &&
					actor.Trait<CivilInfrastructure>().IsActive &&
					ClosestSettlement(player, actor) == settlement)
				.Sum(actor => Math.Max(0, actor.Info.TraitInfo<CivilInfrastructureInfo>().TradeCapacity));
		}

		static Actor ClosestSettlement(Player player, Actor infrastructure)
		{
			return player.World.ActorsHavingTrait<SettlementCore>()
				.Where(actor => !actor.IsDead && actor.Owner == player)
				.OrderBy(actor => DistanceSquared(actor.Location, infrastructure.Location))
				.ThenBy(actor => actor.ActorID)
				.FirstOrDefault();
		}

		[FluentReference]
		const string RouteLine = "notification-trade-route";

		[FluentReference]
		const string StatusActive = "trade-status-active";

		[FluentReference]
		const string StatusSuspended = "trade-status-suspended";

		[FluentReference]
		const string ReasonInitialAgreement = "trade-reason-initial-agreement";

		[FluentReference]
		const string ReasonWarSuspension = "trade-reason-war-suspension";

		[FluentReference]
		const string ReasonCapacityUnavailable = "trade-reason-capacity-unavailable";

		[FluentReference]
		const string ReasonResumed = "trade-reason-resumed";

		static void SetStatus(TradeRoute route, TradeRouteStatus status, TradeRouteReason reason)
		{
			if (route.Status == status && route.StatusReason == reason)
				return;

			route.Status = status;
			route.StatusReason = reason;
			route.StatusSequence++;

			// Display only; see the note on DiplomacyManager.Announce.
			TextNotificationsManager.AddSystemLine(
				FluentProvider.GetMessage(
					RouteLine,
					"first", route.Relation.PlayerA.PlayerName,
					"second", route.Relation.PlayerB.PlayerName,
					"status", FluentProvider.GetMessage(
						status == TradeRouteStatus.Active
							? StatusActive
							: StatusSuspended),
					"reason", FluentProvider.GetMessage(ReasonKey(reason))));
		}

		static string ReasonKey(TradeRouteReason reason)
		{
			return reason switch
			{
				TradeRouteReason.InitialAgreement => ReasonInitialAgreement,
				TradeRouteReason.WarSuspension => ReasonWarSuspension,
				TradeRouteReason.CapacityUnavailable => ReasonCapacityUnavailable,
				_ => ReasonResumed
			};
		}

		static int GetStock(SettlementCore settlement, TradeResource resource)
		{
			return resource switch
			{
				TradeResource.Food => settlement.Food,
				TradeResource.Materials => settlement.Materials,
				TradeResource.Energy => settlement.Energy,
				_ => 0
			};
		}

		static void SetStock(SettlementCore settlement, TradeResource resource, int value)
		{
			switch (resource)
			{
				case TradeResource.Food:
					settlement.Food = value;
					break;
				case TradeResource.Materials:
					settlement.Materials = value;
					break;
				case TradeResource.Energy:
					settlement.Energy = value;
					break;
			}
		}

		static int GetStorage(SettlementCore settlement, TradeResource resource)
		{
			return resource switch
			{
				TradeResource.Food => settlement.FoodStorage,
				TradeResource.Materials => settlement.MaterialsStorage,
				TradeResource.Energy => settlement.EnergyStorage,
				_ => 0
			};
		}

		static int ManhattanDistance(CPos a, CPos b)
		{
			return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
		}

		static long DistanceSquared(CPos a, CPos b)
		{
			var dx = (long)a.X - b.X;
			var dy = (long)a.Y - b.Y;
			return dx * dx + dy * dy;
		}

		readonly record struct Shipment(TradeResource Resource = TradeResource.None, int Amount = 0, int ExporterIndex = 0);
	}

	public sealed class TradeRoute : IEffect, ISync
	{
		public readonly DiplomaticRelation Relation;

		[VerifySync]
		int status;

		[VerifySync]
		int statusReason;

		[VerifySync]
		int lastResource;

		[VerifySync]
		public int DistanceCells;

		[VerifySync]
		public int Capacity;

		[VerifySync]
		public int Risk;

		[VerifySync]
		public int StatusSequence;

		[VerifySync]
		public int ShipmentSequence;

		[VerifySync]
		public int LastTradeTick;

		[VerifySync]
		public int LastAmount;

		[VerifySync]
		public int LastExporterIndex;

		[VerifySync]
		public int FoodAToB;

		[VerifySync]
		public int FoodBToA;

		[VerifySync]
		public int MaterialsAToB;

		[VerifySync]
		public int MaterialsBToA;

		[VerifySync]
		public int EnergyAToB;

		[VerifySync]
		public int EnergyBToA;

		public TradeRouteStatus Status
		{
			get => (TradeRouteStatus)status;
			set => status = (int)value;
		}

		public TradeRouteReason StatusReason
		{
			get => (TradeRouteReason)statusReason;
			set => statusReason = (int)value;
		}

		public TradeResource LastResource
		{
			get => (TradeResource)lastResource;
			set => lastResource = (int)value;
		}

		public TradeRoute(DiplomaticRelation relation)
		{
			Relation = relation;
			Status = TradeRouteStatus.Active;
			StatusReason = TradeRouteReason.InitialAgreement;
		}

		public void RecordShipment(TradeResource resource, int amount, int exporterIndex, int worldTick)
		{
			LastResource = resource;
			LastAmount = amount;
			LastExporterIndex = exporterIndex;
			LastTradeTick = worldTick;
			ShipmentSequence++;
			switch ((resource, exporterIndex))
			{
				case (TradeResource.Food, 0): FoodAToB += amount; break;
				case (TradeResource.Food, 1): FoodBToA += amount; break;
				case (TradeResource.Materials, 0): MaterialsAToB += amount; break;
				case (TradeResource.Materials, 1): MaterialsBToA += amount; break;
				case (TradeResource.Energy, 0): EnergyAToB += amount; break;
				case (TradeResource.Energy, 1): EnergyBToA += amount; break;
			}
		}

		void IEffect.Tick(World world) { }

		IEnumerable<IRenderable> IEffect.Render(WorldRenderer renderer)
		{
			return [];
		}
	}
}
