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
using System.IO;
using System.Text.Json;

namespace OpenRA.Mods.HV
{
	public sealed class SimulationCheckpointManifest
	{
		public int SchemaVersion { get; init; }
		public string CheckpointName { get; init; }
		public int WorldTick { get; init; }
		public string SynchronizedStateHash { get; init; }
		public SimulationUniverseResult Universe { get; init; }
	}

	public static class SimulationCheckpointManifestWriter
	{
		static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = true
		};

		public static void Write(string path, SimulationCheckpointManifest manifest)
		{
			var directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directory))
				Directory.CreateDirectory(directory);

			var temporaryPath = path + $".tmp-{Environment.ProcessId}-{Guid.NewGuid():N}";
			try
			{
				File.WriteAllText(temporaryPath, JsonSerializer.Serialize(manifest, JsonOptions));
				File.Move(temporaryPath, path, true);
			}
			finally
			{
				if (File.Exists(temporaryPath))
					File.Delete(temporaryPath);
			}
		}
	}
}
