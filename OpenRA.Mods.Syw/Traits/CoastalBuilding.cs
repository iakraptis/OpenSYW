using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("Building placed on a coastline: its footprint mixes land and water, a builder can reach it over land,",
		"and at least one exit opens onto water.")]
	public class CoastalBuildingInfo : TraitInfo<CoastalBuilding>
	{
		[Desc("Minimum number of footprint cells that must be water.")]
		public readonly int MinimumWaterCells = 3;

		[Desc("Terrain type counted as water.")]
		public readonly string WaterType = "Water";
	}

	public sealed class CoastalBuilding
	{
		// Used by the placement preview, the placement order and synchronized Builder validation.
		public static bool CanPlace(World world, Actor builder, ActorInfo actor, BuildingInfo info, CPos cell)
		{
			if (!world.CanPlaceBuilding(cell, actor, info, null))
				return false;

			var coastal = actor.TraitInfoOrDefault<CoastalBuildingInfo>();
			if (coastal == null)
				return true;

			bool IsWater(CPos c) => world.Map.Contains(c) && world.Map.GetTerrainInfo(c).Type == coastal.WaterType;

			var footprint = info.Tiles(cell).ToList();
			if (footprint.Count(IsWater) < coastal.MinimumWaterCells)
				return false;

			// The builder walks up to the site over land.
			var move = builder.Trait<Mobile>();
			var perimeter = footprint.SelectMany(c => CVec.Directions.Select(d => c + d))
				.Distinct().Where(c => !footprint.Contains(c) && world.Map.Contains(c));
			if (!perimeter.Any(c => !IsWater(c) && move.CanStayInCell(c) && move.CanEnterCell(c)))
				return false;

			// Ships leave through an exit cell on open, unoccupied water.
			return actor.TraitInfos<ExitInfo>().Any(e => IsWater(cell + e.ExitCell) &&
				!world.ActorMap.GetActorsAt(cell + e.ExitCell).Any());
		}
	}
}
