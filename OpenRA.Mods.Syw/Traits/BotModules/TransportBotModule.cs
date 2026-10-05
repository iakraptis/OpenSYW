using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Pathfinder;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Ferries soldiers to enemy buildings they can't walk to (islands), by ship or by air, one trip at a time:",
		"orders a transport if it has none, gathers idle soldiers at a pickup point on its own side, carries them to a",
		"drop point with a walking route to the target, unloads and sends them to attack-move on the target. Every step",
		"has a timeout, so a sunk transport or a blocked landing never stalls the bot.")]
	public class TransportBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Ships that carry units. Unloaded from water next to the shore.")]
		public readonly HashSet<string> NavalTransportTypes = new();

		[Desc("Aircraft that carry units. Land to load and unload.")]
		public readonly HashSet<string> AirTransportTypes = new();

		[Desc("Units the bot may send on a trip (idle ones near its base).")]
		public readonly HashSet<string> PassengerTypes = new();

		[Desc("Fewest idle soldiers worth a trip.")]
		public readonly int MinimumPassengers = 4;

		[Desc("Ticks between plans while no trip is running.")]
		public readonly int ScanInterval = 100;

		[Desc("Ticks a step (sailing to the pickup, loading, travelling, unloading) may take before the trip is dropped.")]
		public readonly int StepTimeout = 2000;

		[Desc("Ticks to wait after a trip (or a dropped one) before planning the next.")]
		public readonly int Cooldown = 500;

		[Desc("How far from the base and the target to look for a pickup or drop point, in cells.")]
		public readonly int SearchRadius = 20;

		[Desc("Drop points are chosen to have as few armed enemies (towers, units) within this many cells as possible.")]
		public readonly int DangerRadius = 11;

		[Desc("Locomotor of the passengers and of the ships.")]
		public readonly string FootLocomotor = "foot";
		public readonly string NavalLocomotor = "naval";

		public override object Create(ActorInitializer init) { return new TransportBotModule(init.Self, this); }
	}

	public class TransportBotModule : ConditionalTrait<TransportBotModuleInfo>, IBotTick
	{
		enum Phase { Idle, ToPickup, Loading, Travelling, Unloading }

		// Ticks between progress checks of a running trip.
		const int StepInterval = 25;

		// Orders are repeated this often while a step waits (the squad manager may have sent a passenger elsewhere).
		const int ReorderInterval = 150;

		readonly World world;
		readonly Player player;
		IPathFinder pathFinder;
		Locomotor foot, naval;

		Phase phase;
		int ticks, stepStarted, nextPlan, lastOrders;
		Actor transport, target;
		bool byAir;
		CPos pickup, drop, pickupLand, dropLand;
		List<Actor> passengers = new();

		public TransportBotModule(Actor self, TransportBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void Created(Actor self)
		{
			pathFinder = world.WorldActor.Trait<IPathFinder>();
			foot = BotMapAnalysis.Locomotor(world, Info.FootLocomotor);
			naval = BotMapAnalysis.Locomotor(world, Info.NavalLocomotor);
			base.Created(self);
		}

		void IBotTick.BotTick(IBot bot)
		{
			ticks++;
			if (foot == null)
				return;

			if (phase == Phase.Idle)
			{
				if (ticks >= nextPlan && ticks % Info.ScanInterval == 0)
					Plan(bot);

				return;
			}

			if (ticks % StepInterval == 0)
				Step(bot);
		}

		bool Alive(Actor a) => a != null && !a.IsDead && !a.Disposed && a.Owner == player;

		Cargo TransportCargo => transport.Trait<Cargo>();

		void Plan(IBot bot)
		{
			var home = world.ActorsHavingTrait<Building>()
				.Where(a => a.Owner == player && !a.IsDead && a.IsInWorld && a.Info.HasTraitInfo<HealthInfo>())
				.Select(a => BotMapAnalysis.LandCellBeside(world, a, foot)).FirstOrDefault(c => c != null);
			if (home == null)
				return;

			var homeCell = home.Value;
			target = BotMapAnalysis.UnreachableByLand(world, player, foot, homeCell)
				.OrderBy(b => (b.Location - homeCell).LengthSquared).FirstOrDefault();
			if (target == null)
				return;

			var targetLand = BotMapAnalysis.LandCellBeside(world, target, foot);
			if (targetLand == null)
				return;

			var soldiers = world.Actors.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead && a.IsIdle
					&& Info.PassengerTypes.Contains(a.Info.Name) && (a.Location - homeCell).LengthSquared <= 30 * 30
					&& pathFinder.PathExistsForLocomotor(foot, a.Location, homeCell))
				.ToList();
			if (soldiers.Count < Info.MinimumPassengers)
				return;

			// Not IsIdle: an aircraft circling or waiting in the air always has an activity.
			var transports = world.Actors.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead
				&& (Info.NavalTransportTypes.Contains(a.Info.Name) || Info.AirTransportTypes.Contains(a.Info.Name))
				&& a.Trait<Cargo>().IsEmpty()).ToList();

			foreach (var t in transports.OrderBy(t => Info.NavalTransportTypes.Contains(t.Info.Name) ? 0 : 1))
				if (TryRoute(t, homeCell, targetLand.Value))
				{
					Start(bot, t, soldiers);
					return;
				}

			if (!world.Actors.Any(a => a.Owner == player && !a.IsDead
				&& (Info.NavalTransportTypes.Contains(a.Info.Name) || Info.AirTransportTypes.Contains(a.Info.Name))))
				OrderTransport(bot, targetLand.Value);
		}

		// Pickup and drop points for this transport: shore cells on both ends for a ship (on water it can sail
		// between), open ground for an aircraft.
		bool TryRoute(Actor t, CPos homeCell, CPos targetLand)
		{
			byAir = Info.AirTransportTypes.Contains(t.Info.Name);
			var armed = ArmedEnemies();
			if (byAir)
			{
				var landing = OpenGround(targetLand, target.Location).Select(c => (Cell: c, Danger: Danger(armed, c)))
					.OrderBy(c => c.Danger).Select(c => (CPos?)c.Cell).FirstOrDefault();
				var start = OpenGround(homeCell, homeCell).Cast<CPos?>().FirstOrDefault();
				if (landing == null || start == null)
					return false;

				pickup = pickupLand = start.Value;
				drop = dropLand = landing.Value;
				return true;
			}

			if (naval == null)
				return false;

			var dropShore = BotMapAnalysis.Shore(world, naval, t.Location, foot, targetLand, target.Location, Info.SearchRadius)
				.OrderBy(s => Danger(armed, s.Water)).Cast<(CPos, CPos)?>().FirstOrDefault();
			var pickupShore = BotMapAnalysis.Shore(world, naval, t.Location, foot, homeCell, homeCell, Info.SearchRadius)
				.Cast<(CPos, CPos)?>().FirstOrDefault();
			if (dropShore == null || pickupShore == null)
				return false;

			(drop, dropLand) = dropShore.Value;
			(pickup, pickupLand) = pickupShore.Value;
			return true;
		}

		// Free land cells (no building, not water) 3 to SearchRadius cells from center, nearest first, from which reach
		// can be walked to; an aircraft lands there, beside the target rather than on it.
		IEnumerable<CPos> OpenGround(CPos reach, CPos center)
		{
			foreach (var cell in world.Map.FindTilesInAnnulus(center, 3, Info.SearchRadius))
			{
				if (BotMapAnalysis.IsWater(world, cell) || foot.MovementCostForCell(cell) == PathGraph.MovementCostForUnreachableCell
					|| world.ActorMap.GetActorsAt(cell).Any(a => a.TraitOrDefault<Building>() != null))
					continue;

				if (pathFinder.PathExistsForLocomotor(foot, cell, reach))
					yield return cell;
			}
		}

		// Where the enemy's towers and armed units stand.
		List<CPos> ArmedEnemies() =>
			world.ActorsHavingTrait<Armament>().Where(a => !a.IsDead && a.IsInWorld && !a.Owner.NonCombatant
				&& player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy).Select(a => a.Location).ToList();

		// How many of them are within DangerRadius of a drop point: a transport landing or unloading there is a sitting
		// target, so the safest point near the target is used (the nearest of equally safe ones).
		int Danger(List<CPos> armed, CPos cell) =>
			armed.Count(a => (a - cell).LengthSquared <= Info.DangerRadius * Info.DangerRadius);

		// Orders a transport from a building that can make one: a ship if one of our shipyards can reach the target's
		// coast, otherwise an aircraft.
		void OrderTransport(IBot bot, CPos targetLand)
		{
			var queues = world.ActorsWithTrait<ProductionQueue>()
				.Where(q => q.Actor.Owner == player && !q.Actor.IsDead && q.Trait.Enabled).Select(q => q.Trait).ToList();

			bool Queued(HashSet<string> types) => queues.Any(q => q.AllQueued().Any(i => types.Contains(i.Item)));
			if (Queued(Info.NavalTransportTypes) || Queued(Info.AirTransportTypes))
				return;

			foreach (var q in queues)
			{
				var ship = q.BuildableItems().FirstOrDefault(i => Info.NavalTransportTypes.Contains(i.Name));
				if (ship != null && naval != null && BotMapAnalysis.Shore(world, naval, NearestWater(q.Actor.Location),
					foot, targetLand, target.Location, Info.SearchRadius).Any())
				{
					AIUtils.BotDebug("{0}: ordering transport ship {1} for {2}.", player, ship.Name, target.Info.Name);
					bot.QueueOrder(Order.StartProduction(q.Actor, ship.Name, 1));
					return;
				}
			}

			foreach (var q in queues)
			{
				var aircraft = q.BuildableItems().FirstOrDefault(i => Info.AirTransportTypes.Contains(i.Name));
				if (aircraft != null)
				{
					AIUtils.BotDebug("{0}: ordering transport aircraft {1} for {2}.", player, aircraft.Name, target.Info.Name);
					bot.QueueOrder(Order.StartProduction(q.Actor, aircraft.Name, 1));
					return;
				}
			}
		}

		CPos NearestWater(CPos cell) =>
			world.Map.FindTilesInCircle(cell, 6).Cast<CPos?>().FirstOrDefault(c => BotMapAnalysis.IsWater(world, c.Value)) ?? cell;

		void Start(IBot bot, Actor t, List<Actor> soldiers)
		{
			transport = t;
			var cargo = t.Info.TraitInfo<CargoInfo>();
			var room = cargo.MaxWeight;
			passengers = new List<Actor>();
			foreach (var s in soldiers.OrderBy(s => (s.Location - pickupLand).LengthSquared))
			{
				var p = s.Info.TraitInfoOrDefault<PassengerInfo>();
				if (p == null || !cargo.Types.Contains(p.CargoType) || p.Weight > room)
					continue;

				passengers.Add(s);
				room -= p.Weight;
			}

			if (passengers.Count < Info.MinimumPassengers)
				return;

			AIUtils.BotDebug("{0}: {1} carries {2} soldiers from {3} to {4} for {5}.", player, t.Info.Name, passengers.Count,
				pickup, drop, target.Info.Name);
			bot.QueueOrder(new Order("Move", transport, Target.FromCell(world, pickup), false));
			Enter(Phase.ToPickup);
		}

		void Enter(Phase next)
		{
			phase = next;
			stepStarted = ticks;
			lastOrders = 0;
		}

		void Finish()
		{
			phase = Phase.Idle;
			nextPlan = ticks + Info.Cooldown;
			transport = null;
			target = null;
			passengers.Clear();
		}

		bool Near(CPos cell, int cells) => (transport.Location - cell).LengthSquared <= cells * cells;

		bool Reorder()
		{
			if (ticks - lastOrders < ReorderInterval)
				return false;

			lastOrders = ticks;
			return true;
		}

		void Step(IBot bot)
		{
			if (!Alive(transport))
			{
				AIUtils.BotDebug("{0}: transport lost, trip dropped.", player);
				Finish();
				return;
			}

			var timedOut = ticks - stepStarted > Info.StepTimeout;
			var cargo = TransportCargo;
			passengers.RemoveAll(p => !Alive(p));

			switch (phase)
			{
				case Phase.ToPickup:
					if (Near(pickup, 1) || (byAir && Near(pickup, 2)))
					{
						Enter(Phase.Loading);
						return;
					}

					if (timedOut || passengers.Count == 0)
						Release(bot);
					else if (transport.IsIdle && Reorder())
						bot.QueueOrder(new Order("Move", transport, Target.FromCell(world, pickup), false));
					return;

				case Phase.Loading:
				{
					var waiting = passengers.Where(p => p.IsInWorld).ToList();
					if (waiting.Count == 0 || (timedOut && !cargo.IsEmpty()))
					{
						if (cargo.IsEmpty())
						{
							Release(bot);
							return;
						}

						bot.QueueOrder(new Order("Move", transport, Target.FromCell(world, drop), false));
						Enter(Phase.Travelling);
						return;
					}

					if (timedOut)
					{
						Release(bot);
						return;
					}

					if (Reorder())
						foreach (var p in waiting)
							bot.QueueOrder(new Order("EnterTransport", p, Target.FromActor(transport), false));
					return;
				}

				case Phase.Travelling:
					if (Near(drop, 1) || (byAir && Near(drop, 2)))
					{
						bot.QueueOrder(new Order("Unload", transport, false));
						Enter(Phase.Unloading);
						return;
					}

					if (timedOut)
					{
						// Stuck on the way: try to unload where it is, else bring them home.
						bot.QueueOrder(new Order("Unload", transport, false));
						Enter(Phase.Unloading);
					}
					else if (transport.IsIdle && Reorder())
						bot.QueueOrder(new Order("Move", transport, Target.FromCell(world, drop), false));
					return;

				case Phase.Unloading:
					if (cargo.IsEmpty())
					{
						var attackAt = Alive(target) ? target.Location : dropLand;
						foreach (var p in passengers.Where(p => p.IsInWorld))
							bot.QueueOrder(new Order("AttackMove", p, Target.FromCell(world, attackAt), false));

						bot.QueueOrder(new Order("Move", transport, Target.FromCell(world, pickup), false));
						AIUtils.BotDebug("{0}: landed {1} soldiers near {2}.", player, passengers.Count, attackAt);
						Finish();
					}
					else if (timedOut)
					{
						AIUtils.BotDebug("{0}: could not unload, trip dropped.", player);
						Finish();
					}
					else if (transport.IsIdle && Reorder())
						bot.QueueOrder(new Order("Unload", transport, false));
					return;
			}
		}

		// Ends the trip before departure: anyone already aboard gets off again at home.
		void Release(IBot bot)
		{
			if (!TransportCargo.IsEmpty())
				bot.QueueOrder(new Order("Unload", transport, false));

			AIUtils.BotDebug("{0}: trip dropped before departure.", player);
			Finish();
		}
	}
}
