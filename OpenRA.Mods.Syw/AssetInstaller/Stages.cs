using System;
using System.Collections.Generic;

namespace OpenRA.Mods.Syw.AssetInstaller
{
	public sealed record Stage(string Name, string Description, Action<InstallContext> Run);

	// Everything --install-assets produces, in run order.
	public static class Stages
	{
		public static readonly IReadOnlyList<Stage> All = new Stage[]
		{
			new("copies", "Knight.pal and the menu music, copied as they are", Copies),
			new("units", "Every unit's sprite sheets", Units.Export),
			new("buildings", "Building states, animated idles and the scaffold", Buildings.Export),
			new("effects", "Effects, projectiles, training and spell buttons, portraits", Effects.Export),
			new("terrain", "FIELD3 tileset, the Fighter map (Korea Multi 1) and the crop resources", Terrain.Export),
			new("chrome", "Title screen, sidebar, main menu fire and mouse cursors", Chrome.Export),
			new("objects", "Trees, Jangseung totems, brazier and flags from TREE.SPR (map objects)", MapObjects.Export),
			new("audio", "Sound effects and voices from effect/ (silent placeholders skipped)", Audio.Export),
			new("maps", "Our maps on converted original maps (bot-test, ffa-spectate); needs terrain", Maps.Export),
		};

		static void Copies(InstallContext c)
		{
			c.CopyGameFile("FNT1/Knight.pal", "palettes/Knight.pal");
			c.CopyGameFile("effect/menumus.WAV", "music/menumus.wav");
		}
	}
}
