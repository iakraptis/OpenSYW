using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
    [Desc("Draws each resource cell with the sprite for its position in a field block, read from the terrain tile below.")]
    public class FieldResourceRendererInfo : ResourceRendererInfo
    {
        [Desc("Template ID of the top-left cell of the bare field block.")]
        public readonly int FieldTemplateOrigin = 0;

        [Desc("Templates per row in the tileset sheet the field block comes from.")]
        public readonly int TemplateColumns = 20;

        public readonly int FieldSize = 5;

        [Desc("Sequence used on cells that are not part of a field block.")]
        public readonly string FallbackSequence = "r2c2";

        public override object Create(ActorInitializer init) { return new FieldResourceRenderer(init.Self, this); }
    }

    public class FieldResourceRenderer : ResourceRenderer
    {
        readonly FieldResourceRendererInfo info;

        public FieldResourceRenderer(Actor self, FieldResourceRendererInfo info)
            : base(self, info)
        {
            this.info = info;
        }

        protected override ISpriteSequence ChooseVariant(string resourceType, CPos cell)
        {
            var offset = World.Map.Tiles[cell].Type - info.FieldTemplateOrigin;
            var row = offset / info.TemplateColumns;
            var col = offset % info.TemplateColumns;
            var inBlock = offset >= 0 && row < info.FieldSize && col < info.FieldSize;
            var variants = Variants[resourceType];

            // Resource types without field-position sequences (e.g. ordinary patches) use a random variant.
            if (variants.TryGetValue(inBlock ? $"r{row}c{col}" : info.FallbackSequence, out var sequence))
                return sequence;

            return base.ChooseVariant(resourceType, cell);
        }
    }
}
