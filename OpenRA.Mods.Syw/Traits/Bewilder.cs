using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("Japanese Witch spell Bewilderment: takes control of one enemy unit, with a chance to fail.",
		"Recovered from syw.exe command 0x20: 35 of 60 power, 20% failure.")]
	public class BewilderInfo : TraitInfo, Requires<AmmoPoolInfo>, Requires<MobileInfo>
	{
		public readonly int ManaCost = 60;
		public readonly WDist Range = WDist.FromCells(5);

		[Desc("Chance (percent) that the unit changes sides. The mana is spent either way.")]
		public readonly int SuccessPercent = 80;

		public override object Create(ActorInitializer init) => new Bewilder(this);
	}

	public class Bewilder : IResolveOrder, IUnitTargetSpell
	{
		public const string OrderId = "SywBewilder";
		public readonly BewilderInfo Info;
		public Bewilder(BewilderInfo info) { Info = info; }

		public string SpellOrderId => OrderId;

		static AmmoPool Mana(Actor self) => self.TraitsImplementing<AmmoPool>().First(p => p.Info.Name == "mana");

		public bool CanCast(Actor self) => !self.IsDead && self.IsInWorld && Mana(self).CurrentAmmoCount >= Info.ManaCost;

		// Enemy units only (ground, naval or air); never buildings or mines.
		public bool ValidTarget(Actor self, Actor target) => target != null && target != self && !target.IsDead &&
			!target.Disposed && target.IsInWorld && self.Owner.RelationshipWith(target.Owner) == PlayerRelationship.Enemy &&
			(target.Info.HasTraitInfo<MobileInfo>() || target.Info.HasTraitInfo<AircraftInfo>());

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != OrderId || order.Target.Type != TargetType.Actor ||
				!CanCast(self) || !ValidTarget(self, order.Target.Actor))
				return;

			self.QueueActivity(order.Queued, new CastBewilder(this, order.Target.Actor));
		}

		sealed class CastBewilder : Activity
		{
			readonly Bewilder spell;
			readonly Actor target;
			bool approached;

			public CastBewilder(Bewilder spell, Actor target)
			{
				this.spell = spell;
				this.target = target;
			}

			public override bool Tick(Actor self)
			{
				if (IsCanceling || !spell.ValidTarget(self, target) || !spell.CanCast(self))
					return true;

				var t = Target.FromActor(target);
				if (!t.IsInRange(self.CenterPosition, spell.Info.Range))
				{
					if (approached)
						return true;

					approached = true;
					QueueChild(self.Trait<Mobile>().MoveWithinRange(t, spell.Info.Range, targetLineColor: Color.Red));
					return false;
				}

				self.World.AddFrameEndTask(w =>
				{
					if (self.Disposed || !spell.ValidTarget(self, target) || !spell.CanCast(self))
						return;

					Mana(self).TakeAmmo(self, spell.Info.ManaCost);
					if (w.SharedRandom.Next(100) < spell.Info.SuccessPercent)
					{
						target.CancelActivity();
						target.ChangeOwner(self.Owner);
					}
				});

				return true;
			}
		}
	}
}
