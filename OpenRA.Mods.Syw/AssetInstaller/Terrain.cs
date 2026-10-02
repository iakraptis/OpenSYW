using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenRA.Mods.Syw.AssetInstaller
{
	// The FIELD3 tileset (the original grassland tiles), the converted Korea Multi 1 map and the crop resources.
	// Formats: SYWtoORA/docs/formats/map.md and stg.md. Each .map cell is (tile id, variant); the game draws global
	// frame variant * 256 + tile id of the three 300-frame sheets FIELD31, FIELD32 and Field33 loaded back to back.
	// Field33 holds six 5x5 farm blocks with identical outlines (crop growth stages); crop cells are drawn as the bare
	// block and carry a Rice or Potato resource, which FieldResourceRenderer draws on top.
	public static class Terrain
	{
		const int Columns = 20;
		const int FrameCount = 300;
		const int Tile = 32;
		const int VariantStride = 256;
		const int ResourceDensity = 12;

		static readonly string[] Sheets = { "field31", "field32", "field33" };
		static readonly Dictionary<string, string> SheetFiles = new() { ["field31"] = "FIELD31", ["field32"] = "FIELD32", ["field33"] = "Field33.spr" };
		static readonly Dictionary<string, int> TemplateBase = new() { ["field31"] = 1000, ["field32"] = 1300, ["field33"] = 1600, ["border"] = 1999 };
		static readonly (string Sheet, int Frame) BorderKey = ("border", 0);

		static readonly HashSet<int> BridgeFrames = new() { 149, 167, 168, 170, 171, 185, 186, 187, 188, 190, 191, 205, 206, 207, 208, 209, 227 };

		// Impassable cliff pieces, confirmed in the real game as kmulti1 variant-1 ids (FIELD32 frame = id - 44).
		static readonly HashSet<int> CliffFrames = new[]
		{
			46, 47, 48, 49, 50, 51, 65, 66, 71, 72, 84, 85, 92, 93, 104, 108, 109, 113, 124, 126, 127, 128, 132, 133, 144, 145,
			146, 147, 148, 149, 150, 152, 153, 164, 165, 168, 169, 172, 173, 184, 185, 186, 191, 192, 193, 205, 206, 207, 210,
			211, 212, 226, 227, 228, 229, 230, 231, 247, 248, 249, 250
		}.Select(i => i - 44).ToHashSet();

		static readonly HashSet<int> WellFrames = new[] { 196, 197 }.Select(i => i - 44).ToHashSet();

		// Shoreline pieces confirmed non-walkable although under half water (FIELD31 frame = id).
		static readonly HashSet<int> ShoreWaterFrames = new() { 20, 22, 23, 26, 31, 44, 47, 50, 166, 229 };

		// Shore rock formations, blocked for ships and land units (Field33 frame = variant-3 id + 168).
		static readonly HashSet<int> ShoreRockFrames = new[] { 56, 96, 97 }.Select(i => i + 168).ToHashSet();

		// Confirmed walkable despite looking like stone.
		static readonly HashSet<int> WalkableFrames = new[] { 234 }.Select(i => i - 44).ToHashSet();

		// Field33 5x5 blocks by top-left (row, col): the crop, or null for the bare field.
		static readonly Dictionary<(int Row, int Col), string> FieldBlocks = new()
		{
			[(0, 1)] = "rice",
			[(0, 7)] = "rice",
			[(0, 13)] = "potato",
			[(6, 1)] = "potato",
			[(6, 7)] = "potato",
			[(6, 13)] = null
		};

		static readonly (int Row, int Col) BareFieldBlock = (6, 13);
		static readonly Dictionary<string, byte> ResourceIndex = new() { ["potato"] = 1, ["rice"] = 2 };
		static readonly Dictionary<string, string> CropPreviewColors = new() { ["potato"] = "8A6A3A", ["rice"] = "D8B040" };

		// Crop sheets: (young block, ripe block) top-left in Field33; four density frames per position: young, young,
		// ripe, ripe (OpenRA picks the frame from density, so a full cell is ripe and a regrowing one starts young).
		static readonly (string Crop, (int Row, int Col) Young, (int Row, int Col) Ripe)[] Crops =
		{
			("rice", (0, 1), (0, 7)), ("potato", (6, 7), (6, 1))
		};

		static readonly (string Name, string Target, string Color)[] TerrainTypes =
		{
			("Clear", "Ground", "65754B"), ("Water", "Water", "285F82"), ("Rock", "Ground", "6E6A60"),
			("Cliff", "Ground", "7A5230"), ("Crops", "Ground", "8A6A3A"),
		};

		// Original maps: (map, title, start moves requested by the user keyed by the original start, 0-based).
		static readonly (string Name, string Title, Dictionary<(int, int), (int, int)> StartMoves)[] Maps =
		{
			("kmulti1", "Korea Multi 1", new() { [(113, 116)] = (113, 115) }),
		};

		sealed record Template(int Id, string Sheet, int Frame, string TerrainType, string Category, string Color);

		public static void Export(InstallContext c)
		{
			var sheets = Sheets.ToDictionary(s => s, s => Enumerable.Range(0, FrameCount).Select(i => c.Spr(SheetFiles[s]).Frame(i)).ToArray());
			foreach (var (name, frames) in sheets)
				c.WriteRgba($"tilesets/field3/{name}.png", Columns * Tile, FrameCount / Columns * Tile,
					SheetRgba(frames, c.Knight), FrameData(frames.Length));

			var border = new byte[Tile * Tile * 4];
			for (var i = 3; i < border.Length; i += 4)
				border[i] = 255;

			c.WriteRgba("tilesets/field3/border.png", Columns * Tile, Tile, Pad(border, Tile, Columns * Tile), FrameData(1));

			var templates = new SortedDictionary<(string Sheet, int Frame), Template>(Comparer<(string Sheet, int Frame)>.Create((a, b) =>
				string.CompareOrdinal(a.Sheet, b.Sheet) != 0 ? string.CompareOrdinal(a.Sheet, b.Sheet) : a.Frame.CompareTo(b.Frame)));

			foreach (var (name, title, moves) in Maps)
			{
				var (width, height, cells) = ReadMap(c.Game.Read($"cusmap/{name}.map"));
				var starts = ReadStarts(c.Game.Read($"cusmap/{name}.stg"));
				var keys = new (string Sheet, int Frame)[height, width];
				var crops = new Dictionary<(int X, int Y), string>();
				for (var y = 0; y < height; y++)
					for (var x = 0; x < width; x++)
					{
						var (sheet, frame) = SourceFrame(cells[y, x].Variant, cells[y, x].TileId);
						var block = FieldBlock(sheet, frame);
						if (block != null)
						{
							var crop = FieldBlocks[block.Value.Block];
							if (crop != null)
								crops[(x, y)] = crop;

							frame = (BareFieldBlock.Row + block.Value.Row) * Columns + BareFieldBlock.Col + block.Value.Col;
						}

						keys[y, x] = (sheet, frame);
					}

				foreach (var key in keys.Cast<(string, int)>().Append(BorderKey))
					if (!templates.ContainsKey(key))
						templates[key] = MakeTemplate(key, key == BorderKey ? null : sheets[key.Item1][key.Item2], c.Knight);

				var start = starts[0];
				var land = Reachable(keys, templates, start, width, height);
				var cut = starts.FindAll(s => !land.Contains(s));
				if (cut.Count > 0)
					throw new InvalidDataException($"{name}: starts not connected over land: {string.Join(", ", cut)}");

				WriteMap(c, name, title, keys, templates, crops, starts.ConvertAll(s => moves.TryGetValue(s, out var moved) ? moved : s));
			}

			c.RepoYaml("tilesets/field3.yaml", TilesetYaml(templates.Values));
			ExportCrops(c, sheets["field33"]);
		}

		static (string Sheet, int Frame) SourceFrame(int variant, int tileId)
		{
			var index = variant * VariantStride + tileId;
			return (Sheets[index / FrameCount], index % FrameCount);
		}

		static ((int Row, int Col) Block, int Row, int Col)? FieldBlock(string sheet, int frame)
		{
			if (sheet != "field33")
				return null;

			int row = frame / Columns, col = frame % Columns;
			foreach (var (top, left) in FieldBlocks.Keys)
				if (top <= row && row < top + 5 && left <= col && col < left + 5)
					return ((top, left), row - top, col - left);

			return null;
		}

		static Template MakeTemplate((string Sheet, int Frame) key, IndexedImage image, SywPalette palette)
		{
			var (sheet, frame) = key;
			if (image == null)
				return new Template(TemplateBase[sheet] + frame, sheet, frame, "Clear", "Border", "000000");

			var rgb = Enumerable.Range(0, image.Pixels.Length).Select(i => palette.Colors[image.Pixels[i]]).ToArray();
			var terrain = Classify(sheet, frame, rgb);
			string category;
			if (sheet == "field31" && BridgeFrames.Contains(frame))
				category = "Bridge";
			else if (FieldBlock(sheet, frame) != null)
				category = "Field";
			else if (terrain is "Water" or "Rock" or "Cliff")
				category = terrain;
			else
				category = sheet == "field31" ? "Land" : "Dirt";

			return new Template(TemplateBase[sheet] + frame, sheet, frame, terrain, category, AverageColor(rgb, image.Width, image.Height));
		}

		static string Classify(string sheet, int frame, Primitives.Color[] rgb)
		{
			if (FieldBlock(sheet, frame) != null || (sheet == "field31" && BridgeFrames.Contains(frame)))
				return "Clear";
			if (sheet == "field33" && ShoreRockFrames.Contains(frame))
				return "Rock";
			if (sheet == "field31" && ShoreWaterFrames.Contains(frame))
				return "Water";
			if (sheet == "field32" && CliffFrames.Contains(frame))
				return "Cliff";
			if (sheet == "field32" && WellFrames.Contains(frame))
				return "Rock";
			if (sheet == "field32" && WalkableFrames.Contains(frame))
				return "Clear";

			// Otherwise by colour: mostly blue is water; outside FIELD31, a quarter grey is rock.
			var blue = rgb.Count(p => p.B > p.R + 20 && p.B > p.G + 15) / (double)rgb.Length;
			var gray = rgb.Count(p => Math.Max(p.R, Math.Max(p.G, p.B)) - Math.Min(p.R, Math.Min(p.G, p.B)) < 22
				&& Math.Max(p.R, Math.Max(p.G, p.B)) > 85) / (double)rgb.Length;
			if (blue >= 0.5)
				return "Water";

			return sheet != "field31" && gray >= 0.25 ? "Rock" : "Clear";
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

		static HashSet<(int X, int Y)> Reachable((string Sheet, int Frame)[,] keys, SortedDictionary<(string Sheet, int Frame), Template> templates,
			(int X, int Y) start, int width, int height)
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
						int nx = x + dx, ny = y + dy;
						if ((dx != 0 || dy != 0) && nx >= 0 && ny >= 0 && nx < width && ny < height && !seen.Contains((nx, ny))
							&& templates[keys[ny, nx]].TerrainType == "Clear")
						{
							seen.Add((nx, ny));
							queue.Enqueue((nx, ny));
						}
					}
			}

			return seen;
		}

		static (int Width, int Height, (int TileId, int Variant)[,] Cells) ReadMap(byte[] data)
		{
			const int Header = 52;
			int width = BitConverter.ToInt32(data, 0), height = BitConverter.ToInt32(data, 4);
			if (data.Length != Header + width * height * 4)
				throw new InvalidDataException("Unexpected .map size.");

			var cells = new (int, int)[height, width];
			for (var i = 0; i < width * height; i++)
				cells[i / width, i % width] = (data[Header + 4 * i], data[Header + 4 * i + 1]);

			return (width, height, cells);
		}

		// Player starts: the type 1 records of the .stg placement table (20-byte records from byte 3488, ending at the
		// first run of 10 empty records).
		static List<(int X, int Y)> ReadStarts(byte[] data)
		{
			var starts = new List<(int, int)>();
			var empty = 0;
			for (var offset = 3488; offset + 20 <= data.Length; offset += 20)
			{
				var values = Enumerable.Range(0, 5).Select(k => BitConverter.ToInt32(data, offset + 4 * k)).ToArray();
				if (values.All(v => v == 0))
				{
					if (++empty >= 10)
						break;

					continue;
				}

				empty = 0;
				if (values[2] == 1)
					starts.Add((values[0], values[1]));
			}

			return starts;
		}

		static void WriteMap(InstallContext c, string name, string title, (string Sheet, int Frame)[,] keys,
			SortedDictionary<(string Sheet, int Frame), Template> templates, Dictionary<(int X, int Y), string> crops, List<(int X, int Y)> starts)
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
						writer.Write((ushort)templates[inside ? keys[y - 1, x - 1] : BorderKey].Id);
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

			var preview = new byte[w * h * 4];
			for (var y = 0; y < h; y++)
				for (var x = 0; x < w; x++)
				{
					var hex = crops.TryGetValue((x, y), out var crop) ? CropPreviewColors[crop] : templates[keys[y, x]].Color;
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
				$"Author: OpenSY (converted from original SYW {name})", "", "Tileset: FIELD3", "",
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

			yaml.AddRange(new[] { "", "Rules: starting-hq.yaml" });
			c.WriteText($"maps/{name}/map.yaml", string.Join("\n", yaml) + "\n");
			c.WriteText($"maps/{name}/starting-hq.yaml",
				"world:\n\tStartingUnits@Korea:\n\t\tBaseActor: hq\n\tStartingUnits@Japan:\n\t\tBaseActor: jhq\n");
		}

		static string TilesetYaml(IEnumerable<Template> templates)
		{
			var lines = new List<string>
			{
				"General:", "\tName: SYW Grassland", "\tId: FIELD3",
				"\tEditorTemplateOrder: Land, Dirt, Field, Water, Bridge, Cliff, Rock, Border", "\tPalette:", "", "Terrain:"
			};
			foreach (var (name, target, color) in TerrainTypes)
				lines.AddRange(new[] { $"\tTerrainType@{name}:", $"\t\tType: {name}", $"\t\tTargetTypes: {target}", $"\t\tColor: {color}" });

			lines.AddRange(new[] { "", "Templates:" });
			foreach (var t in templates)
				lines.AddRange(new[]
				{
					$"\tTemplate@{t.Id}:", $"\t\tId: {t.Id}", $"\t\tImages: tilesets/field3/{t.Sheet}.png", $"\t\tFrames: {t.Frame}",
					"\t\tSize: 1,1", $"\t\tCategories: {t.Category}", "\t\tTiles:", $"\t\t\t0: {t.TerrainType}",
					$"\t\t\t\tMinColor: {t.Color}", $"\t\t\t\tMaxColor: {t.Color}", ""
				});

			return string.Join("\n", lines);
		}

		static void ExportCrops(InstallContext c, IndexedImage[] field33)
		{
			var yaml = new List<string>();
			foreach (var (crop, young, ripe) in Crops)
			{
				var frames = new List<IndexedImage>();
				for (var row = 0; row < 5; row++)
					for (var col = 0; col < 5; col++)
						foreach (var (blockRow, blockCol) in new[] { young, young, ripe, ripe })
							frames.Add(field33[(blockRow + row) * Columns + blockCol + col]);

				var rows = (frames.Count + Columns - 1) / Columns;
				c.WriteRgba($"art/resources/{crop}.png", Columns * Tile, rows * Tile, SheetRgba(frames, c.Knight), FrameData(frames.Count));

				yaml.AddRange(new[] { $"{crop}:", "\tDefaults:", $"\t\tFilename: art/resources/{crop}.png", "\t\tLength: 4" });
				for (var row = 0; row < 5; row++)
					for (var col = 0; col < 5; col++)
						yaml.AddRange(new[] { $"\tr{row}c{col}:", $"\t\tStart: {(row * 5 + col) * 4}" });

				yaml.Add("");
			}

			c.RepoYaml("sequences/resources.yaml", string.Join("\n", yaml));
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
