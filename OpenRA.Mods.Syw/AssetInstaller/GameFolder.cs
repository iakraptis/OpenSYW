using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace OpenRA.Mods.Syw.AssetInstaller
{
	// The original Seven Years War folder. File names in the game mix cases (FIELD31.SPR, Field33.spr), so lookups ignore
	// case to work on case-sensitive file systems too.
	public sealed class GameFolder
	{
		// Files that must exist for a folder to count as the game.
		static readonly string[] RequiredFiles = { "syw.exe", "FNT1/Knight.pal", "FNT1/TANK.SPR", "fst/Pannel13.fst", "Ani/ani.anm" };

		public readonly string Root;
		readonly Dictionary<string, string> files = new(StringComparer.OrdinalIgnoreCase);

		public GameFolder(string root)
		{
			Root = Path.GetFullPath(root);
			if (!Directory.Exists(Root))
				throw new DirectoryNotFoundException($"Game folder not found: {Root}");

			foreach (var path in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
				files[Path.GetRelativePath(Root, path).Replace('\\', '/')] = path;

			var missing = RequiredFiles.Where(f => !files.ContainsKey(f)).ToList();
			if (missing.Count > 0)
				throw new FileNotFoundException(
					$"{Root} does not look like a Seven Years War folder; missing: {string.Join(", ", missing)}");
		}

		// A quick check (no full file scan) whether root holds the game, for auto-detection and the folder picker.
		public static bool LooksLike(string root)
		{
			try
			{
				if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
					return false;

				var options = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = false };
				return RequiredFiles.All(file =>
				{
					var folder = Path.Combine(root, Path.GetDirectoryName(file) ?? "");
					return Directory.Exists(folder) && Directory.EnumerateFiles(folder, Path.GetFileName(file), options).Any();
				});
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
			{
				return false;
			}
		}

		public string Find(string relative)
		{
			if (files.TryGetValue(relative.Replace('\\', '/'), out var path))
				return path;

			throw new FileNotFoundException($"Missing game file: {relative}");
		}

		public byte[] Read(string relative) => File.ReadAllBytes(Find(relative));

		// The files directly inside a folder (any case), as relative paths in the game's own spelling, sorted.
		public IEnumerable<string> Files(string folder)
		{
			var prefix = folder.Replace('\\', '/').TrimEnd('/') + "/";
			return files.Keys
				.Where(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && f.IndexOf('/', prefix.Length) < 0)
				.OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
		}

		public string Sha1(string relative) => Convert.ToHexString(SHA1.HashData(Read(relative))).ToLowerInvariant();
	}
}
