using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("A DockHost that is unavailable while a worker can't stand on its dock cell: another building, a tree or",
		"impassable terrain covers it. Stock DockHost never checks the cell (dock selection searches outward from the",
		"dock cells, ignoring actors), so a worker kept picking a buried dock and walked into the wall forever instead of",
		"using the building's other docks. Units standing on the cell don't count: they move out of the way.")]
	public class BlockableDockHostInfo : DockHostInfo
	{
		public override object Create(ActorInitializer init) => new BlockableDockHost(init.Self, this);
	}

	public class BlockableDockHost : DockHost
	{
		readonly Actor self;

		public BlockableDockHost(Actor self, BlockableDockHostInfo info)
			: base(self, info)
		{
			this.self = self;
		}

		public override bool IsDockingPossible(Actor clientActor, IDockClient client, bool ignoreReservations = false)
		{
			if (!base.IsDockingPossible(clientActor, client, ignoreReservations))
				return false;

			var mobile = clientActor.TraitOrDefault<Mobile>();
			return mobile == null || mobile.CanEnterCell(self.World.Map.CellContaining(DockPosition), clientActor, BlockedByActor.Immovable);
		}
	}
}
