using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.Syw.Widgets.Logic
{
	// Shows the unit command row (Attack, Stop) only while the local player has some of their own units selected
	// (ground, naval or air); buildings, enemy actors or an empty selection hide it. Each button's DISABLED overlay
	// darkens it while the button is disabled (bound here: a container's logic runs once all its children exist).
	public class UnitCommandsVisibilityLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public UnitCommandsVisibilityLogic(Widget widget, World world)
		{
			widget.IsVisible = () => world.Selection.Actors.Any(a => a.Owner == world.LocalPlayer && a.IsInWorld && !a.IsDead
				&& (a.Info.HasTraitInfo<MobileInfo>() || a.Info.HasTraitInfo<AircraftInfo>()));

			foreach (var button in widget.Children.OfType<ButtonWidget>())
				if (button.GetOrNull("DISABLED") is Widget overlay)
					overlay.IsVisible = () => button.IsDisabled();
		}
	}
}
