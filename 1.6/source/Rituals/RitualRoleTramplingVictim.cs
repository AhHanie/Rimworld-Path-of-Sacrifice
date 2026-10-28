using RimWorld;
using Verse;

namespace Path_of_Sacrifice
{
    /// <summary>
    /// The person who lies in front of the altar. Unlike vanilla's scarification target this does not depend on the
    /// victim's ideoligion, so adult colonists, prisoners and slaves of the colony all qualify.
    /// </summary>
    public class RitualRoleTramplingVictim : RitualRole
    {
        public override bool AppliesToPawn(Pawn p, out string reason, TargetInfo selectedTarget, LordJob_Ritual ritual = null, RitualRoleAssignments assignments = null, Precept_Ritual precept = null, bool skipReason = false)
        {
            if (!AppliesIfChild(p, out reason, skipReason))
            {
                return false;
            }

            if (!p.RaceProps.Humanlike)
            {
                reason = skipReason ? null : "PoS_Trampling_VictimMustBeHumanlike".Translate(LabelCap).Resolve();
                return false;
            }
            if (!p.DevelopmentalStage.Adult())
            {
                reason = skipReason ? null : "PoS_Trampling_VictimMustBeAdult".Translate(LabelCap).Resolve();
                return false;
            }
            bool colonyControlled = p.Faction == Faction.OfPlayer || p.IsPrisonerOfColony || p.IsSlaveOfColony;
            if (!colonyControlled)
            {
                reason = skipReason ? null : "PoS_Trampling_VictimMustBeControlled".Translate(LabelCap).Resolve();
                return false;
            }
            if (p.Dead || !p.Spawned || p.InMentalState)
            {
                reason = skipReason ? null : "PoS_Trampling_VictimMustBeAvailable".Translate(LabelCap).Resolve();
                return false;
            }
            if (p.Downed)
            {
                reason = skipReason ? null : "PoS_Trampling_VictimMustNotBeDowned".Translate(LabelCap).Resolve();
                return false;
            }
            return true;
        }

        public override bool AppliesToRole(Precept_Role role, out string reason, Precept_Ritual ritual = null, Pawn pawn = null, bool skipReason = false)
        {
            reason = null;
            return false;
        }
    }
}
