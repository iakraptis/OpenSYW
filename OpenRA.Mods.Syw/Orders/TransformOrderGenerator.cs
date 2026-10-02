using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Syw.Traits;

namespace OpenRA.Mods.Syw.Orders
{
	// Unit-target spell targeting (Shaman Transform, Witch Bewilderment): left-click a valid unit, right-click cancels.
	// OrderGenerator (not UnitOrderGenerator) so the left-click reaches us instead of the selection logic.
	public class TransformOrderGenerator : OrderGenerator
	{
		readonly Actor caster;
		readonly IUnitTargetSpell spell;

		public TransformOrderGenerator(Actor caster)
			: this(caster, caster.Trait<ShamanTransform>()) { }

		public TransformOrderGenerator(Actor caster, IUnitTargetSpell spell)
		{
			this.caster = caster;
			this.spell = spell;
		}

		public bool CanCast => !caster.Disposed && caster.Owner == caster.World.LocalPlayer && spell.CanCast(caster);

		public bool ValidTarget(Actor actor) => CanCast && !caster.World.FogObscures(actor) && spell.ValidTarget(caster, actor);

		Actor TargetUnderMouse(World world, int2 worldPixel) => world.ScreenMap.ActorsAtMouse(worldPixel)
			.Select(a => a.Actor).FirstOrDefault(ValidTarget);

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
			yield return new Order(spell.SpellOrderId, caster, OpenRA.Traits.Target.FromActor(target), mi.Modifiers.HasModifier(Modifiers.Shift));
		}

		protected override string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi) =>
			TargetUnderMouse(world, worldPixel) != null ? "enter" : "move-blocked";

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
