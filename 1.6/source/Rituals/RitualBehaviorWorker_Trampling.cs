using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Path_of_Sacrifice
{
    public class RitualBehaviorWorker_Trampling : RitualBehaviorWorker
    {
        public RitualBehaviorWorker_Trampling()
        {
        }

        public RitualBehaviorWorker_Trampling(RitualBehaviorDef def)
            : base(def)
        {
        }

        protected override LordJob CreateLordJob(TargetInfo target, Pawn organizer, Precept_Ritual ritual, RitualObligation obligation, RitualRoleAssignments assignments)
        {
            return new LordJob_Ritual_Trampling(target, ritual, obligation, def.stages, assignments, organizer);
        }

        private const int AwaitCarryTicks = 2500;

        // A prisoner stays lying where they are until the escort carries them back to bed, as after vanilla's execution ritual.
        public override void PostCleanup(LordJob_Ritual ritual)
        {
            if (!(ritual is LordJob_Ritual_Trampling trampling))
            {
                return;
            }
            Pawn victim = trampling.Victim;
            Pawn escort = trampling.Escort;
            if (victim == null || !victim.IsPrisonerOfColony || victim.Dead || !victim.Spawned)
            {
                return;
            }
            // Long enough to be carried back to bed before escaping becomes an option again.
            victim.guest?.WaitInsteadOfEscapingFor(AwaitCarryTicks);

            if (!victim.Downed)
            {
                Job lie = JobMaker.MakeJob(PoS_JobDefOf.PoS_AwaitCarry, victim.Position);
                lie.expiryInterval = AwaitCarryTicks;
                victim.jobs.StartJob(lie, JobCondition.InterruptForced);
            }

            if (escort == null || !escort.Spawned || escort.Downed || escort.Dead || victim.InAggroMentalState || victim.IsForbidden(escort)
                || !escort.CanReserveAndReach(victim, PathEndMode.OnCell, escort.NormalMaxDanger(), 1, -1, null, ignoreOtherReservations: true))
            {
                return;
            }
            Building_Bed bed = RestUtility.FindBedFor(victim, escort, checkSocialProperness: false, ignoreOtherReservations: false, GuestStatus.Prisoner);
            if (bed == null)
            {
                return;
            }
            // The vanilla take-to-bed driver carries its target, so the prisoner is never walked back on their own feet.
            Job carry = JobMaker.MakeJob(JobDefOf.TakeWoundedPrisonerToBed, victim, bed);
            carry.count = 1;
            escort.jobs.StartJob(carry, JobCondition.InterruptForced);
        }

        // Auto-fill the trampler list, but never beyond the cap, so the dialog does not start in a blocked state.
        public override bool ShouldInitAsSpectator(Pawn pawn, RitualRoleAssignments assignments)
        {
            return assignments.SpectatorsForReading.Count < TramplingLayout.MaxWalkers;
        }
    }
}
