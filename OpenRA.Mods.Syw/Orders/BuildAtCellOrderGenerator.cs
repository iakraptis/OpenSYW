#region Copyright & License Information
/*
 * Part of the OpenSYW "Seven Years War" mod.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Syw.Traits;
using OpenRA.Orders;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Syw.Orders
{
	/// <summary>
	/// Targeting-cursor mode entered by the BuilderPalette chrome logic after the player clicks a "Build X"
	/// button. Reuses the engine's building-footprint validity helpers (World.CanPlaceBuilding etc.) but,
	/// unlike the vanilla PlaceBuildingOrderGenerator, issues the order to a specific Peasant rather than
	/// to a player-wide production queue - that Peasant walks to the cell and builds it (see Builder.cs).
	/// </summary>
	public class BuildAtCellOrderGenerator : IOrderGenerator
	{
		readonly string worldDefaultCursor = ChromeMetrics.Get<string>("WorldDefaultCursor");
		readonly Actor builderActor;
		readonly string actorType;
		readonly ActorInfo actorInfo;
		readonly BuildingInfo buildingInfo;

		public BuildAtCellOrderGenerator(World world, Actor builderActor, string actorType)
		{
			this.builderActor = builderActor;
			this.actorType = actorType;
			actorInfo = world.Map.Rules.Actors[actorType];
			buildingInfo = actorInfo.TraitInfo<BuildingInfo>();
		}

		IEnumerable<Order> IOrderGenerator.Order(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (mi.Button == MouseButton.Right && mi.Event == MouseInputEvent.Up)
			{
				world.CancelInputMode();
				return Enumerable.Empty<Order>();
			}

			if (mi.Button != MouseButton.Left || mi.Event != MouseInputEvent.Down)
				return Enumerable.Empty<Order>();

			if (builderActor.IsDead || builderActor.Disposed || !builderActor.IsInWorld)
			{
				world.CancelInputMode();
				return Enumerable.Empty<Order>();
			}

			if (!CoastalBuilding.CanPlace(world, builderActor, actorInfo, buildingInfo, cell))
				return Enumerable.Empty<Order>();

			world.CancelInputMode();
			return new[]
			{
				new Order(Builder.OrderID, builderActor, Target.FromCell(world, cell), false)
				{
					TargetString = actorType,
					SuppressVisualFeedback = true,
				},
			};
		}

		void IOrderGenerator.Tick(World world)
		{
			if (builderActor.IsDead || builderActor.Disposed || !builderActor.IsInWorld)
				world.CancelInputMode();
		}

		void IOrderGenerator.SelectionChanged(World world, IEnumerable<Actor> selected) { }

		IEnumerable<IRenderable> IOrderGenerator.Render(WorldRenderer wr, World world) { yield break; }

		IEnumerable<IRenderable> IOrderGenerator.RenderAboveShroud(WorldRenderer wr, World world)
		{
			yield break;
		}

		IEnumerable<IRenderable> IOrderGenerator.RenderAnnotations(WorldRenderer wr, World world)
		{
			if (builderActor.IsDead || builderActor.Disposed || !builderActor.IsInWorld)
				yield break;

			// Footprint outline under the cursor: white if the building can be placed there, red if not.
			// Uses the same check as the order itself, so the preview always matches what is accepted.
			var cell = wr.Viewport.ViewToWorld(Viewport.LastMousePos);
			var tiles = buildingInfo.Tiles(cell).ToList();
			if (tiles.Count == 0)
				yield break;

			var color = CoastalBuilding.CanPlace(world, builderActor, actorInfo, buildingInfo, cell) ? Color.White : Color.Red;
			var half = new WVec(512, 512, 0);
			var topLeft = world.Map.CenterOfCell(new CPos(tiles.Min(t => t.X), tiles.Min(t => t.Y))) - half;
			var bottomRight = world.Map.CenterOfCell(new CPos(tiles.Max(t => t.X), tiles.Max(t => t.Y))) + half;
			var corners = new[]
			{
				topLeft, new WPos(bottomRight.X, topLeft.Y, topLeft.Z), bottomRight, new WPos(topLeft.X, bottomRight.Y, topLeft.Z),
			};

			yield return new PolygonAnnotationRenderable(corners, topLeft, 2, color);
		}

		string IOrderGenerator.GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			return CoastalBuilding.CanPlace(world, builderActor, actorInfo, buildingInfo, cell) ? worldDefaultCursor : "move-blocked";
		}

		bool IOrderGenerator.HandleKeyPress(KeyInput e) { return false; }

		void IOrderGenerator.Deactivate() { }
	}
}
