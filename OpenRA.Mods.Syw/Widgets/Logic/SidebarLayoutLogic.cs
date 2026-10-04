using System;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.Syw.Widgets.Logic
{
	// Fits the sidebar's lower half (selection stats, unit commands, the build / training / cargo / spell panels) to
	// the window height, which with a UI scale above 100% is the scaled height. ingame-player.yaml holds the full
	// layout for tall windows (1060 px and more); below that the stats panel moves up under the radar, the panels
	// follow the command row (or take its place while it is hidden), and the palettes show only the rows that fit,
	// scrolling for the rest. Under 700 px the radar is hidden too so the controls keep their room.
	public class SidebarLayoutLogic : ChromeLogic
	{
		const int FullLayoutHeight = 1060;
		const int RadarMinHeight = 700;

		// The yaml layout for tall windows.
		const int FullStatsY = 408;
		const int CompactStatsY = 316;
		const int NoRadarStatsY = 48;

		const int StatsToCommands = 184;
		const int CommandsToTitle = 64;
		const int TitleToPalette = 30;
		const int RowHeight = 52;
		const int BottomMargin = 8;
		const int MaxRows = 7;

		readonly Widget stats, commands, title, productionBackground, buildPalette, cargo, spells;
		readonly ProductionPaletteWidget production;
		readonly Widget productionUp, productionDown;
		readonly Func<bool> radarShown;
		int rows = MaxRows;

		[ObjectCreator.UseCtor]
		public SidebarLayoutLogic(Widget widget)
		{
			stats = widget.Get("STATS_PANEL");
			commands = widget.Get("COMMAND_BAR");
			title = widget.Get("TRAINING_TITLE");
			productionBackground = widget.Get("PRODUCTION_BACKGROUND");
			production = widget.Get<ProductionPaletteWidget>("PRODUCTION_PALETTE");
			buildPalette = widget.Get("BUILD_PALETTE");
			cargo = widget.Get("CARGO_PANEL");
			spells = widget.Get("SPELL_PANEL");

			var radar = widget.Get("RADAR_PANEL");
			radarShown = radar.IsVisible;
			radar.IsVisible = () => Height >= RadarMinHeight && radarShown();

			// Arrows beside the training palette; its mouse wheel scrolling is the engine's.
			var up = widget.Get<ButtonWidget>("PRODUCTION_SCROLL_UP");
			up.IsVisible = () => production.CurrentQueue != null && production.CanScrollUp;
			up.OnClick = production.ScrollUp;
			var down = widget.Get<ButtonWidget>("PRODUCTION_SCROLL_DOWN");
			down.IsVisible = () => production.CurrentQueue != null && production.CanScrollDown;
			down.OnClick = production.ScrollDown;
			productionUp = up;
			productionDown = down;

			Layout();
		}

		// The layout's WINDOW_HEIGHT: the effective resolution, already divided by the UI scale.
		static int Height => Game.Renderer.Resolution.Height;

		public override void Tick() => Layout();

		void Layout()
		{
			var height = Height;
			var full = height >= FullLayoutHeight;
			var statsY = full ? FullStatsY : height >= RadarMinHeight ? CompactStatsY : NoRadarStatsY;
			var commandsY = statsY + StatsToCommands;

			// The full layout keeps the command row's space; the compact ones close the gap while it is hidden.
			var titleY = full || commands.IsVisible() ? commandsY + CommandsToTitle : commandsY;
			var paletteY = titleY + TitleToPalette;
			var newRows = Math.Clamp((height - paletteY - BottomMargin + RowHeight - production.IconSize.Y) / RowHeight, 1, MaxRows);

			stats.Bounds.Y = statsY;
			commands.Bounds.Y = commandsY;
			title.Bounds.Y = titleY;
			cargo.Bounds.Y = titleY;
			spells.Bounds.Y = titleY;
			productionBackground.Bounds.Y = paletteY;
			production.Bounds.Y = paletteY + 1;
			buildPalette.Bounds.Y = paletteY;
			buildPalette.Bounds.Height = newRows * RowHeight - 2;
			productionUp.Bounds.Y = paletteY + 2;
			productionDown.Bounds.Y = paletteY + (newRows - 1) * RowHeight + 28;

			if (newRows != rows)
			{
				rows = newRows;
				production.MaxIconRowOffset = rows;
				production.ScrollToTop();
			}
		}
	}
}
