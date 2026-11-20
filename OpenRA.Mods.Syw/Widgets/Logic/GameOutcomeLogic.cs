using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.Syw.Widgets.Logic
{
	// Watches the local player's result and, when they win or lose, shows the original Victory!/Defeat! screen
	// (GAME_OUTCOME_SCREEN) after the same delay the engine waits before switching to the observer view. The screen is
	// loaded into the UI root, above the observer view and the in-game menu that opens when the game ends, so it outlives
	// the player widgets this ticker belongs to.
	public class GameOutcomeLogic : ChromeLogic
	{
		bool shown;

		[ObjectCreator.UseCtor]
		public GameOutcomeLogic(Widget widget, World world)
		{
			var player = world.LocalPlayer;
			if (player == null)
				return;

			var delay = player.PlayerActor.Info.TraitInfoOrDefault<MissionObjectivesInfo>()?.GameOverDelay ?? 0;
			((LogicTickerWidget)widget).OnTick = () =>
			{
				if (shown || player.WinState == WinState.Undefined)
					return;

				shown = true;
				Game.RunAfterDelay(delay, () =>
				{
					if (Game.IsCurrentWorld(world))
						Game.LoadWidget(world, "GAME_OUTCOME_SCREEN", Ui.Root, []);
				});
			};
		}
	}
}
