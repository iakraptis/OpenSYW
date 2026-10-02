using System;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.Syw.Widgets.Logic
{
	// Hosted on the always-visible player root so selection changes keep ticking.
	// Shows a portrait for each unit aboard the selected transport, in the build palette's 2-column grid.
	public class TransportCargoLogic : ChromeLogic
	{
		// Same grid as the build palette buttons (X 29 / 97, 68 x 52 steps).
		const int GridLeft = 29;
		const int StepX = 68;
		const int StepY = 52;

		readonly World world;
		readonly Widget panel;
		readonly Widget rows;
		readonly Widget template;
		readonly LabelWidget empty;
		Actor selectedTransport;
		Actor[] passengers = Array.Empty<Actor>();

		[ObjectCreator.UseCtor]
		public TransportCargoLogic(Widget widget, World world)
		{
			this.world = world;
			panel = widget.Get("CARGO_PANEL");
			rows = widget.Get("CARGO_ROWS");
			template = rows.Get("CARGO_SLOT_TEMPLATE");
			rows.RemoveChildren();
			empty = widget.Get<LabelWidget>("CARGO_EMPTY");
			empty.GetText = () => FluentProvider.GetMessage("label-transport-cargo-empty");
			empty.IsVisible = () => passengers.Length == 0;
			panel.IsVisible = () => selectedTransport != null;
		}

		public override void Tick()
		{
			var selection = world.Selection.Actors.ToArray();
			var actor = selection.Length == 1 ? selection[0] : null;
			var cargo = actor != null && actor.IsInWorld && !actor.IsDead && !actor.Disposed &&
				actor.Owner == world.LocalPlayer ? actor.TraitOrDefault<Cargo>() : null;
			actor = cargo != null ? actor : null;
			var current = cargo?.Passengers.ToArray() ?? Array.Empty<Actor>();
			if (selectedTransport == actor && passengers.SequenceEqual(current))
				return;

			selectedTransport = actor;
			passengers = current;
			rows.RemoveChildren();
			for (var i = 0; i < current.Length; i++)
			{
				var slot = template.Clone();
				slot.Bounds.X = GridLeft + i % 2 * StepX;
				slot.Bounds.Y = i / 2 * StepY;
				var name = current[i].Info.Name;
				var portrait = world.Map.Sequences.HasSequence("unit-portraits", name) ?
					world.Map.Sequences.GetSequence("unit-portraits", name).GetSprite(0) : null;
				var portraitWidget = slot.Get<SpriteWidget>("PORTRAIT");
				portraitWidget.GetSprite = () => portrait;

				// SpriteWidget's copy constructor doesn't copy GetScale, so a cloned slot would crash when drawn.
				portraitWidget.GetScale = () => portraitWidget.Scale;
				rows.AddChild(slot);
			}

			panel.Bounds.Height = Math.Max(24, (current.Length + 1) / 2 * StepY);
		}
	}
}
