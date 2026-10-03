using System.Linq;
using OpenRA.Activities;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("Japanese Witch spell Earthquake: strikes a ground location with the given weapon (area damage).",
		"Recovered from syw.exe command 0x1e: 35 of 60 power, power 105 over the 5x5 cells around the target.")]
	public class EarthquakeSpellInfo : TraitInfo, Requires<AmmoPoolInfo>, Requires<MobileInfo>, IRulesetLoaded
	{
		public readonly int ManaCost = 60;

		[WeaponReference]
		[FieldLoader.Require]
		public readonly string Weapon = null;

		public WeaponInfo WeaponInfo { get; private set; }

		public void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			WeaponInfo = rules.Weapons[Weapon.ToLowerInvariant()];
		}

		[VoiceReference]
		[Desc("Voice played when the Earthquake spell is ordered.")]
		public readonly string Voice = "Earthquake";

		public override object Create(ActorInitializer init) => new EarthquakeSpell(this);
	}

	public class EarthquakeSpell : IResolveOrder, IOrderVoice
	{
		public const string OrderId = "SywEarthquake";
		public readonly EarthquakeSpellInfo Info;
		public EarthquakeSpell(EarthquakeSpellInfo info) { Info = info; }

		public WDist Range => Info.WeaponInfo.Range;

		static AmmoPool Mana(Actor self) => self.TraitsImplementing<AmmoPool>().First(p => p.Info.Name == "mana");

		public bool CanCast(Actor self) => !self.IsDead && self.IsInWorld && Mana(self).CurrentAmmoCount >= Info.ManaCost;

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			return order.OrderString == OrderId ? Info.Voice : null;
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != OrderId || order.Target.Type != TargetType.Terrain || !CanCast(self))
				return;

			self.QueueActivity(order.Queued, new CastEarthquake(this, order.Target));
		}

		sealed class CastEarthquake : Activity
		{
			readonly EarthquakeSpell spell;
			readonly Target target;
			bool approached;

			public CastEarthquake(EarthquakeSpell spell, Target target)
			{
				this.spell = spell;
				this.target = target;
			}

			public override bool Tick(Actor self)
			{
				if (IsCanceling || !spell.CanCast(self))
					return true;

				if (!target.IsInRange(self.CenterPosition, spell.Range))
				{
					if (approached)
						return true;

					approached = true;
					QueueChild(self.Trait<Mobile>().MoveWithinRange(target, spell.Range, targetLineColor: Color.Red));
					return false;
				}

				self.World.AddFrameEndTask(w =>
				{
					if (self.Disposed || !spell.CanCast(self))
						return;

					Mana(self).TakeAmmo(self, spell.Info.ManaCost);
					spell.Info.WeaponInfo.Impact(target, self);
				});

				return true;
			}
		}
	}
}
