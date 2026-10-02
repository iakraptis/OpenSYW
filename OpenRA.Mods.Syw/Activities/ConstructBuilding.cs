#region Copyright & License Information
/*
 * Part of the OpenSY "Seven Years War" mod.
 */
#endregion

using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Syw.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Activities
{
	/// <summary>
	/// Walks the Peasant up to an unfinished building, then parks it inside (removes it from the world) so the
	/// building's UnderConstruction trait can take over. The Peasant is brought back into the world later by
	/// UnderConstruction once the building finishes. Used both for a just-placed site and to resume an abandoned one.
	/// </summary>
	public class ConstructBuilding : Activity, IActivityInterface
	{
		// Path attempts before giving up on a site the Peasant can't reach (e.g. walled in by other units).
		const int MaxMoveAttempts = 3;

		readonly Actor building;
		int moveAttempts;

		public ConstructBuilding(Actor building)
		{
			this.building = building;
		}

		protected override void OnFirstRun(Actor self)
		{
			self.Trait<Builder>().Claim(building);
		}

		public override bool Tick(Actor self)
		{
			if (IsCanceling || building.IsDead || building.Disposed || !building.IsInWorld)
				return true;

			var underConstruction = building.TraitOrDefault<UnderConstruction>();
			if (underConstruction == null || underConstruction.IsComplete || underConstruction.HasBuilder)
				return true;

			var target = Target.FromActor(building);
			var range = underConstruction.Info.EnterRange;
			if (!target.IsInRange(self.CenterPosition, range))
			{
				if (moveAttempts++ >= MaxMoveAttempts)
					return true;

				QueueChild(self.Trait<IMove>().MoveWithinRange(target, range));
				return false;
			}

			if (underConstruction.TryAssignBuilder(self))
				self.World.AddFrameEndTask(w => w.Remove(self));

			// If the site was taken first, Builder notices the activity is gone and frees this unit.
			return true;
		}
	}
}
