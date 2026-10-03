using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("Shaman spell: turns one of the owner's (or an ally's) Peasants into a random infantry unit.")]
	public class ShamanTransformInfo : TraitInfo, Requires<AmmoPoolInfo>, Requires<MobileInfo>
	{
		public readonly int ManaCost = 10;
		[Desc("Cast range. The default 1c512 is melee: the Shaman must stand next to the Peasant (diagonals included).")]
		public readonly WDist Range = new(1536);

		[ActorReference]
		public readonly string[] TargetActors = { "kpeasant" };

		[ActorReference]
		[Desc("One of these is picked at random (synced) for each cast.")]
		public readonly string[] Results = System.Array.Empty<string>();

		public override object Create(ActorInitializer init) => new ShamanTransform(this);
	}

	public class ShamanTransform : IResolveOrder, IUnitTargetSpell
	{
		public const string OrderId = "SywTransform";
		public readonly ShamanTransformInfo Info;
		public ShamanTransform(ShamanTransformInfo info) { Info = info; }

		public string SpellOrderId => OrderId;

		static AmmoPool Mana(Actor self) => self.TraitsImplementing<AmmoPool>().First(p => p.Info.Name == "mana");

		public bool CanCast(Actor self) => !self.IsDead && self.IsInWorld && Info.Results.Length > 0 &&
			Mana(self).CurrentAmmoCount >= Info.ManaCost;

		public bool ValidTarget(Actor self, Actor target) => target != null && target != self && !target.IsDead &&
			!target.Disposed && target.IsInWorld && Info.TargetActors.Contains(target.Info.Name) &&
			self.Owner.IsAlliedWith(target.Owner);

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != OrderId || order.Target.Type != TargetType.Actor ||
				!CanCast(self) || !ValidTarget(self, order.Target.Actor))
				return;

			self.QueueActivity(order.Queued, new CastTransform(this, order.Target.Actor));
		}

		sealed class CastTransform : Activity
		{
			readonly ShamanTransform spell;
			readonly Actor target;
			bool approached;

			public CastTransform(ShamanTransform spell, Actor target)
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
					// Walk once; give up if the Peasant is still out of reach afterwards.
					if (approached)
						return true;

					approached = true;
					QueueChild(self.Trait<Mobile>().MoveWithinRange(t, spell.Info.Range, targetLineColor: Color.Green));
					return false;
				}

				self.World.AddFrameEndTask(w => Transform(self, w));
				return true;
			}

			void Transform(Actor self, World w)
			{
				if (self.Disposed || !spell.ValidTarget(self, target) || !spell.CanCast(self))
					return;

				Mana(self).TakeAmmo(self, spell.Info.ManaCost);
				var result = spell.Info.Results[w.SharedRandom.Next(spell.Info.Results.Length)];
				var health = target.Trait<Health>();
				var init = new TypeDictionary
				{
					new OwnerInit(target.Owner),
					new LocationInit(target.Location),
					new FacingInit(target.Orientation.Yaw),
					new HealthInit(health.HP * 100 / health.MaxHP),
				};

				var mobile = target.TraitOrDefault<Mobile>();
				if (mobile != null)
					init.Add(new SubCellInit(mobile.FromSubCell));

				target.Dispose();
				w.CreateActor(result, init);
			}
		}
	}
}
