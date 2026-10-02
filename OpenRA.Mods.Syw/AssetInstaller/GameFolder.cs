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

		public string Find(string relative)
		{
			if (files.TryGetValue(relative.Replace('\\', '/'), out var path))
				return path;

			throw new FileNotFoundException($"Missing game file: {relative}");
		}

		public byte[] Read(string relative) => File.ReadAllBytes(Find(relative));

		public string Sha1(string relative) => Convert.ToHexString(SHA1.HashData(Read(relative))).ToLowerInvariant();
	}
}
