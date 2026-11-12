using System.Linq;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Warheads
{
	[Desc("SpreadDamage that also hurts the shooter's own and allied actors, at a reduced share: cannon shells and rockets",
		"don't pick who stands in the blast. Add Ally to ValidRelationships for it to apply.")]
	public class SplashDamageWarhead : SpreadDamageWarhead
	{
		[Desc("Percentage of the damage dealt to own and allied actors.")]
		public readonly int AllyDamage = 75;

		protected override void InflictDamage(Actor victim, Actor firedBy, HitShape shape, WarheadArgs args)
		{
			if (firedBy.Owner.RelationshipWith(victim.Owner) == PlayerRelationship.Ally)
				args = new WarheadArgs(args) { DamageModifiers = args.DamageModifiers.Append(AllyDamage).ToArray() };

			base.InflictDamage(victim, firedBy, shape, args);
		}
	}
}
