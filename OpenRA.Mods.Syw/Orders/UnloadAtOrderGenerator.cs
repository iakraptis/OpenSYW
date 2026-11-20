using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Syw.Traits;

namespace OpenRA.Mods.Syw.Orders
{
	// The Unload button's targeting: click a cell with the action button and every transport with passengers that was
	// selected goes there and unloads (shift queues it); the other button cancels.
	// The transports are remembered when the mode starts, because under the Classic mouse style the base class clears
	// the selection.
	public class UnloadAtOrderGenerator : OrderGenerator
	{
		Actor[] transports;

		public UnloadAtOrderGenerator(World world)
			: this(world, Transports(world).ToArray()) { }

		UnloadAtOrderGenerator(World world, Actor[] transports)
			: base(world)
		{
			this.transports = transports;
		}

		protected override MouseActionType ActionType => MouseActionType.SupportPower;

		public static IEnumerable<Actor> Transports(World world) => world.Selection.Actors.Where(a => CanUnload(world, a));

		static bool CanUnload(World world, Actor a) => a.Owner == world.LocalPlayer && a.IsInWorld && !a.IsDead &&
			(a.TraitOrDefault<UnloadAtLocation>()?.HasPassengers ?? false);

		static bool ValidCell(World world, CPos cell) => world.Map.Contains(cell) && !world.ShroudObscures(cell);

		protected override IEnumerable<Order> OrderInner(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (!ValidCell(world, cell))
				yield break;

			var queued = mi.Modifiers.HasModifier(Modifiers.Shift);
			if (!queued)
				world.CancelInputMode();

			foreach (var transport in transports.Where(a => CanUnload(world, a)))
				yield return new Order(UnloadAtLocation.OrderId, transport, OpenRA.Traits.Target.FromCell(world, cell), queued);
		}

		protected override string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi) =>
			ValidCell(world, cell) ? "deploy" : "deploy-blocked";

		protected override void Tick(World world)
		{
			if (!transports.Any(a => CanUnload(world, a)))
				world.CancelInputMode();
		}

		// A new selection replaces the remembered transports; one without loaded transports ends the mode.
		protected override void SelectionChanged(World world, IEnumerable<Actor> selected)
		{
			transports = selected.Where(a => CanUnload(world, a)).ToArray();
			if (transports.Length == 0)
				world.CancelInputMode();
		}

		protected override IEnumerable<IRenderable> Render(WorldRenderer wr, World world) { yield break; }
		protected override IEnumerable<IRenderable> RenderAboveShroud(WorldRenderer wr, World world) { yield break; }
		protected override IEnumerable<IRenderable> RenderAnnotations(WorldRenderer wr, World world) { yield break; }
	}
}
