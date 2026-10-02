using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("Monk spell Disturb: spends mana to grant a condition for a while. The condition enables a DetectCloaked",
		"on the caster, revealing invisible enemies (mines) around it. The original spell re-shrouded the area for enemies.")]
	public class DisturbInfo : TraitInfo, Requires<AmmoPoolInfo>
	{
		public readonly int ManaCost = 40;

		[Desc("How long the detection lasts, in ticks (125 = 5 seconds at normal speed).")]
		public readonly int Duration = 125;

		[Desc("Radius shown in the tooltip; the detection radius itself is set on the DetectCloaked trait.")]
		public readonly WDist Range = WDist.FromCells(5);

		[GrantedConditionReference]
		[FieldLoader.Require]
		public readonly string Condition = null;

		public override object Create(ActorInitializer init) => new Disturb(this);
	}

	public class Disturb : IResolveOrder, ITick
	{
		public const string OrderId = "SywDisturb";
		public readonly DisturbInfo Info;
		int token = Actor.InvalidConditionToken;
		int remaining;

		public Disturb(DisturbInfo info) { Info = info; }

		static AmmoPool Mana(Actor self) => self.TraitsImplementing<AmmoPool>().First(p => p.Info.Name == "mana");

		public bool Active => token != Actor.InvalidConditionToken;

		public bool CanCast(Actor self) => !self.IsDead && self.IsInWorld && !Active && Mana(self).CurrentAmmoCount >= Info.ManaCost;

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != OrderId || !CanCast(self))
				return;

			Mana(self).TakeAmmo(self, Info.ManaCost);
			token = self.GrantCondition(Info.Condition);
			remaining = Info.Duration;
		}

		void ITick.Tick(Actor self)
		{
			if (!Active || --remaining > 0)
				return;

			token = self.RevokeCondition(token);
		}
	}
}
