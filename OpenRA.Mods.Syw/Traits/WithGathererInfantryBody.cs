using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("Infantry body with loaded movement and a harvesting animation driven by stock Harvester events.")]
	public class WithGathererInfantryBodyInfo : WithInfantryBodyInfo, Requires<HarvesterInfo>
	{
		[SequenceReference]
		public readonly string HarvestSequence = "harvest";

		[SequenceReference(prefix: true)]
		public readonly string LoadedSequencePrefix = "loaded-";

		public override object Create(ActorInitializer init) { return new WithGathererInfantryBody(init, this); }
	}

	public class WithGathererInfantryBody : WithInfantryBody, INotifyHarvestAction
	{
		readonly WithGathererInfantryBodyInfo gathererInfo;
		Harvester harvester;
		IMove move;
		bool wasLoaded;
		bool harvestPending;

		public WithGathererInfantryBody(ActorInitializer init, WithGathererInfantryBodyInfo info)
			: base(init, info)
		{
			gathererInfo = info;
		}

		protected override void Created(Actor self)
		{
			harvester = self.Trait<Harvester>();
			move = self.Trait<IMove>();
			base.Created(self);
		}

		protected override string NormalizeInfantrySequence(Actor self, string baseSequence)
		{
			// The base constructor calls this before our fields are initialized.
			if (harvester != null && !harvester.IsEmpty &&
				DefaultAnimation.HasSequence(gathererInfo.LoadedSequencePrefix + baseSequence))
				return gathererInfo.LoadedSequencePrefix + baseSequence;

			return base.NormalizeInfantrySequence(self, baseSequence);
		}

		protected override void Tick(Actor self)
		{
			var loaded = !harvester.IsEmpty;
			if (loaded != wasLoaded && state != AnimationState.Attacking)
				PlayStandAnimation(self);
			wasLoaded = loaded;

			// Retains infantry movement, idle, and interruption behavior. Moving
			// immediately replaces a harvesting animation with the appropriate walk.
			base.Tick(self);
		}

		void INotifyHarvestAction.Harvested(Actor self, string resourceType)
		{
			harvestPending = true;
			// Match WithInfantryBody's attack timing: start after its movement tick,
			// otherwise stopping on the resource cell can overwrite this animation.
			self.World.AddFrameEndTask(_ =>
			{
				if (!harvestPending || IsTraitDisabled || !self.IsInWorld || self.IsDead ||
					move.CurrentMovementTypes.HasMovementType(MovementType.Horizontal))
					return;

				harvestPending = false;
				state = AnimationState.Attacking;
				DefaultAnimation.PlayThen(gathererInfo.HarvestSequence, () => PlayStandAnimation(self));
			});
		}

		void CancelHarvestAnimation(Actor self)
		{
			harvestPending = false;
			if (state == AnimationState.Attacking)
				PlayStandAnimation(self);
		}

		void INotifyHarvestAction.MovingToResources(Actor self, CPos targetCell) { CancelHarvestAnimation(self); }
		void INotifyHarvestAction.MovementCancelled(Actor self) { CancelHarvestAnimation(self); }
	}
}
