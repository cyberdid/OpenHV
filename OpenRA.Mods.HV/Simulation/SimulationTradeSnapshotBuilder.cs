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

using System.Linq;
using OpenRA.Mods.HV.Traits;

namespace OpenRA.Mods.HV
{
	public static class SimulationTradeSnapshotBuilder
	{
		public static SimulationTradeRoute[] Build(World world)
		{
			var manager = world.WorldActor.TraitOrDefault<TradeManager>();
			return manager?.Routes
				.OrderBy(route => route.Relation.PlayerAIndex)
				.ThenBy(route => route.Relation.PlayerBIndex)
				.Select(route => new SimulationTradeRoute
				{
					RouteId = $"{route.Relation.PlayerA.InternalName}:{route.Relation.PlayerB.InternalName}",
					PlayerA = route.Relation.PlayerA.ResolvedPlayerName,
					PlayerB = route.Relation.PlayerB.ResolvedPlayerName,
					Status = StatusIdentifier(route.Status),
					StatusReason = ReasonIdentifier(route.StatusReason),
					DistanceCells = route.DistanceCells,
					Capacity = route.Capacity,
					Risk = route.Risk,
					StatusSequence = route.StatusSequence,
					ShipmentSequence = route.ShipmentSequence,
					LastTradeTick = route.LastTradeTick,
					LastResource = ResourceIdentifier(route.LastResource),
					LastAmount = route.LastAmount,
					LastExporter = route.LastExporterIndex == 0
						? route.Relation.PlayerA.ResolvedPlayerName
						: route.Relation.PlayerB.ResolvedPlayerName,
					FoodAToB = route.FoodAToB,
					FoodBToA = route.FoodBToA,
					MaterialsAToB = route.MaterialsAToB,
					MaterialsBToA = route.MaterialsBToA,
					EnergyAToB = route.EnergyAToB,
					EnergyBToA = route.EnergyBToA
				})
				.ToArray() ?? [];
		}

		static string StatusIdentifier(TradeRouteStatus status)
		{
			return status == TradeRouteStatus.Active ? "active" : "suspended";
		}

		static string ReasonIdentifier(TradeRouteReason reason)
		{
			return reason switch
			{
				TradeRouteReason.InitialAgreement => "initial-agreement",
				TradeRouteReason.WarSuspension => "war-suspension",
				TradeRouteReason.CapacityUnavailable => "capacity-unavailable",
				TradeRouteReason.Resumed => "resumed",
				_ => "unknown"
			};
		}

		static string ResourceIdentifier(TradeResource resource)
		{
			return resource switch
			{
				TradeResource.Food => "food",
				TradeResource.Materials => "materials",
				TradeResource.Energy => "energy",
				_ => "none"
			};
		}
	}
}
