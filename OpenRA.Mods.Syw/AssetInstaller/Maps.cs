using System.IO;
using System.Linq;

namespace OpenRA.Mods.Syw.AssetInstaller
{
	// Maps of ours that play on a converted original map: their map.yaml (and scripts) are embedded in this assembly
	// (AssetInstaller/MapTemplates), the map.bin and preview come from the converted map, so the folders can only exist
	// after an install.
	public static class Maps
	{
		// (map, the converted original map it uses, files copied from a map in the repository)
		static readonly (string Name, string Base, (string File, string From)[] Shared)[] All =
		{
			("bot-test", "kmulti1", new[] { ("testlib.lua", "maps/start-test/testlib.lua") }),
			("ffa-spectate", "kmulti1", System.Array.Empty<(string, string)>()),
		};

		public static void Export(InstallContext c)
		{
			var assembly = typeof(Maps).Assembly;
			var resources = assembly.GetManifestResourceNames().Select(n => (Name: n, Path: n.Replace('\\', '/'))).ToList();
			foreach (var (name, source, shared) in All)
			{
				var prefix = $"MapTemplates/{name}/";
				var templates = resources.Where(r => r.Path.StartsWith(prefix, System.StringComparison.Ordinal)).ToList();
				if (templates.Count == 0)
					throw new FileNotFoundException($"No embedded template for map {name}.");

				foreach (var (resource, path) in templates)
				{
					using var stream = assembly.GetManifestResourceStream(resource);
					using var memory = new MemoryStream();
					stream.CopyTo(memory);
					c.WriteBytes($"maps/{name}/{path[prefix.Length..]}", memory.ToArray());
				}

				foreach (var file in new[] { "map.bin", "map.png" })
					c.WriteBytes($"maps/{name}/{file}", File.ReadAllBytes(c.OutputPath($"maps/{source}/{file}")));

				foreach (var (file, from) in shared)
					c.WriteBytes($"maps/{name}/{file}", File.ReadAllBytes(Path.Combine(c.ModFolder, from)));
			}
		}
	}
}
