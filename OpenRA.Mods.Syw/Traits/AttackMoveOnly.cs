using OpenRA.Network;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[Desc("Suicide boat (Japanese Torpedo): only accepts attack-move orders, and detonates on arrival.",
		"Plain move orders are rejected by the world's RejectMoveOrders trait. When the unit goes idle at its",
		"attack-move destination it kills itself, which fires its FireWarheadsOnDeath blast.")]
	public class AttackMoveOnlyInfo : TraitInfo
	{
		[Desc("How close to the destination counts as arrived.")]
		public readonly WDist ArrivalRange = WDist.FromCells(1);

		public override object Create(ActorInitializer init) => new AttackMoveOnly(this);
	}

	public class AttackMoveOnly : IResolveOrder, INotifyIdle
	{
		readonly AttackMoveOnlyInfo info;
		WPos? destination;

		public AttackMoveOnly(AttackMoveOnlyInfo info) { this.info = info; }

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			// The bots' naval squads can send an attack-move whose target has just become invalid.
			if ((order.OrderString == "AttackMove" || order.OrderString == "AssaultMove") && order.Target.Type != TargetType.Invalid)
				destination = order.Target.CenterPosition;
			else if (order.OrderString == "Stop")
				destination = null;
		}

		void INotifyIdle.TickIdle(Actor self)
		{
			if (destination == null)
				return;

			// Arrived: explode. Stopped short (blocked): wait for a new attack-move.
			var arrived = (self.CenterPosition - destination.Value).HorizontalLength <= info.ArrivalRange.Length;
			destination = null;
			if (arrived)
				self.Kill(self);
		}
	}

	[TraitLocation(SystemActors.World)]
	[Desc("Rejects plain move orders for units with AttackMoveOnly (players and bots alike).")]
	public class RejectMoveOrdersInfo : TraitInfo<RejectMoveOrders> { }

	public class RejectMoveOrders : IValidateOrder
	{
		public bool OrderValidation(OrderManager orderManager, World world, int clientId, Order order) =>
			order.OrderString != "Move" || order.Subject == null || !order.Subject.Info.HasTraitInfo<AttackMoveOnlyInfo>();
	}
}
