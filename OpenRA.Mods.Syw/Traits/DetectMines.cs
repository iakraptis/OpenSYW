using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("Japanese Priest spell Detect mine: clears every mine around the caster, whoever owns it.",
		"Recovered from syw.exe command 0x21 (11x11 cells, 25 of 60 power).")]
	public class DetectMinesInfo : TraitInfo, Requires<AmmoPoolInfo>
	{
		public readonly int ManaCost = 40;
		public readonly WDist Range = new(5632);

		[ActorReference]
		public readonly string[] MineActors = { "syw-mine", "syw-mine-building" };

		public override object Create(ActorInitializer init) => new DetectMines(this);
	}

	public class DetectMines : IResolveOrder
	{
		public const string OrderId = "SywDetectMines";
		public readonly DetectMinesInfo Info;
		public DetectMines(DetectMinesInfo info) { Info = info; }

		static AmmoPool Mana(Actor self) => self.TraitsImplementing<AmmoPool>().First(p => p.Info.Name == "mana");

		public bool CanCast(Actor self) => !self.IsDead && self.IsInWorld && Mana(self).CurrentAmmoCount >= Info.ManaCost;

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != OrderId || !CanCast(self))
				return;

			self.World.AddFrameEndTask(w =>
			{
				if (self.Disposed || !CanCast(self))
					return;

				Mana(self).TakeAmmo(self, Info.ManaCost);

				// Defused: removed without detonating, so the caster is never caught in the blasts.
				foreach (var mine in w.FindActorsInCircle(self.CenterPosition, Info.Range)
					.Where(a => !a.IsDead && a.IsInWorld && Info.MineActors.Contains(a.Info.Name)).ToList())
					mine.Dispose();
			});
		}
	}
}
