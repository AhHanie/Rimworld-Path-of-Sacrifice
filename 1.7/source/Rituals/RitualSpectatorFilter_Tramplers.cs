using RimWorld;
using Verse;

namespace Path_of_Sacrifice
{
    /// <summary>Walkers must be free, able-bodied adult colonists.</summary>
    public class RitualSpectatorFilter_Tramplers : RitualSpectatorFilter
    {
        public override bool Allowed(Pawn p)
        {
            return p.IsFreeNonSlaveColonist
                && p.DevelopmentalStage.Adult()
                && !p.Downed
                && !p.InMentalState
                && p.health.capacities.CapableOf(PawnCapacityDefOf.Moving);
        }
    }
}
