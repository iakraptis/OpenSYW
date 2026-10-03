using System.Collections.Generic;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.Syw.Widgets.Logic
{
	// Shows one frame of a sprite sequence in a Sprite widget, so chrome buttons can use the game's own icons:
	//   Sprite@COMMAND_ICON:
	//       Logic: SequenceIconLogic
	//           Image: spell-icons
	//           Sequence: attack
	public class SequenceIconLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public SequenceIconLogic(Widget widget, World world, Dictionary<string, MiniYaml> logicArgs)
		{
			var image = logicArgs["Image"].Value;
			var sequence = logicArgs["Sequence"].Value;
			var sprite = world.Map.Sequences.HasSequence(image, sequence) ? world.Map.Sequences.GetSequence(image, sequence).GetSprite(0) : null;
			((SpriteWidget)widget).GetSprite = () => sprite;
		}
	}
}
