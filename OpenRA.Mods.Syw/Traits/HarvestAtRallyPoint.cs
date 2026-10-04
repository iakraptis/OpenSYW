using System.Linq;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("A harvester trained while its producer's rally point is on a crop field starts harvesting there once it",
		"has walked to the rally point, instead of standing idle on the field. Any order given before then cancels it.")]
	public class HarvestAtRallyPointInfo : TraitInfo, Requires<HarvesterInfo>
	{
		[Desc("The rally point counts as on a field when a harvestable cell is within this many cells of it.")]
		public readonly int SearchRadius = 1;

		public override object Create(ActorInitializer init) => new HarvestAtRallyPoint(init, this);
	}

	public class HarvestAtRallyPoint : INotifyCreated, INotifyIdle, IResolveOrder
	{
		readonly HarvestAtRallyPointInfo info;
		CPos? field;

		public HarvestAtRallyPoint(ActorInitializer init, HarvestAtRallyPointInfo info)
		{
			this.info = info;

			// The producer's rally path (Production passes it to new units); the unit walks it on creation.
			var path = init.GetOrDefault<RallyPointInit>()?.Value;
			if (path != null && path.Length > 0)
				field = path[^1];
		}

		void INotifyCreated.Created(Actor self)
		{
			if (field == null)
				return;

			var harvester = self.Trait<Harvester>();
			var rally = field.Value;
			var onField = self.World.Map.FindTilesInCircle(rally, info.SearchRadius).Any(harvester.CanHarvestCell);
			if (!onField)
				field = null;
		}

		// First idle after the walk to the rally point (the creation activity keeps the unit busy until then).
		void INotifyIdle.TickIdle(Actor self)
		{
			if (field == null)
				return;

			var cell = field.Value;
			field = null;
			self.QueueActivity(new FindAndDeliverResources(self, cell));
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			field = null;
		}
	}
}
