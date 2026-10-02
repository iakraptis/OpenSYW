using System;
using System.Linq;
using OpenRA.Primitives;

namespace OpenRA.Mods.Syw.AssetInstaller
{
	// A 256-colour game palette (Knight.pal, TITLE.PAL). The files store 6-bit components (0-63), scaled here to 8 bits
	// with round(c * 255 / 63), as SYWtoORA's pal_extract.py did.
	public sealed class SywPalette
	{
		public readonly Color[] Colors;

		public SywPalette(byte[] data)
		{
			if (data.Length < 768)
				throw new ArgumentException("A palette needs 768 bytes.");

			var sixBit = data.Take(768).All(b => b <= 63);
			Colors = new Color[256];
			for (var i = 0; i < 256; i++)
				Colors[i] = Color.FromArgb(Scale(data[3 * i], sixBit), Scale(data[3 * i + 1], sixBit), Scale(data[3 * i + 2], sixBit));
		}

		static byte Scale(byte c, bool sixBit) => sixBit ? (byte)Math.Min(255, (int)Math.Round(c * 255.0 / 63)) : c;

		// The first index after 0 that is pure black, for opaque black pixels (index 0 means transparent in the PNGs).
		public int FirstBlackAfterZero()
		{
			for (var i = 1; i < 256; i++)
				if (Colors[i].R == 0 && Colors[i].G == 0 && Colors[i].B == 0)
					return i;

			return -1;
		}

		// The palette as written into indexed PNGs: index 0 transparent, the rest opaque.
		public Color[] ForPng()
		{
			var colors = (Color[])Colors.Clone();
			colors[0] = Color.FromArgb(0, colors[0].R, colors[0].G, colors[0].B);
			return colors;
		}
	}
}
