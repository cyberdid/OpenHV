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
using OpenRA.Effects;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.HV.Traits
{
	public enum PlanetLifecycleStage
	{
		Lifeless,
		Biosphere,
		SapientLife,
		Civilization,
		Spacefaring
	}

	public enum UniverseMacroEventType
	{
		MacroDayAdvanced = 1
	}

	public sealed class PlanetDefinition
	{
		public int Index { get; }
		public string PlanetId { get; }
		public string Name { get; }

		public PlanetDefinition(int index, string planetId, string name)
		{
			Index = index;
			PlanetId = planetId;
			Name = name;
		}
	}

	public sealed class UniverseMacroEvent
	{
		public int Sequence { get; }
		public string EventId { get; }
		public UniverseMacroEventType EventType { get; }
		public int MacroDay { get; }
		public int PlanetIndex { get; }

		public UniverseMacroEvent(
			int sequence,
			string eventId,
			UniverseMacroEventType eventType,
			int macroDay,
			int planetIndex)
		{
			Sequence = sequence;
			EventId = eventId;
			EventType = eventType;
			MacroDay = macroDay;
			PlanetIndex = planetIndex;
		}
	}

	/// <summary>A synchronized planet node. Physical fields are added here in Phase 2.</summary>
	public sealed class PlanetState : IEffect, ISync
	{
		[VerifySync]
		bool active;

		[VerifySync]
		int lifecycleStage = (int)PlanetLifecycleStage.Lifeless;

		public PlanetDefinition Definition { get; }
		public bool Active => active;
		public PlanetLifecycleStage LifecycleStage => (PlanetLifecycleStage)lifecycleStage;

		public PlanetState(PlanetDefinition definition, bool active)
		{
			Definition = definition;
			this.active = active;
		}

		internal void Restore(bool restoredActive, PlanetLifecycleStage restoredLifecycleStage)
		{
			active = restoredActive;
			lifecycleStage = (int)restoredLifecycleStage;
		}

		void IEffect.Tick(World world) { }

		IEnumerable<IRenderable> IEffect.Render(WorldRenderer renderer) { return []; }
	}

	/// <summary>The synchronized star-system node that owns exactly three planets.</summary>
	public sealed class StarSystemState : IEffect, ISync
	{
		[VerifySync]
		int activePlanetCount = 1;

		public string StarSystemId { get; }
		public IReadOnlyList<PlanetState> Planets { get; }
		public int ActivePlanetCount => activePlanetCount;

		public StarSystemState(string starSystemId, IReadOnlyList<PlanetState> planets)
		{
			StarSystemId = starSystemId;
			Planets = planets;
		}

		internal void RestoreActivePlanetCount(int restoredActivePlanetCount)
		{
			activePlanetCount = restoredActivePlanetCount;
		}

		void IEffect.Tick(World world) { }

		IEnumerable<IRenderable> IEffect.Render(WorldRenderer renderer) { return []; }
	}

	[TraitLocation(SystemActors.World)]
	[Desc("Owns the synchronized Universe hierarchy, three planetary slots, slow clock, and checkpoint data.")]
	public sealed class UniverseStateInfo : TraitInfo
	{
		[Desc("OpenRA world ticks in one Universe macro day.")]
		public readonly int TicksPerMacroDay = 250;

		public override object Create(ActorInitializer init) { return new UniverseState(this); }
	}

	/// <summary>
	/// Authoritative root of the long-running simulation. Mutable values use only
	/// synchronized integers/bools. OpenRA game saves replay world orders and then
	/// restore this versioned trait payload at the exact checkpoint boundary.
	/// </summary>
	public sealed class UniverseState : IWorldLoaded, INotifyGameLoaded, ITick, ISync, IGameSaveTraitData
	{
		public const int CheckpointSchemaVersion = 1;
		public const string UniverseId = "universe-0001";
		public const string StarSystemId = "tyranthos-system";

		static readonly PlanetDefinition[] PlanetDefinitions =
		[
			new(0, "planet-0001", "Tyranthos"),
			new(1, "planet-0002", "Planet II"),
			new(2, "planet-0003", "Planet III")
		];

		readonly UniverseStateInfo info;
		readonly List<UniverseMacroEvent> events = [];

		[VerifySync]
		int macroDay;

		[VerifySync]
		int macroTickRemainder;

		[VerifySync]
		int macroEventSequence;

		[VerifySync]
		int lastEventType;

		[VerifySync]
		int lastEventMacroDay;

		[VerifySync]
		int lastEventPlanetIndex = -1;

		public int MacroDay => macroDay;
		public int MacroTickRemainder => macroTickRemainder;
		public int MacroEventSequence => macroEventSequence;
		public int TicksPerMacroDay => Math.Max(1, info.TicksPerMacroDay);
		public StarSystemState StarSystem { get; }
		public IReadOnlyList<UniverseMacroEvent> Events => events;

		public UniverseState(UniverseStateInfo info)
		{
			this.info = info;
			var planets = new PlanetState[PlanetDefinitions.Length];
			for (var i = 0; i < planets.Length; i++)
				planets[i] = new PlanetState(PlanetDefinitions[i], i == 0);

			StarSystem = new StarSystemState(StarSystemId, planets);
		}

		void IWorldLoaded.WorldLoaded(World world, WorldRenderer worldRenderer)
		{
			// Register in stable hierarchy order so each node contributes to World.SyncHash.
			world.Add(StarSystem);
			foreach (var planet in StarSystem.Planets)
				world.Add(planet);
		}

		void INotifyGameLoaded.GameLoaded(World world)
		{
			// The normal save UI opens the options menu after restoration, which pauses
			// a headless observer forever. Autonomous simulations have no menu/user to
			// close it, so resume both local and synchronized pause state explicitly.
			if (!Game.IsDeterministicSimulation)
				return;

			world.SetLocalPauseState(false);
			world.SetPauseState(false);
		}

		void ITick.Tick(Actor self)
		{
			if (!Game.IsDeterministicSimulation)
				return;

			AdvanceTicks(1);
		}

		void AdvanceTicks(int ticks)
		{
			if (ticks < 0)
				throw new ArgumentOutOfRangeException(nameof(ticks));

			var totalTicks = (long)macroTickRemainder + ticks;
			var advancedDays = checked((int)(totalTicks / TicksPerMacroDay));
			macroTickRemainder = (int)(totalTicks % TicksPerMacroDay);
			for (var i = 0; i < advancedDays; i++)
			{
				macroDay++;
				AppendEvent(UniverseMacroEventType.MacroDayAdvanced, macroDay, 0);
			}
		}

		void AppendEvent(UniverseMacroEventType eventType, int eventMacroDay, int planetIndex)
		{
			macroEventSequence++;
			lastEventType = (int)eventType;
			lastEventMacroDay = eventMacroDay;
			lastEventPlanetIndex = planetIndex;
			events.Add(new UniverseMacroEvent(
				macroEventSequence,
				$"{UniverseId}:event:{macroEventSequence:D10}",
				eventType,
				eventMacroDay,
				planetIndex));
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			var data = new List<MiniYamlNode>
			{
				new("SchemaVersion", FieldSaver.FormatValue(CheckpointSchemaVersion)),
				new("UniverseId", UniverseId),
				new("StarSystemId", StarSystemId),
				new("WorldTick", FieldSaver.FormatValue(self.World.WorldTick)),
				new("MacroDay", FieldSaver.FormatValue(macroDay)),
				new("MacroTickRemainder", FieldSaver.FormatValue(macroTickRemainder)),
				new("MacroEventSequence", FieldSaver.FormatValue(macroEventSequence)),
				new("BotRandomTotalCount", FieldSaver.FormatValue(self.World.BotRandom.TotalCount)),
				new("LastEventType", FieldSaver.FormatValue(lastEventType)),
				new("LastEventMacroDay", FieldSaver.FormatValue(lastEventMacroDay)),
				new("LastEventPlanetIndex", FieldSaver.FormatValue(lastEventPlanetIndex)),
				new("ActivePlanetCount", FieldSaver.FormatValue(StarSystem.ActivePlanetCount))
			};

			foreach (var planet in StarSystem.Planets)
			{
				var prefix = $"Planet{planet.Definition.Index}";
				data.Add(new MiniYamlNode($"{prefix}Id", planet.Definition.PlanetId));
				data.Add(new MiniYamlNode($"{prefix}Active", FieldSaver.FormatValue(planet.Active)));
				data.Add(new MiniYamlNode(
					$"{prefix}LifecycleStage",
					FieldSaver.FormatValue((int)planet.LifecycleStage)));
			}

			return data;
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, MiniYaml data)
		{
			if (self.World.IsReplay)
				return;

			var schemaVersion = ReadInt(data, "SchemaVersion");
			if (schemaVersion != CheckpointSchemaVersion)
				throw new InvalidOperationException(
					$"Universe checkpoint schema {schemaVersion} is not supported; expected {CheckpointSchemaVersion}.");

			RequireIdentity(data, "UniverseId", UniverseId);
			RequireIdentity(data, "StarSystemId", StarSystemId);
			var checkpointWorldTick = ReadInt(data, "WorldTick");
			macroDay = ReadInt(data, "MacroDay");
			macroTickRemainder = ReadInt(data, "MacroTickRemainder");
			macroEventSequence = ReadInt(data, "MacroEventSequence");
			var botRandomTotalCount = ReadInt(data, "BotRandomTotalCount");
			lastEventType = ReadInt(data, "LastEventType");
			lastEventMacroDay = ReadInt(data, "LastEventMacroDay");
			lastEventPlanetIndex = ReadInt(data, "LastEventPlanetIndex");
			StarSystem.RestoreActivePlanetCount(ReadInt(data, "ActivePlanetCount"));

			foreach (var planet in StarSystem.Planets)
			{
				var prefix = $"Planet{planet.Definition.Index}";
				RequireIdentity(data, $"{prefix}Id", planet.Definition.PlanetId);
				planet.Restore(
					ReadBool(data, $"{prefix}Active"),
					(PlanetLifecycleStage)ReadInt(data, $"{prefix}LifecycleStage"));
			}

			events.Clear();
			AdvanceTicks(Math.Max(0, self.World.WorldTick - checkpointWorldTick));

			var botSeed = unchecked(
				self.World.LobbyInfo.GlobalSettings.RandomSeed ^ (int)0xBB67AE85u);
			self.World.BotRandom.Reset(botSeed);
			for (var i = 0; i < botRandomTotalCount; i++)
				self.World.BotRandom.NextUint();
		}

		static int ReadInt(MiniYaml data, string key)
		{
			var node = RequiredNode(data, key);
			return FieldLoader.GetValue<int>(key, node.Value.Value);
		}

		static bool ReadBool(MiniYaml data, string key)
		{
			var node = RequiredNode(data, key);
			return FieldLoader.GetValue<bool>(key, node.Value.Value);
		}

		static void RequireIdentity(MiniYaml data, string key, string expected)
		{
			var actual = RequiredNode(data, key).Value.Value;
			if (!string.Equals(actual, expected, StringComparison.Ordinal))
				throw new InvalidOperationException(
					$"Universe checkpoint {key} '{actual}' does not match runtime identity '{expected}'.");
		}

		static MiniYamlNode RequiredNode(MiniYaml data, string key)
		{
			return data.NodeWithKeyOrDefault(key) ??
				throw new InvalidOperationException($"Universe checkpoint is missing required field '{key}'.");
		}
	}
}
