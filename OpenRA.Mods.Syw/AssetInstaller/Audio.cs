using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenRA.Mods.Syw.AssetInstaller
{
	// The sound effects and voices in effect/*.wav, copied as they are (plain PCM WAV, which the engine plays directly) into
	// audio/ under lower-case names, so the YAML can name them the same way on every file system. The menu theme goes to
	// music/ with the "copies" stage instead.
	public static class Audio
	{
		static readonly HashSet<string> Music = new(StringComparer.OrdinalIgnoreCase) { "menumus.wav" };

		public static void Export(InstallContext c)
		{
			var stubs = new List<string>();
			foreach (var file in c.Game.Files("effect").Where(f => f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)))
			{
				var name = Path.GetFileName(file).ToLowerInvariant();
				if (Music.Contains(name))
					continue;

				var data = c.Game.Read(file);
				if (IsSilentStub(data))
				{
					stubs.Add(Path.GetFileNameWithoutExtension(name));
					continue;
				}

				c.WriteBytes("audio/" + name, data);
			}

			// Some releases of the game ship placeholders (a few bytes of silence) instead of the voice-overs. Skipping them
			// keeps the engine from playing nothing; a complete copy of the game installs them on the next run.
			if (stubs.Count > 0)
				c.Warnings.Add($"{stubs.Count} sounds in effect/ are silent placeholders in this copy of the game and were " +
					$"skipped: {string.Join(", ", stubs.OrderBy(s => s, StringComparer.Ordinal))}");
		}

		// A PCM WAV whose samples are all silence (0x80 for 8-bit, 0 for 16-bit). Anything that isn't plain PCM counts as real.
		static bool IsSilentStub(byte[] wav)
		{
			if (wav.Length < 44 || BitConverter.ToUInt32(wav, 0) != 0x46464952 || BitConverter.ToUInt16(wav, 20) != 1)
				return false;

			var bits = BitConverter.ToUInt16(wav, 34);
			var data = wav.AsSpan().IndexOf(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
			if (data < 0 || data + 8 > wav.Length)
				return false;

			var length = (int)Math.Min(BitConverter.ToUInt32(wav, data + 4), (uint)(wav.Length - data - 8));
			var silence = bits == 8 ? (byte)0x80 : (byte)0;
			for (var i = data + 8; i < data + 8 + length; i++)
				if (wav[i] != silence)
					return false;

			return true;
		}
	}
}
