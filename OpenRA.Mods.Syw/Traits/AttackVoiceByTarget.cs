using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("Picks the voice for attack orders by target: an allied target (the Monk's heal) gets AllyVoice, anyone else",
		"EnemyVoice. Give the attack trait an empty Voice so only this one speaks.")]
	public class AttackVoiceByTargetInfo : TraitInfo
	{
		[VoiceReference]
		public readonly string AllyVoice = "Heal";

		[VoiceReference]
		public readonly string EnemyVoice = "Attack";

		public override object Create(ActorInitializer init) => new AttackVoiceByTarget(this);
	}

	public class AttackVoiceByTarget : IOrderVoice
	{
		readonly AttackVoiceByTargetInfo info;

		public AttackVoiceByTarget(AttackVoiceByTargetInfo info) { this.info = info; }

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			if (order.OrderString != "Attack" && order.OrderString != "ForceAttack")
				return null;

			var owner = order.Target.Type switch
			{
				TargetType.Actor => order.Target.Actor.Owner,
				TargetType.FrozenActor => order.Target.FrozenActor.Owner,
				_ => null,
			};

			return owner != null && self.Owner.IsAlliedWith(owner) ? info.AllyVoice : info.EnemyVoice;
		}
	}
}
