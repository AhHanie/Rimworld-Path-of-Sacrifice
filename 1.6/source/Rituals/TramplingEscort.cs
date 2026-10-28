using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Path_of_Sacrifice
{
    /// <summary>
    /// A prisoner cannot be expected to walk to the altar on their own, so one of the tramplers carries them there.
    /// Colonists and slaves still walk to the victim cell themselves.
    /// </summary>
    public static class TramplingEscort
    {
        public static bool Needed(Pawn victim)
        {
            return victim != null && victim.IsPrisonerOfColony;
        }

        public static bool CanEscort(Pawn candidate, Pawn victim, Thing altar)
        {
            return candidate != null
                && candidate != victim
                && candidate.Spawned
                && !candidate.Downed
                && candidate.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
                && candidate.CanReach(victim, PathEndMode.Touch, Danger.Deadly)
                && (altar == null || candidate.CanReach(altar, PathEndMode.Touch, Danger.Deadly));
        }

        // The closest able trampler, so the walk to the prisoner is short.
        public static Pawn Pick(IEnumerable<Pawn> candidates, Pawn victim, Thing altar)
        {
            return candidates
                .Where(c => CanEscort(c, victim, altar))
                .OrderBy(c => c.Position.DistanceToSquared(victim.Position))
                .FirstOrDefault();
        }
    }
}
