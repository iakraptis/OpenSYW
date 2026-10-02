#region Copyright & License Information
/*
 * Part of the OpenSY "Seven Years War" mod.
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
		readonly World world;
		readonly ButtonWidget[] slotButtons;
		readonly LabelWidget title;

		string[] slotTypes = System.Array.Empty<string>();

		[ObjectCreator.UseCtor]
		public BuilderPaletteLogic(Widget widget, World world)
		{
			this.world = world;

			title = widget.Get<LabelWidget>("BUILD_TITLE");
			title.GetText = () => FluentProvider.GetMessage("actor-peasant.name");
			slotButtons = widget.Children.OfType<ButtonWidget>().ToArray();
			title.Visible = false;
			foreach (var button in slotButtons)
			{
				button.Visible = false;
				button.Get<SpriteWidget>("PORTRAIT").GetSprite = () => null;
			}
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

			for (var i = 0; i < slotButtons.Length; i++)
			{
				var button = slotButtons[i];
				if (button == null)
					continue;

				if (i >= slotTypes.Length)
				{
					button.Visible = false;
					continue;
				}

				var actorType = slotTypes[i];
				var actorInfo = world.Map.Rules.Actors[actorType];
				var cost = actorInfo.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0;
				var buildableInfo = actorInfo.TraitInfoOrDefault<BuildableInfo>();
				var prereqsOk = buildableInfo == null || buildableInfo.Prerequisites.Length == 0 || techTree.HasPrerequisites(buildableInfo.Prerequisites);

				button.Visible = true;
				button.GetText = () => "";
				button.Get<SpriteWidget>("PORTRAIT").GetSprite = () => world.Map.Sequences.GetSequence("building-portraits", actorType).GetSprite(0);
				button.Get("DISABLED").IsVisible = () => button.IsDisabled();
				button.GetTooltipText = () => $"{FluentProvider.GetMessage($"actor-{actorType}.name")} (${cost})";
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
