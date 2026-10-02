using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace OpenRA.Mods.Syw.AssetInstaller
{
	// Menu and interface art: the title screen and sidebar (FST screens), the main menu fire loop (ani.anm) and the
	// mouse cursors (syw.exe resources). Chrome images are uploaded whole as textures, so they are padded to
	// power-of-two sizes.
	public static class Chrome
	{
		// Sidebar: the FST Pannel13 column (160x768), cut into the emblem and stat boxes (rows 0-297, kept), the plain
		// sandstone middle (298-671, repeated with every other copy flipped so the joins don't show) and the rocks
		// (672-, kept), lengthened to SidebarHeight rows and doubled. chrome.yaml `sidebar-panel` slices it.
		const int SidebarBoxesEnd = 298;
		const int SidebarRocksStart = 672;
		const int SidebarHeight = 1440;

		// Main menu fire: ani.anm frames (640x400, TITLE.PAL), the flat background (index 1) transparent, rows 146- kept
		// (the rows above hold under 0.4% of the flame pixels), packed 2 pixels apart into 2048-wide sheets.
		const int FireCropTop = 146;
		const int FireBackground = 1;
		const int FireGutter = 2;
		const int FireSheet = 2048;

		const int CursorSize = 32;

		// OpenRA cursor -> (syw.exe cursor group, struck out). Groups 115-167:
		//   115 hand pointer, 122 crossed swords, 140 selection brackets, 144 pitchfork, 146 cross in brackets,
		//   167 crossed swords struck out; scroll arrows 130 N, 132 NE, 134 E, 136 SE, 117 S, 124 SW, 126 W, 128 NW and
		//   the same struck out: 159, 161, 163, 165, 149, 152, 155, 157.
		// Every name the engine or the rules can ask for is listed (a missing one leaves the pointer invisible). The hand
		// stays the pointer with units selected (user decision). "-blocked" cursors SYW lacks get a struck-out copy.
		static readonly (string Name, int Group, bool StruckOut)[] Cursors = new (string, int, bool)[]
		{
			("default", 115, false), ("generic-blocked", 115, true), ("sell", 115, false), ("sell-blocked", 115, true),
			("select", 140, false), ("joystick-all", 140, false),
			("move", 115, false), ("move-blocked", 115, true), ("enter", 115, false), ("enter-blocked", 115, true),
			("capture", 115, false), ("capture-blocked", 115, true),
			("attack", 122, false), ("attackoutsiderange", 122, false), ("attackmove", 122, false),
			("attackmove-blocked", 167, false), ("assaultmove", 122, false), ("assaultmove-blocked", 167, false),
			("c4", 122, false), ("harvest", 144, false),
			("deploy", 146, false), ("deploy-blocked", 146, true), ("heal", 146, false), ("ability", 146, false),
			("repair", 146, false), ("repair-blocked", 146, true), ("goldwrench", 146, false), ("goldwrench-blocked", 146, true),
		}.Concat(new (string Dir, int Free, int Blocked)[]
		{
			("t", 130, 159), ("tr", 132, 161), ("r", 134, 163), ("br", 136, 165),
			("b", 117, 149), ("bl", 124, 152), ("l", 126, 155), ("tl", 128, 157),
		}.SelectMany(s => new[]
		{
			($"scroll-{s.Dir}", s.Free, false), ($"scroll-{s.Dir}-blocked", s.Blocked, false), ($"joystick-{s.Dir}-blocked", s.Blocked, false)
		})).ToArray();

		public static void Export(InstallContext c)
		{
			var title = new SywPalette(c.Game.Read("FNT1/TITLE.PAL"));

			// Title screen: FST Title (640x480) with TITLE.PAL, top-left in a 1024x512 texture.
			var titleImage = Fst(c.Game.Read("fst/Title.fst"));
			int titleW = Pow2(titleImage.Width), titleH = Pow2(titleImage.Height);
			c.WriteRgba("chrome/assets/title.png", titleW, titleH, Opaque(titleImage, title, titleW, titleH));

			var panel = Fst(c.Game.Read("fst/Pannel13.fst"));
			var sidebar = Sidebar(panel, SidebarHeight);
			var doubled = Effects.ScaleNearest(sidebar, sidebar.Width * 2, sidebar.Height * 2);
			c.WriteRgba("chrome/assets/sidebar.png", Pow2(doubled.Width), Pow2(doubled.Height), Opaque(doubled, c.Knight, Pow2(doubled.Width), Pow2(doubled.Height)));

			MenuFire(c, title);
			ExportCursors(c);
		}

		static int Pow2(int n) => 1 << (int)Math.Ceiling(Math.Log2(n));

		// An FST screen: 48-byte header (1, width, height, 13, ...), then PCX-style RLE to the end.
		static IndexedImage Fst(byte[] data)
		{
			int width = BitConverter.ToInt32(data, 4), height = BitConverter.ToInt32(data, 8);
			return new IndexedImage(width, height, Rle(data, 48, data.Length, width * height));
		}

		// PCX-style RLE (FST and ANM): a byte >= 0xC0 repeats the next byte (byte & 0x3F) times; others are literals.
		static byte[] Rle(byte[] data, int start, int end, int target)
		{
			var pixels = new byte[target];
			var o = 0;
			for (var i = start; i < end && o < target;)
			{
				var b = data[i++];
				if (b >= 0xC0)
				{
					if (i >= end)
						break;

					var value = data[i++];
					for (var k = 0; k < (b & 0x3F) && o < target; k++)
						pixels[o++] = value;
				}
				else
					pixels[o++] = b;
			}

			return pixels;
		}

		// Every pixel opaque (FST screens have no transparency), placed top-left on a transparent canvas.
		static byte[] Opaque(IndexedImage image, SywPalette palette, int width, int height)
		{
			var rgba = new byte[width * height * 4];
			for (var y = 0; y < image.Height; y++)
				for (var x = 0; x < image.Width; x++)
				{
					var color = palette.Colors[image[x, y]];
					var i = 4 * (y * width + x);
					rgba[i] = color.R;
					rgba[i + 1] = color.G;
					rgba[i + 2] = color.B;
					rgba[i + 3] = 255;
				}

			return rgba;
		}

		static IndexedImage Sidebar(IndexedImage panel, int height)
		{
			var top = panel.Crop(0, 0, panel.Width, SidebarBoxesEnd);
			var middle = panel.Crop(0, SidebarBoxesEnd, panel.Width, SidebarRocksStart - SidebarBoxesEnd);
			var bottom = panel.Crop(0, SidebarRocksStart, panel.Width, panel.Height - SidebarRocksStart);
			var flipped = new IndexedImage(middle.Width, middle.Height);
			for (var y = 0; y < middle.Height; y++)
				for (var x = 0; x < middle.Width; x++)
					flipped[x, middle.Height - 1 - y] = middle[x, y];

			var middleHeight = height - top.Height - bottom.Height;
			var lengthened = new IndexedImage(panel.Width, middleHeight);
			for (int y = 0, i = 0; y < middleHeight; y += middle.Height, i++)
				lengthened.Paste(i % 2 == 1 ? flipped : middle, 0, y);

			var result = new IndexedImage(panel.Width, height);
			result.Paste(top, 0, 0);
			result.Paste(lengthened, 0, top.Height);
			result.Paste(bottom, 0, height - bottom.Height);
			return result;
		}

		// ani.anm: 4 x uint32 header (frames, width, height, data size), then (offset, size) per frame relative to the
		// data after the table; each frame is PCX-style RLE.
		static void MenuFire(InstallContext c, SywPalette palette)
		{
			var data = c.Game.Read("Ani/ani.anm");
			int count = BitConverter.ToInt32(data, 0), width = BitConverter.ToInt32(data, 4), height = BitConverter.ToInt32(data, 8);
			var pixelBase = 16 + count * 8;
			var cropped = height - FireCropTop;
			int cellW = width + FireGutter, cellH = cropped + FireGutter;
			int columns = FireSheet / cellW, perSheet = columns * (FireSheet / cellH);

			var frames = new List<IndexedImage>();
			for (var f = 0; f < count; f++)
			{
				int offset = BitConverter.ToInt32(data, 16 + 8 * f), size = BitConverter.ToInt32(data, 20 + 8 * f);
				var full = new IndexedImage(width, height, Rle(data, pixelBase + offset, pixelBase + offset + size, width * height));
				frames.Add(full.Crop(0, FireCropTop, width, cropped));
			}

			var hash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant()[..16];
			var yaml = new List<string>
			{
				$"# Generated by --install-assets (AssetInstaller/Chrome.cs) from ani.anm (sha256 {hash}...).",
				$"# {count} frames of {width}x{cropped}, {perSheet} per sheet."
			};
			for (var s = 0; s * perSheet < frames.Count; s++)
			{
				var chunk = frames.Skip(s * perSheet).Take(perSheet).ToList();
				var sheetHeight = Pow2((chunk.Count + columns - 1) / columns * cellH);
				var rgba = new byte[FireSheet * sheetHeight * 4];
				yaml.AddRange(new[] { "", $"menu-fire-{s}:", $"\tImage: chrome/assets/menu-fire-{s}.png", "\tRegions:" });
				for (var j = 0; j < chunk.Count; j++)
				{
					int left = j % columns * cellW + FireGutter / 2, top = j / columns * cellH + FireGutter / 2;
					for (var y = 0; y < cropped; y++)
						for (var x = 0; x < width; x++)
						{
							var p = chunk[j][x, y];
							if (p == FireBackground)
								continue;

							var color = palette.Colors[p];
							var i = 4 * ((top + y) * FireSheet + left + x);
							rgba[i] = color.R;
							rgba[i + 1] = color.G;
							rgba[i + 2] = color.B;
							rgba[i + 3] = 255;
						}

					yaml.Add($"\t\t{s * perSheet + j}: {left}, {top}, {width}, {cropped}");
				}

				c.WriteRgba($"chrome/assets/menu-fire-{s}.png", FireSheet, sheetHeight, rgba);
			}

			c.RepoYaml("chrome/menu-fire.yaml", string.Join("\n", yaml) + "\n");
		}

		static void ExportCursors(InstallContext c)
		{
			var exe = c.Game.Read("syw.exe");
			var resources = PeResources(exe);
			var images = resources[1];
			var groups = resources[12];

			var frames = new List<byte[]>();
			var hotspots = new List<(int X, int Y)>();
			var index = new Dictionary<(int, bool), int>();
			var yaml = new List<string>
			{
				"# Generated by --install-assets (AssetInstaller/Chrome.cs) from the cursors in syw.exe; edit the table there.",
				"Cursors:", "\tcursors/assets/cursors.png:"
			};

			foreach (var (name, group, struckOut) in Cursors)
			{
				if (!index.TryGetValue((group, struckOut), out var frame))
				{
					var imageId = BitConverter.ToUInt16(exe, groups[group] + 6 + 12);   // the group's first (only) image
					var (rgba, hotspot) = DecodeCursor(exe, images[imageId]);
					index[(group, struckOut)] = frame = frames.Count;
					frames.Add(struckOut ? StruckOut(rgba) : rgba);
					hotspots.Add(hotspot);
				}

				// OpenRA measures the hotspot from the frame centre.
				var (hx, hy) = hotspots[frame];
				yaml.AddRange(new[] { $"\t\t{name}:", $"\t\t\tStart: {frame}", $"\t\t\tX: {hx - CursorSize / 2}", $"\t\t\tY: {hy - CursorSize / 2}" });
			}

			var sheet = new byte[CursorSize * frames.Count * CursorSize * 4];
			for (var f = 0; f < frames.Count; f++)
				for (var y = 0; y < CursorSize; y++)
					Array.Copy(frames[f], y * CursorSize * 4, sheet, (y * CursorSize * frames.Count + f * CursorSize) * 4, CursorSize * 4);

			c.WriteRgba("cursors/assets/cursors.png", CursorSize * frames.Count, CursorSize, sheet, new Dictionary<string, string>
			{
				["FrameSize"] = $"{CursorSize},{CursorSize}",
				["FrameAmount"] = frames.Count.ToStringInvariant()
			});
			c.RepoYaml("cursors/default.yaml", string.Join("\n", yaml) + "\n");
		}

		// A cursor resource: hotspot (2 x uint16), then a 1-bit DIB whose AND mask follows the colour bitmap (bottom-up).
		static (byte[] Rgba, (int X, int Y) Hotspot) DecodeCursor(byte[] data, int offset)
		{
			int hx = BitConverter.ToUInt16(data, offset), hy = BitConverter.ToUInt16(data, offset + 2);
			var header = offset + 4;
			int size = BitConverter.ToInt32(data, header), width = BitConverter.ToInt32(data, header + 4);
			var height = BitConverter.ToInt32(data, header + 8) / 2;
			if (BitConverter.ToUInt16(data, header + 14) != 1)
				throw new InvalidOperationException("Expected monochrome cursors in syw.exe.");

			var colours = BitConverter.ToInt32(data, header + 32);
			colours = colours == 0 ? 2 : colours;
			var pixels = header + size + colours * 4;
			var stride = (width + 31) / 32 * 4;
			var mask = pixels + stride * height;
			var rgba = new byte[width * height * 4];
			for (var y = 0; y < height; y++)
				for (var x = 0; x < width; x++)
				{
					var bit = 7 - x % 8;
					if (((data[mask + (height - 1 - y) * stride + x / 8] >> bit) & 1) == 1)
						continue;   // transparent (SYW's cursors use no screen-inverting pixels)

					var entry = header + size + 4 * ((data[pixels + (height - 1 - y) * stride + x / 8] >> bit) & 1);
					var i = 4 * (y * width + x);
					rgba[i] = data[entry + 2];
					rgba[i + 1] = data[entry + 1];
					rgba[i + 2] = data[entry];
					rgba[i + 3] = 255;
				}

			return (rgba, (hx, hy));
		}

		// A 13x13 X of 3-pixel white strokes with a black outline, in the bottom-right corner, matching SYW's style.
		static byte[] StruckOut(byte[] rgba)
		{
			var result = (byte[])rgba.Clone();
			static bool White(int x, int y) => x >= 1 && x <= 11 && y >= 1 && y <= 11 && (Math.Abs(x - y) <= 1 || Math.Abs(x + y - 12) <= 1);
			const int Left = CursorSize - 13;
			for (var y = 0; y < 13; y++)
				for (var x = 0; x < 13; x++)
				{
					byte? value = White(x, y) ? 255
						: Enumerable.Range(-1, 3).Any(dx => Enumerable.Range(-1, 3).Any(dy => White(x + dx, y + dy))) ? 0 : null;
					if (value == null)
						continue;

					var i = 4 * ((Left + y) * CursorSize + Left + x);
					result[i] = result[i + 1] = result[i + 2] = value.Value;
					result[i + 3] = 255;
				}

			return result;
		}

		// A PE file's resources: type id -> resource id -> file offset of the data (first language only).
		static Dictionary<int, Dictionary<int, int>> PeResources(byte[] data)
		{
			var pe = BitConverter.ToInt32(data, 0x3C);
			var sectionCount = BitConverter.ToUInt16(data, pe + 6);
			var optionalSize = BitConverter.ToUInt16(data, pe + 20);
			var rva = BitConverter.ToInt32(data, pe + 24 + 96 + 2 * 8);
			int va = 0, raw = 0;
			for (var s = 0; s < sectionCount; s++)
			{
				var o = pe + 24 + optionalSize + s * 40;
				int size = BitConverter.ToInt32(data, o + 8), address = BitConverter.ToInt32(data, o + 12);
				if (address <= rva && rva < address + size)
					(va, raw) = (address, BitConverter.ToInt32(data, o + 20));
			}

			var baseOffset = raw + rva - va;
			IEnumerable<(int Name, int Offset)> Entries(int offset)
			{
				var count = BitConverter.ToUInt16(data, offset + 12) + BitConverter.ToUInt16(data, offset + 14);
				for (var i = 0; i < count; i++)
					yield return (BitConverter.ToInt32(data, offset + 16 + 8 * i), BitConverter.ToInt32(data, offset + 20 + 8 * i));
			}

			int Leaf(int offset)
			{
				while ((offset & 0x80000000) != 0)
					offset = Entries(baseOffset + (offset & 0x7FFFFFFF)).First().Offset;

				return raw + BitConverter.ToInt32(data, baseOffset + offset) - va;
			}

			return Entries(baseOffset).ToDictionary(t => t.Name,
				t => Entries(baseOffset + (t.Offset & 0x7FFFFFFF)).ToDictionary(e => e.Name, e => Leaf(e.Offset)));
		}
	}
}
