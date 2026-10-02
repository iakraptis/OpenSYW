using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Syw.Traits;

namespace OpenRA.Mods.Syw.Orders
{
    // Ground-target spell targeting (Witch Earthquake): left-click a visible cell, right-click cancels.
    public class GroundSpellOrderGenerator : OrderGenerator
    {
        readonly Actor caster;
        readonly EarthquakeSpell spell;

        public GroundSpellOrderGenerator(Actor caster)
        {
            this.caster = caster;
            spell = caster.Trait<EarthquakeSpell>();
        }

        public bool CanCast => !caster.Disposed && caster.Owner == caster.World.LocalPlayer && spell.CanCast(caster);

        bool ValidCell(World world, CPos cell) => CanCast && world.Map.Contains(cell) && !world.ShroudObscures(cell);

        protected override IEnumerable<Order> OrderInner(World world, CPos cell, int2 worldPixel, MouseInput mi)
        {
            if (mi.Button == MouseButton.Right || !CanCast)
            {
                world.CancelInputMode();
                yield break;
            }

            if (!ValidCell(world, cell))
                yield break;

            world.CancelInputMode();
            yield return new Order(EarthquakeSpell.OrderId, caster, OpenRA.Traits.Target.FromCell(world, cell), mi.Modifiers.HasModifier(Modifiers.Shift));
        }

        protected override string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi) =>
            ValidCell(world, cell) ? "attack" : "move-blocked";

        protected override void Tick(World world)
        {
            if (!CanCast)
                world.CancelInputMode();
        }

        protected override void SelectionChanged(World world, IEnumerable<Actor> selected)
        {
            var actors = selected.ToArray();
            if (actors.Length != 1 || actors[0] != caster)
                world.CancelInputMode();
        }

        protected override IEnumerable<IRenderable> Render(WorldRenderer wr, World world) { yield break; }
        protected override IEnumerable<IRenderable> RenderAboveShroud(WorldRenderer wr, World world) { yield break; }
        protected override IEnumerable<IRenderable> RenderAnnotations(WorldRenderer wr, World world) { yield break; }
    }
}
