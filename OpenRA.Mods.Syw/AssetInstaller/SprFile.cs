using System;
using System.Collections.Generic;
using System.IO;

namespace OpenRA.Mods.Syw.AssetInstaller
{
	// An original .spr sprite sheet (format: SYWtoORA/docs/formats/spr.md).
	//   header: 4 x uint32 (group count, frame width, frame height, reserved), then `reserved` zero uint32s;
	//   a non-decreasing uint32 frame offset table (it ends at the first value that drops or passes the file end);
	//   pixel data from byte 3060: per frame, 0x01 N = N transparent pixels, any other byte = one palette index.
	// A literal 0x00 is opaque black, not transparency. Index 0 stays reserved for transparency in the output, so when a
	// file has opaque black pixels they are moved to the palette's first other black index (the whole file uses the
	// same one, as spr_extract.py did).
	public sealed class SprFile
	{
		const int PixelDataOffset = 3060;
		const byte SkipMarker = 0x01;

		public readonly int Width;
		public readonly int Height;
		public int FrameCount => offsets.Count;

		readonly byte[] data;
		readonly List<int> offsets = new();
		readonly IndexedImage[] frames;

		public SprFile(byte[] data, SywPalette palette, string name)
		{
			this.data = data;
			Width = BitConverter.ToInt32(data, 4);
			Height = BitConverter.ToInt32(data, 8);
			var reserved = BitConverter.ToInt32(data, 12);

			var last = -1;
			for (var i = 16 + reserved * 4; i + 4 <= data.Length; i += 4)
			{
				var value = BitConverter.ToInt32(data, i);
				if (value < last || value >= data.Length)
					break;

				offsets.Add(value);
				last = value;
			}

			frames = new IndexedImage[offsets.Count];
			var opaque = new bool[offsets.Count][];
			var anyOpaqueZero = false;
			for (var f = 0; f < offsets.Count; f++)
			{
				(frames[f], opaque[f]) = Decode(f);
				for (var p = 0; p < frames[f].Pixels.Length && !anyOpaqueZero; p++)
					anyOpaqueZero = frames[f].Pixels[p] == 0 && opaque[f][p];
			}

			if (!anyOpaqueZero)
				return;

			var black = palette.FirstBlackAfterZero();
			if (black < 0)
				throw new InvalidDataException($"{name}: has opaque black pixels but the palette has no second black index.");

			for (var f = 0; f < frames.Length; f++)
				for (var p = 0; p < frames[f].Pixels.Length; p++)
					if (frames[f].Pixels[p] == 0 && opaque[f][p])
						frames[f].Pixels[p] = (byte)black;
		}

		(IndexedImage Image, bool[] Opaque) Decode(int index)
		{
			var start = PixelDataOffset + offsets[index];
			var end = index + 1 < offsets.Count ? PixelDataOffset + offsets[index + 1] : data.Length;
			var target = Width * Height;
			var pixels = new byte[target];
			var opaque = new bool[target];
			var o = 0;
			for (var i = start; i < end && o < target;)
			{
				var v = data[i];
				if (v == SkipMarker && i + 1 < end)
				{
					o += data[i + 1];
					i += 2;
				}
				else
				{
					pixels[o] = v;
					opaque[o] = true;
					o++;
					i++;
				}
			}

			return (new IndexedImage(Width, Height, pixels), opaque);
		}

		// A copy of one frame, safe to modify.
		public IndexedImage Frame(int index)
		{
			if (index < 0 || index >= frames.Length)
				throw new ArgumentOutOfRangeException(nameof(index), $"Frame {index} is outside 0-{frames.Length - 1}.");

			return frames[index].Clone();
		}
	}
}
