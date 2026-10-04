using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Syw.Orders;
using OpenRA.Mods.Syw.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Syw.Widgets.Logic
{
	// Caster panel styled like the builder palette: caster name, mana, then an icon button per spell
	// (FIRE.SPR art) with the spell name and cost in its tooltip. The buttons follow the caster's spell traits:
	//   slot 1: Heal / Lightning (secondary armament), the Miner's single mine, or the Witch's Earthquake;
	//   slot 2: Minefield, Transform, Bewilderment or Mass Heal;
	//   slot 3 (second row): Disturb (Korean Monk) or Detect mine (Japanese Priest).
	public class SpellPaletteLogic : ChromeLogic
	{
		const int ThirdRowHeight = 52;

		readonly World world;
		readonly LabelWidget title;
		readonly LabelWidget manaLabel;
		readonly LabelWidget status;
		readonly LabelWidget hint;
		readonly ButtonWidget button;
		readonly ButtonWidget second;
		readonly ButtonWidget third;
		readonly int statusY;
		readonly int hintY;
		Actor caster;
		Armament spell;
		AmmoPool mana;
		PaidMineLayer mineLayer;
		ShamanTransform transform;
		Bewilder bewilder;
		EarthquakeSpell earthquake;
		MassHeal massHeal;
		Disturb disturb;
		DetectMines detectMines;

		bool ValidCaster => caster != null && !caster.Disposed && !caster.IsDead && caster.IsInWorld && caster.Owner == world.LocalPlayer;
		bool Targeting => world.OrderGenerator is SpellOrderGenerator || world.OrderGenerator is MineLineOrderGenerator ||
			world.OrderGenerator is TransformOrderGenerator || world.OrderGenerator is GroundSpellOrderGenerator;

		// The heal weapon (Monk, Priest) is the one that targets the Heal target type; Lightning targets enemies.
		bool IsHeal => spell != null && spell.Weapon.ValidTargets.Contains("Heal");
		bool HasSecond => mineLayer != null || transform != null || bewilder != null || massHeal != null;
		bool HasThird => disturb != null || detectMines != null;

		[ObjectCreator.UseCtor]
		public SpellPaletteLogic(Widget widget, World world)
		{
			this.world = world;
			var panel = widget.Get("SPELL_PANEL");
			title = panel.Get<LabelWidget>("SPELL_TITLE");
			manaLabel = panel.Get<LabelWidget>("SPELL_MANA");
			status = panel.Get<LabelWidget>("SPELL_STATUS");
			hint = panel.Get<LabelWidget>("SPELL_CANCEL");
			button = panel.Get<ButtonWidget>("SPELL_BUTTON");
			second = panel.Get<ButtonWidget>("SPELL_BUTTON_2");
			third = panel.Get<ButtonWidget>("SPELL_BUTTON_3");
			panel.IsVisible = () => ValidCaster;
			title.GetText = () => !ValidCaster ? "" : FluentProvider.GetMessage(caster.Info.TraitInfo<TooltipInfo>().Name);
			manaLabel.GetText = () => !ValidCaster || mana == null
				? ""
				: FluentProvider.GetMessage("spell-mana", "current", mana.CurrentAmmoCount, "maximum", mana.Info.Ammo);

			// Slot 1.
			button.GetText = () => "";
			button.GetTooltipText = () => !ValidCaster ? "" :
				mineLayer != null ? FluentProvider.GetMessage("spell-mine-single", "mana", mineLayer.Info.ManaCost, "cost", mineLayer.Info.CreditCost) :
				earthquake != null ? FluentProvider.GetMessage("spell-earthquake", "cost", earthquake.Info.ManaCost) :
				spell == null ? "" : FluentProvider.GetMessage(IsHeal ? "spell-heal" : "spell-lightning", "cost", spell.Info.AmmoUsage);
			button.IsDisabled = () => !ValidCaster || (mineLayer != null ? !mineLayer.CanPlace(caster) :
				earthquake != null ? !earthquake.CanCast(caster) : spell == null ||
				(caster.TraitOrDefault<ManualLightning>() is ManualLightning manual
					? !manual.CanCast(caster, spell)
					: spell.IsTraitDisabled || spell.IsTraitPaused || spell.IsReloading));
			button.IsHighlighted = () => ValidCaster && (world.OrderGenerator is SpellOrderGenerator || world.OrderGenerator is GroundSpellOrderGenerator);
			button.Get("DISABLED").IsVisible = () => button.IsDisabled();
			button.Get<SpriteWidget>("ICON").GetSprite = () => !ValidCaster ? null :
				Icon(mineLayer != null ? "mine" : earthquake != null ? "earthquake" : IsHeal ? "heal" : "lightning");
			button.OnClick = () =>
			{
				if (!ValidCaster || button.IsDisabled())
					return;

				if (mineLayer != null)
					IssueWithVoice(new Order(PaidMineLayer.OrderId, caster, false));
				else if (earthquake != null)
					world.OrderGenerator = new GroundSpellOrderGenerator(caster);
				else
					world.OrderGenerator = new SpellOrderGenerator(caster, spell);
			};

			// Slot 2.
			second.IsVisible = () => ValidCaster && HasSecond;
			second.GetText = () => "";
			second.GetTooltipText = () =>
			{
				if (!ValidCaster)
					return "";

				if (mineLayer != null)
					return FluentProvider.GetMessage("spell-lay-mine",
						"count", mineLayer.Info.LineLength, "mana", mineLayer.LineManaCost, "cost", mineLayer.LineCreditCost);

				if (transform != null)
					return FluentProvider.GetMessage("spell-transform", "cost", transform.Info.ManaCost);

				if (bewilder != null)
					return FluentProvider.GetMessage("spell-bewilder", "chance", bewilder.Info.SuccessPercent, "cost", bewilder.Info.ManaCost);

				if (massHeal != null)
					return FluentProvider.GetMessage("spell-mass-heal", "amount", massHeal.Info.Amount, "range", massHeal.Info.Range.Length / 1024);

				return "";
			};
			second.IsDisabled = () => !ValidCaster || (mineLayer != null ? !mineLayer.CanAffordLine(caster) :
				transform != null ? !transform.CanCast(caster) : bewilder != null ? !bewilder.CanCast(caster) :
				massHeal == null || !massHeal.CanCast(caster));
			second.IsHighlighted = () => ValidCaster && (world.OrderGenerator is MineLineOrderGenerator || world.OrderGenerator is TransformOrderGenerator);
			second.Get("DISABLED").IsVisible = () => second.IsDisabled();
			second.Get<SpriteWidget>("ICON").GetSprite = () => !ValidCaster ? null :
				Icon(mineLayer != null ? "minefield" : transform != null ? "transform" : bewilder != null ? "bewilder" : "massheal");
			second.OnClick = () =>
			{
				if (second.IsDisabled())
					return;

				if (mineLayer != null)
					world.OrderGenerator = new MineLineOrderGenerator(caster);
				else if (transform != null)
					world.OrderGenerator = new TransformOrderGenerator(caster, transform);
				else if (bewilder != null)
					world.OrderGenerator = new TransformOrderGenerator(caster, bewilder);
				else
					IssueWithVoice(new Order(MassHeal.OrderId, caster, false));
			};

			// Slot 3 (second row).
			third.IsVisible = () => ValidCaster && HasThird;
			third.GetText = () => "";
			third.GetTooltipText = () =>
			{
				if (!ValidCaster)
					return "";

				if (disturb != null)
					return FluentProvider.GetMessage("spell-disturb",
						"range", disturb.Info.Range.Length / 1024, "seconds", disturb.Info.Duration / 25, "cost", disturb.Info.ManaCost);

				if (detectMines != null)
					return FluentProvider.GetMessage("spell-detect-mines", "range", detectMines.Info.Range.Length / 1024, "cost", detectMines.Info.ManaCost);

				return "";
			};
			third.IsDisabled = () => !ValidCaster || (disturb != null ? !disturb.CanCast(caster) : detectMines == null || !detectMines.CanCast(caster));
			third.IsHighlighted = () => ValidCaster && disturb != null && disturb.Active;
			third.Get("DISABLED").IsVisible = () => third.IsDisabled() && !third.IsHighlighted();
			third.Get<SpriteWidget>("ICON").GetSprite = () => !ValidCaster ? null : Icon(disturb != null ? "disturb" : "detectmines");
			third.OnClick = () =>
			{
				if (third.IsDisabled())
					return;

				IssueWithVoice(new Order(disturb != null ? Disturb.OrderId : DetectMines.OrderId, caster, false));
			};

			// The status lines sit below whichever button rows are shown.
			statusY = status.Bounds.Y;
			hintY = hint.Bounds.Y;

			status.GetText = () => !ValidCaster ? "" : FluentProvider.GetMessage(Status());
			hint.GetText = () => !ValidCaster || !Targeting ? "" :
				FluentProvider.GetMessage(world.OrderGenerator is MineLineOrderGenerator ? "spell-mine-rotate" : "spell-cancel");
		}

		// Button orders skip the world click handler, which is what normally plays the unit's voice for an order.
		void IssueWithVoice(Order order)
		{
			world.IssueOrder(order);
			new[] { order }.PlayVoiceForOrders();
		}

		Sprite Icon(string name) => world.Map.Sequences.HasSequence("spell-icons", name) ?
			world.Map.Sequences.GetSequence("spell-icons", name).GetSprite(0) : null;

		string Status()
		{
			if (mineLayer != null)
				return world.OrderGenerator is MineLineOrderGenerator ? "spell-mine-place" :
					mineLayer.CanPlace(caster) || mineLayer.CanAffordLine(caster) ? "spell-mine-ready" : "spell-mine-unavailable";

			if (world.OrderGenerator is TransformOrderGenerator)
				return bewilder != null ? "spell-select-enemy" : "spell-select-peasant";

			if (world.OrderGenerator is GroundSpellOrderGenerator)
				return "spell-select-ground";

			if (world.OrderGenerator is SpellOrderGenerator)
				return "spell-select-target";

			if (earthquake != null)
				return earthquake.CanCast(caster) || (bewilder != null && bewilder.CanCast(caster)) ? "spell-ready" : "spell-low-mana";

			if (spell != null && mana.CurrentAmmoCount < spell.Info.AmmoUsage)
				return "spell-low-mana";

			return spell != null && spell.FireDelay > 0 ? "spell-cooldown" : "spell-ready";
		}

		public override void Tick()
		{
			var selection = world.Selection.Actors.ToArray();
			caster = selection.Length == 1 ? selection[0] : null;
			if (caster == null || caster.IsDead || caster.Disposed || !caster.IsInWorld || caster.Owner != world.LocalPlayer)
				caster = null;

			spell = caster?.TraitsImplementing<Armament>().FirstOrDefault(a => a.Info.Name == "secondary");
			mana = caster?.TraitsImplementing<AmmoPool>().FirstOrDefault(a => a.Info.Name == "mana");
			mineLayer = caster?.TraitOrDefault<PaidMineLayer>();
			transform = caster?.TraitOrDefault<ShamanTransform>();
			bewilder = caster?.TraitOrDefault<Bewilder>();
			earthquake = caster?.TraitOrDefault<EarthquakeSpell>();
			massHeal = caster?.TraitOrDefault<MassHeal>();
			disturb = caster?.TraitOrDefault<Disturb>();
			detectMines = caster?.TraitOrDefault<DetectMines>();

			// A caster is any unit with a mana pool and at least a first-slot spell.
			if ((spell == null && mineLayer == null && earthquake == null) || mana == null)
				caster = null;

			var offset = caster != null && HasThird ? ThirdRowHeight : 0;
			status.Bounds.Y = statusY + offset;
			hint.Bounds.Y = hintY + offset;
		}
	}
}
