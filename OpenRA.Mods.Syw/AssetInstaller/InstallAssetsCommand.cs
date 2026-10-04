using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace OpenRA.Mods.Syw.AssetInstaller
{
	// utility.cmd --install-assets [GAME_FOLDER] [--out FOLDER] [--only STAGE,...] [--write-yaml] [--list]
	// Converts the art, palettes, music, sounds and maps from an original Seven Years War folder into the mod. GAME_FOLDER
	// defaults to a SYWAR folder next to the repository; the output goes into the player's content folder
	// (%APPDATA%/OpenRA/Content/syw/v1, the same place the in-game installer writes) unless --out is given.
	sealed class InstallAssetsCommand : IUtilityCommand
	{
		string IUtilityCommand.Name => "--install-assets";

		bool IUtilityCommand.ValidateArguments(string[] args) => true;

		[Desc("[GAME_FOLDER]", "[--out FOLDER] [--only STAGE,...] [--write-yaml] [--list]",
			"Convert the assets of an original Seven Years War installation into the mod.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			var modFolder = utility.ModData.Manifest.Package.Name;
			var options = args.Skip(1).ToList();

			if (options.Contains("--list"))
			{
				foreach (var stage in Stages.All)
					Console.WriteLine($"{stage.Name,-22} {stage.Description}");

				return;
			}

			var output = TakeValue(options, "--out") ?? SywContent.Folder;
			var only = TakeValue(options, "--only")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			var writeYaml = options.Remove("--write-yaml");
			var gamePath = options.FirstOrDefault(o => !o.StartsWith("--", StringComparison.Ordinal))
				?? Path.Combine(modFolder, "..", "..", "..", "SYWAR");

			var unknown = only?.Where(name => Stages.All.All(s => s.Name != name)).ToList();
			if (unknown?.Count > 0)
				throw new ArgumentException($"Unknown stage(s): {string.Join(", ", unknown)}. Use --list to see them.");

			Console.WriteLine($"Seven Years War folder: {Path.GetFullPath(gamePath)}");
			Console.WriteLine($"Writing to: {Path.GetFullPath(output)}");

			var total = Stopwatch.StartNew();
			var watch = new Stopwatch();
			string running = null;
			void Report()
			{
				if (running != null)
					Console.WriteLine($"  {running,-22} {watch.ElapsedMilliseconds,6} ms");
			}

			var warnings = SywContent.Run(gamePath, output, modFolder, Stages.All.Where(s => only == null || only.Contains(s.Name)),
				writeYaml, mergeManifest: only != null, progress: (stage, _, _) =>
				{
					Report();
					running = stage.Name;
					watch.Restart();
				});
			Report();

			foreach (var warning in warnings)
				Console.WriteLine($"WARNING: {warning}");

			Console.WriteLine($"Done in {total.Elapsed.TotalSeconds:0.0} s.");
		}

		static string TakeValue(List<string> options, string name)
		{
			var i = options.IndexOf(name);
			if (i < 0)
				return null;

			if (i + 1 >= options.Count)
				throw new ArgumentException($"{name} needs a value.");

			var value = options[i + 1];
			options.RemoveRange(i, 2);
			return value;
		}
	}
}
