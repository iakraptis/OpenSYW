using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("AI base building for Peasant-built factions (no construction yard queue).",
		"Picks the building type furthest below its desired share of the base, finds a site near the base centre",
		"and sends a free builder with the same SywBuildAt order the player's build palette issues.")]
	public class PeasantBaseBuilderBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Units with the Builder trait the AI may use.")]
		public readonly HashSet<string> BuilderTypes = new() { "peasant" };

		[Desc("Buildings the base is centred on (the first one found is used).")]
		public readonly HashSet<string> BaseCenterTypes = new() { "hq" };

		[Desc("Desired share of each building type in the base (relative weights). Types not listed are never built.")]
		public readonly Dictionary<string, int> BuildingFractions = new();

		[Desc("Maximum number of each building type.")]
		public readonly Dictionary<string, int> BuildingLimits = new();

		[Desc("Ticks between building decisions.")]
		public readonly int DecisionInterval = 60;

		[Desc("How many buildings may be under construction at once.")]
		public readonly int MaxConcurrentConstructions = 1;

		[Desc("Search annulus around the base centre, in cells.")]
		public readonly int MinBaseRadius = 3;
		public readonly int MaxBaseRadius = 18;

		[Desc("Free cells kept around each building so units can pass and exits stay clear.")]
		public readonly int Spacing = 1;

		[Desc("How many of the nearest valid sites to choose from at random.")]
		public readonly int SiteChoices = 6;

		[Desc("Economy first: no new buildings until the AI owns at least this many of HarvesterTypes.")]
		public readonly int MinimumHarvesters = 4;

		[Desc("Units counted for MinimumHarvesters.")]
		public readonly HashSet<string> HarvesterTypes = new() { "peasant", "bull" };

		[Desc("Cash kept back after paying for a building, so unit production (workers) never starves.")]
		public readonly int CashReserve = 500;

		[Desc("Drop-off buildings (like C&C refineries): placed next to the crop field nearest the base instead of",
			"around the base centre.")]
		public readonly HashSet<string> ResourceBuildingTypes = new() { "mill" };

		[Desc("How far from the base to look for crop fields for ResourceBuildingTypes, in cells.")]
		public readonly int ResourceSearchRadius = 30;

		[Desc("Towers: placed next to one of the DefendedBuildingTypes instead of around the base centre, at the one",
			"with the fewest towers nearby.")]
		public readonly HashSet<string> DefenseBuildingTypes = new();

		[Desc("Buildings the DefenseBuildingTypes guard (the Mills out at the crop fields).")]
		public readonly HashSet<string> DefendedBuildingTypes = new();

		[Desc("Distance from the guarded building for towers, in cells.")]
		public readonly int DefenseMinRadius = 2;
		public readonly int DefenseMaxRadius = 5;

		public override object Create(ActorInitializer init) { return new PeasantBaseBuilderBotModule(init.Self, this); }
	}

	public class PeasantBaseBuilderBotModule : ConditionalTrait<PeasantBaseBuilderBotModuleInfo>, IBotTick
	{
		readonly World world;
		readonly Player player;
		PlayerResources resources;
		TechTree techTree;
		IResourceLayer resourceLayer;
		int ticks;

		public PeasantBaseBuilderBotModule(Actor self, PeasantBaseBuilderBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void Created(Actor self)
		{
			resources = player.PlayerActor.Trait<PlayerResources>();
			techTree = player.PlayerActor.Trait<TechTree>();
			resourceLayer = world.WorldActor.TraitOrDefault<IResourceLayer>();
			base.Created(self);
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (++ticks % Info.DecisionInterval != 0)
				return;

			var builders = world.ActorsHavingTrait<Builder>()
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead && Info.BuilderTypes.Contains(a.Info.Name))
				.ToList();

			if (builders.Count(b => b.Trait<Builder>().IsBusy) >= Info.MaxConcurrentConstructions)
				return;

			var free = builders.Where(b => !b.Trait<Builder>().IsBusy).ToList();
			if (free.Count == 0)
				return;

			// Economy first: grow the workforce before spending on buildings. Builders inside a construction
			// site are out of the world but still count.
			var harvesters = world.Actors.Count(a => a.Owner == player && !a.IsDead && Info.HarvesterTypes.Contains(a.Info.Name));
			if (harvesters < Info.MinimumHarvesters)
				return;

			// Try building types in priority order: a type with no valid site (e.g. a 3x3 building in a cramped
			// base) must not block everything else. Wait for money only for the first buildable choice.
			var baseCenter = BaseCenter(free);
			foreach (var type in ChooseBuildings(free[0].Trait<Builder>()))
			{
				var actorInfo = world.Map.Rules.Actors[type];
				var cost = actorInfo.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0;
				if (resources.GetCashAndResources() < cost + Info.CashReserve)
					return;

				var center = baseCenter;
				var minRadius = Info.MinBaseRadius;
				var maxRadius = Info.MaxBaseRadius;
				if (Info.ResourceBuildingTypes.Contains(type) && NearestField(center) is CPos field)
				{
					center = field;
					minRadius = 1;
					maxRadius = 6;
				}
				else if (Info.DefenseBuildingTypes.Contains(type) && LeastDefended() is CPos guarded)
				{
					center = guarded;
					minRadius = Info.DefenseMinRadius;
					maxRadius = Info.DefenseMaxRadius;
				}

				var builder = free.OrderBy(b => b.IsIdle ? 0 : 1).ThenBy(b => (b.Location - center).LengthSquared).First();
				var cell = FindSite(builder, actorInfo, center, minRadius, maxRadius);
				if (cell == null)
				{
					AIUtils.BotDebug("{0}: no site for {1}, trying the next building type.", player, type);
					continue;
				}

				AIUtils.BotDebug("{0}: building {1} at {2}.", player, type, cell.Value);
				bot.QueueOrder(new Order(Builder.OrderID, builder, Target.FromCell(world, cell.Value), false) { TargetString = type });
				return;
			}
		}

		IEnumerable<string> ChooseBuildings(Builder builder)
		{
			var counts = world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && !a.IsDead)
				.GroupBy(a => a.Info.Name)
				.ToDictionary(g => g.Key, g => g.Count());

			int Count(string t) => counts.TryGetValue(t, out var n) ? n : 0;

			// The type whose (count + 1) / weight is smallest is furthest below its desired share.
			return Info.BuildingFractions
				.Where(kv => kv.Value > 0 && builder.Info.Types.Contains(kv.Key) && world.Map.Rules.Actors.ContainsKey(kv.Key)
					&& (!Info.BuildingLimits.TryGetValue(kv.Key, out var limit) || Count(kv.Key) < limit))
				.Where(kv =>
				{
					var prereqs = world.Map.Rules.Actors[kv.Key].TraitInfoOrDefault<BuildableInfo>()?.Prerequisites;
					return prereqs == null || prereqs.Length == 0 || techTree.HasPrerequisites(prereqs);
				})
				.OrderBy(kv => (Count(kv.Key) + 1) * 1000 / kv.Value)
				.ThenByDescending(kv => kv.Value)
				.Select(kv => kv.Key)
				.ToList();
		}

		CPos BaseCenter(List<Actor> builders)
		{
			var hq = world.ActorsHavingTrait<Building>()
				.FirstOrDefault(a => a.Owner == player && !a.IsDead && Info.BaseCenterTypes.Contains(a.Info.Name));
			if (hq != null)
				return hq.Location + new CVec(1, 1);

			var anyBuilding = world.ActorsHavingTrait<Building>().FirstOrDefault(a => a.Owner == player && !a.IsDead);
			return anyBuilding?.Location ?? builders[0].Location;
		}

		// The guarded building (a Mill) with the fewest of our towers around it, or null if there is none.
		CPos? LeastDefended()
		{
			var towers = world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && !a.IsDead && Info.DefenseBuildingTypes.Contains(a.Info.Name))
				.Select(a => a.Location).ToList();
			var guarded = world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && !a.IsDead && Info.DefendedBuildingTypes.Contains(a.Info.Name))
				.Select(a => a.Location + new CVec(1, 1))
				.OrderBy(c => towers.Count(t => (t - c).LengthSquared <= 64))
				.ToList();
			return guarded.Count == 0 ? null : guarded[0];
		}

		// Nearest crop cell to the base that no other drop-off already serves.
		CPos? NearestField(CPos center)
		{
			if (resourceLayer == null)
				return null;

			var dropOffs = world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && !a.IsDead && Info.ResourceBuildingTypes.Contains(a.Info.Name))
				.Select(a => a.Location).ToList();

			foreach (var cell in world.Map.FindTilesInCircle(center, Info.ResourceSearchRadius))
				if (resourceLayer.GetResource(cell).Type != null && dropOffs.All(d => (d - cell).LengthSquared > 64))
					return cell;

			return null;
		}

		CPos? FindSite(Actor builder, ActorInfo actorInfo, CPos center, int minRadius, int maxRadius)
		{
			var buildingInfo = actorInfo.TraitInfo<BuildingInfo>();
			var valid = new List<CPos>();
			foreach (var cell in world.Map.FindTilesInAnnulus(center, minRadius, maxRadius))
			{
				if (!CoastalBuilding.CanPlace(world, builder, actorInfo, buildingInfo, cell) || !Clear(buildingInfo, cell))
					continue;

				valid.Add(cell);
				if (valid.Count >= Info.SiteChoices)
					break;
			}

			return valid.Count == 0 ? null : valid.Random(world.LocalRandom);
		}

		// Keeps Spacing free cells around the footprint (no other buildings) and never covers crop fields.
		bool Clear(BuildingInfo info, CPos topLeft)
		{
			var footprint = info.Tiles(topLeft).ToList();
			if (resourceLayer != null && footprint.Any(c => resourceLayer.GetResource(c).Type != null))
				return false;

			var s = Info.Spacing;
			foreach (var c in footprint)
				for (var dx = -s; dx <= s; dx++)
					for (var dy = -s; dy <= s; dy++)
					{
						var n = c + new CVec(dx, dy);
						if (world.Map.Contains(n) && world.ActorMap.GetActorsAt(n).Any(a => a.TraitOrDefault<Building>() != null))
							return false;
					}

			return true;
		}
	}
}
