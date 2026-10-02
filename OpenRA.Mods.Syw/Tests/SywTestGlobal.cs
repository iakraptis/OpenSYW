using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Syw.Traits;
using OpenRA.Scripting;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Syw.Tests
{
	// Lua helpers for the test maps (maps/*-test/test.lua). Orders are resolved directly on the actor's traits,
	// so a script can drive any player's units, including the mod's own orders (construction, spells) that the
	// stock Lua API doesn't expose. Queries cover what Lua can't see: visibility, mana and the in-game panels.
	[ScriptGlobal("SywTest")]
	public class SywTestGlobal : ScriptGlobal
	{
		public SywTestGlobal(ScriptContext context)
			: base(context) { }

		static void Resolve(Actor actor, Order order)
		{
			foreach (var t in actor.TraitsImplementing<IResolveOrder>().ToArray())
				t.ResolveOrder(actor, order);
		}

		[Desc("Issue an order without a target (Mass Heal, Disturb, Detect mine, single mine).")]
		public void Order(Actor actor, string order) => Resolve(actor, new Order(order, actor, false));

		[Desc("Issue an order targeting an actor (Lightning, Transform, Bewilderment).")]
		public void OrderOn(Actor actor, string order, Actor target) =>
			Resolve(actor, new Order(order, actor, Target.FromActor(target), false));

		[Desc("Issue an order targeting a cell (Earthquake, minefield).")]
		public void OrderAt(Actor actor, string order, CPos cell) =>
			Resolve(actor, new Order(order, actor, Target.FromCell(actor.World, cell), false));

		[Desc("Order a Peasant to construct a building at a cell (the real build order).")]
		public void Construct(Actor builder, string building, CPos cell) =>
			Resolve(builder, new Order(Builder.OrderID, builder, Target.FromCell(builder.World, cell), false) { TargetString = building });

		[Desc("Whether the player can currently see the actor (cloaked units are hidden from enemies).")]
		public bool Visible(Actor actor, Player player) => actor.IsInWorld && actor.CanBeViewedByPlayer(player);

		[Desc("Current mana of a caster, or -1 without a mana pool.")]
		public int Mana(Actor actor) =>
			actor.TraitsImplementing<AmmoPool>().FirstOrDefault(p => p.Info.Name == "mana")?.CurrentAmmoCount ?? -1;

		[Desc("Whether the building has finished construction.")]
		public bool Finished(Actor actor) => actor.TraitOrDefault<UnderConstruction>()?.IsComplete ?? true;

		[Desc("Whether an idle overlay with this image is currently shown on the actor (damage smoke, flags, ...).")]
		public bool Overlay(Actor actor, string image) =>
			actor.TraitsImplementing<Common.Traits.Render.WithIdleOverlay>().Any(o => o.Info.Image == image && !o.IsTraitDisabled);

		[Desc("Select one actor, as a click would. The panels update on the next UI tick.")]
		public void Select(Actor actor) => actor.World.Selection.Combine(actor.World, new[] { actor }, false, true);

		[Desc("What the training panel shows: 'queue|items|icons' (icons = buttons actually drawn).")]
		public string TrainingPanel()
		{
			var palette = Ui.Root.GetOrNull<ProductionPaletteWidget>("PRODUCTION_PALETTE");
			var queue = palette?.CurrentQueue;
			if (queue == null)
				return "none||0";

			return $"{queue.Info.Type}|{string.Join(",", queue.AllItems().Select(a => a.Name))}|{palette.DisplayedIconCount}";
		}

		[Desc("Spell buttons shown for the selected caster: one entry per visible button, 'icon' or 'noicon'.")]
		public string SpellButtons()
		{
			var panel = Ui.Root.GetOrNull("SPELL_PANEL");
			if (panel == null || !panel.IsVisible())
				return "";

			var shown = new[] { "SPELL_BUTTON", "SPELL_BUTTON_2", "SPELL_BUTTON_3" }
				.Select(n => panel.GetOrNull<ButtonWidget>(n))
				.Where(b => b != null && b.IsVisible())
				.Select(b => b.Get<SpriteWidget>("ICON").GetSprite() != null ? "icon" : "noicon");
			return string.Join(",", shown);
		}

		[Desc("Passenger portraits drawn in the cargo panel for the selected transport.")]
		public int CargoPortraits()
		{
			var rows = Ui.Root.GetOrNull("CARGO_ROWS");
			return rows?.Children.Count(c => c.GetOrNull<SpriteWidget>("PORTRAIT")?.GetSprite() != null) ?? 0;
		}
	}
}
