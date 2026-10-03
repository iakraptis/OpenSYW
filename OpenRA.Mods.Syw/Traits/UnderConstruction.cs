#region Copyright & License Information
/*
 * Part of the OpenSYW "Seven Years War" mod.
 */
#endregion

using System.Linq;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	/// <summary>
	/// Marker init added only by Builder.ResolveOrder when it spawns a building via a Peasant. Without it,
	/// UnderConstruction treats the actor as already complete - this is what stops map-placed/editor-placed
	/// buildings (which never get a Peasant walking up to "finish" them) from being stuck construcing forever.
	/// </summary>
	public class UnderConstructionInit : RuntimeFlagInit, ISingleInstanceInit
	{
	}

	[Desc("Building starts at minimal HP when placed by a Builder (see the Peasant's Builder trait) and only",
		"completes construction once that unit has walked up and been 'parked' inside it. Warcraft-3-style:",
		"one builder per site, builder is hidden while construction runs, and is freed again once it finishes.",
		"Actors created any other way (map/editor placement) are treated as already complete.")]
	public class UnderConstructionInfo : TraitInfo, Requires<HealthInfo>
	{
		[Desc("Ticks needed to fully construct the building once a builder has arrived.")]
		public readonly int Duration = 500;

		[GrantedConditionReference]
		[Desc("Condition granted to self while construction is incomplete. Use this to disable Production, " +
			"Armament, etc. on the building's other traits via RequiresCondition.")]
		public readonly string Condition = "constructing";

		[GrantedConditionReference]
		[Desc("Granted during the second half of construction, while Condition remains active.")]
		public readonly string StructureCondition = "construction-structure";

		[Desc("Range (in cells) the builder must reach before it is allowed to enter and start construction.")]
		public readonly WDist EnterRange = WDist.FromCells(2);

		public override object Create(ActorInitializer init) { return new UnderConstruction(init, this); }
	}

	public class UnderConstruction : ITick, INotifyRemovedFromWorld
	{
		public readonly UnderConstructionInfo Info;

		readonly Health health;

		int ticksRemaining;
		int conditionToken = Actor.InvalidConditionToken;
		int structureConditionToken = Actor.InvalidConditionToken;

		public bool IsComplete { get; private set; }
		public bool HasBuilder => Builder != null;

		/// <summary>The unit parked inside, building it, or null.</summary>
		public Actor Builder { get; private set; }

		public UnderConstruction(ActorInitializer init, UnderConstructionInfo info)
		{
			Info = info;
			health = init.Self.Trait<Health>();
			ticksRemaining = info.Duration;

			// Map/editor-placed actors (no UnderConstructionInit) start already finished.
			if (init.GetOrDefault<UnderConstructionInit>() == null)
			{
				IsComplete = true;
				return;
			}

			if (!string.IsNullOrEmpty(info.Condition))
				conditionToken = init.Self.GrantCondition(info.Condition);
		}

		/// <summary>Called by the Builder's ConstructBuilding activity once it has walked up to the site.</summary>
		public bool TryAssignBuilder(Actor peasant)
		{
			if (IsComplete || Builder != null)
				return false;

			Builder = peasant;
			return true;
		}

		void ITick.Tick(Actor self)
		{
			if (IsComplete || Builder == null)
				return;

			if (--ticksRemaining <= 0)
			{
				Complete(self);
				return;
			}

			// Heal linearly from whatever HP we started at up to MaxHP over Duration ticks.
			// Visual progress follows elapsed building time, not HP: damage cannot
			// push the construction art back to scaffolding.
			if ((long)(Info.Duration - ticksRemaining) * 2 >= Info.Duration &&
				structureConditionToken == Actor.InvalidConditionToken && !string.IsNullOrEmpty(Info.StructureCondition))
				structureConditionToken = self.GrantCondition(Info.StructureCondition);

			var targetHp = (long)health.MaxHP * (Info.Duration - ticksRemaining) / Info.Duration;
			var heal = (int)targetHp - health.HP;
			if (heal > 0)
				self.InflictDamage(self, new Damage(-heal));
		}

		void Complete(Actor self)
		{
			IsComplete = true;

			if (structureConditionToken != Actor.InvalidConditionToken)
			{
				self.RevokeCondition(structureConditionToken);
				structureConditionToken = Actor.InvalidConditionToken;
			}

			var heal = health.MaxHP - health.HP;
			if (heal > 0)
				self.InflictDamage(self, new Damage(-heal));

			if (conditionToken != Actor.InvalidConditionToken)
			{
				self.RevokeCondition(conditionToken);
				conditionToken = Actor.InvalidConditionToken;
			}

			ReleaseBuilder(self);
		}

		void ReleaseBuilder(Actor self)
		{
			if (Builder == null)
				return;

			var freedBuilder = Builder;
			Builder = null;

			self.World.AddFrameEndTask(w =>
			{
				if (freedBuilder.IsDead || freedBuilder.Disposed)
					return;

				var cell = FindFreeAdjacentCell(self, freedBuilder);
				w.Add(freedBuilder);
				freedBuilder.Trait<IPositionable>().SetPosition(freedBuilder, cell);
				freedBuilder.Trait<Builder>().Release();
			});
		}

		static CPos FindFreeAdjacentCell(Actor building, Actor peasant)
		{
			var positionable = peasant.Trait<IPositionable>();
			var candidates = Util.AdjacentCells(building.World, Target.FromActor(building))
				.OrderBy(c => (c - building.Location).LengthSquared);

			foreach (var cell in candidates)
				if (positionable.CanEnterCell(cell))
					return cell;

			// Fallback: nothing free nearby, just drop them on the building's own cell.
			return building.Location;
		}

		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			// The construction site was destroyed/sold/removed while a builder was parked inside it -
			// there is no sensible "outside" position to eject them to, so they go down with the building.
			if (Builder != null && !Builder.IsDead && !Builder.Disposed)
				Builder.Dispose();

			Builder = null;
		}
	}
}
