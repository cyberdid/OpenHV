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

using System.Collections.Generic;
using OpenRA.Traits;

namespace OpenRA.Mods.HV.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Exposes deterministic living-faction scenario profiles as a synchronized lobby option.")]
	public sealed class CivilizationScenarioInfo : TraitInfo, ILobbyOptions
	{
		public const string OptionId = "civilizationprofile";
		public const string Balanced = "balanced";
		public const string Scarcity = "scarcity";

		[FluentReference]
		public readonly string Label = "options-civilization-profile.label";

		[FluentReference]
		public readonly string Description = "options-civilization-profile.description";

		[FluentReference(dictionaryReference: LintDictionaryReference.Values)]
		public readonly Dictionary<string, string> Values = new()
		{
			{ Balanced, "options-civilization-profile.balanced" },
			{ Scarcity, "options-civilization-profile.scarcity" }
		};

		public readonly string Default = Balanced;
		public readonly bool Locked;
		public readonly bool Visible = true;
		public readonly int DisplayOrder = 9;

		IEnumerable<LobbyOption> ILobbyOptions.LobbyOptions(MapPreview map)
		{
			yield return new LobbyOption(
				map,
				OptionId,
				Label,
				Description,
				Visible,
				DisplayOrder,
				Values,
				Default,
				Locked);
		}

		public override object Create(ActorInitializer init) { return new CivilizationScenario(this); }
	}

	public sealed class CivilizationScenario : INotifyCreated, ISync
	{
		readonly CivilizationScenarioInfo info;

		[VerifySync]
		int profile;

		public string Profile =>
			profile == 1 ? CivilizationScenarioInfo.Scarcity : CivilizationScenarioInfo.Balanced;

		public CivilizationScenario(CivilizationScenarioInfo info)
		{
			this.info = info;
			profile = info.Default == CivilizationScenarioInfo.Scarcity ? 1 : 0;
		}

		void INotifyCreated.Created(Actor self)
		{
			var selected = self.World.LobbyInfo.GlobalSettings.OptionOrDefault(
				CivilizationScenarioInfo.OptionId,
				info.Default);
			profile = selected == CivilizationScenarioInfo.Scarcity ? 1 : 0;
		}
	}
}
