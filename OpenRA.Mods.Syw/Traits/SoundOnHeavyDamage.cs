using System;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("Plays a sound when the actor drops below 50% HP (the Heavy damage state), like the engine's",
		"SoundOnDamageTransition, but at most once per Cooldown: a building repaired while under attack keeps crossing",
		"the threshold and would otherwise repeat the sound every few seconds.")]
	public class SoundOnHeavyDamageInfo : TraitInfo
	{
		[FieldLoader.Require]
		[Desc("Play a random sound from this list.")]
		public readonly string[] Sounds = Array.Empty<string>();

		[Desc("Minimum ticks between two plays (25 ticks = 1 second at normal speed).")]
		public readonly int Cooldown = 500;

		public override object Create(ActorInitializer init) => new SoundOnHeavyDamage(this);
	}

	public class SoundOnHeavyDamage : INotifyDamageStateChanged
	{
		readonly SoundOnHeavyDamageInfo info;

		// World tick of the last play; far in the past so the first crossing always plays.
		long lastPlayed = long.MinValue / 2;

		public SoundOnHeavyDamage(SoundOnHeavyDamageInfo info) { this.info = info; }

		void INotifyDamageStateChanged.DamageStateChanged(Actor self, AttackInfo e)
		{
			if (e.DamageState < DamageState.Heavy || e.DamageState == DamageState.Dead || e.PreviousDamageState >= DamageState.Heavy)
				return;

			var now = self.World.WorldTick;
			if (now - lastPlayed < info.Cooldown)
				return;

			lastPlayed = now;
			Game.Sound.Play(SoundType.World, info.Sounds.RandomOrDefault(Game.CosmeticRandom), self.CenterPosition);
		}
	}
}
