using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.Syw.Widgets.Logic
{
	// Keep this logic on an always-visible container so it can notice new selections.
	public class BuildingTrainingLogic : ChromeLogic
	{
		readonly World world;
		readonly ProductionPaletteWidget palette;
		readonly LabelWidget title;

		[ObjectCreator.UseCtor]
		public BuildingTrainingLogic(Widget widget, World world)
		{
			this.world = world;
			palette = widget.Get<ProductionPaletteWidget>("PRODUCTION_PALETTE");
			title = widget.Get<LabelWidget>("TRAINING_TITLE");
			var background = widget.Get("PRODUCTION_BACKGROUND");
			var template = background.Get("ICON_TEMPLATE");
			background.RemoveChildren();
			palette.OnIconCountChanged += (_, count) =>
			{
				background.RemoveChildren();
				for (var i = 0; i < count; i++)
				{
					var icon = template.Clone();
					icon.Bounds.X = i % palette.Columns * (palette.IconSize.X + palette.IconMargin.X);
					icon.Bounds.Y = i / palette.Columns * (palette.IconSize.Y + palette.IconMargin.Y);
					background.AddChild(icon);
				}
			};
			background.IsVisible = () => palette.CurrentQueue != null;
			title.IsVisible = () => palette.CurrentQueue != null;
			title.GetText = () => palette.CurrentQueue == null ? "" :
				FluentProvider.GetMessage(palette.CurrentQueue.Actor.Info.TraitInfo<TooltipInfo>().Name);
		}

		public override void Tick()
		{
			// Require a single owned producer: never route orders to an unselected building.
			var selected = world.Selection.Actors.ToArray();
			var actor = selected.Length == 1 ? selected[0] : null;
			var queue = actor != null && actor.IsInWorld && !actor.IsDead &&
				actor.Owner == world.LocalPlayer
				? actor.TraitsImplementing<ProductionQueue>().FirstOrDefault(q => q.Enabled && q.AnyItemsToBuild())
				: null;
			if (palette.CurrentQueue == queue)
				return;

			palette.CurrentQueue = queue;
			palette.ScrollToTop();
		}
	}
}
