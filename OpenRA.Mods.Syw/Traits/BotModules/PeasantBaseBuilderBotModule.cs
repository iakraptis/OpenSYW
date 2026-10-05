using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Pathfinder;
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
		public readonly HashSet<string> BuilderTypes = new() { "kpeasant", "jpeasant" };

		[Desc("Buildings the base is centred on (the first one found is used).")]
		public readonly HashSet<string> BaseCenterTypes = new() { "khq", "jhq" };

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

		[Desc("If above 0, a ResourceBuildingTypes site must be at most this many steps' walk from its crop field (over",
			"passable ground, around cliffs and buildings), and the nearest walk wins. Keeps drop-offs off the far side of a",
			"cliff, where every load would walk around it. 0 picks any free site near the field.")]
		public readonly int MaxDropOffWalk = 0;

		[Desc("Economy first: no new buildings until the AI owns at least this many of HarvesterTypes.")]
		public readonly int MinimumHarvesters = 4;

		[Desc("Units counted for MinimumHarvesters.")]
		public readonly HashSet<string> HarvesterTypes = new() { "kpeasant", "kbull", "jpeasant", "jbull" };

		[Desc("Cash kept back after paying for a building, so unit production (workers) never starves.")]
		public readonly int CashReserve = 500;

		[Desc("Drop-off buildings (like C&C refineries): placed next to the crop field nearest the base instead of",
			"around the base centre.")]
		public readonly HashSet<string> ResourceBuildingTypes = new() { "kmill", "jmill" };

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

		[Desc("Tower build stages: a finished building of the key's type allows up to the value's number of",
			"DefenseBuildingTypes in total (the highest applicable value counts). Until the bot owns any of them it builds",
			"no towers; while it has fewer than allowed, towers come before anything else. Empty: towers follow their shares.")]
		public readonly Dictionary<string, int> TowerSteps = new();

		[Desc("Shipyards: built only when the map needs a navy (an enemy building the army can't walk to, or at least",
			"NavalWaterShare percent of the map is water), on a shore the builders can walk to, and if possible on water",
			"that reaches the enemy's coast.")]
		public readonly HashSet<string> NavalBuildingTypes = new();

		[Desc("Percentage of water on the map from which a navy is wanted even when every enemy can be walked to.")]
		public readonly int NavalWaterShare = 25;

		[Desc("How far from the base to look for a shore for NavalBuildingTypes, in cells.")]
		public readonly int NavalSearchRadius = 30;

		[Desc("Locomotor of the ships, to check that a shipyard's water reaches the enemy.")]
		public readonly string NavalLocomotor = "naval";

		public override object Create(ActorInitializer init) { return new PeasantBaseBuilderBotModule(init.Self, this); }
	}

	public class PeasantBaseBuilderBotModule : ConditionalTrait<PeasantBaseBuilderBotModuleInfo>, IBotTick
	{
		// Crop field spots tried for a drop-off before giving up on that building type for this decision.
		const int FieldCandidateCount = 12;

		// Building decisions between two checks whether the map needs a navy (the answer changes only as enemies die).
		const int NavalCheckDecisions = 10;

		readonly World world;
		readonly Player player;
		PlayerResources resources;
		TechTree techTree;
		IResourceLayer resourceLayer;
		IPathFinder pathFinder;
		int ticks;
		int decisions;
		int waterShare = -1;
		bool navalNeeded;

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
			pathFinder = world.WorldActor.Trait<IPathFinder>();
			base.Created(self);
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (++ticks % Info.DecisionInterval != 0)
				return;

			decisions++;

			var builders = world.ActorsHavingTrait<Builder>()
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead && Info.BuilderTypes.Contains(a.Info.Name))
				.ToList();

			var free = builders.Where(b => !b.Trait<Builder>().IsBusy).ToList();
			if (free.Count == 0)
				return;

			// Finish what is already paid for before starting anything new.
			if (ResumeAbandonedSite(bot, builders, free))
				return;

			if (builders.Count(b => b.Trait<Builder>().IsBusy) >= Info.MaxConcurrentConstructions)
				return;

			// Economy first: grow the workforce before spending on buildings. Builders inside a construction
			// site are out of the world but still count.
			var harvesters = world.Actors.Count(a => a.Owner == player && !a.IsDead && Info.HarvesterTypes.Contains(a.Info.Name));
			if (harvesters < Info.MinimumHarvesters)
				return;

			// Try building types in priority order: a type with no valid site (e.g. a 3x3 building in a cramped
			// base) must not block everything else. Wait for money only for the first buildable choice.
			var baseCenter = BaseCenter(free);
			if (Info.NavalBuildingTypes.Count > 0 && decisions % NavalCheckDecisions == 1)
				navalNeeded = NavalNeeded(free[0]);

			foreach (var type in ChooseBuildings(free[0].Trait<Builder>()))
			{
				var actorInfo = world.Map.Rules.Actors[type];
				var cost = actorInfo.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0;
				if (resources.GetCashAndResources() < cost + Info.CashReserve)
					return;

				// Where to look, in order: a drop-off beside a crop field (the nearest may have no room: trees and cliffs
				// often edge the fields), a tower beside each guarded building from the least defended up (one may be
				// boxed in by fields), anything else around the base.
				IEnumerable<(CPos Center, int Min, int Max)> places = new[] { (baseCenter, Info.MinBaseRadius, Info.MaxBaseRadius) };
				if (Info.ResourceBuildingTypes.Contains(type))
					places = FieldCandidates(baseCenter, free[0]).Select(f => (f, 1, 6));
				else if (Info.NavalBuildingTypes.Contains(type))
					places = ShoreCandidates(baseCenter, free[0]).Select(s => (s, 0, 3));
				else if (Info.DefenseBuildingTypes.Contains(type))
				{
					var guarded = GuardedByFewestTowers();
					if (guarded.Count > 0)
						places = guarded.Select(g => (g, Info.DefenseMinRadius, Info.DefenseMaxRadius));
				}

				var dropOff = Info.ResourceBuildingTypes.Contains(type) && Info.MaxDropOffWalk > 0;
				var site = places
					.Select(p => (Builder: NearestBuilder(free, p.Center), Place: p))
					.Select(c => (Cell: dropOff ? FindDropOffSite(c.Builder, actorInfo, c.Place.Center, c.Place.Max)
						: FindSite(c.Builder, actorInfo, c.Place.Center, c.Place.Min, c.Place.Max), c.Builder))
					.FirstOrDefault(c => c.Cell != null);

				if (site.Cell == null)
				{
					AIUtils.BotDebug("{0}: no site for {1}, trying the next building type.", player, type);
					continue;
				}

				AIUtils.BotDebug("{0}: building {1} at {2}.", player, type, site.Cell.Value);
				bot.QueueOrder(new Order(Builder.OrderID, site.Builder, Target.FromCell(world, site.Cell.Value), false) { TargetString = type });
				return;
			}
		}

		// Sends the nearest free builder to one of our unfinished sites that nobody is building or walking to
		// (its builder was killed, blocked or pulled away). Returns false if there is no such site.
		bool ResumeAbandonedSite(IBot bot, List<Actor> builders, List<Actor> free)
		{
			var claimed = builders.Select(b => b.Trait<Builder>().Site).Where(s => s != null).ToHashSet();
			var site = world.ActorsHavingTrait<UnderConstruction>()
				.FirstOrDefault(a => a.Owner == player && !a.IsDead && a.IsInWorld && !claimed.Contains(a) &&
					free.Any(b => b.Trait<Builder>().CanResume(b, a)));
			if (site == null)
				return false;

			var builder = free.Where(b => b.Trait<Builder>().CanResume(b, site))
				.OrderBy(b => (b.Location - site.Location).LengthSquared).First();
			AIUtils.BotDebug("{0}: resuming abandoned {1} at {2}.", player, site.Info.Name, site.Location);
			bot.QueueOrder(new Order(Builder.ResumeOrderID, builder, Target.FromActor(site), false));
			return true;
		}

		IEnumerable<string> ChooseBuildings(Builder builder)
		{
			var counts = world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && !a.IsDead)
				.GroupBy(a => a.Info.Name)
				.ToDictionary(g => g.Key, g => g.Count());

			int Count(string t) => counts.TryGetValue(t, out var n) ? n : 0;

			// The type whose (count + 1) / weight is smallest is furthest below its desired share.
			var byShare = Info.BuildingFractions
				.Where(kv => kv.Value > 0 && builder.Info.Types.Contains(kv.Key) && world.Map.Rules.Actors.ContainsKey(kv.Key)
					&& (navalNeeded || !Info.NavalBuildingTypes.Contains(kv.Key))
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

			if (Info.TowerSteps.Count == 0)
				return byShare;

			// Tower stages: no towers before the first step's building is finished; below the allowed number, towers
			// first (GuardedByFewestTowers puts each beside the least defended Mill); at it, no more towers for now.
			var allowed = Info.TowerSteps.Where(kv => FinishedCount(kv.Key) > 0).Select(kv => kv.Value).DefaultIfEmpty(0).Max();
			var towers = Info.DefenseBuildingTypes.Sum(Count);
			var others = byShare.Where(t => !Info.DefenseBuildingTypes.Contains(t));
			return towers < allowed ? byShare.Where(Info.DefenseBuildingTypes.Contains).Concat(others).ToList() : others.ToList();
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

		// Each guarded building (a Mill) by centre, with the number of our towers within 8 cells of it.
		List<(CPos Center, int Towers)> GuardedTowers()
		{
			var towers = world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && !a.IsDead && Info.DefenseBuildingTypes.Contains(a.Info.Name))
				.Select(a => a.Location).ToList();
			return world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && !a.IsDead && Info.DefendedBuildingTypes.Contains(a.Info.Name))
				.Select(a => a.Location + new CVec(1, 1))
				.Select(c => (c, towers.Count(t => (t - c).LengthSquared <= 64)))
				.ToList();
		}

		// Our buildings of a type that have finished construction.
		int FinishedCount(string type) => world.ActorsHavingTrait<Building>()
			.Count(a => a.Owner == player && !a.IsDead && a.Info.Name == type && (a.TraitOrDefault<UnderConstruction>()?.IsComplete ?? true));

		// The centres of the guarded buildings, the one with the fewest towers around it first.
		List<CPos> GuardedByFewestTowers() => GuardedTowers().OrderBy(g => g.Towers).Select(g => g.Center).ToList();

		static Actor NearestBuilder(List<Actor> free, CPos center) =>
			free.OrderBy(b => b.IsIdle ? 0 : 1).ThenBy(b => (b.Location - center).LengthSquared).First();

		// Crop cells no drop-off serves yet and the Peasants can walk to (not across a river), nearest the base first,
		// at most FieldCandidateCount of them and spread at least 4 cells apart so each try looks at a different part of
		// a field (or another field).
		IEnumerable<CPos> FieldCandidates(CPos center, Actor peasant)
		{
			if (resourceLayer == null)
				yield break;

			var locomotor = peasant.TraitOrDefault<Mobile>()?.Locomotor;

			var dropOffs = world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && !a.IsDead && Info.ResourceBuildingTypes.Contains(a.Info.Name))
				.Select(a => a.Location).ToList();

			var tried = new List<CPos>();
			foreach (var cell in world.Map.FindTilesInCircle(center, Info.ResourceSearchRadius))
			{
				if (resourceLayer.GetResource(cell).Type == null || dropOffs.Any(d => (d - cell).LengthSquared <= 64)
					|| tried.Any(t => (t - cell).LengthSquared < 16)
					|| (locomotor != null && !pathFinder.PathExistsForLocomotor(locomotor, peasant.Location, cell)))
					continue;

				tried.Add(cell);
				yield return cell;
				if (tried.Count >= FieldCandidateCount)
					yield break;
			}
		}

		// A navy is wanted when an enemy building can't be walked to from the base, or the map is mostly water.
		bool NavalNeeded(Actor builder)
		{
			if (waterShare < 0)
				waterShare = BotMapAnalysis.WaterShare(world);

			if (waterShare >= Info.NavalWaterShare)
				return true;

			var foot = builder.TraitOrDefault<Mobile>()?.Locomotor;
			return foot != null && BotMapAnalysis.UnreachableByLand(world, player, foot, builder.Location).Count > 0;
		}

		// Land cells on a shore within NavalSearchRadius of the base that the builder can walk to, nearest first, spread
		// at least 4 cells apart. Shores on water that reaches an enemy's coast come first (a lake is no use to ships).
		List<CPos> ShoreCandidates(CPos center, Actor builder)
		{
			var foot = builder.TraitOrDefault<Mobile>()?.Locomotor;
			var naval = BotMapAnalysis.Locomotor(world, Info.NavalLocomotor);
			if (foot == null)
				return new List<CPos>();

			var enemyWater = naval == null ? new List<CPos>() : BotMapAnalysis.EnemyBuildings(world, player)
				.Select(b => world.Map.FindTilesInCircle(b.Location, 12).Cast<CPos?>()
					.FirstOrDefault(c => BotMapAnalysis.IsWater(world, c.Value)))
				.Where(c => c != null).Select(c => c.Value).Take(8).ToList();

			var shores = new List<(CPos Land, bool ReachesEnemy)>();
			foreach (var cell in world.Map.FindTilesInCircle(center, Info.NavalSearchRadius))
			{
				if (BotMapAnalysis.IsWater(world, cell) || shores.Any(s => (s.Land - cell).LengthSquared < 16))
					continue;

				var water = CVec.Directions.Select(d => cell + d).Where(c => BotMapAnalysis.IsWater(world, c)).ToList();
				if (water.Count == 0 || !pathFinder.PathExistsForLocomotor(foot, builder.Location, cell))
					continue;

				var reaches = naval != null && water.Any(w => enemyWater.Any(e => pathFinder.PathExistsForLocomotor(naval, w, e)));
				shores.Add((cell, reaches));
				if (shores.Count >= FieldCandidateCount)
					break;
			}

			return shores.OrderByDescending(s => s.ReachesEnemy).Select(s => s.Land).ToList();
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

		// The drop-off site within maxRadius of the field with the shortest walk from the field to a cell beside it, if
		// that walk is at most MaxDropOffWalk steps.
		CPos? FindDropOffSite(Actor builder, ActorInfo actorInfo, CPos field, int maxRadius)
		{
			var locomotor = builder.TraitOrDefault<Mobile>()?.Locomotor;
			if (locomotor == null)
				return FindSite(builder, actorInfo, field, 1, maxRadius);

			var steps = WalkSteps(locomotor, field, maxRadius + Info.MaxDropOffWalk);
			var buildingInfo = actorInfo.TraitInfo<BuildingInfo>();
			CPos? best = null;
			var bestWalk = int.MaxValue;
			foreach (var cell in world.Map.FindTilesInAnnulus(field, 1, maxRadius))
			{
				if (!CoastalBuilding.CanPlace(world, builder, actorInfo, buildingInfo, cell) || !Clear(buildingInfo, cell))
					continue;

				var footprint = buildingInfo.Tiles(cell).ToHashSet();
				var walk = footprint.SelectMany(c => CVec.Directions.Select(d => c + d))
					.Where(n => !footprint.Contains(n) && steps.ContainsKey(n))
					.Select(n => steps[n]).DefaultIfEmpty(int.MaxValue).Min();

				if (walk <= Info.MaxDropOffWalk && walk < bestWalk)
				{
					best = cell;
					bestWalk = walk;
				}
			}

			return best;
		}

		// Steps a worker needs from start to each cell within limit steps, walking over passable terrain and around
		// buildings (trees included).
		Dictionary<CPos, int> WalkSteps(Locomotor locomotor, CPos start, int limit)
		{
			var steps = new Dictionary<CPos, int> { [start] = 0 };
			var queue = new Queue<CPos>();
			queue.Enqueue(start);
			while (queue.Count > 0)
			{
				var cell = queue.Dequeue();
				var next = steps[cell] + 1;
				if (next > limit)
					continue;

				foreach (var d in CVec.Directions)
				{
					var n = cell + d;
					if (steps.ContainsKey(n) || !world.Map.Contains(n)
						|| locomotor.MovementCostForCell(n) == PathGraph.MovementCostForUnreachableCell
						|| world.ActorMap.GetActorsAt(n).Any(a => a.TraitOrDefault<Building>() != null))
						continue;

					steps[n] = next;
					queue.Enqueue(n);
				}
			}

			return steps;
		}

		// Keeps Spacing free cells around the footprint (no other buildings) and never covers crop fields. Map objects
		// (trees, totems; buildings without health) block their own cells only: a site may stand next to a tree.
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
						if (world.Map.Contains(n) && world.ActorMap.GetActorsAt(n).Any(a => a.TraitOrDefault<Building>() != null && a.Info.HasTraitInfo<HealthInfo>()))
							return false;
					}

			return true;
		}
	}
}
