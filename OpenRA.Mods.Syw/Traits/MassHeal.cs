using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("Monk spell: spends the whole (full) mana pool to heal every allied unit around the caster at once.")]
	public class MassHealInfo : TraitInfo, Requires<AmmoPoolInfo>
	{
		[Desc("HP restored to each unit.")]
		public readonly int Amount = 6000;
		public readonly WDist Range = WDist.FromCells(4);
		[VoiceReference]
		[Desc("Voice played when the Mass Heal spell is ordered.")]
		public readonly string Voice = "MassHeal";

		public override object Create(ActorInitializer init) => new MassHeal(this);
	}

	public class MassHeal : IResolveOrder, IOrderVoice
	{
		public const string OrderId = "SywMassHeal";
		public readonly MassHealInfo Info;
		public MassHeal(MassHealInfo info) { Info = info; }

		static AmmoPool Mana(Actor self) => self.TraitsImplementing<AmmoPool>().First(p => p.Info.Name == "mana");

		public bool CanCast(Actor self)
		{
			if (self.IsDead || !self.IsInWorld)
				return false;

			var mana = Mana(self);
			return mana.CurrentAmmoCount >= mana.Info.Ammo;
		}

		// Units only (mobile or aircraft), so buildings and mines are never affected.
		static bool IsUnit(Actor a) => a.Info.HasTraitInfo<MobileInfo>() || a.Info.HasTraitInfo<AircraftInfo>();

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			return order.OrderString == OrderId ? Info.Voice : null;
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != OrderId || !CanCast(self))
				return;

			self.World.AddFrameEndTask(w =>
			{
				if (self.Disposed || !CanCast(self))
					return;

				var mana = Mana(self);
				mana.TakeAmmo(self, mana.CurrentAmmoCount);
				var targets = w.FindActorsInCircle(self.CenterPosition, Info.Range)
					.Where(a => !a.IsDead && a.IsInWorld && self.Owner.IsAlliedWith(a.Owner) && IsUnit(a))
					.ToList();

				foreach (var a in targets)
				{
					var health = a.TraitOrDefault<Health>();
					if (health != null && health.HP < health.MaxHP)
						health.InflictDamage(a, self, new Damage(-Info.Amount), true);
				}
			});
		}
	}
}
