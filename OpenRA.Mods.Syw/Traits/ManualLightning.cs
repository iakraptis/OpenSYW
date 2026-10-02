using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Syw.Traits
{
    public class ManualLightningInfo : TraitInfo, Requires<AttackFrontalInfo>, Requires<AmmoPoolInfo>
    {
        [GrantedConditionReference]
        public readonly string Condition = "casting-lightning";
        public override object Create(ActorInitializer init) => new ManualLightning(this);
    }
    public class ManualLightning : IResolveOrder, INotifyAttack, ITick
    {
        public const string OrderId = "SywCastLightning";
        readonly ManualLightningInfo info;
        public ManualLightning(ManualLightningInfo info) { this.info = info; }
        int token = Actor.InvalidConditionToken;
        Target target;
        public bool CanCast(Actor self, Armament spell) => token == Actor.InvalidConditionToken &&
            self.TraitsImplementing<AmmoPool>().First(p => p.Info.Name == "mana").CurrentAmmoCount >= spell.Info.AmmoUsage &&
            spell.FireDelay == 0;
        void Clear(Actor self)
        {
            if (token != Actor.InvalidConditionToken)
                token = self.RevokeCondition(token);
        }
        public void ResolveOrder(Actor self, Order order)
        {
            if (order.OrderString != OrderId)
            {
                if (order.OrderString == "Stop" || order.OrderString == "Move" || order.OrderString == "Attack" || order.OrderString == "ForceAttack")
                    Clear(self);
                return;
            }
            var spell = self.TraitsImplementing<Armament>().First(a => a.Info.Name == "secondary");
            if (!CanCast(self, spell) || order.Target.Type != TargetType.Actor || !order.Target.IsValidFor(self) ||
                self.World.FogObscures(order.Target.Actor) ||
                !spell.Info.TargetRelationships.HasRelationship(self.Owner.RelationshipWith(order.Target.Actor.Owner)) ||
                !spell.Weapon.IsValidAgainst(order.Target, self.World, self))
                return;
            target = order.Target;
            token = self.GrantCondition(info.Condition);
            self.TraitsImplementing<AttackFrontal>().First(a => a.Info.Armaments.Contains("secondary")).AttackTarget(target, AttackSource.Default, false, true);
        }
        public void Attacking(Actor self, in Target target, Armament armament, Barrel barrel)
        {
            if (armament.Info.Name != "secondary" || token == Actor.InvalidConditionToken)
                return;
            self.World.AddFrameEndTask(w =>
            {
                if (self.Disposed) return;
                self.CancelActivity();
                Clear(self);
            });
        }
        public void PreparingAttack(Actor self, in Target target, Armament armament, Barrel barrel) { }
        public void Tick(Actor self)
        {
            if (token != Actor.InvalidConditionToken && (self.IsIdle || !target.IsValidFor(self)))
                Clear(self);
        }
    }
}
