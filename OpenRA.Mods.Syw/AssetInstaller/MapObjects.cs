using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenRA.Mods.Syw.AssetInstaller
{
	// The trees, Jangseung totems, brazier and flags an original map places on its terrain (TREE.SPR).
	//
	// From syw.exe (research: SYWtoORA/tools/map_objects.py, docs/formats/stg.md "Map objects"): a .stg file keeps the
	// object list at a fixed offset (count at 0x2A98, then 200-entry int32 arrays of type, x and y). The stage loader
	// (0x40ed10) calls one setup function per type (SetTree1-22, JangSung, FireTable, KoreaFlag, JapanFlag), whose
	// constructor sets the fields below. Pieces are frame = first + row * 20 + column.
	public static class MapObjects
	{
		const int Tile = 32;
		const int Columns = 20;
		const int CountOffset = 0x2A98;
		const int Capacity = 200;
		const int ShadowAlpha = 110;

		// Shadows draw below units: a soldier standing in a tree's shadow is not darkened by it.
		const int ShadowZOffset = -3072;

		public sealed record ObjectType(int Id, string Name, string Tooltip, int First, int W, int H, int OffX, int OffY,
			int? Shadow, int ShadowW, int ShadowH, int ShadowX, int ShadowY, bool BlocksArea, int Frames = 1)
		{
			// Blocked cells relative to the object's cell: the trunk row for trees, the whole patch for thickets.
			public (int X, int Y, int W, int H) Footprint => BlocksArea ? (OffX, OffY, W, H) : (0, 0, W, 1);
		}

		static ObjectType Tree(int id, string name, int first, int w, int h, int? shadow, int sw, int sh, int sx, int sy) =>
			new(id, name, "actor-tree", first, w, h, 0, 1 - h, shadow, sw, sh, sx, sy, false);

		static ObjectType Thicket(int id, string name, int first, int size) =>
			new(id, name, "actor-thicket", first, size, size, -1, -1, null, 0, 0, 0, 0, true);

		// Indexed by the .stg type; values from the constructors at 0x4260d0-0x427050.
		public static readonly ObjectType[] Types =
		{
			Tree(0x32, "tree1", 33, 2, 3, 70, 3, 1, -2, 0),
			Tree(0x33, "tree2", 104, 3, 4, 140, 4, 1, -2, -1),
			Tree(0x34, "tree3", 0, 2, 2, null, 0, 0, 0, 0) with { Tooltip = "actor-hut" },
			Tree(0x35, "tree4", 2, 3, 3, 62, 2, 2, -1, -1),
			Tree(0x36, "tree5", 40, 1, 2, 61, 1, 1, -1, 0),
			Tree(0x37, "tree6", 80, 1, 2, 101, 2, 1, -1, 0),
			Tree(0x38, "tree7", 5, 2, 3, 65, 2, 2, -1, -1),
			Tree(0x39, "jangseung", 7, 1, 2, 47, 1, 1, 0, 0) with { Tooltip = "actor-jangseung" },

			// The brazier and flags animate through the next frames of their row (TREE 240-247, 260-263 over 280-283,
			// 264-267 over 284-287); the frame counts are read off the sprite sheet, not yet traced in syw.exe.
			new(0x3a, "firetable", "actor-firetable", 240, 1, 1, 0, 0, null, 0, 0, 0, 0, false, 8),
			new(0x3b, "koreaflag", "actor-koreaflag", 260, 1, 2, 0, -1, null, 0, 0, 0, 0, false, 4),
			new(0x3c, "japanflag", "actor-japanflag", 264, 1, 2, 0, -1, null, 0, 0, 0, 0, false, 4),
			Tree(0x3d, "tree8", 182, 2, 3, 200, 2, 2, -1, -1),
			Tree(0x3e, "tree9", 186, 2, 3, 204, 2, 2, -1, -1),
			Tree(0x3f, "tree10", 251, 2, 3, 268, 3, 2, -2, -1),
			Tree(0x40, "tree11", 255, 2, 3, 273, 2, 2, -1, -1),
			Tree(0x41, "tree12", 15, 3, 3, 75, 3, 2, -2, -1),
			Tree(0x42, "tree13", 18, 2, 2, 58, 2, 1, -1, 0),
			Tree(0x43, "tree14", 11, 2, 2, 51, 2, 1, -1, 0),
			Tree(0x44, "tree15", 188, 1, 2, 228, 2, 1, -1, 0),
			Tree(0x45, "tree16", 190, 1, 2, 230, 2, 1, -1, 0),
			Tree(0x46, "tree17", 192, 1, 2, 232, 2, 1, -1, 0),
			Thicket(0x47, "tree18", 194, 3),
			Thicket(0x48, "tree19", 157, 2),
			Thicket(0x49, "tree20", 197, 2),
			Thicket(0x4a, "tree21", 237, 2),
			Thicket(0x4b, "tree22", 277, 2),
		};

		static readonly Dictionary<int, ObjectType> ById = Types.ToDictionary(t => t.Id);

		// The objects of a map, in file order: (type, cell). Unknown types are an error, so a new map can't lose objects
		// silently.
		public static List<(ObjectType Type, int X, int Y)> Read(byte[] stg)
		{
			var count = BitConverter.ToInt32(stg, CountOffset);
			if (count < 0 || count > Capacity)
				throw new InvalidDataException($"Map object count {count} out of range.");

			int Value(int array, int i) => BitConverter.ToInt32(stg, CountOffset + 4 + 4 * (array * Capacity + i));

			var objects = new List<(ObjectType, int, int)>();
			for (var i = 0; i < count; i++)
			{
				var id = Value(0, i);
				if (!ById.TryGetValue(id, out var type))
					throw new InvalidDataException($"Unknown map object type {id} at entry {i}.");

				objects.Add((type, Value(1, i), Value(2, i)));
			}

			return objects;
		}

		// map.yaml actors for a map's objects. The footprint's top-left cell is the actor location (+1 for the cordon).
		public static IEnumerable<string> ActorYaml(IEnumerable<(ObjectType Type, int X, int Y)> objects)
		{
			var i = 0;
			foreach (var (type, x, y) in objects)
			{
				var (fx, fy, _, _) = type.Footprint;
				yield return $"\tObject{i++}: {type.Name}";
				yield return "\t\tOwner: Neutral";
				yield return $"\t\tLocation: {x + fx + 1},{y + fy + 1}";
			}
		}

		// Cells the objects block, for the map preview.
		public static IEnumerable<(int X, int Y)> BlockedCells(IEnumerable<(ObjectType Type, int X, int Y)> objects)
		{
			foreach (var (type, x, y) in objects)
			{
				var (fx, fy, fw, fh) = type.Footprint;
				for (var j = 0; j < fh; j++)
					for (var i = 0; i < fw; i++)
						yield return (x + fx + i, y + fy + j);
			}
		}

		public static void Export(InstallContext c)
		{
			var tree = c.Spr("TREE");
			foreach (var t in Types)
			{
				var frames = Enumerable.Range(0, t.Frames).Select(k => Picture(tree, t.First + k, t.W, t.H)).ToList();
				c.WriteRgbaStrip($"art/objects/{t.Name}.png", frames);
				if (t.Shadow != null)
					c.WriteRgba($"art/objects/{t.Name}-shadow.png", t.ShadowW * Tile, t.ShadowH * Tile,
						Shadow(Picture(tree, t.Shadow.Value, t.ShadowW, t.ShadowH)));
			}

			c.RepoYaml("rules/objects.yaml", RulesYaml());
			c.RepoYaml("sequences/objects.yaml", SequencesYaml());
		}

		static IndexedImage Picture(SprFile sprite, int first, int w, int h)
		{
			var tiles = new IndexedImage[h, w];
			for (var r = 0; r < h; r++)
				for (var col = 0; col < w; col++)
					tiles[r, col] = sprite.Frame(first + r * Columns + col);

			return IndexedImage.Grid(tiles);
		}

		// The shadow frames are drawn in solid black; the game darkens the ground under them.
		static byte[] Shadow(IndexedImage image)
		{
			var rgba = new byte[image.Pixels.Length * 4];
			for (var i = 0; i < image.Pixels.Length; i++)
				rgba[4 * i + 3] = image.Pixels[i] == 0 ? (byte)0 : (byte)ShadowAlpha;

			return rgba;
		}

		// Pixel offset from the actor's centre (the centre of its footprint) to the centre of a picture whose top-left
		// cell is (x, y) relative to the object's cell.
		static string Offset(ObjectType t, int x, int y, int w, int h)
		{
			var (fx, fy, fw, fh) = t.Footprint;
			var dx = x * Tile + w * Tile / 2 - (fx * Tile + fw * Tile / 2);
			var dy = y * Tile + h * Tile / 2 - (fy * Tile + fh * Tile / 2);
			return $"{dx},{dy}";
		}

		static string RulesYaml()
		{
			var lines = new List<string>
			{
				"# Generated by --install-assets (AssetInstaller/MapObjects.cs) from the object table recovered from syw.exe;",
				"# edit the table there. Map objects are neutral, cannot be selected or destroyed, and block movement on",
				"# the cells the original blocks.",
				"^syw-object:",
				"\tInteractable:",
				"\tHiddenUnderShroud:",
				"\tTooltip:",
				"\t\tShowOwnerRow: false",
				"\tBodyOrientation:",
				"\t\tQuantizedFacings: 1",
				"\tRenderSprites:",
				"\tWithSpriteBody:",
				"\tMapEditorData:",
				"\t\tCategories: Decoration",
				""
			};

			foreach (var t in Types)
			{
				var (_, _, fw, fh) = t.Footprint;
				lines.AddRange(new[]
				{
					$"{t.Name}:", "\tInherits: ^syw-object", "\tTooltip:", $"\t\tName: {t.Tooltip}.name", "\tBuilding:",
					$"\t\tFootprint: {string.Join(" ", Enumerable.Repeat(new string('x', fw), fh))}", $"\t\tDimensions: {fw},{fh}"
				});
				if (t.Shadow != null)
					lines.AddRange(new[] { "\tWithIdleOverlay@SHADOW:", "\t\tSequence: shadow" });

				lines.Add("");
			}

			return string.Join("\n", lines);
		}

		static string SequencesYaml()
		{
			var lines = new List<string> { "# Generated by --install-assets (AssetInstaller/MapObjects.cs); see rules/objects.yaml." };
			foreach (var t in Types)
			{
				lines.AddRange(new[] { $"{t.Name}:", "\tidle:", $"\t\tFilename: art/objects/{t.Name}.png", $"\t\tOffset: {Offset(t, t.OffX, t.OffY, t.W, t.H)}" });
				if (t.Frames > 1)
					lines.AddRange(new[] { $"\t\tLength: {t.Frames}", "\t\tTick: 120" });

				if (t.Shadow != null)
					lines.AddRange(new[]
					{
						"\tshadow:", $"\t\tFilename: art/objects/{t.Name}-shadow.png",
						$"\t\tOffset: {Offset(t, t.ShadowX, t.ShadowY, t.ShadowW, t.ShadowH)}", $"\t\tZOffset: {ShadowZOffset}"
					});

				lines.Add("");
			}

			return string.Join("\n", lines);
		}
	}
}
