using System;
using OpenRA.Widgets;

namespace OpenRA.Mods.Syw.Widgets
{
	// An invisible area that reports mouse wheel turns over it (OnScroll gets +1 for up, -1 for down). Put it first among
	// its siblings: buttons on top of it ignore the wheel, so the turn falls through to this area.
	public class MouseWheelAreaWidget : Widget
	{
		public Action<int> OnScroll = _ => { };

		public MouseWheelAreaWidget() { }

		protected MouseWheelAreaWidget(MouseWheelAreaWidget other)
			: base(other)
		{
			OnScroll = other.OnScroll;
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Event != MouseInputEvent.Scroll || mi.Delta.Y == 0)
				return false;

			OnScroll(Math.Sign(mi.Delta.Y));
			return true;
		}

		public override Widget Clone() => new MouseWheelAreaWidget(this);
	}
}
