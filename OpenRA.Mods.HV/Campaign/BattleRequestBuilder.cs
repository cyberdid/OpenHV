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
using System.Text.Json.Nodes;

namespace OpenRA.Mods.HV.Campaign
{
	/// <summary>
	/// Builds a battle-request-v1 document from one planet cell.
	///
	/// Shared by --battle-from-planet and the in-game planet screen on purpose.
	/// Two builders would drift, and a request that differs by which button made
	/// it is a request that cannot be reproduced from the campaign it came from.
	/// </summary>
	public static class BattleRequestBuilder
	{
		public static readonly string[] BiomeNames =
		{
			"ice", "tundra", "barrens", "steppe", "growth", "deep-growth", "scorched"
		};

		// Maps that seat exactly two players AND on which two bots actually fight.
		//
		// The seat count matters because PanelLoadScreen fills every open slot on
		// the map, cycling the bot list: a request for two sides played on a
		// four-spawn map silently becomes a 2v2, and the result reports four
		// players the campaign never committed. coldrage - the harness default
		// everywhere else - has four.
		//
		// The fighting had to be measured rather than assumed. Twenty-two maps
		// have two spawn points; all twenty-two were run at aggressor vs fortress,
		// seed 12345, 30000 ticks, and two of them produced no combat at all:
		// river-fight and nowheres-land ended 0 kills and 0 earned, meaning the
		// bots reached neither each other nor any resources in ten minutes of game
		// time. They are dropped. The remaining twenty ranged from 16,900 kills
		// (frcfreez, which ended in a natural victory) to 289,500 (slippery-
		// tensions).
		//
		// Ground is still not chosen by biome. All sixty-nine maps declare
		// Tileset: PLANET and there is no generator, so there is no way to make a
		// steppe cell fight on steppe. This picks reproducibly, not appropriately.
		// The environment block carries the real physics, so a generator can
		// honour it later without the contract changing.
		public static readonly string[] TwoSpawnMaps =
		{
			"beyond-destruction", "business-as-usual", "crescendo", "dimrets2",
			"firesouls", "frcfreez", "ggreens", "hypoderm", "keepglss",
			"processing-station", "season93", "shade-duel", "silverman",
			"slippery-tensions", "supercharged", "swamp-conquest", "thrtmthr",
			"twin-lakes", "tyriansun", "war-playground",
		};

		/// <summary>Same cell at the same step must always produce the same battle,
		/// or the campaign is not reproducible and neither is any experiment run
		/// against it.</summary>
		public static int SeedFor(int step, int x, int y)
		{
			return unchecked((step * 73856093) ^ (x * 19349663) ^ (y * 83492791)) & 0x7FFFFFFF;
		}

		public static string BiomeName(int code)
		{
			return code >= 0 && code < BiomeNames.Length ? BiomeNames[code] : $"class-{code}";
		}

		public static JsonObject Build(
			string planetId, int step,
			int x, int y, int latitudeCells, int longitudeCells,
			int biome, int biomass, int population,
			int holderIndex, string holderName,
			double surfaceTemperatureK)
		{
			// Latitude runs north to south, matching the row-major order the cell
			// arrays are served in. Longitude is 0..360 rather than -180..180 -
			// that is what the schema requires and what events.py normalises to,
			// so a district lookup on either side lands on the same place.
			var latitude = 90.0 - 180.0 * (y + 0.5) / latitudeCells;
			var longitude = 360.0 * (x + 0.5) / longitudeCells;
			var seed = SeedFor(step, x, y);

			return new JsonObject
			{
				["schemaVersion"] = 1,
				["requestId"] = $"{planetId}-{step}-{x}-{y}",
				["origin"] = new JsonObject
				{
					["campaignId"] = planetId,
					["step"] = step,
				},
				["cell"] = new JsonObject
				{
					["latitudeIndex"] = y,
					["longitudeIndex"] = x,
					["latitude"] = Math.Round(latitude, 4),
					["longitude"] = Math.Round(longitude, 4),
				},
				["environment"] = new JsonObject
				{
					["surfaceTemperatureK"] = Math.Round(surfaceTemperatureK, 2),
					["populationDensity"] = Math.Round(population / 255.0, 4),
					["biome"] = BiomeName(biome),
				},
				["map"] = new JsonObject
				{
					["name"] = TwoSpawnMaps[seed % TwoSpawnMaps.Length],
				},
				["participants"] = new JsonArray
				{
					new JsonObject
					{
						["factionId"] = Math.Max(holderIndex, 0),
						["name"] = holderName,
						["botType"] = "aggressor",
						["faction"] = "sw",
						["role"] = "attacker",
						// What the swarm can put on the ground is what the cell
						// feeds, so this is density rather than a free parameter.
						["armyValue"] = 1000 + (40 * population),
					},
					new JsonObject
					{
						["factionId"] = 900,
						["name"] = "Yuruki",
						["botType"] = "fortress",
						["faction"] = "yi",
						["role"] = "defender",
						// Standing biomass is what the locals still have to hold
						// with: a stripped cell is one they are already losing.
						["armyValue"] = 1000 + (40 * biomass),
					},
				},
				["stakes"] = new JsonObject
				{
					["cellControl"] = true,
					["attackerWithdrawsOnLoss"] = true,
					["casualtiesFeedRadicalisation"] = true,
				},
				["determinism"] = new JsonObject
				{
					["seed"] = seed,
					["maxWorldTicks"] = 30000,
					["gameSpeed"] = "fastest",
					["watchdogSeconds"] = 180,
				},
			};
		}
	}
}
