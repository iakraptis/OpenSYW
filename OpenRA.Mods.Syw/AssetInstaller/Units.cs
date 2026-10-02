using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Syw.AssetInstaller
{
	// Every unit's sprite sheets, from one table per body layout. Frame numbers are SPR frame indices (frame_NNNN in the
	// SPR viewer); most Japanese units sit in the same SPR file as their Korean counterpart at a fixed offset.
	public static class Units
	{
		// Infantry, workers and cavalry draw five directions, 4 frames apart: S +0, SW +4, W +8, NW +12, N +16. The other
		// three facings are mirrors. Listed in OpenRA's facing order: N, NW, W, SW, S, SE, E, NE.
		static readonly (int Start, bool Mirror)[] FiveDirections =
		{
			(16, false), (12, false), (8, false), (4, false), (0, false), (4, true), (8, true), (12, true)
		};

		// Sheets: walk.png (8 facings x 4), shoot.png (8 facings x ShootLength, from Base + 20), death.png.
		sealed record Infantry(string Unit, string Sprite, int Base, int ShootLength = 4, int DeathOffset = 40,
			int DeathLength = 4, bool Fireball = false);

		static readonly Infantry[] InfantryUnits =
		{
			new("archer", "ARCHER", 0, DeathOffset: 41, DeathLength: 3),
			new("footman", "FOOTMAN", 0, DeathOffset: 41, DeathLength: 3),
			new("gunner", "ARCHER", 96, ShootLength: 2, DeathOffset: 30),
			new("monk", "BUDA", 0, Fireball: true),
			new("shaman", "BUDA", 96),
			new("commander", "GENERAL", 0, DeathLength: 6),
			new("jarcher", "ARCHER", 48),
			new("jfootman", "FOOTMAN", 48),
			new("jgunner", "ARCHER", 130, ShootLength: 2, DeathOffset: 30),
			new("priest", "BUDA", 48),
			new("witch", "BUDA", 144),
			new("general", "GENERAL", 48, DeathLength: 6),
		};

		// Sheets: walk.png, loaded-walk.png (from Base + 20), harvest.png (Base + 40, 4 frames), death.png (Base + 44).
		static readonly (string Unit, string Sprite, int Base)[] Workers =
		{
			("peasant", "FARMER", 0), ("bull", "BULL", 0), ("jpeasant", "FARMER", 48), ("jbull", "BULL", 48),
		};

		// Sheets: walk.png and death.png (Base + 20, 4 frames).
		static readonly (string Unit, string Sprite, int Base)[] Walkers =
		{
			("occupier", "FARMER", 128), ("thief", "FARMER", 160), ("miner", "FARMER", 96),
		};

		// flight.png: 8 facings x 2 phases; views S +0, SW +1, W +2, NW +3, N +4, the second phase 9 frames later.
		static readonly (int Start, bool Mirror)[] AircraftFacings =
		{
			(4, false), (3, false), (2, false), (1, false), (0, false), (1, true), (2, true), (3, true)
		};

		static readonly (string Unit, string Sprite, int Base)[] Aircraft =
		{
			("fighter", "FLY3", 0), ("transporter", "FLY2", 0), ("jtransporter", "Fly1", 0), ("bomber", "Fly1", 24),
			("jfighter", "FLY3", 24),
		};

		// body.png: 16 facings. Views in OpenRA order N, NNW, ..., S (east side mirrored); the sail sits SailOffset frames
		// after its hull and is drawn over it (null: no sail).
		static readonly (int View, bool Mirror)[] ShipFacings =
			new[] { 4, 8, 3, 7, 2, 6, 1, 5, 0 }.Select(v => (v, false))
			.Concat(new[] { 5, 1, 6, 2, 7, 3, 8 }.Select(v => (v, true))).ToArray();

		static readonly (string Unit, string Sprite, int Base, int? SailOffset)[] Ships =
		{
			("patrolship", "SHIP1", 0, 9), ("cannonship", "SHIP1", 18, 9), ("transportship", "SHIP3", 0, 9),
			("jcannonship", "SHIP2", 0, 9), ("arrowship", "SHIP2", 24, 9), ("submarine", "SHIP3", 24, null),
		};

		// TANK.SPR vehicles, 23 frames from Base: body.png = 8 facings x 2 frames (views S +0, SW +2, W +4, NW +6, N +8);
		// turret.png = 16 angles from frames +14..+22 (N = +22 counter-clockwise to S = +14, east side mirrored).
		// TurretShift moves the turret frames inside their canvas: the Armored Car's turret is drawn around (15,11)
		// instead of the centre.
		static readonly (int Start, bool Mirror)[] VehicleBody =
		{
			(8, false), (6, false), (4, false), (2, false), (0, false), (2, true), (4, true), (6, true)
		};

		static readonly (int Frame, bool Mirror)[] VehicleTurret =
			Enumerable.Range(14, 9).Reverse().Select(i => (i, false)).Concat(Enumerable.Range(15, 7).Select(i => (i, true))).ToArray();

		static readonly (string Unit, int Base, int ShiftX, int ShiftY)[] Vehicles =
		{
			("firecar", 0, 0, 0), ("cannon", 52, 0, 0), ("armoredcar", 26, 9, 13),
		};

		// Torpedo: FIRE 160-168 in OpenRA facing order N..S (user-identified: 160 S, 162 W, 167 WNW, 168 NW), east mirrored.
		static readonly int[] TorpedoWest = { 164, 163, 168, 167, 162, 166, 161, 165, 160 };

		public static void Export(InstallContext c)
		{
			foreach (var u in InfantryUnits)
			{
				var spr = c.Spr(u.Sprite);
				c.WriteStrip($"art/units/{u.Unit}/walk.png", Frames(spr, Directional(u.Base, 4)));
				c.WriteStrip($"art/units/{u.Unit}/shoot.png", Frames(spr, Directional(u.Base + 20, u.ShootLength)));
				c.WriteStrip($"art/units/{u.Unit}/death.png", Frames(spr, Run(u.Base + u.DeathOffset, u.DeathLength)));
				if (u.Fireball)
					c.WriteStrip($"art/units/{u.Unit}/fireball.png", Frames(c.Spr("FIRE"),
						Enumerable.Range(40, 9).Reverse().Select(i => (i, false)).Concat(Enumerable.Range(41, 7).Select(i => (i, true)))));
			}

			foreach (var (unit, sprite, first) in Workers)
			{
				var spr = c.Spr(sprite);
				c.WriteStrip($"art/units/{unit}/walk.png", Frames(spr, Directional(first, 4)));
				c.WriteStrip($"art/units/{unit}/loaded-walk.png", Frames(spr, Directional(first + 20, 4)));
				c.WriteStrip($"art/units/{unit}/harvest.png", Frames(spr, Run(first + 40, 4)));
				c.WriteStrip($"art/units/{unit}/death.png", Frames(spr, Run(first + 44, 4)));
			}

			foreach (var (unit, sprite, first) in Walkers)
			{
				var spr = c.Spr(sprite);
				c.WriteStrip($"art/units/{unit}/walk.png", Frames(spr, Directional(first, 4)));
				c.WriteStrip($"art/units/{unit}/death.png", Frames(spr, Run(first + 20, 4)));
			}

			// The Miner's mine (user-identified): BUILD1 200 while being laid, 201 once armed.
			c.WriteStrip("art/units/miner/mine-building.png", Frames(c.Spr("BUILD1"), Run(200, 1)));
			c.WriteStrip("art/units/miner/mine.png", Frames(c.Spr("BUILD1"), Run(201, 1)));

			foreach (var (unit, sprite, first) in Aircraft)
			{
				var spr = c.Spr(sprite);
				var layout = AircraftFacings.SelectMany(f => new[] { (first + f.Start, f.Mirror), (first + f.Start + 9, f.Mirror) });
				c.WriteStrip($"art/units/{unit}/flight.png", Frames(spr, layout));
			}

			foreach (var (unit, sprite, first, sail) in Ships)
			{
				var spr = c.Spr(sprite);
				var frames = ShipFacings.Select(f =>
				{
					var hull = spr.Frame(first + f.View);
					if (sail.HasValue)
						hull.PasteOver(spr.Frame(first + f.View + sail.Value), 0, 0);

					return f.Mirror ? hull.MirrorX() : hull;
				}).ToList();
				c.WriteStrip($"art/units/{unit}/body.png", frames);
			}

			var tank = c.Spr("TANK");
			foreach (var (unit, first, shiftX, shiftY) in Vehicles)
			{
				var frames = Enumerable.Range(0, 23).Select(i =>
				{
					var frame = tank.Frame(first + i);
					if (i < 14 || (shiftX == 0 && shiftY == 0))
						return frame;

					var moved = new IndexedImage(frame.Width, frame.Height);
					moved.Paste(frame, shiftX, shiftY);
					return moved;
				}).ToArray();

				var body = VehicleBody.SelectMany(f => new[] { (f.Start, f.Mirror), (f.Start + 1, f.Mirror) });
				c.WriteStrip($"art/units/{unit}/body.png", body.Select(f => f.Mirror ? frames[f.Item1].MirrorX() : frames[f.Item1]).ToList());
				c.WriteStrip($"art/units/{unit}/turret.png", VehicleTurret.Select(f => f.Mirror ? frames[f.Frame].MirrorX() : frames[f.Frame]).ToList());
			}

			// Fire Car rocket: FIRE 0-8, one sprite per direction S..N (the sequence mirrors the east side).
			c.WriteStrip("art/units/firecar/missile.png", Frames(c.Spr("FIRE"), Run(0, 9)));

			var torpedo = TorpedoWest.Select(i => (i, false)).Concat(TorpedoWest.Skip(1).Take(7).Reverse().Select(i => (i, true)));
			c.WriteStrip("art/units/torpedo/body.png", Frames(c.Spr("FIRE"), torpedo));
		}

		// Five-direction animation: for each facing, perFacing frames starting at first + direction * perFacing.
		static IEnumerable<(int Frame, bool Mirror)> Directional(int first, int perFacing) =>
			FiveDirections.SelectMany(f => Enumerable.Range(0, perFacing).Select(n => (first + f.Start / 4 * perFacing + n, f.Mirror)));

		static IEnumerable<(int Frame, bool Mirror)> Run(int first, int count) =>
			Enumerable.Range(first, count).Select(i => (i, false));

		public static List<IndexedImage> Frames(SprFile spr, IEnumerable<(int Frame, bool Mirror)> layout) =>
			layout.Select(f => f.Mirror ? spr.Frame(f.Frame).MirrorX() : spr.Frame(f.Frame)).ToList();
	}
}
