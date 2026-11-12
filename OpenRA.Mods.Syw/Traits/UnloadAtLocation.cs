using System.Linq;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Pathfinder;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("Lets a transport unload its passengers at a chosen place (the sidebar's Unload button, then a click on the map).",
		"An aircraft flies there, lands and unloads; a ship sails to the shore next to the place; anything else drives there.")]
	public class UnloadAtLocationInfo : TraitInfo, Requires<CargoInfo>
	{
		[Desc("How far from the clicked cell to look for a cell to unload from (a ship: the shore), in cells.")]
		public readonly int ShoreSearchRadius = 8;

		public override object Create(ActorInitializer init) { return new UnloadAtLocation(init.Self, this); }
	}

	public class UnloadAtLocation : IResolveOrder, IOrderVoice
	{
		public const string OrderId = "UnloadAt";

		readonly UnloadAtLocationInfo info;
		readonly Cargo cargo;
		readonly Mobile mobile;
		readonly bool isAircraft;

		public UnloadAtLocation(Actor self, UnloadAtLocationInfo info)
		{
			this.info = info;
			cargo = self.Trait<Cargo>();
			mobile = self.TraitOrDefault<Mobile>();
			isAircraft = self.Info.HasTraitInfo<AircraftInfo>();
		}

		public bool HasPassengers => !cargo.IsEmpty();

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != OrderId || order.Target.Type != TargetType.Terrain || cargo.IsEmpty())
				return;

			var cell = self.World.Map.CellContaining(order.Target.CenterPosition);
			if (isAircraft)
			{
				// UnloadCargo lands first (within the landing range of the cell), unloads, then takes off again.
				self.QueueActivity(order.Queued, new UnloadCargo(self, Target.FromCell(self.World, cell), cargo.Info.LoadRange));
				self.ShowTargetLines();
				return;
			}

			if (mobile == null)
				return;

			var destination = CanUnloadAt(self, cell) ? cell : UnloadCellNear(self, cell);
			if (destination == null)
				return;

			self.QueueActivity(order.Queued, new UnloadCargo(self, Target.FromCell(self.World, destination.Value), WDist.Zero));
			self.ShowTargetLines();
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order) =>
			order.OrderString == OrderId && !cargo.IsEmpty() ? cargo.Info.UnloadVoice : null;

		// The transport can stand there and unload (a ship: on its unload terrain, water), and no building is in the way.
		bool CanUnloadAt(Actor self, CPos cell) =>
			self.World.Map.Contains(cell) && mobile.CanEnterCell(cell, self, BlockedByActor.Immovable) && UnloadTerrain(self, cell);

		bool UnloadTerrain(Actor self, CPos cell) =>
			cargo.Info.UnloadTerrainTypes.Count == 0 || cargo.Info.UnloadTerrainTypes.Contains(self.World.Map.GetTerrainInfo(cell).Type);

		// Terrain only (buildings ignored): tells land from water.
		static bool Passable(Locomotor locomotor, CPos cell) => locomotor.MovementCostForCell(cell) != PathGraph.MovementCostForUnreachableCell;

		// The clicked cell is out of reach (a ship clicked onto land, a car onto a cliff): the nearest cell the transport
		// can reach and unload from. For a ship that cell must be on the shore, with land beside it that the first
		// passenger can step onto.
		CPos? UnloadCellNear(Actor self, CPos target)
		{
			var needsShore = cargo.Info.UnloadTerrainTypes.Count > 0;
			var passengerMobile = cargo.Passengers.Select(p => p.TraitOrDefault<Mobile>()).FirstOrDefault(m => m != null);
			if (needsShore && passengerMobile == null)
				return null;

			var pathFinder = self.World.WorldActor.Trait<IPathFinder>();
			var map = self.World.Map;
			foreach (var cell in map.FindTilesInCircle(target, info.ShoreSearchRadius))
			{
				if (!CanUnloadAt(self, cell))
					continue;

				// Land beside it (the ship can't go there) that the passenger can step onto, not a building.
				if (needsShore && !CVec.Directions.Select(d => cell + d).Any(c => map.Contains(c) &&
						!Passable(mobile.Locomotor, c) && passengerMobile.CanEnterCell(c, null, BlockedByActor.Immovable)))
					continue;

				if (pathFinder.PathExistsForLocomotor(mobile.Locomotor, self.Location, cell))
					return cell;
			}

			return null;
		}
	}
}
