using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Path_of_Sacrifice
{
    /// <summary>Keeps the victim lying face up on the exact cell until the closing stage.</summary>
    public class JobGiver_TramplingVictim : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            LordJob_Ritual_Trampling lordJob = pawn.GetLord()?.LordJob as LordJob_Ritual_Trampling;
            if (lordJob?.Layout == null || lordJob.VictimReleased)
            {
                return null;
            }
            if (lordJob.DeliveryPending)
            {
                // A carried prisoner just waits to be picked up; they cannot walk to the altar themselves.
                Job wait = JobMaker.MakeJob(JobDefOf.Wait);
                wait.expiryInterval = 120;
                wait.checkOverrideOnExpire = true;
                return wait;
            }
            return JobMaker.MakeJob(PoS_JobDefOf.PoS_LieAsVictim, lordJob.Layout.victimCell);
        }
    }

    /// <summary>The active trampler crosses; everyone else waits at their own cell.</summary>
    public class JobGiver_Trampler : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            LordJob_Ritual_Trampling lordJob = pawn.GetLord()?.LordJob as LordJob_Ritual_Trampling;
            if (lordJob?.Layout == null || !lordJob.IsActiveTrampler(pawn))
            {
                return null;
            }

            if (lordJob.TryGetDeliveryJob(pawn, out Job deliver))
            {
                return deliver;
            }

            if (lordJob.TryGetNextWaypoint(pawn, out IntVec3 waypoint))
            {
                Job cross = JobMaker.MakeJob(PoS_JobDefOf.PoS_TramplerCross, waypoint);
                cross.locomotionUrgency = LocomotionUrgency.Walk;
                return cross;
            }

            if (lordJob.TryGetSlot(pawn, out IntVec3 slot))
            {
                Job hold = JobMaker.MakeJob(PoS_JobDefOf.PoS_TramplerHold, slot);
                hold.locomotionUrgency = LocomotionUrgency.Walk;
                return hold;
            }
            return null;
        }
    }
}
