using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("Gives the Stop order (the command bar's Stop button and its hotkey) a voice. No engine trait voices it.")]
	public class StopOrderVoiceInfo : TraitInfo
	{
		[VoiceReference]
		public readonly string Voice = "Stop";

		public override object Create(ActorInitializer init) => new StopOrderVoice(this);
	}

	public class StopOrderVoice : IOrderVoice
	{
		readonly StopOrderVoiceInfo info;

		public StopOrderVoice(StopOrderVoiceInfo info) { this.info = info; }

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order) => order.OrderString == "Stop" ? info.Voice : null;
	}
}
