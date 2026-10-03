using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("Shortens this actor's vision (RevealsShroud) by Penalty while the world's DayNightCycle says it is night.")]
	public class NightVisionPenaltyInfo : TraitInfo, Requires<RevealsShroudInfo>
	{
		[Desc("Vision lost at night.")]
		public readonly WDist Penalty = WDist.FromCells(1);

		public override object Create(ActorInitializer init) { return new NightVisionPenalty(init.Self, this); }
	}

	public class NightVisionPenalty : IRevealsShroudModifier, INotifyCreated
	{
		readonly int nightModifier;
		DayNightCycle cycle;

		public NightVisionPenalty(Actor self, NightVisionPenaltyInfo info)
		{
			// The engine only takes percentages, so turn "range minus Penalty" into one for this actor's range, rounded
			// up so the night range is never shorter than asked (at most a few hundredths of a cell longer).
			var range = self.Info.TraitInfos<RevealsShroudInfo>().Max(r => r.Range.Length);
			var night = System.Math.Max(0, range - info.Penalty.Length);
			nightModifier = range > 0 ? (int)((night * 100L + range - 1) / range) : 100;
		}

		void INotifyCreated.Created(Actor self)
		{
			cycle = self.World.WorldActor.TraitOrDefault<DayNightCycle>();
		}

		int IRevealsShroudModifier.GetRevealsShroudModifier() => cycle != null && cycle.IsNight ? nightModifier : 100;
	}
}
