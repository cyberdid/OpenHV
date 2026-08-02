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
		MacroDayAdvanced = 1,
		PlanetClimatePulse = 2
	}

	public sealed class PlanetPhysicsDefinition
	{
		public int MassEarthMillionths { get; }
		public int RadiusKilometers { get; }
		public int RotationPeriodMinutes { get; }
		public int OrbitalDistanceMillionKilometers { get; }
		public int OrbitalPeriodDays { get; }
		public int AxialTiltMilliDegrees { get; }
		public int OrbitalEccentricityMillionths { get; }
		public int StellarFluxWattsPerSquareMeter { get; }
		public int TectonicPlateCount { get; }
		public int InitialTemperatureMilliKelvin { get; }
		public int InitialAtmospherePressurePascals { get; }
		public int InitialCarbonDioxidePartsPerMillion { get; }
		public int InitialAtmosphericWaterPartsPerMillion { get; }
		public int InitialTectonicActivityPerMille { get; }

		public PlanetPhysicsDefinition(
			int mass, int radius, int rotation, int orbitalDistance, int orbitalPeriod,
			int axialTilt, int eccentricity, int stellarFlux, int plateCount,
			int temperature, int pressure, int carbonDioxide, int atmosphericWater, int tectonicActivity)
		{
			MassEarthMillionths = mass;
			RadiusKilometers = radius;
			RotationPeriodMinutes = rotation;
			OrbitalDistanceMillionKilometers = orbitalDistance;
			OrbitalPeriodDays = orbitalPeriod;
			AxialTiltMilliDegrees = axialTilt;
			OrbitalEccentricityMillionths = eccentricity;
			StellarFluxWattsPerSquareMeter = stellarFlux;
			TectonicPlateCount = plateCount;
			InitialTemperatureMilliKelvin = temperature;
			InitialAtmospherePressurePascals = pressure;
			InitialCarbonDioxidePartsPerMillion = carbonDioxide;
			InitialAtmosphericWaterPartsPerMillion = atmosphericWater;
			InitialTectonicActivityPerMille = tectonicActivity;
		}
	}

	public sealed class PlanetDefinition
	{
		public int Index { get; }
		public string PlanetId { get; }
		public string Name { get; }
		public PlanetPhysicsDefinition Physics { get; }

		public PlanetDefinition(int index, string planetId, string name, PlanetPhysicsDefinition physics)
		{
			Index = index;
			PlanetId = planetId;
			Name = name;
			Physics = physics;
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

	public sealed class PlanetPhysicsSaveData
	{
		[FieldLoader.Require]
		public long GeologicalAgeYears;

		[FieldLoader.Require]
		public int RotationPeriodMinutes;

		[FieldLoader.Require]
		public int BondAlbedoPerMille;

		[FieldLoader.Require]
		public int AbsorbedSolarWattsPerSquareMeter;

		[FieldLoader.Require]
		public int RadiativeEquilibriumMilliKelvin;

		[FieldLoader.Require]
		public int MeanSurfaceTemperatureMilliKelvin;

		[FieldLoader.Require]
		public int EnergyImbalanceMilliWattsPerSquareMeter;

		[FieldLoader.Require]
		public int AtmospherePressurePascals;

		[FieldLoader.Require]
		public int CarbonDioxidePartsPerMillion;

		[FieldLoader.Require]
		public int AtmosphericWaterPartsPerMillion;

		[FieldLoader.Require]
		public long SurfaceWaterCubicKilometers;

		[FieldLoader.Require]
		public int OceanCoveragePerMille;

		[FieldLoader.Require]
		public int TectonicActivityPerMille;

		[FieldLoader.Require]
		public int ClimatePulseSequence;
	}

	/// <summary>Deterministic fixed-point global physics for one planet.</summary>
	public sealed class PlanetPhysicalState : IEffect, ISync
	{
		const long TargetOceanVolume = 1_400_000_000;
		[VerifySync]
		int geologicalAgeMillionYears;

		[VerifySync]
		int rotationPeriodMinutes;

		[VerifySync]
		int bondAlbedoPerMille = 380;

		[VerifySync]
		int absorbedSolarWattsPerSquareMeter;

		[VerifySync]
		int radiativeEquilibriumMilliKelvin;

		[VerifySync]
		int meanSurfaceTemperatureMilliKelvin;

		[VerifySync]
		int energyImbalanceMilliWattsPerSquareMeter;

		[VerifySync]
		int atmospherePressurePascals;

		[VerifySync]
		int carbonDioxidePartsPerMillion;

		[VerifySync]
		int atmosphericWaterPartsPerMillion;

		[VerifySync]
		int surfaceWaterCubicKilometers;

		[VerifySync]
		int oceanCoveragePerMille;

		[VerifySync]
		int tectonicActivityPerMille;

		[VerifySync]
		int climatePulseSequence;

		readonly PlanetPhysicsDefinition definition;

		public long GeologicalAgeYears => geologicalAgeMillionYears * 1_000_000L;
		public int MassEarthMillionths => definition.MassEarthMillionths;
		public int RadiusKilometers => definition.RadiusKilometers;
		public int SurfaceGravityMilliMetersPerSecondSquared { get; }
		public int RotationPeriodMinutes => rotationPeriodMinutes;
		public int OrbitalDistanceMillionKilometers => definition.OrbitalDistanceMillionKilometers;
		public int OrbitalPeriodDays => definition.OrbitalPeriodDays;
		public int AxialTiltMilliDegrees => definition.AxialTiltMilliDegrees;
		public int OrbitalEccentricityMillionths => definition.OrbitalEccentricityMillionths;
		public int StellarFluxWattsPerSquareMeter => definition.StellarFluxWattsPerSquareMeter;
		public int BondAlbedoPerMille => bondAlbedoPerMille;
		public int AbsorbedSolarWattsPerSquareMeter => absorbedSolarWattsPerSquareMeter;
		public int RadiativeEquilibriumMilliKelvin => radiativeEquilibriumMilliKelvin;
		public int MeanSurfaceTemperatureMilliKelvin => meanSurfaceTemperatureMilliKelvin;
		public int EnergyImbalanceMilliWattsPerSquareMeter => energyImbalanceMilliWattsPerSquareMeter;
		public int AtmospherePressurePascals => atmospherePressurePascals;
		public int CarbonDioxidePartsPerMillion => carbonDioxidePartsPerMillion;
		public int AtmosphericWaterPartsPerMillion => atmosphericWaterPartsPerMillion;
		public long SurfaceWaterCubicKilometers => surfaceWaterCubicKilometers;
		public int OceanCoveragePerMille => oceanCoveragePerMille;
		public int TectonicPlateCount => definition.TectonicPlateCount;
		public int TectonicActivityPerMille => tectonicActivityPerMille;
		public int ClimatePulseSequence => climatePulseSequence;

		public PlanetPhysicalState(PlanetPhysicsDefinition definition)
		{
			this.definition = definition;
			rotationPeriodMinutes = definition.RotationPeriodMinutes;
			meanSurfaceTemperatureMilliKelvin = definition.InitialTemperatureMilliKelvin;
			atmospherePressurePascals = definition.InitialAtmospherePressurePascals;
			carbonDioxidePartsPerMillion = definition.InitialCarbonDioxidePartsPerMillion;
			atmosphericWaterPartsPerMillion = definition.InitialAtmosphericWaterPartsPerMillion;
			tectonicActivityPerMille = definition.InitialTectonicActivityPerMille;
			var numerator = 9810L * definition.MassEarthMillionths * 6371 * 6371;
			var denominator = 1_000_000L * definition.RadiusKilometers * definition.RadiusKilometers;
			SurfaceGravityMilliMetersPerSecondSquared = checked((int)(numerator / denominator));
			UpdateEnergyState(0);
		}

		internal void AdvanceClimate(int macroDay)
		{
			climatePulseSequence++;
			geologicalAgeMillionYears++;
			if (climatePulseSequence % 100 == 0)
				rotationPeriodMinutes++;
			if (climatePulseSequence % 50 == 0 && tectonicActivityPerMille > 100)
				tectonicActivityPerMille--;

			UpdateEnergyState(macroDay);
			meanSurfaceTemperatureMilliKelvin = Math.Max(100_000,
				meanSurfaceTemperatureMilliKelvin +
				(radiativeEquilibriumMilliKelvin - meanSurfaceTemperatureMilliKelvin) / 8);

			if (meanSurfaceTemperatureMilliKelvin < 373_150 && atmosphericWaterPartsPerMillion > 10_000)
			{
				var condensation = Math.Min(atmosphericWaterPartsPerMillion - 10_000,
					20_000 + (373_150 - meanSurfaceTemperatureMilliKelvin) / 4);
				atmosphericWaterPartsPerMillion -= condensation;
				surfaceWaterCubicKilometers = checked((int)Math.Min(TargetOceanVolume,
					surfaceWaterCubicKilometers + condensation * 2000L));
				atmospherePressurePascals = Math.Max(1000,
					atmospherePressurePascals - atmospherePressurePascals * condensation / 1_000_000);
			}

			oceanCoveragePerMille = checked((int)Math.Min(710,
				surfaceWaterCubicKilometers * 710 / TargetOceanVolume));
			bondAlbedoPerMille = 380 - oceanCoveragePerMille * 120 / 710;
			if (oceanCoveragePerMille > 0)
				carbonDioxidePartsPerMillion = Math.Max(280,
					carbonDioxidePartsPerMillion - Math.Max(1, oceanCoveragePerMille / 20));
			atmospherePressurePascals += Math.Max(0, tectonicActivityPerMille / 100);
			UpdateEnergyState(macroDay);
		}

		internal PlanetPhysicsSaveData CreateSaveData() => new()
		{
			GeologicalAgeYears = GeologicalAgeYears,
			RotationPeriodMinutes = rotationPeriodMinutes,
			BondAlbedoPerMille = bondAlbedoPerMille,
			AbsorbedSolarWattsPerSquareMeter = absorbedSolarWattsPerSquareMeter,
			RadiativeEquilibriumMilliKelvin = radiativeEquilibriumMilliKelvin,
			MeanSurfaceTemperatureMilliKelvin = meanSurfaceTemperatureMilliKelvin,
			EnergyImbalanceMilliWattsPerSquareMeter = energyImbalanceMilliWattsPerSquareMeter,
			AtmospherePressurePascals = atmospherePressurePascals,
			CarbonDioxidePartsPerMillion = carbonDioxidePartsPerMillion,
			AtmosphericWaterPartsPerMillion = atmosphericWaterPartsPerMillion,
			SurfaceWaterCubicKilometers = surfaceWaterCubicKilometers,
			OceanCoveragePerMille = oceanCoveragePerMille,
			TectonicActivityPerMille = tectonicActivityPerMille,
			ClimatePulseSequence = climatePulseSequence
		};

		internal void Restore(PlanetPhysicsSaveData data)
		{
			if (data.GeologicalAgeYears % 1_000_000 != 0)
				throw new InvalidOperationException("Saved geological age is not aligned to one million years.");

			geologicalAgeMillionYears = checked((int)(data.GeologicalAgeYears / 1_000_000));
			rotationPeriodMinutes = data.RotationPeriodMinutes;
			bondAlbedoPerMille = data.BondAlbedoPerMille;
			absorbedSolarWattsPerSquareMeter = data.AbsorbedSolarWattsPerSquareMeter;
			radiativeEquilibriumMilliKelvin = data.RadiativeEquilibriumMilliKelvin;
			meanSurfaceTemperatureMilliKelvin = data.MeanSurfaceTemperatureMilliKelvin;
			energyImbalanceMilliWattsPerSquareMeter = data.EnergyImbalanceMilliWattsPerSquareMeter;
			atmospherePressurePascals = data.AtmospherePressurePascals;
			carbonDioxidePartsPerMillion = data.CarbonDioxidePartsPerMillion;
			atmosphericWaterPartsPerMillion = data.AtmosphericWaterPartsPerMillion;
			surfaceWaterCubicKilometers = checked((int)data.SurfaceWaterCubicKilometers);
			oceanCoveragePerMille = data.OceanCoveragePerMille;
			tectonicActivityPerMille = data.TectonicActivityPerMille;
			climatePulseSequence = data.ClimatePulseSequence;
		}

		void UpdateEnergyState(int macroDay)
		{
			var orbit = Math.Max(1, definition.OrbitalPeriodDays);
			var orbitalDay = Math.Abs(macroDay % orbit);
			var half = Math.Max(1, orbit / 2);
			var triangle = orbitalDay <= half ? orbitalDay * 2000 / half - 1000 :
				1000 - (orbitalDay - half) * 2000 / Math.Max(1, orbit - half);
			var seasonalFlux = definition.StellarFluxWattsPerSquareMeter *
				definition.OrbitalEccentricityMillionths * triangle / 1_000_000_000L;
			var incidentFlux = definition.StellarFluxWattsPerSquareMeter + (int)seasonalFlux;
			absorbedSolarWattsPerSquareMeter = incidentFlux * (1000 - bondAlbedoPerMille) / 4000;
			var greenhouse = 20_000 + carbonDioxidePartsPerMillion / 2 +
				atmosphericWaterPartsPerMillion / 10;
			radiativeEquilibriumMilliKelvin = Math.Clamp(
				255_000 + (absorbedSolarWattsPerSquareMeter - 239) * 270 + greenhouse,
				100_000, 900_000);
			energyImbalanceMilliWattsPerSquareMeter = Math.Clamp(
				(radiativeEquilibriumMilliKelvin - meanSurfaceTemperatureMilliKelvin) * 4,
				-2_000_000, 2_000_000);
		}

		void IEffect.Tick(World world) { }

		IEnumerable<IRenderable> IEffect.Render(WorldRenderer renderer) { return []; }
	}

	/// <summary>A synchronized planet node with its authoritative physical state.</summary>
	public sealed class PlanetState : IEffect, ISync
	{
		[VerifySync]
		bool active;

		[VerifySync]
		int lifecycleStage = (int)PlanetLifecycleStage.Lifeless;

		public PlanetDefinition Definition { get; }
		public PlanetPhysicalState Physics { get; }
		public PlanetSurfaceState Surface { get; }
		public bool Active => active;
		public PlanetLifecycleStage LifecycleStage => (PlanetLifecycleStage)lifecycleStage;

		public PlanetState(PlanetDefinition definition, bool active)
		{
			Definition = definition;
			Physics = new PlanetPhysicalState(definition.Physics);
			Surface = new PlanetSurfaceState(definition);
			this.active = active;
		}

		internal void AdvanceClimate(int macroDay)
		{
			if (active)
				Physics.AdvanceClimate(macroDay);
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
		const int TraitSaveSchemaVersion = 3;
		public const string UniverseId = "universe-0001";
		public const string StarSystemId = "tyranthos-system";

		static readonly PlanetDefinition[] PlanetDefinitions =
		[
			new(0, "planet-0001", "Tyranthos", new(
				1_020_000, 6450, 1020, 151_000, 380, 23_500, 18_000,
				1340, 12, 420_000, 420_000, 120_000, 650_000, 920)),
			new(1, "planet-0002", "Planet II", new(
				800_000, 5800, 1800, 210_000, 590, 12_000, 40_000,
				750, 8, 310_000, 90_000, 20_000, 80_000, 500)),
			new(2, "planet-0003", "Planet III", new(
				1_300_000, 7200, 780, 100_000, 210, 5000, 10_000,
				2300, 15, 700_000, 1_500_000, 450_000, 300_000, 800))
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
			{
				world.Add(planet);
				world.Add(planet.Physics);
				world.Add(planet.Surface);
			}
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
			ArgumentOutOfRangeException.ThrowIfNegative(ticks);

			var totalTicks = (long)macroTickRemainder + ticks;
			var advancedDays = checked((int)(totalTicks / TicksPerMacroDay));
			macroTickRemainder = (int)(totalTicks % TicksPerMacroDay);
			for (var i = 0; i < advancedDays; i++)
			{
				macroDay++;
				AppendEvent(UniverseMacroEventType.MacroDayAdvanced, macroDay, 0);
				foreach (var planet in StarSystem.Planets)
				{
					if (!planet.Active)
						continue;

					planet.AdvanceClimate(macroDay);
					AppendEvent(UniverseMacroEventType.PlanetClimatePulse, macroDay, planet.Definition.Index);
				}
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
				new("SchemaVersion", FieldSaver.FormatValue(TraitSaveSchemaVersion)),
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
				data.Add(new MiniYamlNode($"{prefix}Physics", FieldSaver.Save(planet.Physics.CreateSaveData())));
				data.Add(new MiniYamlNode($"{prefix}Surface", FieldSaver.Save(planet.Surface.CreateSaveData())));
			}

			return data;
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, MiniYaml data)
		{
			if (self.World.IsReplay)
				return;

			var schemaVersion = ReadInt(data, "SchemaVersion");
			if (schemaVersion < 1 || schemaVersion > TraitSaveSchemaVersion)
				throw new InvalidOperationException(
					$"Universe trait save schema {schemaVersion} is not supported; expected 1–{TraitSaveSchemaVersion}.");

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
				if (schemaVersion >= 2)
					planet.Physics.Restore(FieldLoader.Load<PlanetPhysicsSaveData>(
						RequiredNode(data, $"{prefix}Physics").Value));
				if (schemaVersion >= 3)
					planet.Surface.Restore(FieldLoader.Load<PlanetSurfaceSaveData>(
						RequiredNode(data, $"{prefix}Surface").Value));
			}

			events.Clear();
			self.World.RestoreGameSaveWorldTick(checkpointWorldTick);

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
