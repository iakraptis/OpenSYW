using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Pathfinder;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	// Map questions the bot modules share: which enemy buildings an army can't walk to, how much of the map is water,
	// and where land meets water. Terrain only (buildings and units never block a path), so results stay valid and can
	// be cached between decisions.
	public static class BotMapAnalysis
	{
		public const string WaterType = "Water";

		public static bool IsWater(World world, CPos cell) =>
			world.Map.Contains(cell) && world.Map.GetTerrainInfo(cell).Type == WaterType;

		public static Locomotor Locomotor(World world, string name) =>
			world.WorldActor.TraitsImplementing<Locomotor>().FirstOrDefault(l => l.Info.Name == name);

		// Living enemy buildings (map objects such as trees have no health and don't count).
		public static IEnumerable<Actor> EnemyBuildings(World world, Player player) =>
			world.ActorsHavingTrait<Building>().Where(a => !a.IsDead && a.IsInWorld && a.Info.HasTraitInfo<HealthInfo>()
				&& a.Owner != player && !a.Owner.NonCombatant && player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy);

		// A land cell next to the building's footprint, from which the building can be attacked on foot.
		public static CPos? LandCellBeside(World world, Actor building, Locomotor foot)
		{
			var footprint = building.TraitOrDefault<Building>()?.Info.Tiles(building.Location).ToHashSet()
				?? new HashSet<CPos> { building.Location };
			return footprint.SelectMany(c => CVec.Directions.Select(d => c + d))
				.Where(c => !footprint.Contains(c) && world.Map.Contains(c) && foot.MovementCostForCell(c) != PathGraph.MovementCostForUnreachableCell)
				.Cast<CPos?>().FirstOrDefault();
		}

		// Enemy buildings no unit can walk to from start.
		public static List<Actor> UnreachableByLand(World world, Player player, Locomotor foot, CPos start)
		{
			var pathFinder = world.WorldActor.Trait<IPathFinder>();
			return EnemyBuildings(world, player).Where(b =>
			{
				var beside = LandCellBeside(world, b, foot);
				return beside != null && !pathFinder.PathExistsForLocomotor(foot, start, beside.Value);
			}).ToList();
		}

		// Share of the map's cells (in percent) that are water, sampled every second cell.
		public static int WaterShare(World world)
		{
			int water = 0, total = 0;
			foreach (var cell in world.Map.AllCells)
			{
				if (((cell.X + cell.Y) & 1) != 0)
					continue;

				total++;
				if (IsWater(world, cell))
					water++;
			}

			return total == 0 ? 0 : water * 100 / total;
		}

		// Water cells next to land that both a ship (naval, from shipFrom) and a walker (foot, from walkFrom) can reach,
		// nearest to near first. The land cell beside each is where passengers step on or off.
		public static IEnumerable<(CPos Water, CPos Land)> Shore(World world, Locomotor naval, CPos shipFrom, Locomotor foot,
			CPos walkFrom, CPos near, int radius)
		{
			var pathFinder = world.WorldActor.Trait<IPathFinder>();
			foreach (var cell in world.Map.FindTilesInCircle(near, radius))
			{
				if (!IsWater(world, cell) || naval.MovementCostForCell(cell) == PathGraph.MovementCostForUnreachableCell)
					continue;

				foreach (var d in CVec.Directions)
				{
					var land = cell + d;
					if (!world.Map.Contains(land) || IsWater(world, land) || foot.MovementCostForCell(land) == PathGraph.MovementCostForUnreachableCell)
						continue;

					if (pathFinder.PathExistsForLocomotor(naval, shipFrom, cell) && pathFinder.PathExistsForLocomotor(foot, walkFrom, land))
					{
						yield return (cell, land);
						break;
					}
				}
			}
		}
	}
}
