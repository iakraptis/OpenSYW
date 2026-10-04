using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using OpenRA.FileFormats;
using OpenRA.Graphics;

namespace OpenRA.Mods.Syw.AssetInstaller
{
	// Shared state for one install run: the game folder, the palettes, cached sprite sheets, and the output folder
	// (normally the player content folder, SywContent.Folder). Every file written goes through here so it lands in the
	// manifest.
	public sealed class InstallContext
	{
		public readonly GameFolder Game;
		public readonly string Output;

		// The mod folder in the repository, where the generated YAML lives (it is kept in git).
		public readonly string ModFolder;
		public readonly bool WriteYaml;
		public readonly SywPalette Knight;
		public readonly SortedDictionary<string, string> Written = new(StringComparer.Ordinal);
		public readonly List<string> Warnings = new();

		readonly Dictionary<string, SprFile> sprites = new(StringComparer.OrdinalIgnoreCase);

		public InstallContext(GameFolder game, string output, string modFolder, bool writeYaml)
		{
			Game = game;
			Output = Path.GetFullPath(output);
			ModFolder = Path.GetFullPath(modFolder);
			WriteYaml = writeYaml;
			Knight = new SywPalette(game.Read("FNT1/Knight.pal"));
		}

		// YAML the mod loads, generated from game data but kept in git. Normally only checked against the repository copy;
		// with --write-yaml it is written there.
		public void RepoYaml(string relative, string text)
		{
			var path = Path.Combine(ModFolder, relative.Replace('/', Path.DirectorySeparatorChar));
			var current = File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : null;
			if (current == text.Replace("\r\n", "\n"))
				return;

			if (WriteYaml)
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				File.WriteAllText(path, text);
				Console.WriteLine($"  updated {relative}");
			}
			else
				Warnings.Add($"{relative} differs from what the game data gives (run with --write-yaml to update it)");
		}

		// FNT1/<name>.SPR decoded with Knight.pal, cached for the whole run.
		public SprFile Spr(string name)
		{
			if (!sprites.TryGetValue(name, out var spr))
			{
				var file = name.Contains('.') ? name : name + ".SPR";
				sprites[name] = spr = new SprFile(Game.Read("FNT1/" + file), Knight, file);
			}

			return spr;
		}

		public string OutputPath(string relative)
		{
			var path = Path.Combine(Output, relative.Replace('/', Path.DirectorySeparatorChar));
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			return path;
		}

		// An indexed PNG with Knight.pal (or another palette); frameSize/frameAmount become the PngSheet metadata.
		public void WritePng(string relative, IndexedImage image, (int W, int H)? frameSize = null, int frameAmount = 0,
			SywPalette palette = null)
		{
			var embedded = new Dictionary<string, string>();
			if (frameSize.HasValue)
			{
				embedded["FrameSize"] = $"{frameSize.Value.W},{frameSize.Value.H}";
				embedded["FrameAmount"] = frameAmount.ToStringInvariant();
			}

			var png = new Png(image.Pixels, SpriteFrameType.Indexed8, image.Width, image.Height, (palette ?? Knight).ForPng(), embedded);
			WriteBytes(relative, png.Save());
		}

		// Frames side by side, with FrameSize/FrameAmount set: the usual sprite sheet layout.
		public void WriteStrip(string relative, IReadOnlyList<IndexedImage> frames, SywPalette palette = null)
		{
			WritePng(relative, IndexedImage.Strip(frames), (frames[0].Width, frames[0].Height), frames.Count, palette);
		}

		// Frames side by side as an RGBA PNG: palette colours, index 0 fully transparent.
		public void WriteRgbaStrip(string relative, IReadOnlyList<IndexedImage> frames, SywPalette palette = null)
		{
			var strip = IndexedImage.Strip(frames);
			var embedded = new Dictionary<string, string>
			{
				["FrameSize"] = $"{frames[0].Width},{frames[0].Height}",
				["FrameAmount"] = frames.Count.ToStringInvariant()
			};
			WriteRgba(relative, strip.Width, strip.Height, ToRgba(strip, palette ?? Knight), embedded);
		}

		public static byte[] ToRgba(IndexedImage image, SywPalette palette)
		{
			var rgba = new byte[image.Pixels.Length * 4];
			for (var i = 0; i < image.Pixels.Length; i++)
			{
				var p = image.Pixels[i];
				var color = palette.Colors[p];
				rgba[4 * i] = color.R;
				rgba[4 * i + 1] = color.G;
				rgba[4 * i + 2] = color.B;
				rgba[4 * i + 3] = p == 0 ? (byte)0 : (byte)255;
			}

			return rgba;
		}

		public void WriteRgba(string relative, int width, int height, byte[] rgba, Dictionary<string, string> embedded = null)
		{
			var png = new Png(rgba, SpriteFrameType.Rgba32, width, height, null, embedded);
			WriteBytes(relative, png.Save());
		}

		public void WriteBytes(string relative, byte[] data)
		{
			File.WriteAllBytes(OutputPath(relative), data);
			Written[relative] = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
		}

		public void WriteText(string relative, string text) => WriteBytes(relative, System.Text.Encoding.UTF8.GetBytes(text));

		public void CopyGameFile(string gameRelative, string relative) => WriteBytes(relative, Game.Read(gameRelative));
	}
}
