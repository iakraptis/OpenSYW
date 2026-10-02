using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Random rain showers. Rolls the synced shared random every CheckInterval ticks; while it rains the world actor",
		"gets Condition (drive WeatherOverlay with it) and RegrowsResources grows crops faster.")]
	public class RainControllerInfo : TraitInfo
	{
		[Desc("Ticks between rain rolls while it is dry.")]
		public readonly int CheckInterval = 1500;

		[Desc("Percent chance of rain starting at each roll.")]
		public readonly int Chance = 8;

		[Desc("Minimum and maximum shower length in ticks.")]
		public readonly int[] Duration = { 1500, 3000 };

		[GrantedConditionReference]
		[Desc("Condition granted to the world actor while it rains.")]
		public readonly string Condition = "raining";

		[FluentReference(optional: true)]
		public readonly string StartNotification = null;

		[FluentReference(optional: true)]
		public readonly string StopNotification = null;

		public override object Create(ActorInitializer init) { return new RainController(this); }
	}

	public class RainController : ITick
	{
		readonly RainControllerInfo info;
		int ticks;
		int token = Actor.InvalidConditionToken;

		public RainController(RainControllerInfo info)
		{
			this.info = info;
			ticks = info.CheckInterval;
		}

		public bool IsRaining => token != Actor.InvalidConditionToken;

		void ITick.Tick(Actor self)
		{
			if (--ticks > 0)
				return;

			if (IsRaining)
			{
				token = self.RevokeCondition(token);
				ticks = info.CheckInterval;
				TextNotificationsManager.AddTransientLine(null, info.StopNotification);
				return;
			}

			// SharedRandom keeps every client in sync.
			if (self.World.SharedRandom.Next(100) < info.Chance)
			{
				token = self.GrantCondition(info.Condition);
				ticks = self.World.SharedRandom.Next(info.Duration[0], info.Duration[1] + 1);
				TextNotificationsManager.AddTransientLine(null, info.StartNotification);
			}
			else
				ticks = info.CheckInterval;
		}
	}
}
