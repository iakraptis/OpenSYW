using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Day and night: day, dusk, night, dawn, repeating. Fades the TintPostProcessEffect between full daylight and",
		"NightTint, and while it is night reports IsNight (NightVisionPenalty) and grants Condition to the world actor.",
		"Driven by the world tick, so every client agrees on the time of day.")]
	public class DayNightCycleInfo : TraitInfo, Requires<TintPostProcessEffectInfo>
	{
		[Desc("Ticks of full daylight (25 ticks = 1 second at normal speed).")]
		public readonly int DayLength = 9000;

		[Desc("Ticks of full night.")]
		public readonly int NightLength = 4500;

		[Desc("Ticks of each fade, dusk and dawn. Night starts and ends halfway through a fade.")]
		public readonly int TransitionLength = 750;

		[Desc("Ticks into the cycle when the game starts: 0 is the start of the day.")]
		public readonly int StartTime = 0;

		[Desc("Red, green and blue multipliers at full night (1 = daylight).")]
		public readonly float[] NightTint = { 0.55f, 0.6f, 0.85f };

		[GrantedConditionReference]
		[Desc("Condition granted to the world actor while it is night, if set.")]
		public readonly string Condition = null;

		[Desc("Sound played over the whole map when night falls.")]
		public readonly string NightSound = null;

		[Desc("Sound played over the whole map when day breaks.")]
		public readonly string DaySound = null;

		[FluentReference(optional: true)]
		public readonly string NightNotification = null;

		[FluentReference(optional: true)]
		public readonly string DayNotification = null;

		public override object Create(ActorInitializer init) { return new DayNightCycle(this); }
	}

	public class DayNightCycle : INotifyCreated, ITick
	{
		readonly DayNightCycleInfo info;
		TintPostProcessEffect tint;
		int token = Actor.InvalidConditionToken;

		public DayNightCycle(DayNightCycleInfo info)
		{
			this.info = info;
		}

		public bool IsNight { get; private set; }

		int CycleLength => info.DayLength + info.NightLength + 2 * info.TransitionLength;

		void INotifyCreated.Created(Actor self)
		{
			tint = self.Trait<TintPostProcessEffect>();
			Update(self, true);
		}

		void ITick.Tick(Actor self) => Update(self, false);

		void Update(Actor self, bool initial)
		{
			// Cycle layout: day, dusk, night, dawn. Darkness runs from 0 (day) to 1 (night).
			var t = (self.World.WorldTick + info.StartTime) % CycleLength;
			var duskStart = info.DayLength;
			var nightStart = duskStart + info.TransitionLength;
			var dawnStart = nightStart + info.NightLength;

			float darkness;
			if (t < duskStart)
				darkness = 0;
			else if (t < nightStart)
				darkness = (float)(t - duskStart) / info.TransitionLength;
			else if (t < dawnStart)
				darkness = 1;
			else
				darkness = 1 - (float)(t - dawnStart) / info.TransitionLength;

			tint.Red = 1 + (info.NightTint[0] - 1) * darkness;
			tint.Green = 1 + (info.NightTint[1] - 1) * darkness;
			tint.Blue = 1 + (info.NightTint[2] - 1) * darkness;

			// Night for gameplay from halfway through dusk to halfway through dawn. Integer ticks only, so it stays in sync.
			var night = t >= duskStart + info.TransitionLength / 2 && t < dawnStart + info.TransitionLength / 2;
			if (night == IsNight)
				return;

			IsNight = night;
			if (night && !string.IsNullOrEmpty(info.Condition))
				token = self.GrantCondition(info.Condition);
			else if (!night && token != Actor.InvalidConditionToken)
				token = self.RevokeCondition(token);

			// No announcement for the state the game starts in.
			if (initial)
				return;

			Game.Sound.Play(SoundType.World, night ? info.NightSound : info.DaySound);
			TextNotificationsManager.AddTransientLine(null, night ? info.NightNotification : info.DayNotification);
		}
	}
}
