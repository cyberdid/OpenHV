#region Copyright & License Information
/*
 * Copyright 2023 The OpenHV Developers (see CREDITS)
 * This file is part of OpenHV, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.IO;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.LoadScreens;
using OpenRA.Network;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.HV.LoadScreens
{
	public sealed class PanelLoadScreen : SheetLoadScreen
	{
		float2 panelPosition;
		Sprite panel;

		Sheet lastSheet;
		int lastDensity;
		Size lastResolution;

		public override void StartGame(Arguments args)
		{
			if (!args.Contains("Launch.Simulation"))
			{
				base.StartGame(args);
				return;
			}

			Launch = new LaunchArguments(args);
			if (string.IsNullOrEmpty(Launch.Map))
				throw new ArgumentException("Launch.Map must specify a map when Launch.Simulation is enabled.");

			var map = Game.ModData.MapCache.SingleOrDefault(m =>
				m.Uid == Launch.Map || Path.GetFileName(m.Path) == Launch.Map);
			if (map == null)
				throw new ArgumentException($"Could not find simulation map '{Launch.Map}'.");

			var config = SimulationConfig.Parse(args, map);
			var startedUtc = DateTime.UtcNow;
			var simulationComplete = false;
			SimulationTelemetryWriter telemetry = null;
			SimulationLifecycleMonitor lifecycle = null;

			Ui.ResetAll();
			Game.Settings.Save();

			OrderManager orderManager = null;
			string[] simulationSlots = null;
			var lobbyConfigurationIssued = false;
			var simulationStarted = false;
			var factionsPending = false;
			void StartSimulation()
			{
				if (simulationStarted)
					return;

				if (orderManager?.LocalClient == null || !orderManager.LocalClient.IsAdmin)
				{
					Game.RunAfterTick(StartSimulation);
					return;
				}

				var localClientIndex = orderManager.LocalClient.Index;
				if (!lobbyConfigurationIssued)
				{
					lobbyConfigurationIssued = true;
					simulationSlots = orderManager.LobbyInfo.Slots
						.Where(slot => slot.Value.AllowBots && !slot.Value.Closed)
						.Select(slot => slot.Key)
						.ToArray();
					if (simulationSlots.Length == 0)
						throw new InvalidOperationException(
							$"Simulation map '{map.Title}' does not contain any open bot-compatible slots.");

					if (config.RequestedRandomSeed.HasValue)
					{
						orderManager.LobbyInfo.GlobalSettings.RandomSeed = config.RequestedRandomSeed.Value;
						orderManager.IssueOrder(Order.Command($"sync_lobby {orderManager.LobbyInfo.Serialize()}"));
					}

					orderManager.IssueOrder(Order.Command("spectate"));
					for (var i = 0; i < simulationSlots.Length; i++)
					{
						var slotBotType = config.BotTypes[i % config.BotTypes.Length];
						orderManager.IssueOrder(
							Order.Command($"slot_bot {simulationSlots[i]} {localClientIndex} {slotBotType}"));
					}

					// Bots are placed before factions because the faction order needs the
					// bot client to already occupy the slot.
					if (config.Factions.Length > 0)
						factionsPending = true;

					orderManager.IssueOrder(Order.Command($"option gamespeed {config.GameSpeed}"));
					orderManager.IssueOrder(
						Order.Command($"option civilizationprofile {config.CivilizationProfile}"));
					orderManager.IssueOrder(
						Order.Command($"option tradeenabled {config.TradeEnabled}"));
					Game.RunAfterTick(StartSimulation);
					return;
				}

				var simulationClients = simulationSlots
					.Select(orderManager.LobbyInfo.ClientInSlot)
					.ToArray();
				if (simulationClients.Any(client => client == null))
				{
					Game.RunAfterTick(StartSimulation);
					return;
				}

				for (var i = 0; i < simulationClients.Length; i++)
				{
					var expectedBotType = config.BotTypes[i % config.BotTypes.Length];
					if (simulationClients[i].Bot != expectedBotType)
					{
						Game.RunAfterTick(StartSimulation);
						return;
					}
				}

				if (factionsPending)
				{
					factionsPending = false;
					for (var i = 0; i < simulationClients.Length; i++)
					{
						var faction = config.Factions[i % config.Factions.Length];
						orderManager.IssueOrder(
							Order.Command($"faction {simulationClients[i].Index} {faction}"));
					}

					Game.RunAfterTick(StartSimulation);
					return;
				}

				for (var i = 0; i < simulationClients.Length; i++)
				{
					if (config.Factions.Length == 0)
						break;

					var faction = config.Factions[i % config.Factions.Length];
					if (simulationClients[i].Faction != faction)
					{
						Game.RunAfterTick(StartSimulation);
						return;
					}
				}

				simulationStarted = true;
				Game.LobbyInfoChanged -= StartSimulation;
				config.EffectiveRandomSeed = orderManager.LobbyInfo.GlobalSettings.RandomSeed;
				Console.WriteLine(
					$"Starting autonomous simulation on {map.Title} with {simulationSlots.Length} bots: " +
					string.Join(", ", simulationSlots.Select((_, i) =>
						config.BotTypes[i % config.BotTypes.Length])) + ".");

				void FinishSimulation(SimulationEndReason endReason, string endDetail)
				{
					if (simulationComplete)
						return;

					simulationComplete = true;
					orderManager.World.SetLocalPauseState(true);
					lifecycle?.Update(orderManager.World);
					telemetry?.Complete(orderManager.World, endReason);

					if (!string.IsNullOrEmpty(config.ResultPath))
						SimulationResultWriter.Write(
							config.ResultPath,
							orderManager.World,
							config,
							lifecycle?.BuildSnapshot(),
							endReason,
							endDetail,
							startedUtc);

					// Natural victory already finalizes the World, but artificial
					// simulation cutoffs must do the same so replay metadata records
					// the terminal game tick before the connection is disposed.
					if (!orderManager.World.IsGameOver)
						orderManager.World.EndGame();

					Console.WriteLine($"Simulation ended: {endReason.ToIdentifier()} ({endDetail}).");
					Game.Exit();
				}

				void GameStarted()
				{
					Game.AfterGameStart -= GameStarted;
					lifecycle = new SimulationLifecycleMonitor(config);
					lifecycle.Update(orderManager.World);
					var nextTelemetryTick = config.TelemetryIntervalTicks;
					if (config.TelemetryIntervalTicks > 0 && !string.IsNullOrEmpty(config.ResultPath))
					{
						telemetry = new SimulationTelemetryWriter(config, lifecycle);
						telemetry.Start(orderManager.World);
					}

					orderManager.World.GameOver += () =>
						FinishSimulation(SimulationEndReason.NaturalVictory, "The engine declared the match complete.");

					void CheckWorldTick()
					{
						if (simulationComplete)
							return;

						lifecycle.Update(orderManager.World);
						if (telemetry != null && orderManager.World.WorldTick >= nextTelemetryTick)
						{
							telemetry.Capture(orderManager.World);
							nextTelemetryTick += config.TelemetryIntervalTicks;
						}

						if (lifecycle.AllFactionsCollapsed)
							FinishSimulation(
								SimulationEndReason.FactionCollapse,
								"All autonomous factions crossed a configured collapse boundary.");
						else if (config.ScenarioMode == SimulationConfig.LivingWorldScenario &&
							orderManager.World.WorldTick >= config.ObservationHorizonTicks)
							FinishSimulation(
								SimulationEndReason.ObservationHorizon,
								"Reached living-world observation horizon " +
								$"{config.ObservationHorizonTicks}.");
						else if (config.StalemateTerminates && lifecycle.StalemateAdvisory)
							FinishSimulation(
								SimulationEndReason.Stalemate,
								"No meaningful progress since world tick " +
								$"{lifecycle.LastMeaningfulActivityTick}; configured window " +
								$"{config.StalemateWindowTicks}.");
						else if (orderManager.World.WorldTick >= config.MaxWorldTicks)
							FinishSimulation(
								SimulationEndReason.WorldTickLimit,
								$"Reached configured world tick limit {config.MaxWorldTicks}.");
						else
							Game.RunAfterTick(CheckWorldTick);
					}

					Game.RunAfterTick(CheckWorldTick);

					if (config.WatchdogSeconds > 0)
						Game.RunAfterDelay(config.WatchdogSeconds * 1000, () =>
							FinishSimulation(
								SimulationEndReason.WatchdogTimeout,
								$"Exceeded wall-clock watchdog of {config.WatchdogSeconds} seconds."));
				}

				Game.AfterGameStart += GameStarted;
				orderManager.IssueOrder(Order.Command("startgame"));
			}

			Game.LobbyInfoChanged += StartSimulation;
			orderManager = Game.JoinServer(
				Game.CreateLocalServer(map.Uid, randomSeed: config.RequestedRandomSeed),
				"");
			Game.RunAfterTick(StartSimulation);
		}

		public override void DisplayInner(Renderer r, Sheet s, int density)
		{
			if (s != lastSheet || density != lastDensity)
			{
				lastSheet = s;
				lastDensity = density;
				panel = CreateSprite(s, density, new Rectangle(0, 0, 512, 512));
			}

			if (r.Resolution != lastResolution)
			{
				lastResolution = r.Resolution;
				panelPosition = new float2(lastResolution.Width / 2 - 256, lastResolution.Height / 2 - 256);
			}

			if (panel != null)
				r.RgbaSpriteRenderer.DrawSprite(panel, panelPosition);
		}
	}
}
