namespace OpenRA.Mods.Syw.Traits
{
    // A caster spell aimed at one unit (Shaman Transform, Witch Bewilderment). Lets one order generator serve both.
    public interface IUnitTargetSpell
    {
        string SpellOrderId { get; }
        bool CanCast(Actor self);
        bool ValidTarget(Actor self, Actor target);
    }
}
