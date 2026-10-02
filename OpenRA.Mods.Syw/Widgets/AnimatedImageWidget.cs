using OpenRA.Graphics;
using OpenRA.Widgets;

namespace OpenRA.Mods.Syw.Widgets
{
	// Loops chrome images named "0", "1", ... spread over one or more collections, stretched with the same scale as a
	// SourceSize screen stretched to the widget bounds and anchored to the bottom edge. Used for the original fire
	// animation (ani.anm) over the SYW title screen.
	public class AnimatedImageWidget : Widget
	{
		public string[] ImageCollections = System.Array.Empty<string>();
		public int Frames = 1;

		[Desc("Milliseconds per frame.")]
		public int FrameTime = 60;

		[Desc("The screen size the frames were drawn for; they are scaled as that screen would be to fill the widget.")]
		public int2 SourceSize = new(640, 480);

		[ObjectCreator.UseCtor]
		public AnimatedImageWidget() { }

		protected AnimatedImageWidget(AnimatedImageWidget other)
			: base(other)
		{
			ImageCollections = other.ImageCollections;
			Frames = other.Frames;
			FrameTime = other.FrameTime;
			SourceSize = other.SourceSize;
		}

		public override Widget Clone() { return new AnimatedImageWidget(this); }

		Sprite FrameSprite(int frame)
		{
			var name = frame.ToStringInvariant();
			foreach (var collection in ImageCollections)
			{
				var sprite = ChromeProvider.TryGetImage(collection, name);
				if (sprite != null)
					return sprite;
			}

			return null;
		}

		public override void Draw()
		{
			var sprite = FrameSprite((int)(Game.RunTime / FrameTime % Frames));
			if (sprite == null)
				return;

			var b = RenderBounds;
			var scale = new float3((float)b.Width / SourceSize.X, (float)b.Height / SourceSize.Y, 1);
			var pos = new float3(b.X, b.Bottom - sprite.Size.Y * scale.Y, 0);
			Game.Renderer.RgbaSpriteRenderer.DrawSprite(sprite, pos, scale);
		}
	}
}
