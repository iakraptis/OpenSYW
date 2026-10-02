using System.Collections.Generic;

namespace OpenRA.Mods.Syw.AssetInstaller
{
	// Buildings are grids of 32x32 tiles in the BUILD*.SPR sheets: tile (row, col) of a building is frame
	// base + row * 20 + col. Each building has three states side by side in the same 20-frame band: under construction,
	// finished and damaged, 3 frames apart for 3x3 buildings and 2 apart for 2x2 towers.
	public static class Buildings
	{
		const int RowStride = 20;

		// (building, sprite, construction base, size in tiles)
		static readonly (string Name, string Sprite, int Base, int Size)[] All =
		{
			("headquarters", "BUILD2", 0, 3), ("mill", "BUILD2", 9, 3), ("barracks", "BUILD2", 80, 3),
			("beaconmound", "BUILD2", 89, 3), ("heavyarmsworkshop", "BUILD2", 180, 3), ("shipyard", "BUILD2", 189, 3),
			("planeworks", "BUILD2", 240, 3), ("temple", "BUILD2", 249, 3),
			("arrowtower", "BUILD2", 140, 2), ("cannontower", "BUILD2", 146, 2),
			("barracks2", "BUILD5", 0, 3), ("stable", "BUILD5", 60, 3), ("shamanhouse", "BUILD5", 180, 3),

			("jheadquarters", "BUILD3", 0, 3), ("jmill", "BUILD3", 9, 3), ("jbarracks", "BUILD3", 80, 3),
			("jbeacon", "BUILD3", 89, 3), ("jheavyarms", "BUILD3", 180, 3), ("jshipyard", "BUILD3", 189, 3),
			("airport", "BUILD3", 240, 3), ("jtemple", "BUILD3", 249, 3),
			("jarrowtower", "BUILD3", 140, 2), ("jcannontower", "BUILD3", 146, 2),
			("jbarracks2", "BUILD5", 9, 3), ("jstable", "BUILD5", 69, 3), ("witchhouse", "BUILD5", 189, 3),
		};

		// Animated finished states that replace the still idle.png (user-confirmed): whole 3x3 buildings, frame k at
		// columns base + 3k .. base + 3k + 2 of the tile rows.
		static readonly (string Name, string Sprite, int Base, int Frames)[] IdleAnimations =
		{
			("mill", "BUILD5", 240, 3),       // waterfall and wheel
			("temple", "BUILD5", 120, 4),     // flickering aura around the statue
			("jmill", "BUILD1", 80, 4),       // turning windmill
		};

		public static void Export(InstallContext c)
		{
			foreach (var (name, sprite, first, size) in All)
			{
				var spr = c.Spr(sprite);
				c.WritePng($"art/buildings/{name}/underconstruction.png", Stitch(spr, first, size));
				c.WritePng($"art/buildings/{name}/idle.png", Stitch(spr, first + size, size));
				c.WritePng($"art/buildings/{name}/damaged-idle.png", Stitch(spr, first + 2 * size, size));
			}

			foreach (var (name, sprite, first, count) in IdleAnimations)
			{
				var spr = c.Spr(sprite);
				var frames = new List<IndexedImage>();
				for (var k = 0; k < count; k++)
					frames.Add(Stitch(spr, first + 3 * k, 3));

				c.WriteStrip($"art/buildings/{name}/idle.png", frames);
			}

			// Scaffolding shown while a building is placed: BUILD1 frames 4-6, 24-26, 44-46.
			c.WritePng("art/buildings/common/scaffold-medium.png", Stitch(c.Spr("BUILD1"), 4, 3));
		}

		public static IndexedImage Stitch(SprFile spr, int first, int size, int cols = 0)
		{
			cols = cols > 0 ? cols : size;
			var tiles = new IndexedImage[size, cols];
			for (var r = 0; r < size; r++)
				for (var col = 0; col < cols; col++)
					tiles[r, col] = spr.Frame(first + r * RowStride + col);

			return IndexedImage.Grid(tiles);
		}
	}
}
