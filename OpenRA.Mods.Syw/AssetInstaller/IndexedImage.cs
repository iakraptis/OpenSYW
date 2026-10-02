using System;
using System.Collections.Generic;

namespace OpenRA.Mods.Syw.AssetInstaller
{
	// An 8-bit palette-indexed image; index 0 is transparent. The operations copy Pillow's behaviour, which the original
	// SYWtoORA exporters used: Paste copies every pixel (transparent ones included), Bounds is the box of non-zero pixels.
	public sealed class IndexedImage
	{
		public readonly int Width;
		public readonly int Height;
		public readonly byte[] Pixels;

		public IndexedImage(int width, int height, byte[] pixels = null)
		{
			Width = width;
			Height = height;
			Pixels = pixels ?? new byte[width * height];
			if (Pixels.Length != width * height)
				throw new ArgumentException("Pixel count does not match the size.");
		}

		public IndexedImage Clone() => new(Width, Height, (byte[])Pixels.Clone());

		public byte this[int x, int y]
		{
			get => Pixels[y * Width + x];
			set => Pixels[y * Width + x] = value;
		}

		public IndexedImage MirrorX()
		{
			var result = new IndexedImage(Width, Height);
			for (var y = 0; y < Height; y++)
				for (var x = 0; x < Width; x++)
					result[Width - 1 - x, y] = this[x, y];

			return result;
		}

		// Areas outside the source come out transparent, as with Pillow's crop.
		public IndexedImage Crop(int left, int top, int width, int height)
		{
			var result = new IndexedImage(width, height);
			for (var y = 0; y < height; y++)
				for (var x = 0; x < width; x++)
				{
					int sx = left + x, sy = top + y;
					if (sx >= 0 && sy >= 0 && sx < Width && sy < Height)
						result[x, y] = this[sx, sy];
				}

			return result;
		}

		// Copies every source pixel, transparent ones included; parts outside this image are clipped.
		public void Paste(IndexedImage source, int left, int top)
		{
			for (var y = 0; y < source.Height; y++)
				for (var x = 0; x < source.Width; x++)
				{
					int dx = left + x, dy = top + y;
					if (dx >= 0 && dy >= 0 && dx < Width && dy < Height)
						this[dx, dy] = source[x, y];
				}
		}

		// Copies only the non-transparent source pixels.
		public void PasteOver(IndexedImage source, int left, int top)
		{
			for (var y = 0; y < source.Height; y++)
				for (var x = 0; x < source.Width; x++)
				{
					int dx = left + x, dy = top + y;
					var p = source[x, y];
					if (p != 0 && dx >= 0 && dy >= 0 && dx < Width && dy < Height)
						this[dx, dy] = p;
				}
		}

		// (left, top, right, bottom) of the non-zero pixels, right/bottom exclusive; null when the image is empty.
		public (int Left, int Top, int Right, int Bottom)? Bounds()
		{
			int left = Width, top = Height, right = -1, bottom = -1;
			for (var y = 0; y < Height; y++)
				for (var x = 0; x < Width; x++)
					if (this[x, y] != 0)
					{
						left = Math.Min(left, x);
						right = Math.Max(right, x);
						top = Math.Min(top, y);
						bottom = Math.Max(bottom, y);
					}

			return right < 0 ? null : (left, top, right + 1, bottom + 1);
		}

		// Frames side by side in one row.
		public static IndexedImage Strip(IReadOnlyList<IndexedImage> frames)
		{
			var w = frames[0].Width;
			var h = frames[0].Height;
			var sheet = new IndexedImage(w * frames.Count, h);
			for (var i = 0; i < frames.Count; i++)
				sheet.Paste(frames[i], i * w, 0);

			return sheet;
		}

		// Tiles of `rows x cols` frames stitched into one image, row-major, each tile at (col * w, row * h).
		public static IndexedImage Grid(IndexedImage[,] tiles)
		{
			var rows = tiles.GetLength(0);
			var cols = tiles.GetLength(1);
			var w = tiles[0, 0].Width;
			var h = tiles[0, 0].Height;
			var result = new IndexedImage(w * cols, h * rows);
			for (var r = 0; r < rows; r++)
				for (var c = 0; c < cols; c++)
					result.Paste(tiles[r, c], c * w, r * h);

			return result;
		}
	}
}
