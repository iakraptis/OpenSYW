using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Syw.Traits;

namespace OpenRA.Mods.Syw.Orders
{
	// The Unload button's targeting: left-click a cell and every selected transport with passengers goes there and
	// unloads (shift queues it); right-click cancels.
	public class UnloadAtOrderGenerator : OrderGenerator
	{
		public static IEnumerable<Actor> Transports(World world) =>
			world.Selection.Actors.Where(a => a.Owner == world.LocalPlayer && a.IsInWorld && !a.IsDead &&
				(a.TraitOrDefault<UnloadAtLocation>()?.HasPassengers ?? false));

		static bool ValidCell(World world, CPos cell) => world.Map.Contains(cell) && !world.ShroudObscures(cell);

		protected override IEnumerable<Order> OrderInner(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (mi.Button == MouseButton.Right)
			{
				world.CancelInputMode();
				yield break;
			}

			if (!ValidCell(world, cell))
				yield break;

			var queued = mi.Modifiers.HasModifier(Modifiers.Shift);
			if (!queued)
				world.CancelInputMode();

			foreach (var transport in Transports(world))
				yield return new Order(UnloadAtLocation.OrderId, transport, OpenRA.Traits.Target.FromCell(world, cell), queued);
		}

		protected override string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi) =>
			ValidCell(world, cell) ? "deploy" : "deploy-blocked";

		protected override void Tick(World world)
		{
			if (!Transports(world).Any())
				world.CancelInputMode();
		}

		protected override void SelectionChanged(World world, IEnumerable<Actor> selected)
		{
			if (!Transports(world).Any())
				world.CancelInputMode();
		}

		protected override IEnumerable<IRenderable> Render(WorldRenderer wr, World world) { yield break; }
		protected override IEnumerable<IRenderable> RenderAboveShroud(WorldRenderer wr, World world) { yield break; }
		protected override IEnumerable<IRenderable> RenderAnnotations(WorldRenderer wr, World world) { yield break; }
	}
}
