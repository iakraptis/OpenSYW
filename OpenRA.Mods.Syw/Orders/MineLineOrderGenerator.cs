using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Syw.Traits;
using OpenRA.Orders;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Syw.Orders
{
	// Minefield placement: a line of mines follows the cursor, outlined white where it can be laid and red where not.
	// Tab or the mouse wheel rotates the line (horizontal, diagonal, vertical, other diagonal); left-click orders the
	// Miner to walk to the middle cell and lay the line; right-click cancels.
	// Implements IOrderGenerator directly (not UnitOrderGenerator) so every click and key reaches it.
	public class MineLineOrderGenerator : IOrderGenerator
	{
		readonly Actor miner;
		readonly PaidMineLayer layer;
		readonly string worldDefaultCursor = ChromeMetrics.Get<string>("WorldDefaultCursor");

		public int Orientation { get; private set; }

		public MineLineOrderGenerator(Actor miner)
		{
			this.miner = miner;
			layer = miner.Trait<PaidMineLayer>();
		}

		bool MinerValid => !miner.IsDead && !miner.Disposed && miner.IsInWorld && miner.Owner == miner.World.LocalPlayer;

		public bool CanPlace(CPos cell) => MinerValid && layer.CanAffordLine(miner) && layer.CanPlaceLine(miner, cell, Orientation);

		public void Rotate(int steps)
		{
			Orientation = ((Orientation + steps) % PaidMineLayer.Orientations + PaidMineLayer.Orientations) % PaidMineLayer.Orientations;
		}

		public IEnumerable<Order> Order(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (mi.Event == MouseInputEvent.Scroll)
			{
				Rotate(mi.Delta.Y > 0 ? 1 : -1);
				yield break;
			}

			if (mi.Button == MouseButton.Right && mi.Event == MouseInputEvent.Up)
			{
				world.CancelInputMode();
				yield break;
			}

			if (mi.Button != MouseButton.Left || mi.Event != MouseInputEvent.Down || !CanPlace(cell))
				yield break;

			world.CancelInputMode();
			yield return new Order(PaidMineLayer.LineOrderId, miner, Target.FromCell(world, cell), mi.Modifiers.HasModifier(Modifiers.Shift))
			{
				ExtraData = (uint)Orientation
			};
		}

		public void Tick(World world)
		{
			if (!MinerValid)
				world.CancelInputMode();
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr, World world) { yield break; }
		public IEnumerable<IRenderable> RenderAboveShroud(WorldRenderer wr, World world) { yield break; }

		public IEnumerable<IRenderable> RenderAnnotations(WorldRenderer wr, World world)
		{
			if (!MinerValid)
				yield break;

			var cell = wr.Viewport.ViewToWorld(Viewport.LastMousePos);
			var color = CanPlace(cell) ? Color.White : Color.Red;
			var half = new WVec(448, 448, 0);
			foreach (var c in layer.LineCells(cell, Orientation).Where(world.Map.Contains))
			{
				var center = world.Map.CenterOfCell(c);
				var corners = new[]
				{
					center + new WVec(-half.X, -half.Y, 0), center + new WVec(half.X, -half.Y, 0),
					center + new WVec(half.X, half.Y, 0), center + new WVec(-half.X, half.Y, 0),
				};
				yield return new PolygonAnnotationRenderable(corners, center, 2, color);
			}
		}

		public string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi) =>
			CanPlace(cell) ? worldDefaultCursor : "move-blocked";

		public bool HandleKeyPress(KeyInput e)
		{
			if (e.Event != KeyInputEvent.Down || e.Key != Keycode.TAB)
				return false;

			Rotate(e.Modifiers.HasModifier(Modifiers.Shift) ? -1 : 1);
			return true;
		}

		public void Deactivate() { }

		public void SelectionChanged(World world, IEnumerable<Actor> selected)
		{
			var actors = selected.ToArray();
			if (actors.Length != 1 || actors[0] != miner)
				world.CancelInputMode();
		}
	}
}
