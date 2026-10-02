#region Copyright & License Information
/*
 * Part of the OpenSY "Seven Years War" mod.
 */
#endregion

using OpenRA;
using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Syw.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Activities
{
	/// <summary>
	/// Walks the Peasant up to a just-created (1 HP, "constructing") building, then parks it inside
	/// (removes it from the world) so the building's UnderConstruction trait can take over. The Peasant
	/// is brought back into the world later by UnderConstruction once the building finishes.
	/// </summary>
	public class ConstructBuilding : Activity
	{
		readonly Actor building;
		bool moveQueued;
		bool parked;

		public ConstructBuilding(Actor building)
		{
			this.building = building;
		}

		public override bool Tick(Actor self)
		{
			if (building.IsDead || building.Disposed || !building.IsInWorld)
				return true;

			if (!moveQueued)
			{
				moveQueued = true;
				var move = self.Trait<IMove>();
				var range = building.TraitOrDefault<UnderConstruction>()?.Info.EnterRange ?? WDist.FromCells(2);
				QueueChild(move.MoveWithinRange(Target.FromActor(building), range));
				return false;
			}

			var underConstruction = building.TraitOrDefault<UnderConstruction>();
			if (underConstruction != null && underConstruction.TryAssignBuilder(self))
			{
				parked = true;
				self.World.AddFrameEndTask(w => w.Remove(self));
			}

			return true;
		}

		protected override void OnLastRun(Actor self)
		{
			if (!parked)
				self.Trait<Builder>().Abandon();
		}
	}
}
