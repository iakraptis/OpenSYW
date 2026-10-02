using System;
using System.Collections.Generic;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
    [Desc("Regrows a resource type on the cells where the map originally placed it, up to the original density.")]
    public class RegrowsResourcesInfo : TraitInfo, Requires<IResourceLayerInfo>
    {
        [FieldLoader.Require]
        public readonly string ResourceType = null;

        [Desc("Ticks between growth steps.")]
        public readonly int Interval = 250;

        [Desc("Density added to each depleted cell per growth step.")]
        public readonly int Amount = 1;

        [Desc("Growth speed in percent while the world's RainController reports rain (150 = half again as fast).")]
        public readonly int RainGrowthPercent = 100;

        public override object Create(ActorInitializer init) { return new RegrowsResources(init.Self, this); }
    }

    public class RegrowsResources : ITick
    {
        readonly RegrowsResourcesInfo info;
        readonly IResourceLayer layer;
        RainController rain;
        List<(CPos Cell, int Density)> fields;

        // Growth progress in percent-ticks: +100 per dry tick, +RainGrowthPercent per rainy tick.
        int progress;

        public RegrowsResources(Actor self, RegrowsResourcesInfo info)
        {
            this.info = info;
            layer = self.Trait<IResourceLayer>();
        }

        void ITick.Tick(Actor self)
        {
            // Captured on the first tick, after the resource layer has loaded the map contents.
            if (fields == null)
            {
                rain = self.TraitOrDefault<RainController>();
                fields = new List<(CPos, int)>();
                foreach (var cell in self.World.Map.AllCells)
                {
                    var content = layer.GetResource(cell);
                    if (content.Type == info.ResourceType)
                        fields.Add((cell, content.Density));
                }
            }

            progress += rain != null && rain.IsRaining ? info.RainGrowthPercent : 100;
            if (progress < info.Interval * 100)
                return;

            progress -= info.Interval * 100;
            foreach (var (cell, density) in fields)
            {
                var content = layer.GetResource(cell);
                if ((content.Type != null && content.Type != info.ResourceType) || content.Density >= density)
                    continue;

                var amount = Math.Min(info.Amount, density - content.Density);
                if (layer.CanAddResource(info.ResourceType, cell, amount))
                    layer.AddResource(info.ResourceType, cell, amount);
            }
        }
    }
}
