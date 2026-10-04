#region Copyright & License Information
/*
 * Part of the OpenSYW "Seven Years War" mod.
 */
#endregion

using System.Linq;
using OpenRA;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Syw.Orders;
using OpenRA.Mods.Syw.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Syw.Widgets.Logic
{
	/// <summary>
	/// Shows a sidebar of "Build X" buttons whenever the local player has at least one Peasant (an actor
	/// with the Builder trait) selected - this is deliberately NOT the player-wide production sidebar: it
	/// only appears for, and only issues orders to, the currently-selected builder unit(s).
	/// </summary>
	public class BuilderPaletteLogic : ChromeLogic
	{
		// Slot buttons sit in two columns, one row every 52 px (SidebarLayoutLogic sizes the container to the rows
		// that fit; the rest is reached by scrolling).
		const int Columns = 2;
		const int RowHeight = 52;

		readonly World world;
		readonly Widget palette;
		readonly ButtonWidget[] slotButtons;
		readonly LabelWidget title;

		string[] slotTypes = System.Array.Empty<string>();
		Actor shownBuilder;
		int rowOffset;

		[ObjectCreator.UseCtor]
		public BuilderPaletteLogic(Widget widget, World world)
		{
			this.world = world;

			palette = widget;
			title = widget.Get<LabelWidget>("BUILD_TITLE");
			title.GetText = () => FluentProvider.GetMessage("actor-peasant.name");
			slotButtons = widget.Children.OfType<ButtonWidget>().Where(b => b.Id.StartsWith("BUILD_SLOT_", System.StringComparison.Ordinal)).ToArray();
			title.Visible = false;
			foreach (var button in slotButtons)
			{
				button.Visible = false;
				button.Get<SpriteWidget>("PORTRAIT").GetSprite = () => null;
			}

			// The mouse wheel over the palette, or the arrows beside it, scroll by one row.
			widget.Get<MouseWheelAreaWidget>("BUILD_SCROLL_AREA").OnScroll = delta => Scroll(-delta);
			var up = widget.Get<ButtonWidget>("BUILD_SCROLL_UP");
			up.IsVisible = () => shownBuilder != null && rowOffset > 0;
			up.OnClick = () => Scroll(-1);
			var down = widget.Get<ButtonWidget>("BUILD_SCROLL_DOWN");
			down.IsVisible = () => shownBuilder != null && rowOffset < MaxRowOffset;
			down.OnClick = () => Scroll(1);
		}

		int VisibleRows => System.Math.Max(1, (palette.Bounds.Height + 2) / RowHeight);
		int MaxRowOffset => System.Math.Max(0, (slotTypes.Length + Columns - 1) / Columns - VisibleRows);

		void Scroll(int rows)
		{
			rowOffset = System.Math.Clamp(rowOffset + rows, 0, MaxRowOffset);
		}

		public override void Tick()
		{
			var builderActor = world.Selection.Actors
				.FirstOrDefault(a => a.IsInWorld && !a.IsDead && a.Owner == world.LocalPlayer && a.TraitOrDefault<Builder>() != null);

			// Note: the container itself must stay Visible=true always - Widget.TickOuter() only ticks a
			// widget's Tick()/children/LogicObjects while the widget IsVisible(), so if we hid the container
			// we host on, this logic would never run again to notice a Peasant got selected. Hide the
			// individual buttons instead.
			title.Visible = builderActor != null;
			if (builderActor != shownBuilder)
				rowOffset = 0;

			shownBuilder = builderActor;
			if (builderActor == null)
			{
				foreach (var button in slotButtons)
					if (button != null)
						button.Visible = false;

				return;
			}

			var builder = builderActor.Trait<Builder>();
			var playerResources = builderActor.Owner.PlayerActor.Trait<PlayerResources>();
			var techTree = builderActor.Owner.PlayerActor.Trait<TechTree>();

			slotTypes = builder.Info.Types
				.Select(t => world.Map.Rules.Actors[t])
				.OrderBy(ai => ai.TraitInfoOrDefault<BuildableInfo>()?.BuildPaletteOrder ?? 9999)
				.Select(ai => ai.Name)
				.ToArray();

			// Keep the offset valid when the palette shrinks (smaller window) or the list changes.
			rowOffset = System.Math.Clamp(rowOffset, 0, MaxRowOffset);
			var shown = VisibleRows * Columns;
			var down = palette.Get("BUILD_SCROLL_DOWN");
			down.Bounds.Y = (VisibleRows - 1) * RowHeight + 28;

			for (var i = 0; i < slotButtons.Length; i++)
			{
				var button = slotButtons[i];
				if (button == null)
					continue;

				var index = rowOffset * Columns + i;
				if (i >= shown || index >= slotTypes.Length)
				{
					button.Visible = false;
					continue;
				}

				var actorType = slotTypes[index];
				var actorInfo = world.Map.Rules.Actors[actorType];
				var cost = actorInfo.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0;
				var buildableInfo = actorInfo.TraitInfoOrDefault<BuildableInfo>();
				var prereqsOk = buildableInfo == null || buildableInfo.Prerequisites.Length == 0 || techTree.HasPrerequisites(buildableInfo.Prerequisites);

				// The building's own tooltip name, and its portrait (none rather than a crash if the art is missing).
				var tooltipName = actorInfo.TraitInfos<TooltipInfo>().FirstOrDefault()?.Name;
				var portrait = world.Map.Sequences.HasSequence("building-portraits", actorType)
					? world.Map.Sequences.GetSequence("building-portraits", actorType).GetSprite(0) : null;

				button.Visible = true;
				button.GetText = () => "";
				button.Get<SpriteWidget>("PORTRAIT").GetSprite = () => portrait;
				button.Get("DISABLED").IsVisible = () => button.IsDisabled();
				button.GetTooltipText = () => $"{(tooltipName != null ? FluentProvider.GetMessage(tooltipName) : actorType)} (${cost})";
				button.IsDisabled = () => !prereqsOk || playerResources.Cash + playerResources.Resources < cost || builder.IsBusy;
				button.OnClick = () => StartBuild(actorType);
			}
		}

		void StartBuild(string actorType)
		{
			var builderActor = world.Selection.Actors
				.FirstOrDefault(a => a.IsInWorld && !a.IsDead && a.Owner == world.LocalPlayer &&
					(a.TraitOrDefault<Builder>()?.CanBuild(actorType) ?? false));

			if (builderActor == null)
				return;

			world.OrderGenerator = new BuildAtCellOrderGenerator(world, builderActor, actorType);
		}
	}
}
