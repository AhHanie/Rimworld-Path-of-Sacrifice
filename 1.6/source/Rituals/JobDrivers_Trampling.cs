using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Path_of_Sacrifice
{
    /// <summary>Lies face up on the ground at the exact victim cell, and keeps doing so until the ritual releases them.</summary>
    public class JobDriver_TramplingVictim : JobDriver_LayDown
    {
        public override bool CanSleep => false;

        public override bool CanRest => false;

        public override bool LookForOtherJobs => false;

        public override Toil LayDownToil(bool hasBed)
        {
            Toil toil = Toils_LayDown.LayDown(TargetIndex.A, hasBed: false, lookForOtherJobs: false, canSleep: false, gainRestAndHealth: false, noBedLayingPosture: PawnPosture.LayingOnGroundFaceUp);
            toil.AddEndCondition(() => ShouldKeepLying() ? JobCondition.Ongoing : JobCondition.Succeeded);
            return toil;
        }

        protected virtual bool ShouldKeepLying()
        {
            LordJob_Ritual_Trampling lordJob = pawn.GetLord()?.LordJob as LordJob_Ritual_Trampling;
            return lordJob != null && !lordJob.VictimReleased;
        }

        public override string GetReport()
        {
            return "ReportLayingDown".Translate();
        }
    }

    /// <summary>After the ritual a prisoner stays where they are until someone carries them away. The job expires if nobody does.</summary>
    public class JobDriver_TramplingAwaitCarry : JobDriver_TramplingVictim
    {
        protected override bool ShouldKeepLying()
        {
            return pawn.IsPrisonerOfColony && !pawn.InBed();
        }
    }

    /// <summary>The escort picks the prisoner up and sets them down on the victim cell. Mirrors vanilla's deliver-to-altar job.</summary>
    public class JobDriver_DeliverVictim : JobDriver
    {
        private const TargetIndex VictimIndex = TargetIndex.A;
        private const TargetIndex CellIndex = TargetIndex.B;

        private Pawn Victim => (Pawn)job.GetTarget(VictimIndex).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Same as vanilla's delivery: whatever else had claimed the prisoner gives way to the ritual.
            pawn.Map.reservationManager.ReleaseAllForTarget(Victim);
            return pawn.Reserve(Victim, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(VictimIndex);
            this.FailOnAggroMentalStateAndHostile(VictimIndex);

            Toil goToVictim = Toils_Goto.GotoThing(VictimIndex, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(VictimIndex)
                .FailOnSomeonePhysicallyInteracting(VictimIndex);
            Toil startCarrying = Toils_Haul.StartCarryThing(VictimIndex);
            Toil goToCell = Toils_Goto.GotoCell(CellIndex, PathEndMode.OnCell);

            // Already carrying them (a recreated job after a load or an interruption): go straight to the cell.
            yield return Toils_Jump.JumpIf(goToCell, () => pawn.IsCarryingPawn(Victim));
            yield return goToVictim;
            yield return startCarrying;
            yield return goToCell;
            yield return Toils_Haul.PlaceHauledThingInCell(CellIndex, null, storageMode: false);

            Toil placed = ToilMaker.MakeToil("PoS_Placed");
            placed.defaultCompleteMode = ToilCompleteMode.Instant;
            placed.initAction = delegate
            {
                (pawn.GetLord()?.LordJob as LordJob_Ritual_Trampling)?.Notify_VictimPlaced();
            };
            yield return placed;
        }
    }

    /// <summary>Walks to the trampler's own waiting cell and stands there until it is their turn.</summary>
    public class JobDriver_TramplerHold : JobDriver
    {
        private const int WaitTicks = 180;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            Toil wait = ToilMaker.MakeToil("PoS_Hold");
            wait.defaultCompleteMode = ToilCompleteMode.Delay;
            wait.defaultDuration = WaitTicks;
            wait.tickAction = delegate
            {
                // Hand over to the crossing job the moment this trampler's turn begins.
                LordJob_Ritual_Trampling lordJob = pawn.GetLord()?.LordJob as LordJob_Ritual_Trampling;
                if (lordJob != null && lordJob.IsActiveWalker(pawn))
                {
                    EndJobWith(JobCondition.Succeeded);
                }
            };
            yield return wait;
        }
    }

    /// <summary>
    /// Walks one turn: entry, victim cell, exit, then the bypass back to the waiting cell. The lord decides the current
    /// waypoint and what arriving there means, so a recreated job simply carries on from where the lord says.
    /// </summary>
    public class JobDriver_TramplerCross : JobDriver
    {
        private LordJob_Ritual_Trampling LordJob => pawn.GetLord()?.LordJob as LordJob_Ritual_Trampling;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // The turn belongs to the lord; the victim's cell stays reserved by the victim's own job.
            return true;
        }

        public override bool IsContinuation(Job j)
        {
            return j.def == job.def;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil next = ToilMaker.MakeToil("PoS_NextWaypoint");
            next.defaultCompleteMode = ToilCompleteMode.Instant;
            next.initAction = delegate
            {
                LordJob_Ritual_Trampling lordJob = LordJob;
                if (lordJob == null || !lordJob.TryGetNextWaypoint(pawn, out IntVec3 waypoint))
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                job.SetTarget(TargetIndex.A, waypoint);
            };
            yield return next;

            Toil go = Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            go.FailOn(() => LordJob == null || !LordJob.IsActiveWalker(pawn));
            yield return go;

            Toil arrived = ToilMaker.MakeToil("PoS_Arrived");
            arrived.defaultCompleteMode = ToilCompleteMode.Instant;
            arrived.initAction = delegate
            {
                LordJob?.Notify_WaypointReached(pawn);
            };
            yield return arrived;

            yield return Toils_Jump.Jump(next);
        }
    }
}
