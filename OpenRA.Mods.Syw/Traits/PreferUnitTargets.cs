using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("While the actor is hitting a low-priority target it picked by itself (a building, found by an idle scan, an",
		"attack-move or retaliation), drops it as soon as an enemy unit it can attack is within its AutoTarget scan range,",
		"so it turns on the soldiers instead of finishing the wall. AutoTarget alone only chooses when the actor is idle.",
		"Attacks the player ordered directly are never interrupted.")]
	public class PreferUnitTargetsInfo : TraitInfo, Requires<AutoTargetInfo>
	{
		[Desc("Target types that are given up when an enemy unit comes in range.")]
		public readonly BitSet<TargetableType> LowPriorityTargets = new("Structure");

		[Desc("Ticks between two checks.")]
		public readonly int Interval = 10;

		[Desc("A shot older than this many ticks no longer counts as the current target.")]
		public readonly int ShotMemory = 200;

		public override object Create(ActorInitializer init) => new PreferUnitTargets(this);
	}

	public class PreferUnitTargets : INotifyCreated, ITick, IResolveOrder, INotifyIdle, INotifyAttack
	{
		readonly PreferUnitTargetsInfo info;
		AutoTarget autoTarget;

		// Set while the actor carries out the player's own Attack order; cleared once it goes idle.
		bool ordered;
		Target lastShot = Target.Invalid;
		int lastShotTick;
		int countdown;

		public PreferUnitTargets(PreferUnitTargetsInfo info) { this.info = info; }

		void INotifyCreated.Created(Actor self) { autoTarget = self.Trait<AutoTarget>(); }

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			ordered = order.OrderString == "Attack" || order.OrderString == "ForceAttack";
		}

		void INotifyIdle.TickIdle(Actor self)
		{
			ordered = false;
			lastShot = Target.Invalid;
		}

		void INotifyAttack.Attacking(Actor self, in Target target, Armament a, Barrel barrel)
		{
			lastShot = target;
			lastShotTick = self.World.WorldTick;
		}

		void INotifyAttack.PreparingAttack(Actor self, in Target target, Armament a, Barrel barrel) { }

		void ITick.Tick(Actor self)
		{
			if (--countdown > 0)
				return;

			countdown = info.Interval;
			if (ordered || self.IsIdle || autoTarget.IsTraitDisabled || autoTarget.Stance < UnitStance.Defend
				|| self.World.WorldTick - lastShotTick > info.ShotMemory || !IsLowPriority(lastShot))
				return;

			var attacks = self.CurrentActivity.ActivitiesImplementing<IActivityNotifyStanceChanged>().OfType<Activity>().ToList();
			if (attacks.Count == 0 || !EnemyUnitInRange(self))
				return;

			// An attack-move scans again as soon as its attack child ends; a plain attack leaves the actor idle, where
			// AutoTarget picks the unit (AutoTargetPriority@UNITS outranks buildings).
			foreach (var attack in attacks)
				attack.Cancel(self);

			lastShot = Target.Invalid;
		}

		bool IsLowPriority(in Target target)
		{
			if (target.Type == TargetType.Actor)
				return !target.Actor.IsDead && target.Actor.GetEnabledTargetTypes().Overlaps(info.LowPriorityTargets);

			return target.Type == TargetType.FrozenActor && target.FrozenActor.TargetTypes.Overlaps(info.LowPriorityTargets);
		}

		bool EnemyUnitInRange(Actor self)
		{
			foreach (var ab in autoTarget.ActiveAttackBases)
			{
				var range = autoTarget.Info.ScanRadius > 0 ? WDist.FromCells(autoTarget.Info.ScanRadius) : ab.GetMaximumRange();
				foreach (var actor in self.World.FindActorsInCircle(self.CenterPosition, range))
				{
					if (actor.IsDead || !actor.IsInWorld || !actor.AppearsHostileTo(self) || !actor.CanBeViewedByPlayer(self.Owner))
						continue;

					var types = actor.GetEnabledTargetTypes();
					if (types.Overlaps(info.LowPriorityTargets) || !autoTarget.HasValidTargetPriority(self, actor.Owner, types))
						continue;

					if (ab.IsReachableTarget(Target.FromActor(actor), autoTarget.AllowMove))
						return true;
				}
			}

			return false;
		}
	}
}
