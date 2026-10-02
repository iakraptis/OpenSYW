using System.Linq;

namespace OpenRA.Mods.Syw.AssetInstaller
{
	// Effects, projectiles, training and spell buttons, and portraits. Frame numbers were identified with the user.
	public static class Effects
	{
		const int BulletSize = 16;

		// Indexed animation strips: (output, sprite, frames); each frame is a stack of SPR frames, top to bottom.
		static readonly (string Path, string Sprite, int[][] Frames)[] Strips =
		{
			// Beacon Mound chimney smoke: 8 frames of 32x64 (MOUSE 0-7 over 20-27).
			("art/effects/buildings/chimney.png", "MOUSE", Enumerable.Range(0, 8).Select(i => new[] { i, i + 20 }).ToArray()),
			("art/effects/buildings/flag.png", "MOUSE", Singles(80, 3)),          // Barracks tent flags
			("art/effects/buildings/jflag.png", "MOUSE", Singles(84, 4)),         // Japanese Barracks flags
			("art/effects/chest.png", "MOUSE", Singles(50, 1)),                   // dropped by a loaded Peasant
			("art/effects/rally.png", "TREE", Singles(264, 4)),                   // rally point banner
			("art/effects/bomb.png", "MOUSE", Singles(63, 1)),                    // the Bomber's bomb
			("art/effects/damage-smoke.png", "FIRE", Singles(130, 8)),            // below 50% health
			("art/effects/earthquake.png", "FIRE96", Singles(0, 8)),              // the Witch's Earthquake
			("art/effects/missile-trail/trail.png", "MOUSE", Singles(180, 3)),    // smoke puffs, small to large
		};

		// EXP.SPR explosions (formats/exp.md), RGBA: frames of size x size tiles running left to right along a band of
		// tile rows (20 frames apart), continuing in the next band. (output, (first tile, frames) per band, size).
		static readonly (string Path, (int Top, int Count)[] Bands, int Size)[] Explosions =
		{
			("art/effects/cannon-explosion.png", new[] { (0, 10), (40, 5) }, 2),
			("art/effects/vehicle-explosion.png", new[] { (80, 10), (120, 6) }, 2),
			("art/effects/building-explosion.png", new[] { (160, 10), (200, 3) }, 2),
			("art/effects/cannon-impact.png", new[] { (206, 13) }, 1),
			("art/effects/rocket-impact.png", new[] { (226, 13) }, 1),
		};

		// Unit training buttons (FIRE.SPR, 32x32), scaled 1.5x to fill the 64x48 palette slots.
		static readonly (string Name, int Frame)[] UnitIcons =
		{
			("peasant", 212), ("archer", 214), ("gunner", 216), ("footman", 218), ("firecar", 220),
			("cannonship", 222), ("transportship", 224), ("patrolship", 226), ("occupier", 229), ("miner", 231),
			("transporter", 232), ("fighter", 235), ("monk", 237), ("shaman", 239), ("cannon", 241),
			("commander", 242), ("bull", 244),
			("jpeasant", 213), ("jarcher", 215), ("jgunner", 217), ("jfootman", 219), ("armoredcar", 221),
			("jcannonship", 223), ("submarine", 225), ("arrowship", 227), ("torpedo", 228), ("thief", 230),
			("jtransporter", 233), ("bomber", 234), ("jfighter", 236), ("priest", 238), ("witch", 240),
			("general", 243), ("jbull", 245),
		};

		// Spell buttons (FIRE.SPR, 32x32; the icons syw.exe uses for each command).
		static readonly (string Name, int Frame)[] SpellIcons =
		{
			("heal", 195), ("lightning", 197), ("minefield", 202), ("mine", 276), ("transform", 199),
			("massheal", 196), ("repair", 188), ("sell", 189), ("disturb", 191), ("earthquake", 198),
			("bewilder", 200), ("detectmines", 205),
		};

		// PORTRAIT.SPR frames. Japanese buildings are the Korean frame + 40, Japanese units sit at 50-69.
		static readonly (string Name, int Frame)[] BuildingPortraits =
		{
			("hq", 30), ("barracks", 31), ("mill", 33), ("arrowtower", 34), ("heavyarmsworkshop", 35),
			("shipyard", 36), ("barracks2", 37), ("planeworks", 38), ("temple", 39), ("beaconmound", 40),
			("stable", 41), ("shamanhouse", 42), ("cannontower", 43),
			("jhq", 70), ("jbarracks", 71), ("jmill", 73), ("jarrowtower", 74), ("jheavyarms", 75), ("jshipyard", 76),
			("jbarracks2", 77), ("airport", 78), ("jtemple", 79), ("jbeacon", 80), ("jstable", 81), ("witchhouse", 82),
			("jcannontower", 83),
		};

