using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("Stock DockHost hands a unit docked at a captured building to the captor (C&C steals the harvester at a",
		"refinery). SYW doesn't: workers unloading at a captured HQ or Mill stay with their owner.")]
	public class KeepDockedWorkersOnCaptureInfo : TraitInfo
	{
		[Desc("Workers this close to the building are checked.")]
		public readonly WDist Range = WDist.FromCells(4);

		public override object Create(ActorInitializer init) => new KeepDockedWorkersOnCapture(this);
	}

	public class KeepDockedWorkersOnCapture : INotifyOwnerChanged
	{
		readonly KeepDockedWorkersOnCaptureInfo info;

		public KeepDockedWorkersOnCapture(KeepDockedWorkersOnCaptureInfo info) { this.info = info; }

		// Ownership changes first; DockHost's capture handler then transfers the docked unit. Remember the old
		// owner's nearby workers now, and give back any of them that changed hands once the frame ends.
		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			var workers = self.World.FindActorsInCircle(self.CenterPosition, info.Range)
				.Where(a => a.Owner == oldOwner && a != self && a.Info.HasTraitInfo<DockClientManagerInfo>())
				.ToList();
			if (workers.Count == 0)
				return;

			self.World.AddFrameEndTask(w =>
			{
				foreach (var worker in workers)
				{
					if (worker.IsDead || worker.Owner != newOwner)
						continue;

					worker.CancelActivity();
					worker.ChangeOwnerSync(oldOwner);
				}
			});
		}
	}
}
