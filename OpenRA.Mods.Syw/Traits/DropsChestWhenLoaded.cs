using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("Spawns a chest actor (owned by the world owner, collectable by anyone) where the unit dies,",
		"but only if its Harvester was carrying resources.")]
	public class DropsChestWhenLoadedInfo : TraitInfo, Requires<HarvesterInfo>
	{
		[ActorReference]
		[FieldLoader.Require]
		public readonly string Chest = null;

		public override object Create(ActorInitializer init) { return new DropsChestWhenLoaded(this); }
	}

	public class DropsChestWhenLoaded : INotifyKilled
	{
		readonly DropsChestWhenLoadedInfo info;

		public DropsChestWhenLoaded(DropsChestWhenLoadedInfo info) { this.info = info; }

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			if (self.Trait<Harvester>().IsEmpty)
				return;

			var cell = self.Location;
			self.World.AddFrameEndTask(w => w.CreateActor(info.Chest, new TypeDictionary
			{
				new OwnerInit(w.WorldActor.Owner),
				new LocationInit(cell),
			}));
		}
	}
}
