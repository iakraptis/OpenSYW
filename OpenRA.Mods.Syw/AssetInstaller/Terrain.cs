using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenRA.Mods.Syw.AssetInstaller
{
	// The three terrain tilesets, every original skirmish map (cusmap/*.map) and the crop resources, all from the game's
	// own data (research: SYWtoORA/docs/formats/map.md "Tile attributes", stg.md, tools/auto_map_export.py):
	// - A map's terrain set is its header's 4th uint32 (syw.exe loads the header to 0x57fe28, so it lands on the set
	//   selector 0x57fe34 that LoadMapTile reads): 0 = Field1x (dirt), 1 = Field2x (snow), 2 = FIELD3x (grass).
	// - A cell (tile id, variant) draws frame variant * 256 + tile id of the set's three 300-frame sheets back to back.
	// - Passability: each terrain .spr keeps a 300-entry uint32 attribute table after its 16-byte header, from which the
	//   game fills its movement grid (0x419750): 0 and 256 walkable, 2 blocked, 4 and 512 water, 10-13 crop fields
	//   (10 ripe rice, 11 -> 12 -> 13 growing potatoes).
	// - Starts, title and map objects come from the .stg.
	// Crop cells are drawn as the matching cell of the bare field block and carry a Rice or Potato resource, which
	// FieldResourceRenderer draws on top.
	public static class Terrain
	{
		const int Columns = 20;
		const int FrameCount = 300;
		const int Tile = 32;
		const int VariantStride = 256;
		const int ResourceDensity = 12;
		const int BorderId = 1999;

		// Map preview colour of cells blocked by trees and other map objects.
		const string TreePreviewColor = "1E4A18";

		// (OpenRA tileset id, name, the three sheets: (output name, SPR file)). Template ids are 1000 + sheet * 300 + frame
		// in every set, so FieldResourceRenderer's FieldTemplateOrigin (1733, the bare field block) fits all of them.
		static readonly (int Id, string Name, (string Name, string File)[] Sheets)[] Sets =
		{
			(1, "Dirt", new[] { ("field11", "Field11.spr"), ("field12", "Field12.spr"), ("field13", "Field13.spr") }),
			(2, "Snow", new[] { ("field21", "Field21.spr"), ("field22", "Field22.spr"), ("field23", "Field23.spr") }),
			(3, "Grassland", new[] { ("field31", "FIELD31.SPR"), ("field32", "FIELD32.SPR"), ("field33", "Field33.spr") }),
		};

		// The six 5x5 field blocks of the third sheet: crop values cover their 21-cell diamonds.
		static readonly int[] BlockRows = { 0, 6 };
		static readonly int[] BlockCols = { 1, 7, 13 };
		static readonly (int Row, int Col) BareFieldBlock = (6, 13);

		static readonly Dictionary<string, byte> ResourceIndex = new() { ["potato"] = 1, ["rice"] = 2 };
		static readonly Dictionary<string, string> CropPreviewColors = new() { ["potato"] = "8A6A3A", ["rice"] = "D8B040" };

		// Crop sheets: (young block, ripe block) top-left in the third sheet; four density frames per position: young,
		// young, ripe, ripe (OpenRA picks the frame from density, so a full cell is ripe and a regrowing one starts young).
		static readonly (string Crop, (int Row, int Col) Young, (int Row, int Col) Ripe)[] Crops =
		{
			("rice", (0, 1), (0, 7)), ("potato", (6, 7), (6, 1))
		};

		static readonly (string Name, string Target, string Color)[] TerrainTypes =
		{
			("Clear", "Ground", "65754B"), ("Water", "Water", "285F82"), ("Rock", "Ground", "6E6A60"),
			("Cliff", "Ground", "7A5230"), ("Crops", "Ground", "8A6A3A"),
		};

		sealed record Template(int Id, string Sheet, int Frame, int Attribute, string TerrainType, string Category, string Color);

		sealed record SetData(int Id, string Name, (string Name, string File)[] Sheets, Template[] Templates, IndexedImage[][] Frames);

		public static void Export(InstallContext c)
		{
			var sets = Sets.Select(s => ExportTileset(c, s.Id, s.Name, s.Sheets)).ToDictionary(s => s.Id - 1);
			ExportCrops(c, sets.Values);

			var maps = c.Game.Files("cusmap").Where(f => f.EndsWith(".map", StringComparison.OrdinalIgnoreCase))
				.Select(f => Path.GetFileNameWithoutExtension(f).ToLowerInvariant()).ToList();
			foreach (var name in maps)
				ConvertMap(c, name, sets);
		}

		static SetData ExportTileset(InstallContext c, int id, string name, (string Name, string File)[] sheets)
		{
			var frames = sheets.Select(s => Enumerable.Range(0, FrameCount).Select(i => c.Spr(s.File).Frame(i)).ToArray()).ToArray();
			var templates = new List<Template>();
			for (var s = 0; s < sheets.Length; s++)
			{
				c.WriteRgba($"tilesets/field{id}/{sheets[s].Name}.png", Columns * Tile, FrameCount / Columns * Tile,
					SheetRgba(frames[s], c.Knight), FrameData(FrameCount));

				var attributes = Attributes(c.Game.Read($"FNT1/{sheets[s].File}"));
				for (var f = 0; f < FrameCount; f++)
				{
					var terrain = TerrainOf(s, attributes[f]);
					var category = terrain is "Water" or "Rock" or "Cliff" ? terrain
						: IsCrop(attributes[f]) ? "Field" : attributes[f] == 256 ? "Shore" : s == 0 ? "Land" : "Dirt";
					var rgb = frames[s][f].Pixels.Select(p => c.Knight.Colors[p]).ToArray();
					templates.Add(new Template(TemplateId(s, f), sheets[s].Name, f, attributes[f], terrain, category, AverageColor(rgb, Tile, Tile)));
				}
			}

			var border = new byte[Tile * Tile * 4];
			for (var i = 3; i < border.Length; i += 4)
				border[i] = 255;

			c.WriteRgba($"tilesets/field{id}/border.png", Columns * Tile, Tile, Pad(border, Tile, Columns * Tile), FrameData(1));
			c.RepoYaml($"tilesets/field{id}.yaml", TilesetYaml(id, name, templates));
			return new SetData(id, name, sheets, templates.ToArray(), frames);
		}

		static int TemplateId(int sheet, int frame) => 1000 + sheet * FrameCount + frame;

		static bool IsCrop(int attribute) => attribute is >= 10 and <= 13;

		// The terrain .spr attribute table (spr.md's "reserved" block): one uint32 per frame after the 16-byte header.
		static int[] Attributes(byte[] spr) => Enumerable.Range(0, FrameCount).Select(i => BitConverter.ToInt32(spr, 16 + 4 * i)).ToArray();

		static string TerrainOf(int sheet, int attribute)
		{
			if (attribute is 0 or 256 || IsCrop(attribute))
				return "Clear";
			if (attribute == 2)
				return sheet == 1 ? "Cliff" : "Rock";
			if (attribute is 4 or 512)
				return "Water";

			throw new InvalidDataException($"Unknown terrain attribute {attribute}.");
		}

		// The cell of the bare field block at the same position, for a frame inside one of the six field blocks.
		static int? BareFrame(int frame)
		{
			int row = frame / Columns, col = frame % Columns;
			foreach (var top in BlockRows)
				foreach (var left in BlockCols)
					if (top <= row && row < top + 5 && left <= col && col < left + 5)
						return (BareFieldBlock.Row + row - top) * Columns + BareFieldBlock.Col + col - left;

			return null;
		}

		static void ConvertMap(InstallContext c, string name, Dictionary<int, SetData> sets)
		{
			var (width, height, terrainSet, cells) = ReadMap(c.Game.Read($"cusmap/{name}.map"));
			var stage = c.Game.Read($"cusmap/{name}.stg");
			if (!sets.TryGetValue(terrainSet, out var set))
				throw new InvalidDataException($"{name}: unknown terrain set {terrainSet}.");

			var keys = new Template[height, width];
			var crops = new Dictionary<(int X, int Y), string>();
			for (var y = 0; y < height; y++)
				for (var x = 0; x < width; x++)
				{
					var index = cells[y, x].Variant * VariantStride + cells[y, x].TileId;
					int sheet = index / FrameCount, frame = index % FrameCount;
					var template = set.Templates[sheet * FrameCount + frame];
					if (IsCrop(template.Attribute))
					{
						crops[(x, y)] = template.Attribute == 10 ? "rice" : "potato";
						var bare = BareFrame(frame);
						if (bare != null)
							template = set.Templates[sheet * FrameCount + bare.Value];
					}

					keys[y, x] = template;
				}

			bool Inside((int X, int Y) p) => p.X >= 0 && p.Y >= 0 && p.X < width && p.Y < height;

			// Objects: the game ignores cells outside the map, and some maps list objects past their edge (left over from
			// the original editor). OpenRA needs the whole footprint on the map and one building per cell.
			var objects = new List<(MapObjects.ObjectType Type, int X, int Y)>();
			var taken = new HashSet<(int X, int Y)>();
			foreach (var o in MapObjects.Read(stage))
			{
				var blocked = MapObjects.BlockedCells(new[] { o }).ToList();
				if (blocked.All(Inside) && !blocked.Any(taken.Contains))
				{
					objects.Add(o);
					taken.UnionWith(blocked);
				}
			}

			bool Land((int X, int Y) p) => Inside(p) && keys[p.Y, p.X].TerrainType == "Clear" && !taken.Contains(p);

			// Starts: a type 1 placement creates the player's HQ (0x420e70), a 3x3 centred on the start cell. The map keeps
			// the start and the HQ gets BaseActorOffset -1,-1 (starting-hq.yaml). If OpenRA has no room for that 3x3,
			// take the nearest start that does.
			bool Fits((int X, int Y) p) => Enumerable.Range(-1, 3).All(i => Enumerable.Range(-1, 3).All(j => Land((p.X + i, p.Y + j))));
			var ring = Enumerable.Range(-4, 9).SelectMany(dx => Enumerable.Range(-4, 9).Select(dy => (dx, dy)))
				.OrderBy(d => Math.Abs(d.dx) + Math.Abs(d.dy)).ThenBy(d => d.dx).ThenBy(d => d.dy).ToList();
			var starts = ReadStarts(stage).ConvertAll(s => Fits(s) ? s :
				ring.Select(d => (s.X + d.dx, s.Y + d.dy)).Where(Fits).DefaultIfEmpty(s).First());

			if (starts.Count > 0)
			{
				var land = Reachable(Land, starts[0]);
				var cut = starts.FindAll(s => !land.Contains(s));
				if (cut.Count > 0)
					c.Warnings.Add($"{name}: starts {string.Join(", ", cut)} are not connected to {starts[0]} over land (a naval map?)");
			}

			WriteMap(c, name, Title(stage) ?? name, set.Id, keys, crops, starts, objects);
		}

		// The map's name, kept in the .stg after the map file path ("cusmap\kmulti1.map", "Fighter").
		static string Title(byte[] stage)
		{
			var text = Encoding.ASCII.GetString(stage, 0x2A40, 0x2A98 - 0x2A40);
			return text.Split('\0').Select(s => s.Trim()).FirstOrDefault(s => s.Length >= 2 && !s.Contains('\\') && s.All(ch => ch >= 0x20 && ch < 0x7f));
		}

		// The colour a 1x1 box downscale gives (as Pillow computes it): each row averaged and rounded, then the rows.
		static string AverageColor(Primitives.Color[] rgb, int width, int height)
		{
			int Channel(Func<Primitives.Color, int> pick)
			{
				var rows = 0;
				for (var y = 0; y < height; y++)
				{
					var sum = 0;
					for (var x = 0; x < width; x++)
						sum += pick(rgb[y * width + x]);

					rows += (sum + width / 2) / width;
				}

				return (rows + height / 2) / height;
			}

			return $"{Channel(p => p.R):X2}{Channel(p => p.G):X2}{Channel(p => p.B):X2}";
		}

		static HashSet<(int X, int Y)> Reachable(Func<(int X, int Y), bool> passable, (int X, int Y) start)
		{
			var seen = new HashSet<(int, int)> { start };
			var queue = new Queue<(int X, int Y)>();
			queue.Enqueue(start);
			while (queue.Count > 0)
			{
				var (x, y) = queue.Dequeue();
				for (var dx = -1; dx <= 1; dx++)
					for (var dy = -1; dy <= 1; dy++)
					{
						var n = (x + dx, y + dy);
						if ((dx != 0 || dy != 0) && !seen.Contains(n) && passable(n))
						{
							seen.Add(n);
							queue.Enqueue(n);
						}
					}
			}

			return seen;
		}

		static (int Width, int Height, int TerrainSet, (int TileId, int Variant)[,] Cells) ReadMap(byte[] data)
		{
			const int Header = 52;
			int width = BitConverter.ToInt32(data, 0), height = BitConverter.ToInt32(data, 4), terrainSet = BitConverter.ToInt32(data, 12);
			if (data.Length != Header + width * height * 4)
				throw new InvalidDataException("Unexpected .map size.");

			var cells = new (int, int)[height, width];
			for (var i = 0; i < width * height; i++)
				cells[i / width, i % width] = (data[Header + 4 * i], data[Header + 4 * i + 1]);

			return (width, height, terrainSet, cells);
		}

		// Player starts: the type 1 records of the .stg placement table (20-byte records x, y, type, owner, 0 from byte
		// 3488, ending at the first empty record), in player slot order.
		static List<(int X, int Y)> ReadStarts(byte[] data)
		{
			var starts = new List<(int X, int Y, int Owner)>();
			for (var offset = 3488; offset + 20 <= 0x2A40; offset += 20)
			{
				var values = Enumerable.Range(0, 5).Select(k => BitConverter.ToInt32(data, offset + 4 * k)).ToArray();
				if (values.All(v => v == 0))
					break;

				if (values[2] == 1)
					starts.Add((values[0], values[1], values[3]));
			}

			return starts.OrderBy(s => s.Owner).Select(s => (s.X, s.Y)).ToList();
		}

		static void WriteMap(InstallContext c, string name, string title, int tileset, Template[,] keys,
			Dictionary<(int X, int Y), string> crops, List<(int X, int Y)> starts, List<(MapObjects.ObjectType Type, int X, int Y)> objects)
		{
			// OpenRA needs a one-cell cordon around the playable area; it is filled with the black border tile.
			int h = keys.GetLength(0), w = keys.GetLength(1);
			int sizeW = w + 2, sizeH = h + 2;
			const int TilesOffset = 17;
			var resourcesOffset = TilesOffset + 3 * sizeW * sizeH;

			using var bin = new MemoryStream();
			using (var writer = new BinaryWriter(bin, Encoding.ASCII, true))
			{
				writer.Write((byte)2);
				writer.Write((ushort)sizeW);
				writer.Write((ushort)sizeH);
				writer.Write(TilesOffset);
				writer.Write(0);
				writer.Write(resourcesOffset);
				for (var x = 0; x < sizeW; x++)
					for (var y = 0; y < sizeH; y++)
					{
						var inside = x >= 1 && x <= w && y >= 1 && y <= h;
						writer.Write((ushort)(inside ? keys[y - 1, x - 1].Id : BorderId));
						writer.Write((byte)0);
					}

				for (var x = 0; x < sizeW; x++)
					for (var y = 0; y < sizeH; y++)
					{
						if (crops.TryGetValue((x - 1, y - 1), out var crop))
						{
							writer.Write(ResourceIndex[crop]);
							writer.Write((byte)ResourceDensity);
						}
						else
							writer.Write((ushort)0);
					}
			}

			c.WriteBytes($"maps/{name}/map.bin", bin.ToArray());

			var trees = MapObjects.BlockedCells(objects).ToHashSet();
			var preview = new byte[w * h * 4];
			for (var y = 0; y < h; y++)
				for (var x = 0; x < w; x++)
				{
					var hex = trees.Contains((x, y)) ? TreePreviewColor :
						crops.TryGetValue((x, y), out var crop) ? CropPreviewColors[crop] : keys[y, x].Color;
					var i = 4 * (y * w + x);
					preview[i] = Convert.ToByte(hex[..2], 16);
					preview[i + 1] = Convert.ToByte(hex.Substring(2, 2), 16);
					preview[i + 2] = Convert.ToByte(hex.Substring(4, 2), 16);
					preview[i + 3] = 255;
				}

			c.WriteRgba($"maps/{name}/map.png", w, h, preview);

			var players = Enumerable.Range(0, starts.Count).Select(i => $"Multi{i}").ToList();
			var yaml = new List<string>
			{
				"MapFormat: 12", "", "RequiresMod: syw", "", $"Title: {title}", "",
				$"Author: OpenSYW (converted from original SYW {name})", "", $"Tileset: FIELD{tileset}", "",
				$"MapSize: {sizeW},{sizeH}", "", $"Bounds: 1,1,{w},{h}", "", "Visibility: Lobby", "",
				"Categories: Conquest", "", "Players:",
				"\tPlayerReference@Neutral:", "\t\tName: Neutral", "\t\tOwnsWorld: True", "\t\tNonCombatant: True",
				"\t\tFaction: Random",
				"\tPlayerReference@Creeps:", "\t\tName: Creeps", "\t\tNonCombatant: True", "\t\tFaction: Random",
				$"\t\tEnemies: {string.Join(", ", players)}"
			};
			foreach (var p in players)
				yaml.AddRange(new[] { $"\tPlayerReference@{p}:", $"\t\tName: {p}", "\t\tPlayable: True", "\t\tFaction: Random", "\t\tEnemies: Creeps" });

			yaml.AddRange(new[] { "", "Actors:" });
			for (var i = 0; i < starts.Count; i++)
				yaml.AddRange(new[] { $"\tActor{i}: mpspawn", "\t\tOwner: Neutral", $"\t\tLocation: {starts[i].X + 1},{starts[i].Y + 1}" });

			yaml.AddRange(MapObjects.ActorYaml(objects));

			yaml.AddRange(new[] { "", "Rules: starting-hq.yaml" });
			c.WriteText($"maps/{name}/map.yaml", string.Join("\n", yaml) + "\n");

			// The original HQ is centred on the start; OpenRA puts the base actor's top-left there unless offset.
			c.WriteText($"maps/{name}/starting-hq.yaml",
				"world:\n\tStartingUnits@Korea:\n\t\tBaseActor: khq\n\t\tBaseActorOffset: -1,-1\n" +
				"\tStartingUnits@Japan:\n\t\tBaseActor: jhq\n\t\tBaseActorOffset: -1,-1\n");
		}

		static string TilesetYaml(int id, string name, IEnumerable<Template> templates)
		{
			var lines = new List<string>
			{
				"# Generated by --install-assets (AssetInstaller/Terrain.cs) from the terrain sheets' own attribute tables.",
				"General:", $"\tName: SYW {name}", $"\tId: FIELD{id}",
				"\tEditorTemplateOrder: Land, Dirt, Shore, Field, Water, Cliff, Rock, Border", "\tPalette:", "", "Terrain:"
			};
			foreach (var (type, target, color) in TerrainTypes)
				lines.AddRange(new[] { $"\tTerrainType@{type}:", $"\t\tType: {type}", $"\t\tTargetTypes: {target}", $"\t\tColor: {color}" });

			lines.AddRange(new[] { "", "Templates:" });
			foreach (var t in templates)
				lines.AddRange(new[]
				{
					$"\tTemplate@{t.Id}:", $"\t\tId: {t.Id}", $"\t\tImages: tilesets/field{id}/{t.Sheet}.png", $"\t\tFrames: {t.Frame}",
					"\t\tSize: 1,1", $"\t\tCategories: {t.Category}", "\t\tTiles:", $"\t\t\t0: {t.TerrainType}",
					$"\t\t\t\tMinColor: {t.Color}", $"\t\t\t\tMaxColor: {t.Color}", ""
				});

			lines.AddRange(new[]
			{
				$"\tTemplate@{BorderId}:", $"\t\tId: {BorderId}", $"\t\tImages: tilesets/field{id}/border.png", "\t\tFrames: 0",
				"\t\tSize: 1,1", "\t\tCategories: Border", "\t\tTiles:", "\t\t\t0: Clear", "\t\t\t\tMinColor: 000000",
				"\t\t\t\tMaxColor: 000000", ""
			});

			return string.Join("\n", lines);
		}

		// Rice and potato sheets from each set's third sheet: the grassland art is the default, dirt and snow maps use
		// their own (sequence TilesetFilenames).
		static void ExportCrops(InstallContext c, IEnumerable<SetData> sets)
		{
			var yaml = new List<string>();
			foreach (var (crop, young, ripe) in Crops)
			{
				var files = new Dictionary<int, string>();
				foreach (var set in sets)
				{
					var frames = new List<IndexedImage>();
					for (var row = 0; row < 5; row++)
						for (var col = 0; col < 5; col++)
							foreach (var (blockRow, blockCol) in new[] { young, young, ripe, ripe })
								frames.Add(set.Frames[2][(blockRow + row) * Columns + blockCol + col]);

					var file = set.Id == 3 ? $"art/resources/{crop}.png" : $"art/resources/{crop}-field{set.Id}.png";
					var rows = (frames.Count + Columns - 1) / Columns;
					c.WriteRgba(file, Columns * Tile, rows * Tile, SheetRgba(frames, c.Knight), FrameData(frames.Count));
					files[set.Id] = file;
				}

				yaml.AddRange(new[] { $"{crop}:", "\tDefaults:", $"\t\tFilename: {files[3]}", "\t\tTilesetFilenames:" });
				yaml.AddRange(files.Where(f => f.Key != 3).Select(f => $"\t\t\tFIELD{f.Key}: {f.Value}"));
				yaml.Add("\t\tLength: 4");
				for (var row = 0; row < 5; row++)
					for (var col = 0; col < 5; col++)
						yaml.AddRange(new[] { $"\tr{row}c{col}:", $"\t\tStart: {(row * 5 + col) * 4}" });

				yaml.Add("");
			}

			c.RepoYaml("sequences/common/resources.yaml", string.Join("\n", yaml));
		}

		// 32x32 frames on a sheet 20 frames wide, as RGBA.
		static byte[] SheetRgba(IReadOnlyList<IndexedImage> frames, SywPalette palette)
		{
			var rows = (frames.Count + Columns - 1) / Columns;
			var sheet = new IndexedImage(Columns * Tile, rows * Tile);
			for (var i = 0; i < frames.Count; i++)
				sheet.Paste(frames[i], i % Columns * Tile, i / Columns * Tile);

			return InstallContext.ToRgba(sheet, palette);
		}

		static byte[] Pad(byte[] rgba, int width, int newWidth)
		{
			var height = rgba.Length / 4 / width;
			var result = new byte[newWidth * height * 4];
			for (var y = 0; y < height; y++)
				Array.Copy(rgba, y * width * 4, result, y * newWidth * 4, width * 4);

			return result;
		}

		static Dictionary<string, string> FrameData(int count) =>
			new() { ["FrameSize"] = $"{Tile},{Tile}", ["FrameAmount"] = count.ToStringInvariant() };
	}
}
