using System.Linq;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.Syw.Widgets.Logic
{
	// Stats of the selected unit or building (any owner), shown above the build/training panels.
	// Lives on the always-visible PLAYER_WIDGETS container; the panel itself toggles via IsVisible.
	public class SelectionStatsLogic : ChromeLogic
	{
		readonly World world;
		Actor actor;
		int count;

		[ObjectCreator.UseCtor]
		public SelectionStatsLogic(Widget widget, World world)
		{
			this.world = world;
			var panel = widget.Get("STATS_PANEL");
			panel.IsVisible = () => Current != null;

			var portrait = panel.Get<SpriteWidget>("STATS_PORTRAIT");
			portrait.GetSprite = () => Current == null ? null : Portrait(Current.Info.Name);
			panel.Get<LabelWidget>("STATS_NAME").GetText = () => Current == null ? "" : Name(Current);
			panel.Get<LabelWidget>("STATS_COUNT").GetText = () =>
				Current != null && count > 1 ? FluentProvider.GetMessage("selection-stats-count", "count", count) : "";

			var lines = new[]
			{
				panel.Get<LabelWidget>("STATS_LINE_1"), panel.Get<LabelWidget>("STATS_LINE_2"),
				panel.Get<LabelWidget>("STATS_LINE_3"), panel.Get<LabelWidget>("STATS_LINE_4")
			};
			for (var i = 0; i < lines.Length; i++)
			{
				var line = i;
				lines[i].GetText = () => Current == null ? "" : Line(Current, line);
			}

			// Repair toggle for the local player's selected buildings/vehicles (stock RepairableBuilding + AllyRepair).
			var repair = panel.Get<ButtonWidget>("STATS_REPAIR");
			repair.IsVisible = () => Current != null && RepairTargets().Any();
			repair.IsDisabled = () => !RepairTargets().Any(a => Damaged(a) || Repairing(a));
			repair.IsHighlighted = () => RepairTargets().Any(Repairing);
			repair.GetText = () => "";
			repair.Get("DISABLED").IsVisible = () => repair.IsDisabled();
			repair.Get<SpriteWidget>("ICON").GetSprite = () =>
				world.Map.Sequences.HasSequence("spell-icons", "repair") ? world.Map.Sequences.GetSequence("spell-icons", "repair").GetSprite(0) : null;
			repair.GetTooltipText = () =>
			{
				var target = RepairTargets().FirstOrDefault(a => Damaged(a) || Repairing(a));
				if (target == null)
					return "";

				var info = target.Info.TraitInfo<RepairableBuildingInfo>();
				var maxHP = target.Trait<Health>().MaxHP;
				var cost = System.Math.Max(1, (int)((long)info.RepairStep * info.RepairPercent * target.GetSellValue() / (maxHP * 100L)));
				var perSecond = 1000 / (info.RepairInterval * world.Timestep);
				return FluentProvider.GetMessage("selection-stats-repair", "hp", info.RepairStep * perSecond, "cost", cost * perSecond);
			};

			// Clicking stops repairs if any are running, otherwise starts them on every damaged selected target.
			repair.OnClick = () =>
			{
				var targets = RepairTargets().ToArray();
				var stop = targets.Any(Repairing);
				foreach (var a in targets.Where(a => stop ? Repairing(a) : Damaged(a)))
					world.IssueOrder(new Order("RepairBuilding", world.LocalPlayer.PlayerActor, OpenRA.Traits.Target.FromActor(a), false));
			};

			SetupSell(panel);
		}

		// Sell button: stock Sellable, like C&C (refund scaled by health), for the local player's selected buildings.
		void SetupSell(Widget panel)
		{
			var sell = panel.Get<ButtonWidget>("STATS_SELL");
			sell.IsVisible = () => Current != null && SellTargets().Any();
			sell.GetText = () => "";
			sell.Get("DISABLED").IsVisible = () => false;
			sell.Get<SpriteWidget>("ICON").GetSprite = () =>
				world.Map.Sequences.HasSequence("spell-icons", "sell") ? world.Map.Sequences.GetSequence("spell-icons", "sell").GetSprite(0) : null;
			sell.GetTooltipText = () => FluentProvider.GetMessage("selection-stats-sell", "refund", SellTargets().Sum(Refund));
			sell.OnClick = () =>
			{
				foreach (var a in SellTargets().ToArray())
					world.IssueOrder(new Order("Sell", a, false));
			};
		}

		System.Collections.Generic.IEnumerable<Actor> SellTargets() => world.LocalPlayer == null ? Enumerable.Empty<Actor>() :
			world.Selection.Actors.Where(a => a.IsInWorld && !a.IsDead && a.Owner == world.LocalPlayer &&
				a.TraitOrDefault<Sellable>() is Sellable s && !s.IsTraitDisabled);

		static int Refund(Actor a)
		{
			var health = a.Trait<Health>();
			return (int)((long)a.GetSellValue() * a.Info.TraitInfo<SellableInfo>().RefundPercent * health.HP / (100L * health.MaxHP));
		}

		System.Collections.Generic.IEnumerable<Actor> RepairTargets() => world.LocalPlayer == null ? Enumerable.Empty<Actor>() :
			world.Selection.Actors.Where(a => a.IsInWorld && !a.IsDead && a.Owner == world.LocalPlayer &&
				a.TraitOrDefault<RepairableBuilding>() is RepairableBuilding r && !r.IsTraitDisabled);

		static bool Damaged(Actor a) => a.Trait<Health>().HP < a.Trait<Health>().MaxHP;

		bool Repairing(Actor a) => a.Trait<RepairableBuilding>().Repairers.Contains(world.LocalPlayer);

		// The selected actor can die between Tick and Draw; never read traits from a destroyed actor.
		Actor Current => actor != null && !actor.Disposed && !actor.IsDead && actor.IsInWorld ? actor : null;

		public override void Tick()
		{
			var selected = world.Selection.Actors.Where(a => a.IsInWorld && !a.IsDead).ToArray();
			actor = selected.FirstOrDefault();
			count = actor == null ? 0 : selected.Count(a => a.Info.Name == actor.Info.Name);
		}

		Sprite Portrait(string name)
		{
			foreach (var image in new[] { "unit-portraits", "building-portraits" })
				if (world.Map.Sequences.HasSequence(image, name))
					return world.Map.Sequences.GetSequence(image, name).GetSprite(0);

			return null;
		}

		static string Name(Actor a)
		{
			var tooltip = a.Info.TraitInfos<TooltipInfo>().FirstOrDefault();
			return tooltip == null ? a.Info.Name : FluentProvider.GetMessage(tooltip.Name);
		}

		static string Line(Actor a, int line)
		{
			var health = a.TraitOrDefault<Health>();
			var weapon = a.TraitsImplementing<Armament>().Select(x => x.Info.WeaponInfo).FirstOrDefault(w => w != null && Damage(w) > 0);
			var mana = a.TraitsImplementing<AmmoPool>().FirstOrDefault(p => p.Info.Name == "mana");
			var entries = new System.Collections.Generic.List<string>();

			if (health != null)
				entries.Add(FluentProvider.GetMessage("selection-stats-health", "hp", health.HP, "max", health.MaxHP));

			if (weapon != null)
				entries.Add(FluentProvider.GetMessage("selection-stats-attack", "damage", Damage(weapon) * weapon.Burst,
					"range", weapon.Range.Length / 1024));

			var armor = a.Info.TraitInfoOrDefault<ArmorInfo>()?.Type;
			var defense = 100 - a.Info.TraitInfos<DamageMultiplierInfo>().Where(m => m.RequiresCondition == null)
				.Aggregate(100, (total, m) => total * m.Modifier / 100);
			var speed = a.Info.TraitInfoOrDefault<MobileInfo>()?.Speed ?? a.Info.TraitInfoOrDefault<AircraftInfo>()?.Speed ?? 0;
			var cost = a.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0;

			// Zero values are hidden rather than shown as "Defense 0%".
			var details = new System.Collections.Generic.List<string>();
			if (!string.IsNullOrEmpty(armor))
				details.Add(FluentProvider.GetMessage("selection-stats-armor", "armor", armor));
			if (defense != 0)
				details.Add(FluentProvider.GetMessage("selection-stats-defense", "defense", defense));
			if (speed > 0)
				details.Add(FluentProvider.GetMessage("selection-stats-speed", "speed", speed));
			if (details.Count > 0)
				entries.Add(string.Join("   ", details));

			if (mana != null)
				entries.Add(FluentProvider.GetMessage("selection-stats-mana", "mana", mana.CurrentAmmoCount, "max", mana.Info.Ammo));
			else if (cost > 0)
				entries.Add(FluentProvider.GetMessage("selection-stats-cost", "cost", cost));

			return line < entries.Count ? entries[line] : "";
		}

		static int Damage(WeaponInfo weapon)
		{
			return weapon.Warheads.OfType<DamageWarhead>().Select(w => w.Damage).DefaultIfEmpty(0).Max();
		}
	}
}
