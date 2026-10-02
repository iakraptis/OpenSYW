using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;
using OpenRA.Mods.Syw.Traits;

namespace OpenRA.Mods.Syw.Orders
{
    // Target selection only: the synchronized stock Attack order handles casting and mana.
    // Derives from OrderGenerator, not UnitOrderGenerator: the interaction controller treats a UnitOrderGenerator
    // left-click as a selection click and cancels the input mode, so the cast order was never issued.
    public class SpellOrderGenerator : OrderGenerator
    {
        readonly Actor caster;
        readonly Armament spell;
        public SpellOrderGenerator(Actor caster, Armament spell)
        {
            this.caster = caster;
            this.spell = spell;
        }

        public bool CanCast => !caster.IsDead && !caster.Disposed && caster.IsInWorld &&
            caster.Owner == caster.World.LocalPlayer && (caster.TraitOrDefault<ManualLightning>() is ManualLightning manual ? manual.CanCast(caster, spell) : !spell.IsTraitDisabled && !spell.IsTraitPaused);

        public bool ValidTarget(Actor actor) => CanCast && actor != caster && actor.IsInWorld &&
            !actor.IsDead && !actor.Disposed && !caster.World.FogObscures(actor) &&
            spell.Info.TargetRelationships.HasRelationship(caster.Owner.RelationshipWith(actor.Owner)) &&
            spell.Weapon.IsValidAgainst(Target.FromActor(actor), caster.World, caster);

        Actor TargetUnderMouse(World world, int2 worldPixel) => world.ScreenMap.ActorsAtMouse(worldPixel)
            .Select(a => a.Actor).FirstOrDefault(ValidTarget);

        // The base class only forwards left-button Down and right-button Up.
        protected override IEnumerable<Order> OrderInner(World world, CPos cell, int2 worldPixel, MouseInput mi)
        {
            if (mi.Button == MouseButton.Right || !CanCast)
            {
                world.CancelInputMode();
                yield break;
            }
            var target = TargetUnderMouse(world, worldPixel);
            if (target == null)
                yield break;
            world.CancelInputMode();
            yield return new Order(caster.TraitOrDefault<ManualLightning>() != null ? ManualLightning.OrderId : "Attack", caster, Target.FromActor(target), false);
        }

        protected override string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi) =>
            TargetUnderMouse(world, worldPixel) != null ? spell.Info.Cursor : "move-blocked";
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
