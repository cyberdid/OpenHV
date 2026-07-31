#region Copyright & License Information
/*
 * Copyright 2019-2025 The OpenHV Developers (see CREDITS)
 * This file is part of OpenHV, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Lint;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.HV.Traits;
using OpenRA.Network;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.HV.Widgets.Logic
{
	public enum ObserverStatsPanel { None, Basic, Economy, Production, SupportPowers, Combat, Army, Civilization, Relations, Graph, ArmyGraph }

	[ChromeLogicArgsHotkeys(
		"StatisticsBasicKey",
		"StatisticsEconomyKey",
		"StatisticsProductionKey",
		"StatisticsSupportPowersKey",
		"StatisticsCombatKey",
		"StatisticsArmyKey",
		"StatisticsGraphKey",
		"StatisticsArmyGraphKey")]
	public class HardVacuumObserverStatsLogic : ChromeLogic
	{
		[FluentReference]
		const string InformationNone = "options-observer-stats.none";

		[FluentReference]
		const string Basic = "options-observer-stats.basic";

		[FluentReference]
		const string Economy = "options-observer-stats.economy";

		[FluentReference]
		const string Production = "options-observer-stats.production";

		[FluentReference]
		const string SupportPowers = "options-observer-stats.support-powers";

		[FluentReference]
		const string Combat = "options-observer-stats.combat";

		[FluentReference]
		const string Army = "options-observer-stats.army";

		[FluentReference]
		const string Civilization = "options-observer-stats.civilization";

		[FluentReference]
		const string Relations = "options-observer-stats.relations";

		// Chosen per row from the relation's own state, so the lint cannot see
		// them at the call site.
		[FluentReference]
		const string StateNeutral = "relations-state-neutral";

		[FluentReference]
		const string StateWar = "relations-state-war";

		[FluentReference]
		const string StateAlliance = "relations-state-alliance";

		[FluentReference]
		const string TradeOpen = "trade-status-active";

		[FluentReference]
		const string TradeSuspended = "trade-status-suspended";

		[FluentReference]
		const string EarningsGraph = "options-observer-stats.earnings-graph";

		[FluentReference]
		const string ArmyGraph = "options-observer-stats.army-graph";

		[FluentReference("team")]
		const string TeamNumber = "label-team-name";

		[FluentReference]
		const string NoTeam = "label-no-team";

		readonly ContainerWidget basicStatsHeaders;
		readonly ContainerWidget economyStatsHeaders;
		readonly ContainerWidget productionStatsHeaders;
		readonly ContainerWidget supportPowerStatsHeaders;
		readonly ContainerWidget combatStatsHeaders;
		readonly ContainerWidget civilizationStatsHeaders;
		readonly ContainerWidget relationsStatsHeaders;
		readonly ContainerWidget armyHeaders;
		readonly ScrollPanelWidget playerStatsPanel;
		readonly ScrollItemWidget basicPlayerTemplate;
		readonly ScrollItemWidget economyPlayerTemplate;
		readonly ScrollItemWidget productionPlayerTemplate;
		readonly ScrollItemWidget supportPowersPlayerTemplate;
		readonly ScrollItemWidget armyPlayerTemplate;
		readonly ScrollItemWidget combatPlayerTemplate;
		readonly ScrollItemWidget civilizationPlayerTemplate;
		readonly ScrollItemWidget relationsTemplate;
		readonly ContainerWidget incomeGraphContainer;
		readonly ContainerWidget armyValueGraphContainer;
		readonly ScrollableLineGraphWidget incomeGraph;
		readonly ScrollableLineGraphWidget armyValueGraph;
		readonly ScrollItemWidget teamTemplate;
		readonly Player[] players;
		readonly IGrouping<int, Player>[] teams;
		readonly bool hasTeams;
		readonly World world;
		readonly WorldRenderer worldRenderer;

		readonly string clickSound = ChromeMetrics.Get<string>("ClickSound");
		ObserverStatsPanel activePanel;

		readonly CultureInfo englishDollar = new("en-US");

		[ObjectCreator.UseCtor]
		public HardVacuumObserverStatsLogic(World world, ModData modData, WorldRenderer worldRenderer, Widget widget, Dictionary<string, MiniYaml> logicArgs)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;

			MiniYaml yaml;
			var keyNames = Enum.GetNames<ObserverStatsPanel>();
			var statsHotkeys = new HotkeyReference[keyNames.Length];
			for (var i = 0; i < keyNames.Length; i++)
				statsHotkeys[i] = logicArgs.TryGetValue("Statistics" + keyNames[i] + "Key", out yaml) ? modData.Hotkeys[yaml.Value] : new HotkeyReference();

			players = world.Players.Where(p => !p.NonCombatant && p.Playable).ToArray();
			teams = players
				.GroupBy(p => (world.LobbyInfo.ClientWithIndex(p.ClientIndex) ?? new Session.Client()).Team)
				.OrderBy(g => g.Key)
				.ToArray();
			hasTeams = !(teams.Length == 1 && teams[0].Key == 0);

			basicStatsHeaders = widget.Get<ContainerWidget>("BASIC_STATS_HEADERS");
			economyStatsHeaders = widget.Get<ContainerWidget>("ECONOMY_STATS_HEADERS");
			productionStatsHeaders = widget.Get<ContainerWidget>("PRODUCTION_STATS_HEADERS");
			supportPowerStatsHeaders = widget.Get<ContainerWidget>("SUPPORT_POWERS_HEADERS");
			armyHeaders = widget.Get<ContainerWidget>("ARMY_HEADERS");
			combatStatsHeaders = widget.Get<ContainerWidget>("COMBAT_STATS_HEADERS");
			civilizationStatsHeaders = widget.Get<ContainerWidget>("CIVILIZATION_STATS_HEADERS");
			relationsStatsHeaders = widget.Get<ContainerWidget>("RELATIONS_STATS_HEADERS");

			playerStatsPanel = widget.Get<ScrollPanelWidget>("PLAYER_STATS_PANEL");
			playerStatsPanel.Layout = new GridLayout(playerStatsPanel);
			playerStatsPanel.IgnoreMouseOver = true;

			if (ShowScrollBar)
			{
				playerStatsPanel.ScrollBar = ScrollBar.Left;

				AdjustHeader(basicStatsHeaders);
				AdjustHeader(economyStatsHeaders);
				AdjustHeader(productionStatsHeaders);
				AdjustHeader(supportPowerStatsHeaders);
				AdjustHeader(combatStatsHeaders);
				AdjustHeader(civilizationStatsHeaders);
				AdjustHeader(armyHeaders);
			}

			basicPlayerTemplate = playerStatsPanel.Get<ScrollItemWidget>("BASIC_PLAYER_TEMPLATE");
			economyPlayerTemplate = playerStatsPanel.Get<ScrollItemWidget>("ECONOMY_PLAYER_TEMPLATE");
			productionPlayerTemplate = playerStatsPanel.Get<ScrollItemWidget>("PRODUCTION_PLAYER_TEMPLATE");
			supportPowersPlayerTemplate = playerStatsPanel.Get<ScrollItemWidget>("SUPPORT_POWERS_PLAYER_TEMPLATE");
			armyPlayerTemplate = playerStatsPanel.Get<ScrollItemWidget>("ARMY_PLAYER_TEMPLATE");
			combatPlayerTemplate = playerStatsPanel.Get<ScrollItemWidget>("COMBAT_PLAYER_TEMPLATE");
			civilizationPlayerTemplate = playerStatsPanel.Get<ScrollItemWidget>("CIVILIZATION_PLAYER_TEMPLATE");
			relationsTemplate = playerStatsPanel.Get<ScrollItemWidget>("RELATIONS_TEMPLATE");

			incomeGraphContainer = widget.Get<ContainerWidget>("INCOME_GRAPH_CONTAINER");
			incomeGraph = incomeGraphContainer.Get<ScrollableLineGraphWidget>("INCOME_GRAPH");

			armyValueGraphContainer = widget.Get<ContainerWidget>("ARMY_VALUE_GRAPH_CONTAINER");
			armyValueGraph = armyValueGraphContainer.Get<ScrollableLineGraphWidget>("ARMY_VALUE_GRAPH");

			teamTemplate = playerStatsPanel.Get<ScrollItemWidget>("TEAM_TEMPLATE");

			var statsDropDown = widget.Get<DropDownButtonWidget>("STATS_DROPDOWN");
			StatsDropDownOption CreateStatsOption(string title, ObserverStatsPanel panel, ScrollItemWidget template, Action a)
			{
				title = FluentProvider.GetMessage(title);
				return new StatsDropDownOption
				{
					Title = FluentProvider.GetMessage(title),
					IsSelected = () => activePanel == panel,
					OnClick = () =>
					{
						ClearStats();
						playerStatsPanel.Visible = true;
						statsDropDown.GetText = () => title;
						activePanel = panel;
						if (template != null)
							AdjustStatisticsPanel(template);

						a();
						Ui.ResetTooltips();
					}
				};
			}

			var statsDropDownOptions = new StatsDropDownOption[]
			{
				new()
				{
					Title = FluentProvider.GetMessage(InformationNone),
					IsSelected = () => activePanel == ObserverStatsPanel.None,
					OnClick = () =>
					{
						var informationNone = FluentProvider.GetMessage(InformationNone);
						statsDropDown.GetText = () => informationNone;
						playerStatsPanel.Visible = false;
						ClearStats();
						activePanel = ObserverStatsPanel.None;
					}
				},
				CreateStatsOption(Basic, ObserverStatsPanel.Basic, basicPlayerTemplate, () => DisplayStats(BasicStats)),
				CreateStatsOption(Economy, ObserverStatsPanel.Economy, economyPlayerTemplate, () => DisplayStats(EconomyStats)),
				CreateStatsOption(Production, ObserverStatsPanel.Production, productionPlayerTemplate, () => DisplayStats(ProductionStats)),
				CreateStatsOption(SupportPowers, ObserverStatsPanel.SupportPowers, supportPowersPlayerTemplate, () => DisplayStats(SupportPowerStats)),
				CreateStatsOption(Combat, ObserverStatsPanel.Combat, combatPlayerTemplate, () => DisplayStats(CombatStats)),
				CreateStatsOption(Army, ObserverStatsPanel.Army, armyPlayerTemplate, () => DisplayStats(ArmyStats)),
					CreateStatsOption(Civilization, ObserverStatsPanel.Civilization, civilizationPlayerTemplate, () => DisplayStats(CivilizationStats)),
				CreateStatsOption(Relations, ObserverStatsPanel.Relations, relationsTemplate, DisplayRelations),
				CreateStatsOption(EarningsGraph, ObserverStatsPanel.Graph, null, IncomeGraph),
				CreateStatsOption(ArmyGraph, ObserverStatsPanel.ArmyGraph, null, ArmyValueGraph),
			};

			ScrollItemWidget SetupItem(StatsDropDownOption option, ScrollItemWidget template)
			{
				var item = ScrollItemWidget.Setup(template, option.IsSelected, option.OnClick);
				item.Get<LabelWidget>("LABEL").GetText = () => option.Title;
				return item;
			}

			var statsDropDownPanelTemplate = logicArgs.TryGetValue("StatsDropDownPanelTemplate", out yaml) ? yaml.Value : "LABEL_DROPDOWN_TEMPLATE";

			statsDropDown.OnMouseDown = _ => statsDropDown.ShowDropDown(statsDropDownPanelTemplate, 230, statsDropDownOptions, SetupItem);
			statsDropDownOptions[0].OnClick();

			var keyListener = statsDropDown.Get<LogicKeyListenerWidget>("STATS_DROPDOWN_KEYHANDLER");
			keyListener.AddHandler(e =>
			{
				if (e.Event == KeyInputEvent.Down && !e.IsRepeat)
				{
					for (var i = 0; i < statsHotkeys.Length; i++)
					{
						if (statsHotkeys[i].IsActivatedBy(e))
						{
							Game.Sound.PlayNotification(modData.DefaultRules, null, "Sounds", clickSound, null);
							statsDropDownOptions[i].OnClick();
							return true;
						}
					}
				}

				return false;
			});

			if (logicArgs.TryGetValue("ClickSound", out yaml))
				clickSound = yaml.Value;
		}

		void ClearStats()
		{
			playerStatsPanel.Children.Clear();
			basicStatsHeaders.Visible = false;
			economyStatsHeaders.Visible = false;
			productionStatsHeaders.Visible = false;
			supportPowerStatsHeaders.Visible = false;
			armyHeaders.Visible = false;
			combatStatsHeaders.Visible = false;
			civilizationStatsHeaders.Visible = false;
			relationsStatsHeaders.Visible = false;

			incomeGraphContainer.Visible = false;
			armyValueGraphContainer.Visible = false;

			incomeGraph.GetSeries = null;
			armyValueGraph.GetSeries = null;
		}

		void IncomeGraph()
		{
			playerStatsPanel.Visible = false;
			incomeGraphContainer.Visible = true;

			incomeGraph.GetSeries = () =>
				players.Select(p => new ScrollableLineGraphSeries(
					p.ResolvedPlayerName,
					p.Color,
					(p.PlayerActor.TraitOrDefault<PlayerStatistics>() ?? new PlayerStatistics(p.PlayerActor)).IncomeSamples.Select(s => (float)s)));
		}

		void ArmyValueGraph()
		{
			playerStatsPanel.Visible = false;
			armyValueGraphContainer.Visible = true;

			armyValueGraph.GetSeries = () =>
				players.Select(p => new ScrollableLineGraphSeries(
					p.ResolvedPlayerName,
					p.Color,
					(p.PlayerActor.TraitOrDefault<PlayerStatistics>() ?? new PlayerStatistics(p.PlayerActor)).ArmySamples.Select(s => (float)s)));
		}

		void DisplayStats(Func<Player, ScrollItemWidget> createItem)
		{
			foreach (var team in teams)
			{
				if (hasTeams)
				{
					var tt = ScrollItemWidget.Setup(teamTemplate, () => false, () => { });
					tt.IgnoreMouseOver = true;

					var teamLabel = tt.Get<LabelWidget>("TEAM");
					var teamText = team.Key > 0 ? FluentProvider.GetMessage(TeamNumber, "team", team.Key)
						: FluentProvider.GetMessage(NoTeam);
					teamLabel.GetText = () => teamText;
					tt.Bounds.Width = teamLabel.Bounds.Width = Game.Renderer.Fonts[tt.Font].Measure(teamText).X;

					var colorBlockWidget = tt.Get<ColorBlockWidget>("TEAM_COLOR");
					var scrollBarOffset = playerStatsPanel.ScrollBar != ScrollBar.Hidden
						? playerStatsPanel.ScrollbarWidth
						: 0;
					var boundsWidth = tt.Parent.Bounds.Width - scrollBarOffset;
					colorBlockWidget.Bounds.Width = boundsWidth - 200;

					var gradient = tt.Get<GradientColorBlockWidget>("TEAM_GRADIENT");
					gradient.Bounds.X = boundsWidth - 200;

					playerStatsPanel.AddChild(tt);
				}

				foreach (var p in team)
				{
					var player = p;
					playerStatsPanel.AddChild(createItem(player));
				}
			}
		}

		/// <summary>
		/// One row per pair of factions. Every other panel answers "how is this
		/// faction doing"; none of them can show who is at war with whom, who
		/// trades with whom, or what that costs, because a per-faction row has
		/// nowhere to put the other side.
		/// </summary>
		void DisplayRelations()
		{
			relationsStatsHeaders.Visible = true;

			var diplomacy = world.WorldActor.TraitOrDefault<DiplomacyManager>();
			if (diplomacy == null)
				return;

			var trade = world.WorldActor.TraitOrDefault<TradeManager>();
			var routes = trade?.Routes;

			foreach (var relation in diplomacy.Relations)
			{
				var route = routes?.FirstOrDefault(r => r.Relation == relation);
				playerStatsPanel.AddChild(RelationRow(relation, route));
			}
		}

		ScrollItemWidget RelationRow(DiplomaticRelation relation, TradeRoute route)
		{
			var template = ScrollItemWidget.Setup(relationsTemplate, () => false, () => { });
			var first = relation.PlayerA;
			var second = relation.PlayerB;

			var color = template.Get<ColorBlockWidget>("RELATION_COLOR");
			var gradient = template.Get<GradientColorBlockWidget>("RELATION_GRADIENT");
			SetupPlayerColor(first, template, color, gradient);

			var firstLabel = template.Get<LabelWidget>("FIRST");
			var firstName = first.ResolvedPlayerName;
			firstLabel.GetText = () => firstName;
			firstLabel.GetColor = () => first.Color;

			var linkLabel = template.Get<LabelWidget>("LINK");
			linkLabel.GetText = () => "/";

			var secondLabel = template.Get<LabelWidget>("SECOND");
			var secondName = second.ResolvedPlayerName;
			secondLabel.GetText = () => secondName;
			secondLabel.GetColor = () => second.Color;

			// Standing says what the relationship is; the term beside it says which
			// part of the world is pushing it there.
			var stateLabel = template.Get<LabelWidget>("STATE");
			stateLabel.GetText = () => FluentProvider.GetMessage(StateKey(relation.State))
				+ " \u00b7 " + PressureLabel(relation);
			stateLabel.GetColor = () => relation.State switch
			{
				DiplomaticRelationState.War => Color.Salmon,
				DiplomaticRelationState.Alliance => Color.LightGreen,
				_ => Color.LightGray
			};

			var number = new Func<int, string>(i => i.ToString(NumberFormatInfo.CurrentInfo));

			var trustText = new CachedTransform<int, string>(number);
			template.Get<LabelWidget>("TRUST").GetText = () => trustText.Update(relation.Trust);

			// Grievance runs both ways and the asymmetry is the interesting part.
			var grievanceText = new CachedTransform<(int, int), string>(
				pair => $"{pair.Item1} / {pair.Item2}");
			template.Get<LabelWidget>("GRIEVANCE").GetText =
				() => grievanceText.Update((relation.GrievanceA, relation.GrievanceB));

			var tradeLabel = template.Get<LabelWidget>("TRADE");
			if (route == null)
				tradeLabel.GetText = () => "-";
			else
				tradeLabel.GetText = () => FluentProvider.GetMessage(
					route.Status == TradeRouteStatus.Active
						? TradeOpen
						: TradeSuspended);

			var shippedText = new CachedTransform<int, string>(number);
			template.Get<LabelWidget>("SHIPPED").GetText = () => shippedText.Update(
				route == null
					? 0
					: route.FoodAToB + route.FoodBToA
						+ route.MaterialsAToB + route.MaterialsBToA
						+ route.EnergyAToB + route.EnergyBToA);

			var fallenText = new CachedTransform<int, string>(number);
			template.Get<LabelWidget>("CASUALTIES").GetText =
				() => fallenText.Update(relation.LastDeathsA + relation.LastDeathsB);

			return template;
		}

		static string PressureLabel(DiplomaticRelation relation)
		{
			var a = (PressureTerm)relation.PressureTermA;
			var b = (PressureTerm)relation.PressureTermB;
			return a == b ? Short(a) : Short(a) + "/" + Short(b);
		}

		static string Short(PressureTerm term)
		{
			return term switch
			{
				PressureTerm.Disposition => "temper",
				PressureTerm.RelativePower => "power",
				PressureTerm.Prosperity => "wealth",
				PressureTerm.Stability => "order",
				PressureTerm.TradeDependency => "trade",
				PressureTerm.ResearchCommitment => "study",
				PressureTerm.CasualtyAversion => "losses",
				_ => "-"
			};
		}

		static string StateKey(DiplomaticRelationState state)
		{
			return state switch
			{
				DiplomaticRelationState.War => StateWar,
				DiplomaticRelationState.Alliance => StateAlliance,
				_ => StateNeutral
			};
		}

		readonly record struct CivilSummary(
			int Population,
			int Food,
			int Energy,
			int Knowledge,
			int Prosperity,
			int Stability,
			int Mobilized);

		static CivilSummary SummariseCivilization(World world, CivilizationState civilization, Player player)
		{
			var population = 0;
			var food = 0;
			var energy = 0;
			var knowledge = 0;
			var mobilized = 0;
			var weightedProsperity = 0L;
			var weightedStability = 0L;

			foreach (var actor in civilization.Settlements(world, player))
			{
				var settlement = actor.Trait<SettlementCore>();
				population += settlement.Population;
				food += settlement.Food;
				energy += settlement.Energy;
				knowledge += settlement.Knowledge;
				mobilized += settlement.Mobilized;
				weightedProsperity += (long)settlement.Prosperity * settlement.Population;
				weightedStability += (long)settlement.Stability * settlement.Population;
			}

			// Prosperity and stability are per-settlement scores, so a faction-level
			// figure has to be weighted by where the people actually live.
			var weight = Math.Max(population, 1);
			return new CivilSummary(
				population,
				food,
				energy,
				knowledge,
				(int)(weightedProsperity / weight),
				(int)(weightedStability / weight),
				mobilized);
		}

		ScrollItemWidget CivilizationStats(Player player)
		{
			civilizationStatsHeaders.Visible = true;
			var template = SetupPlayerScrollItemWidget(civilizationPlayerTemplate, player);

			AddPlayerFlagAndName(template, player);

			var playerName = template.Get<LabelWithTooltipWidget>("PLAYER");
			playerName.GetColor = () => Color.White;

			var playerColor = template.Get<ColorBlockWidget>("PLAYER_COLOR");
			var playerGradient = template.Get<GradientColorBlockWidget>("PLAYER_GRADIENT");

			SetupPlayerColor(player, template, playerColor, playerGradient);

			var civilization = player.PlayerActor.TraitOrDefault<CivilizationState>();
			if (civilization == null)
				return template;

			// Settlements are re-aggregated at most once per world tick, not once per
			// label per frame.
			var summary = new CachedTransform<int, CivilSummary>(
				_ => SummariseCivilization(world, civilization, player));
			CivilSummary Current() => summary.Update(world.WorldTick);

			var number = new Func<int, string>(i => i.ToString(NumberFormatInfo.CurrentInfo));

			var populationText = new CachedTransform<int, string>(number);
			template.Get<LabelWidget>("POPULATION").GetText =
				() => populationText.Update(Current().Population);

			var foodText = new CachedTransform<int, string>(number);
			template.Get<LabelWidget>("FOOD").GetText = () => foodText.Update(Current().Food);

			var energyText = new CachedTransform<int, string>(number);
			template.Get<LabelWidget>("ENERGY").GetText = () => energyText.Update(Current().Energy);

			var knowledgeText = new CachedTransform<int, string>(number);
			template.Get<LabelWidget>("KNOWLEDGE").GetText =
				() => knowledgeText.Update(Current().Knowledge);

			var prosperityText = new CachedTransform<int, string>(number);
			template.Get<LabelWidget>("PROSPERITY").GetText =
				() => prosperityText.Update(Current().Prosperity);

			var stabilityText = new CachedTransform<int, string>(number);
			template.Get<LabelWidget>("STABILITY").GetText =
				() => stabilityText.Update(Current().Stability);

			var technologiesText = new CachedTransform<int, string>(
				mask => BitOperations.PopCount((uint)mask).ToString(NumberFormatInfo.CurrentInfo));
			template.Get<LabelWidget>("TECHNOLOGIES").GetText =
				() => technologiesText.Update(civilization.CompletedTechnologyMask);

			var strategyText = new CachedTransform<CivilizationStrategy, string>(
				strategy => strategy.ToString());
			template.Get<LabelWidget>("STRATEGY").GetText =
				() => strategyText.Update(civilization.Strategy);

			var warsText = new CachedTransform<int, string>(number);
			template.Get<LabelWidget>("WARS").GetText = () => warsText.Update(civilization.ActiveWars);

			return template;
		}

		ScrollItemWidget CombatStats(Player player)
		{
			combatStatsHeaders.Visible = true;
			var template = SetupPlayerScrollItemWidget(combatPlayerTemplate, player);

			AddPlayerFlagAndName(template, player);

			var playerName = template.Get<LabelWithTooltipWidget>("PLAYER");
			playerName.GetColor = () => Color.White;

			var playerColor = template.Get<ColorBlockWidget>("PLAYER_COLOR");
			var playerGradient = template.Get<GradientColorBlockWidget>("PLAYER_GRADIENT");

			SetupPlayerColor(player, template, playerColor, playerGradient);

			var stats = player.PlayerActor.TraitOrDefault<PlayerStatistics>();
			if (stats == null)
				return template;

			var destroyedText = new CachedTransform<int, string>(i => i.ToString("C0", englishDollar));
			template.Get<LabelWidget>("ASSETS_DESTROYED").GetText = () => destroyedText.Update(stats.KillsCost);

			var lostText = new CachedTransform<int, string>(i => i.ToString("C0", englishDollar));
			template.Get<LabelWidget>("ASSETS_LOST").GetText = () => lostText.Update(stats.DeathsCost);

			var unitsKilledText = new CachedTransform<int, string>(i => i.ToString(NumberFormatInfo.CurrentInfo));
			template.Get<LabelWidget>("UNITS_KILLED").GetText = () => unitsKilledText.Update(stats.UnitsKilled);

			var unitsDeadText = new CachedTransform<int, string>(i => i.ToString(NumberFormatInfo.CurrentInfo));
			template.Get<LabelWidget>("UNITS_DEAD").GetText = () => unitsDeadText.Update(stats.UnitsDead);

			var buildingsKilledText = new CachedTransform<int, string>(i => i.ToString(NumberFormatInfo.CurrentInfo));
			template.Get<LabelWidget>("BUILDINGS_KILLED").GetText = () => buildingsKilledText.Update(stats.BuildingsKilled);

			var buildingsDeadText = new CachedTransform<int, string>(i => i.ToString(NumberFormatInfo.CurrentInfo));
			template.Get<LabelWidget>("BUILDINGS_DEAD").GetText = () => buildingsDeadText.Update(stats.BuildingsDead);

			var armyText = new CachedTransform<int, string>(i => i.ToString("C0", englishDollar));
			template.Get<LabelWidget>("ARMY_VALUE").GetText = () => armyText.Update(stats.ArmyValue);

			var visionText = new CachedTransform<int, string>(Vision);
			template.Get<LabelWidget>("VISION").GetText = () => player.Shroud.Disabled ? "100%" : visionText.Update(player.Shroud.RevealedCells);

			return template;
		}

		ScrollItemWidget ProductionStats(Player player)
		{
			productionStatsHeaders.Visible = true;
			var template = SetupPlayerScrollItemWidget(productionPlayerTemplate, player);

			AddPlayerFlagAndName(template, player);

			var playerName = template.Get<LabelWithTooltipWidget>("PLAYER");
			playerName.GetColor = () => Color.White;

			var playerColor = template.Get<ColorBlockWidget>("PLAYER_COLOR");
			var playerGradient = template.Get<GradientColorBlockWidget>("PLAYER_GRADIENT");

			SetupPlayerColor(player, template, playerColor, playerGradient);

			template.Get<ObserverProductionIconsWidget>("PRODUCTION_ICONS").GetPlayer = () => player;
			template.IgnoreChildMouseOver = false;

			return template;
		}

		ScrollItemWidget SupportPowerStats(Player player)
		{
			supportPowerStatsHeaders.Visible = true;
			var template = SetupPlayerScrollItemWidget(supportPowersPlayerTemplate, player);

			AddPlayerFlagAndName(template, player);

			var playerName = template.Get<LabelWithTooltipWidget>("PLAYER");
			playerName.GetColor = () => Color.White;

			var playerColor = template.Get<ColorBlockWidget>("PLAYER_COLOR");
			var playerGradient = template.Get<GradientColorBlockWidget>("PLAYER_GRADIENT");

			SetupPlayerColor(player, template, playerColor, playerGradient);

			template.Get<ObserverSupportPowerIconsWidget>("SUPPORT_POWER_ICONS").GetPlayer = () => player;
			template.IgnoreChildMouseOver = false;

			return template;
		}

		ScrollItemWidget ArmyStats(Player player)
		{
			armyHeaders.Visible = true;
			var template = SetupPlayerScrollItemWidget(armyPlayerTemplate, player);

			AddPlayerFlagAndName(template, player);

			var playerName = template.Get<LabelWithTooltipWidget>("PLAYER");
			playerName.GetColor = () => Color.White;

			var playerColor = template.Get<ColorBlockWidget>("PLAYER_COLOR");
			var playerGradient = template.Get<GradientColorBlockWidget>("PLAYER_GRADIENT");

			SetupPlayerColor(player, template, playerColor, playerGradient);

			template.Get<ObserverArmyIconsWidget>("ARMY_ICONS").GetPlayer = () => player;
			template.IgnoreChildMouseOver = false;

			return template;
		}

		ScrollItemWidget EconomyStats(Player player)
		{
			economyStatsHeaders.Visible = true;
			var template = SetupPlayerScrollItemWidget(economyPlayerTemplate, player);

			AddPlayerFlagAndName(template, player);

			var playerName = template.Get<LabelWithTooltipWidget>("PLAYER");
			playerName.GetColor = () => Color.White;

			var playerColor = template.Get<ColorBlockWidget>("PLAYER_COLOR");
			var playerGradient = template.Get<GradientColorBlockWidget>("PLAYER_GRADIENT");

			SetupPlayerColor(player, template, playerColor, playerGradient);

			var stats = player.PlayerActor.TraitOrDefault<PlayerStatistics>();
			if (stats == null)
				return template;

			var playerResources = player.PlayerActor.Trait<PlayerResources>();
			var cashText = new CachedTransform<int, string>(i => i.ToString("C0", englishDollar));
			template.Get<LabelWidget>("CASH").GetText = () => cashText.Update(playerResources.GetCashAndResources());

			var incomeText = new CachedTransform<int, string>(i => i.ToString("C0", englishDollar));
			template.Get<LabelWidget>("INCOME").GetText = () => incomeText.Update(stats.DisplayIncome);

			var earnedText = new CachedTransform<int, string>(i => i.ToString("C0", englishDollar));
			template.Get<LabelWidget>("EARNED").GetText = () => earnedText.Update(playerResources.Earned);

			var spentText = new CachedTransform<int, string>(i => i.ToString("C0", englishDollar));
			template.Get<LabelWidget>("SPENT").GetText = () => spentText.Update(playerResources.Spent);

			var assetsText = new CachedTransform<int, string>(i => "$" + i);
			template.Get<LabelWidget>("ASSETS").GetText = () => assetsText.Update(stats.AssetsValue);

			var miners = template.Get<LabelWidget>("MINERS");
			miners.GetText = () => world.ActorsHavingTrait<ResourceCollector>()
				.Count(a => a.Owner == player && !a.IsDead)
				.ToString(NumberFormatInfo.CurrentInfo);

			return template;
		}

		ScrollItemWidget BasicStats(Player player)
		{
			basicStatsHeaders.Visible = true;
			var template = SetupPlayerScrollItemWidget(basicPlayerTemplate, player);

			AddPlayerFlagAndName(template, player);

			var playerName = template.Get<LabelWithTooltipWidget>("PLAYER");
			playerName.GetColor = () => Color.White;

			var playerColor = template.Get<ColorBlockWidget>("PLAYER_COLOR");
			var playerGradient = template.Get<GradientColorBlockWidget>("PLAYER_GRADIENT");

			SetupPlayerColor(player, template, playerColor, playerGradient);

			var playerResources = player.PlayerActor.Trait<PlayerResources>();
			var cashText = new CachedTransform<int, string>(i => i.ToString("C0", englishDollar));
			template.Get<LabelWidget>("CASH").GetText = () => cashText.Update(playerResources.Cash + playerResources.Resources);

			var powerManager = player.PlayerActor.TraitOrDefault<PowerManager>();
			if (powerManager != null)
			{
				var powerLabel = template.Get<LabelWidget>("POWER");
				var powerText = new CachedTransform<(int PowerDrained, int PowerProvided), string>(p => p.PowerDrained + "/" + p.PowerProvided);
				powerLabel.GetText = () => powerText.Update((powerManager.PowerDrained, powerManager.PowerProvided));
				powerLabel.GetColor = () => GetPowerColor(powerManager.PowerState);
			}

			var stats = player.PlayerActor.TraitOrDefault<PlayerStatistics>();
			if (stats == null)
				return template;

			var killsText = new CachedTransform<int, string>(i => i.ToString(NumberFormatInfo.CurrentInfo));
			template.Get<LabelWidget>("KILLS").GetText = () => killsText.Update(stats.UnitsKilled + stats.BuildingsKilled);

			var deathsText = new CachedTransform<int, string>(i => i.ToString(NumberFormatInfo.CurrentInfo));
			template.Get<LabelWidget>("DEATHS").GetText = () => deathsText.Update(stats.UnitsDead + stats.BuildingsDead);

			var destroyedText = new CachedTransform<int, string>(i => i.ToString("C0", englishDollar));
			template.Get<LabelWidget>("ASSETS_DESTROYED").GetText = () => destroyedText.Update(stats.KillsCost);

			var lostText = new CachedTransform<int, string>(i => i.ToString("C0", englishDollar));
			template.Get<LabelWidget>("ASSETS_LOST").GetText = () => lostText.Update(stats.DeathsCost);

			var experienceText = new CachedTransform<int, string>(i => i.ToString(NumberFormatInfo.CurrentInfo));
			template.Get<LabelWidget>("EXPERIENCE").GetText = () => experienceText.Update(stats.Experience);

			var actionsText = new CachedTransform<double, string>(AverageOrdersPerMinute);
			template.Get<LabelWidget>("ACTIONS_MIN").GetText = () => actionsText.Update(stats.OrderCount);

			return template;
		}

		static void SetupPlayerColor(Player player, ScrollItemWidget template, ColorBlockWidget colorBlockWidget, GradientColorBlockWidget gradientColorBlockWidget)
		{
			var color = Color.FromArgb(128, player.Color.R, player.Color.G, player.Color.B);
			var hoverColor = Color.FromArgb(192, player.Color.R, player.Color.G, player.Color.B);

			var isMouseOver = new CachedTransform<Widget, bool>(w => w == template || template.Children.Contains(w));

			colorBlockWidget.GetColor = () => isMouseOver.Update(Ui.MouseOverWidget) ? hoverColor : color;

			gradientColorBlockWidget.GetTopLeftColor = () => isMouseOver.Update(Ui.MouseOverWidget) ? hoverColor : color;
			gradientColorBlockWidget.GetBottomLeftColor = () => isMouseOver.Update(Ui.MouseOverWidget) ? hoverColor : color;
			gradientColorBlockWidget.GetTopRightColor = () => isMouseOver.Update(Ui.MouseOverWidget) ? hoverColor : Color.Transparent;
			gradientColorBlockWidget.GetBottomRightColor = () => isMouseOver.Update(Ui.MouseOverWidget) ? hoverColor : Color.Transparent;
		}

		ScrollItemWidget SetupPlayerScrollItemWidget(ScrollItemWidget template, Player player)
		{
			return ScrollItemWidget.Setup(template, () => false, () =>
			{
				var targetActor = FindPlayerBaseActor(player);
				if (targetActor != null)
					worldRenderer.Viewport.Center(targetActor.CenterPosition);
			});
		}

		Actor FindPlayerBaseActor(Player player)
		{
			// First priority: main base
			var primaryBase = world.ActorsHavingTrait<BaseBuilding>()
				.FirstOrDefault(a => a.Owner == player);

			if (primaryBase != null)
				return primaryBase;

			// Fallback: Any building closest to viewport
			var building = world.ActorsHavingTrait<Building>()
				.OrderBy(a => (worldRenderer.Viewport.CenterPosition - a.CenterPosition).LengthSquared)
				.FirstOrDefault(a => a.Owner == player && a.Info.HasTraitInfo<SelectableInfo>());

			return building;
		}

		void AdjustStatisticsPanel(Widget itemTemplate)
		{
			var height = playerStatsPanel.Bounds.Height;

			var scrollbarWidth = playerStatsPanel.ScrollBar != ScrollBar.Hidden ? playerStatsPanel.ScrollbarWidth : 0;
			playerStatsPanel.Bounds.Width = itemTemplate.Bounds.Width + scrollbarWidth;

			if (playerStatsPanel.Bounds.Height < height)
				playerStatsPanel.ScrollToTop();
		}

		void AdjustHeader(ContainerWidget headerTemplate)
		{
			var offset = playerStatsPanel.ScrollbarWidth;

			headerTemplate.Get<ColorBlockWidget>("HEADER_COLOR").Bounds.Width += offset;
			headerTemplate.Get<GradientColorBlockWidget>("HEADER_GRADIENT").Bounds.X += offset;

			foreach (var headerLabel in headerTemplate.Children.OfType<LabelWidget>())
				headerLabel.Bounds.X += offset;
		}

		static void AddPlayerFlagAndName(ScrollItemWidget template, Player player)
		{
			var flag = template.Get<ImageWidget>("FLAG");
			flag.GetImageCollection = () => "flags";
			flag.GetImageName = () => player.Faction.InternalName;

			var playerName = template.Get<LabelWithTooltipWidget>("PLAYER");
			WidgetUtils.BindPlayerNameAndStatus(playerName, player);

			playerName.GetColor = () => player.Color;
		}

		string AverageOrdersPerMinute(double orders)
		{
			return (world.WorldTick == 0 ? 0 : orders / (world.WorldTick / 1500.0)).ToString("F1", NumberFormatInfo.CurrentInfo);
		}

		string Vision(int revealedCells)
		{
			return (Math.Ceiling(revealedCells * 100d / world.Map.ProjectedCells.Length) / 100).ToString("P0", NumberFormatInfo.CurrentInfo);
		}

		static Color GetPowerColor(PowerState state)
		{
			if (state == PowerState.Critical)
				return Color.Red;

			if (state == PowerState.Low)
				return Color.Orange;

			return Color.LimeGreen;
		}

		// HACK The height of the templates and the scrollpanel needs to be kept in synch
		bool ShowScrollBar => players.Length + (hasTeams ? teams.Length : 0) > 10;

		sealed class StatsDropDownOption
		{
			public string Title;
			public Func<bool> IsSelected;
			public Action OnClick;
		}
	}
}
