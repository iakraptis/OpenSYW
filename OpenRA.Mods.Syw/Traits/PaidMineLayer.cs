using System.Collections.Generic;
using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	public class PaidMineLayerInfo : TraitInfo, Requires<AmmoPoolInfo>, Requires<MobileInfo>
	{
		[ActorReference]
		public readonly string Mine = "syw-mine";
		public readonly int ManaCost = 3;
		public readonly int CreditCost = 100;
		[Desc("Mines laid by one minefield order, in a straight line centred on the target cell.")]
		public readonly int LineLength = 3;
		public override object Create(ActorInitializer init) => new PaidMineLayer(this);
	}

	// A single synchronized transaction: failed/cancelled placement spends nothing.
	public class PaidMineLayer : IResolveOrder
	{
		public const string OrderId = "SywLayMine";
		public const string LineOrderId = "SywLayMineLine";
		public const int Orientations = 4;

		// Horizontal, diagonal (down-right), vertical, diagonal (up-right).
		static readonly CVec[] Directions = { new(1, 0), new(1, 1), new(0, 1), new(1, -1) };

		public readonly PaidMineLayerInfo Info;
		public PaidMineLayer(PaidMineLayerInfo info) { Info = info; }

		static AmmoPool Mana(Actor self) => self.TraitsImplementing<AmmoPool>().First(p => p.Info.Name == "mana");
		public int LineManaCost => Info.ManaCost * Info.LineLength;
		public int LineCreditCost => Info.CreditCost * Info.LineLength;

		public bool CanPlace(Actor self) => !self.IsDead && self.IsInWorld &&
			Mana(self).CurrentAmmoCount >= Info.ManaCost &&
			self.Owner.PlayerActor.Trait<PlayerResources>().GetCashAndResources() >= Info.CreditCost &&
			self.Trait<Mobile>().CanStayInCell(self.Location) &&
			self.World.ActorMap.GetActorsAt(self.Location).All(a => a == self);

		public bool CanAffordLine(Actor self) => !self.IsDead && self.IsInWorld &&
			Mana(self).CurrentAmmoCount >= LineManaCost &&
			self.Owner.PlayerActor.Trait<PlayerResources>().GetCashAndResources() >= LineCreditCost;

		public CPos[] LineCells(CPos center, int orientation)
		{
			var d = Directions[(orientation % Orientations + Orientations) % Orientations];
			var first = -(Info.LineLength - 1) / 2;
			return Enumerable.Range(first, Info.LineLength).Select(i => center + d * i).ToArray();
		}

		// Every cell must be on the map, walkable and empty; only the Miner itself may stand in one.
		public bool CanPlaceLine(Actor self, CPos center, int orientation)
		{
			var mobile = self.Trait<Mobile>();
			return LineCells(center, orientation).All(c => self.World.Map.Contains(c) && mobile.CanStayInCell(c) &&
				self.World.ActorMap.GetActorsAt(c).All(a => a == self));
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == OrderId)
			{
				if (CanPlace(self))
					self.QueueActivity(order.Queued, new LayMine(this));
				return;
			}

			if (order.OrderString != LineOrderId || order.Target.Type != TargetType.Terrain)
				return;

			var center = self.World.Map.CellContaining(order.Target.CenterPosition);
			var orientation = (int)(order.ExtraData % Orientations);
			if (!CanAffordLine(self) || !CanPlaceLine(self, center, orientation))
				return;

			// Walk to the middle cell, then lay the whole line from there.
			self.QueueActivity(order.Queued, self.Trait<Mobile>().MoveTo(center, 0));
			self.QueueActivity(true, new LayMineLine(this, center, orientation));
		}

		sealed class LayMine : Activity
		{
			readonly PaidMineLayer layer;
			Actor construction;
			bool resolved;

			public LayMine(PaidMineLayer layer) { this.layer = layer; }

			protected override void OnFirstRun(Actor self)
			{
				// Once placement begins, Move/Stop must not interrupt construction.
				IsInterruptible = false;
				var owner = self.Owner;
				var cell = self.Location;
				self.World.AddFrameEndTask(w =>
				{
					resolved = true;
					if (self.Disposed || self.Owner != owner || self.Location != cell || !layer.CanPlace(self)) return;
					var resources = owner.PlayerActor.Trait<PlayerResources>();
					if (!resources.TakeCash(layer.Info.CreditCost)) return;
					Mana(self).TakeAmmo(self, layer.Info.ManaCost);
					construction = w.CreateActor(layer.Info.Mine, new TypeDictionary { new OwnerInit(owner), new LocationInit(cell) });
				});
			}

			// The construction actor owns the timer: no second duration to drift.
			public override bool Tick(Actor self) => resolved &&
				(construction == null || construction.IsDead || !construction.IsInWorld);
		}

		sealed class LayMineLine : Activity
		{
			readonly PaidMineLayer layer;
			readonly CPos center;
			readonly int orientation;
			readonly List<Actor> constructions = new();
			bool resolved;

			public LayMineLine(PaidMineLayer layer, CPos center, int orientation)
			{
				this.layer = layer;
				this.center = center;
				this.orientation = orientation;
			}

			protected override void OnFirstRun(Actor self)
			{
				// Once placement begins, Move/Stop must not interrupt construction.
				IsInterruptible = false;
				var owner = self.Owner;
				self.World.AddFrameEndTask(w =>
				{
					resolved = true;

					// All or nothing: a blocked cell or missing funds spends nothing.
					if (self.Disposed || self.Owner != owner || self.Location != center ||
						!layer.CanAffordLine(self) || !layer.CanPlaceLine(self, center, orientation))
						return;

					if (!owner.PlayerActor.Trait<PlayerResources>().TakeCash(layer.LineCreditCost))
						return;

					Mana(self).TakeAmmo(self, layer.LineManaCost);
					foreach (var cell in layer.LineCells(center, orientation))
						constructions.Add(w.CreateActor(layer.Info.Mine, new TypeDictionary { new OwnerInit(owner), new LocationInit(cell) }));
				});
			}

			// The Miner stays until every mine in the line is armed (or destroyed).
			public override bool Tick(Actor self) => resolved && constructions.All(c => c.IsDead || !c.IsInWorld);
		}
	}
}
