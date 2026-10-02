#region Copyright & License Information
/*
 * Part of the OpenSY "Seven Years War" mod.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Syw.Activities;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("Warcraft-3-style construction: this actor can be ordered (via the SywBuildAt order, issued by the",
		"BuilderPalette chrome logic) to walk to a cell and construct one of Types there. It is hidden while",
		"construction runs and is freed again once the building's UnderConstruction trait completes.",
		"Right-clicking an unfinished site that has no builder (SywResumeBuild) sends this unit to finish it.")]
	public class BuilderInfo : TraitInfo
	{
		[Desc("Actor types (from the Rules) this unit is allowed to construct.")]
		public readonly HashSet<string> Types = new();

		public override object Create(ActorInitializer init) { return new Builder(this); }
	}

	public class Builder : IIssueOrder, IResolveOrder, ITick
	{
		public const string OrderID = "SywBuildAt";
		public const string ResumeOrderID = "SywResumeBuild";

		public readonly BuilderInfo Info;

		public bool IsBusy { get; private set; }

		/// <summary>The construction site this unit is walking to or parked in, or null.</summary>
		public Actor Site { get; private set; }

		public Builder(BuilderInfo info)
		{
			Info = info;
		}

		public bool CanBuild(string actorType)
		{
			return !IsBusy && Info.Types.Contains(actorType);
		}

		/// <summary>An unfinished site of ours that nobody is building and that this unit is allowed to build.</summary>
		public bool CanResume(Actor self, Actor site)
		{
			if (site == null || site.IsDead || !site.IsInWorld || site.Owner != self.Owner || !Info.Types.Contains(site.Info.Name))
				return false;

			var underConstruction = site.TraitOrDefault<UnderConstruction>();
			return underConstruction != null && !underConstruction.IsComplete && !underConstruction.HasBuilder;
		}

		IEnumerable<IOrderTargeter> IIssueOrder.Orders
		{
			get { yield return new ResumeConstructionOrderTargeter(this); }
		}

		Order IIssueOrder.IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
		{
			return order.OrderID == ResumeOrderID ? new Order(order.OrderID, self, target, queued) : null;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == ResumeOrderID)
			{
				if (order.Target.Type == TargetType.Actor && CanResume(self, order.Target.Actor))
					self.QueueActivity(order.Queued, new ConstructBuilding(order.Target.Actor));

				return;
			}

			if (order.OrderString != OrderID || IsBusy)
				return;

			var buildingName = order.TargetString;
			if (!Info.Types.Contains(buildingName))
				return;

			var world = self.World;
			if (!world.Map.Rules.Actors.TryGetValue(buildingName, out var actorInfo))
				return;

			var buildingInfo = actorInfo.TraitInfoOrDefault<BuildingInfo>();
			if (buildingInfo == null)
				return;

			var targetCell = world.Map.CellContaining(order.Target.CenterPosition);
			if (!CoastalBuilding.CanPlace(world, self, actorInfo, buildingInfo, targetCell))
				return;

			var cost = actorInfo.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0;
			var playerResources = self.Owner.PlayerActor.Trait<PlayerResources>();
			if (!playerResources.TakeCash(cost, true))
				return;

			var buildableInfo = actorInfo.TraitInfoOrDefault<BuildableInfo>();
			if (buildableInfo != null && buildableInfo.Prerequisites.Length > 0)
			{
				var techTree = self.Owner.PlayerActor.Trait<TechTree>();
				if (!techTree.HasPrerequisites(buildableInfo.Prerequisites))
				{
					playerResources.GiveCash(cost);
					return;
				}
			}

			IsBusy = true;
			self.CancelActivity();

			var faction = self.Owner.Faction.InternalName;
			world.AddFrameEndTask(w =>
			{
				var building = w.CreateActor(buildingName, new TypeDictionary
				{
					new LocationInit(targetCell),
					new OwnerInit(self.Owner),
					new FactionInit(faction),
					new HealthInit(1),
					new UnderConstructionInit(),
				});

				Claim(building);
				self.QueueActivity(false, new ConstructBuilding(building));
			});
		}

		/// <summary>Called by ConstructBuilding when it starts heading for a site.</summary>
		public void Claim(Actor site)
		{
			IsBusy = true;
			Site = site;
		}

		/// <summary>Frees this unit: its building completed, or it gave up on the site (see Tick).</summary>
		public void Release()
		{
			IsBusy = false;
			Site = null;
		}

		void ITick.Tick(Actor self)
		{
			// Parked builders are out of the world and never tick.
			if (Site == null || !self.IsInWorld)
				return;

			// Just parked: the removal from the world happens at the end of this tick.
			if (!Site.Disposed && Site.TraitOrDefault<UnderConstruction>()?.Builder == self)
				return;

			// Gave up before parking: site died, path blocked or the order was replaced (even before the
			// activity first ran). The site stays unfinished; any builder can resume it with SywResumeBuild.
			if (self.CurrentActivity == null || !self.CurrentActivity.ActivitiesImplementing<ConstructBuilding>().Any())
				Release();
		}

		sealed class ResumeConstructionOrderTargeter : UnitOrderTargeter
		{
			readonly Builder builder;

			public ResumeConstructionOrderTargeter(Builder builder)
				: base(ResumeOrderID, 7, "enter", false, true)
			{
				this.builder = builder;
			}

			public override bool CanTargetActor(Actor self, Actor target, TargetModifiers modifiers, ref string cursor)
			{
				return builder.CanResume(self, target);
			}

			public override bool CanTargetFrozenActor(Actor self, FrozenActor target, TargetModifiers modifiers, ref string cursor)
			{
				return false;
			}
		}
	}
}
