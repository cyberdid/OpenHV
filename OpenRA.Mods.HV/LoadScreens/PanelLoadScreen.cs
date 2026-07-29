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

			var botType = args.GetValue("Launch.SimulationBot", "rogue");
			var botTypes = args.GetValue("Launch.SimulationBots", botType)
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			if (botTypes.Length == 0)
				throw new ArgumentException("Launch.SimulationBots must specify at least one bot type.");

			var gameSpeed = args.GetValue("Launch.SimulationSpeed", "fastest");
			var resultPath = args.GetValue("Launch.SimulationResult", "");
			var durationSeconds = int.TryParse(args.GetValue("Launch.SimulationDuration", "0"), out var duration) ?
				Math.Max(0, duration) : 0;
			var randomSeed = int.TryParse(args.GetValue("Launch.SimulationSeed", ""), out var seed) ?
				seed : (int?)null;
			var startedUtc = DateTime.UtcNow;
			var simulationComplete = false;

			Ui.ResetAll();
			Game.Settings.Save();

			OrderManager orderManager = null;
			void StartSimulation()
			{
				if (orderManager?.LocalClient == null || !orderManager.LocalClient.IsAdmin)
					return;

				Game.LobbyInfoChanged -= StartSimulation;

				var localClientIndex = orderManager.LocalClient.Index;
				var simulationSlots = orderManager.LobbyInfo.Slots
					.Where(slot => slot.Value.AllowBots && !slot.Value.Closed)
					.Select(slot => slot.Key)
					.ToArray();

				Console.WriteLine(
					$"Starting autonomous simulation on {map.Title} with {simulationSlots.Length} bots: " +
					string.Join(", ", simulationSlots.Select((_, i) => botTypes[i % botTypes.Length])) + ".");

				if (randomSeed.HasValue)
				{
					orderManager.LobbyInfo.GlobalSettings.RandomSeed = randomSeed.Value;
					orderManager.IssueOrder(Order.Command($"sync_lobby {orderManager.LobbyInfo.Serialize()}"));
				}

				orderManager.IssueOrder(Order.Command("spectate"));
				for (var i = 0; i < simulationSlots.Length; i++)
				{
					var slotBotType = botTypes[i % botTypes.Length];
					orderManager.IssueOrder(Order.Command($"slot_bot {simulationSlots[i]} {localClientIndex} {slotBotType}"));
				}

				orderManager.IssueOrder(Order.Command($"option gamespeed {gameSpeed}"));

				void FinishSimulation(bool timedOut)
				{
					if (simulationComplete)
						return;

					simulationComplete = true;
					if (!string.IsNullOrEmpty(resultPath))
						SimulationResultWriter.Write(
							resultPath,
							orderManager.World,
							map.Title,
							randomSeed,
							timedOut,
							startedUtc);

					Console.WriteLine(timedOut ? "Simulation time limit reached." : "Simulation completed naturally.");
					Game.Exit();
				}

				void GameStarted()
				{
					Game.AfterGameStart -= GameStarted;
					orderManager.World.GameOver += () => FinishSimulation(false);

					if (durationSeconds > 0)
						Game.RunAfterDelay(durationSeconds * 1000, () => FinishSimulation(true));
				}

				Game.AfterGameStart += GameStarted;
				orderManager.IssueOrder(Order.Command("startgame"));
			}

			Game.LobbyInfoChanged += StartSimulation;
			orderManager = Game.JoinServer(Game.CreateLocalServer(map.Uid), "");
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
