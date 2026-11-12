using System.Linq;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Syw.Orders;
using OpenRA.Mods.Syw.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Syw.Widgets.Logic
{
	// The command bar's Unload button: shown while an own transport is selected, disabled while none of the selected
	// transports carries anyone. Click it, then click the map: the transports go there and unload (UnloadAtLocation).
	public class UnloadButtonLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public UnloadButtonLogic(Widget widget, World world)
		{
			var button = (ButtonWidget)widget;
			bool Selected() => world.Selection.Actors.Any(a => a.Owner == world.LocalPlayer && a.IsInWorld && !a.IsDead &&
				a.Info.HasTraitInfo<UnloadAtLocationInfo>());

			button.IsVisible = Selected;
			button.IsDisabled = () => !UnloadAtOrderGenerator.Transports(world).Any();
			button.IsHighlighted = () => world.OrderGenerator is UnloadAtOrderGenerator;
			button.OnClick = () =>
			{
				if (world.OrderGenerator is UnloadAtOrderGenerator)
					world.CancelInputMode();
				else
					world.OrderGenerator = new UnloadAtOrderGenerator();
			};
		}
	}
}