		static readonly (string Name, int Frame)[] UnitPortraits =
		{
			("peasant", 0), ("archer", 1), ("gunner", 2), ("firecar", 3), ("cannonship", 4), ("transportship", 5),
			("patrolship", 6), ("footman", 7), ("occupier", 8), ("miner", 9), ("transporter", 10), ("fighter", 11),
			("monk", 12), ("shaman", 13), ("cannon", 14), ("commander", 15), ("bull", 16), ("syw-mine", 22),
			("syw-mine-building", 22),
			("jpeasant", 50), ("jarcher", 51), ("jgunner", 52), ("armoredcar", 53), ("jcannonship", 54),
			("submarine", 55), ("arrowship", 56), ("jfootman", 57), ("thief", 58), ("jtransporter", 61),
			("jfighter", 62), ("bomber", 63), ("priest", 64), ("witch", 65), ("torpedo", 66), ("general", 67),
			("jbull", 68), ("jcannon", 14),
		};

		// Musket and gun bullet: FIRE 140-148 sweep S -> W -> N (user-identified 140 S, 147 NNW), in OpenRA order N..S.
		static readonly int[] BulletWest = { 148, 147, 146, 145, 144, 143, 142, 141, 140 };

		public static void Export(InstallContext c)
		{
			foreach (var (path, sprite, frames) in Strips)
			{
				var spr = c.Spr(sprite);
				c.WriteStrip(path, frames.Select(stack => Stack(spr, stack)).ToList());
			}

			var exp = c.Spr("EXP");
			foreach (var (path, bands, size) in Explosions)
			{
				var frames = bands.SelectMany(b => Enumerable.Range(0, b.Count).Select(k => Buildings.Stitch(exp, b.Top + size * k, size))).ToList();
				c.WriteRgbaStrip(path, frames);
			}

			// Arrow: FIRE 80-88, one sprite per direction S..N (the sequence mirrors the east side), RGBA.
			c.WriteRgbaStrip("art/effects/arrow/dir.png", Units.Frames(c.Spr("FIRE"), Enumerable.Range(80, 9).Select(i => (i, false))));

			var fire = c.Spr("FIRE");
			var bullet = BulletWest.Select(i => (i, false)).Concat(BulletWest.Skip(1).Take(7).Reverse().Select(i => (i, true)))
				.Select(f => Centred(fire.Frame(f.i), BulletSize, f.Item2)).ToList();
			c.WriteStrip("art/effects/musket-bullet.png", bullet);

			// Shaman sky strike: RAIN tiles in a 2x2 square, top-left empty: - 281 / 289 290.
			var rain = c.Spr("RAIN");
			var strike = new IndexedImage(64, 64);
			strike.Paste(rain.Frame(281), 32, 0);
			strike.Paste(rain.Frame(289), 0, 32);
			strike.Paste(rain.Frame(290), 32, 32);
			c.WritePng("art/effects/shaman-lightning/strike.png", strike);

			foreach (var (name, frame) in UnitIcons)
				c.WritePng($"art/icons/units/{name}.png", ScaleNearest(fire.Frame(frame), 48, 48));

			foreach (var (name, frame) in SpellIcons)
				c.WritePng($"art/icons/spells/{name}.png", fire.Frame(frame));

			var portraits = c.Spr("PORTRAIT");
			foreach (var (name, frame) in BuildingPortraits)
				c.WritePng($"art/portraits/buildings/{name}.png", portraits.Frame(frame));

			foreach (var (name, frame) in UnitPortraits)
				c.WritePng($"art/portraits/units/{name}.png", portraits.Frame(frame));
		}

		static int[][] Singles(int first, int count) => Enumerable.Range(first, count).Select(i => new[] { i }).ToArray();

		static IndexedImage Stack(SprFile spr, int[] frames)
		{
			var result = new IndexedImage(spr.Width, spr.Height * frames.Length);
			for (var i = 0; i < frames.Length; i++)
				result.Paste(spr.Frame(frames[i]), 0, i * spr.Height);

			return result;
		}

		// The drawn part of a frame, centred on a size x size canvas (then mirrored if asked).
		static IndexedImage Centred(IndexedImage frame, int size, bool mirror)
		{
			var (left, top, right, bottom) = frame.Bounds().Value;
			var piece = frame.Crop(left, top, right - left, bottom - top);
			var result = new IndexedImage(size, size);
			result.Paste(piece, (size - piece.Width) / 2, (size - piece.Height) / 2);
			return mirror ? result.MirrorX() : result;
		}

		// Pillow's NEAREST resize, which the original tools used: each target pixel samples the source at its centre,
		// stepping by adding the scale (so the rounding matches theirs exactly).
		public static IndexedImage ScaleNearest(IndexedImage source, int width, int height)
		{
			var xs = Samples(source.Width, width);
			var ys = Samples(source.Height, height);
			var result = new IndexedImage(width, height);
			for (var y = 0; y < height; y++)
				for (var x = 0; x < width; x++)
					result[x, y] = source[xs[x], ys[y]];

			return result;
		}

		static int[] Samples(int from, int to)
		{
			var step = (double)from / to;
			var position = step * 0.5;
			var samples = new int[to];
			for (var i = 0; i < to; i++, position += step)
				samples[i] = (int)position;

			return samples;
		}
	}
}
