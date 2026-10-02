#region Copyright & License Information
/*
 * Part of the OpenSY "Seven Years War" mod.
 */
#endregion

using System.Collections.Generic;
using OpenRA;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Syw.Activities;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("Warcraft-3-style construction: this actor can be ordered (via the SywBuildAt order, issued by the",
		"BuilderPalette chrome logic) to walk to a cell and construct one of Types there. It is hidden while",
		"construction runs and is freed again once the building's UnderConstruction trait completes.")]
	public class BuilderInfo : TraitInfo
	{
		[Desc("Actor types (from the Rules) this unit is allowed to construct.")]
		public readonly HashSet<string> Types = new();

		public override object Create(ActorInitializer init) { return new Builder(this); }
	}

	public class Builder : IResolveOrder
	{
		public const string OrderID = "SywBuildAt";

		public readonly BuilderInfo Info;

		public bool IsBusy { get; private set; }

		public Builder(BuilderInfo info)
		{
			Info = info;
		}

		public bool CanBuild(string actorType)
		{
			return !IsBusy && Info.Types.Contains(actorType);
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
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

				self.QueueActivity(false, new ConstructBuilding(building));
			});
		}

		/// <summary>Called by UnderConstruction once the building this peasant was parked in completes.</summary>
		public void Release()
		{
			IsBusy = false;
		}

		/// <summary>Called by ConstructBuilding if it has to give up before ever parking (e.g. the site died first).</summary>
		public void Abandon()
		{
			IsBusy = false;
		}
	}
}
