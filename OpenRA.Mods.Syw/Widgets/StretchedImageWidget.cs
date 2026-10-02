using OpenRA.Graphics;
using OpenRA.Widgets;

namespace OpenRA.Mods.Syw.Widgets
{
	// Draws a chrome image stretched to fill the widget bounds (stock Image widgets draw at native size only).
	// Used for the original SYW title screen behind the main menu.
	public class StretchedImageWidget : Widget
	{
		public string ImageCollection = "";
		public string ImageName = "";

		[ObjectCreator.UseCtor]
		public StretchedImageWidget() { }

		protected StretchedImageWidget(StretchedImageWidget other)
			: base(other)
		{
			ImageCollection = other.ImageCollection;
			ImageName = other.ImageName;
		}

		public override Widget Clone() { return new StretchedImageWidget(this); }

		public override void Draw()
		{
			var sprite = ChromeProvider.GetImage(ImageCollection, ImageName);
			if (sprite == null)
				return;

			var b = RenderBounds;
			var scale = new float3(b.Width / sprite.Size.X, b.Height / sprite.Size.Y, 1);
			Game.Renderer.RgbaSpriteRenderer.DrawSprite(sprite, new float3(b.X, b.Y, 0), scale);
		}
	}
}
