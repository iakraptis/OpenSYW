using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.Syw.Widgets.Logic
{
	// The end-of-match screen: the original picture for the local player's result (chrome.yaml `game-outcome`: one
	// victory picture per side, one defeat picture). Continue or Escape closes it.
	public class GameOutcomeScreenLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public GameOutcomeScreenLogic(Widget widget, World world)
		{
			var player = world.LocalPlayer;
			var picture = widget.Get<StretchedImageWidget>("PICTURE");
			picture.ImageName = player?.WinState != WinState.Won ? "defeat"
				: player.Faction.InternalName == "japan" ? "victory-japan" : "victory-korea";

			widget.Get<ButtonWidget>("CONTINUE").OnClick = () =>
			{
				// Removed after the input that clicked it has been handled, not while the UI root is going through it.
				widget.Visible = false;
				Game.RunAfterTick(() => widget.Parent?.RemoveChild(widget));
			};
		}
	}
}
