using System.Collections.Generic;
using OpenRA.Mods.Common.Orders;

namespace OpenRA.Mods.Syw.Orders
{
	// Base of SYW's targeting modes: the player presses a button (a spell, Unload...), then confirms the target with the
	// ConfirmOrder button (right under the default Modern mouse style, left under Classic) or cancels with the other one.
	// Both act on the button's release, not its press: a press that ended the mode would leave its release to the default
	// order generator, which turns a right-button release into an ordinary move/attack order for the selected units.
	public abstract class SywOrderGenerator : OrderGenerator
	{
		protected SywOrderGenerator(World world)
			: base(world) { }

		protected override MouseActionType ActionType => MouseActionType.ConfirmOrder;

		// The same buttons for the modes that implement IOrderGenerator directly.
		public static MouseButton ConfirmButton => Game.Settings.Game.ResolveActionButton(MouseActionType.ConfirmOrder);
		public static MouseButton DismissButton => Game.Settings.Game.ResolveCancelButton(MouseActionType.ConfirmOrder);

		public override IEnumerable<Order> Order(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (mi.Event != MouseInputEvent.Up)
				return [];

			if (mi.Button == ActionButton)
				return OrderInner(world, cell, worldPixel, mi);

			if (mi.Button == CancelButton)
				world.CancelInputMode();

			return [];
		}
	}
}
