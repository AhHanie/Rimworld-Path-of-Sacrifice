using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Path_of_Sacrifice
{
    public class TramplerState : IExposable
    {
        public Pawn pawn;
        public int slotIndex;
        public int passes;
        public bool dropped;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref slotIndex, "slotIndex", 0);
            Scribe_Values.Look(ref passes, "passes", 0);
            Scribe_Values.Look(ref dropped, "dropped", false);
        }
    }

    /// <summary>
    /// Owns sequencing and all saved progress of a trampling ritual. Jobs only walk; every consequence of arriving
    /// somewhere is decided here, so a job being recreated after a load or an interruption never repeats a crossing.
    /// Stages: 0 = gather, 1 = crossing, 2 = short closing stage.
    /// </summary>
    public class LordJob_Ritual_Trampling : LordJob_Ritual
    {
        public const int PassesPerWalker = 2;
        public const string VictimRoleId = "victim";

        private const int CrossingStage = 1;
        private const int ClosingStage = 2;
        private const int AssemblyTimeoutTicks = 7500;
        private const int TurnTimeoutTicks = 1800;
        private const int JoyIntervalTicks = 120;
        private const int EscapeGuardTicks = 2500;
        private const int EscapeGuardRefreshTicks = 250;
        private const float TotalJoyPerRitual = 0.08f;
        private const float EstimatedTicksPerCrossing = 260f;

        private TramplingLayout layout;
        private List<TramplerState> tramplers = new List<TramplerState>();
        private Pawn victim;
        private float startingHealth = 1f;
        private Pawn escort;
        private bool deliveryPending;

        private bool assembled;
        private bool scarApplied;
        private bool earlyFinish;
        private bool success;
        private bool failed;

        private int plannedCrossings;
        private int crossingsDone;
        private int droppedCount;

        private Pawn activeWalker;
        private int activeWaypoint;
        private bool activeHit;
        private int activeTicks;
        private int cursor;

        private int phaseStage = -1;
        private int phaseTicks;
        private int joyTimer;

        public TramplingLayout Layout => layout;
        public Pawn Victim => victim;
        public float StartingHealth => startingHealth;
        public bool EarlyFinish => earlyFinish;
        public int DroppedCount => droppedCount;
        public bool AssemblyDone => assembled;
        public Pawn Escort => escort;
        public bool DeliveryPending => deliveryPending;
        public bool ReadyToFinish => success && scarApplied;
        // A prisoner is never released by the lord: they keep lying until the ritual ends and then wait to be carried back.
        public bool VictimReleased => !TramplingEscort.Needed(victim) && (failed || StageIndex >= ClosingStage);

        public int CompletedPercent
        {
            get
            {
                if (plannedCrossings <= 0)
                {
                    return 0;
                }
                return Mathf.RoundToInt(100f * Mathf.Min(crossingsDone, plannedCrossings) / plannedCrossings);
            }
        }

        public LordJob_Ritual_Trampling()
        {
        }

        public LordJob_Ritual_Trampling(TargetInfo selectedTarget, Precept_Ritual ritual, RitualObligation obligation, List<RitualStage> allStages, RitualRoleAssignments assignments, Pawn organizer = null)
            : base(selectedTarget, ritual, obligation, allStages, assignments, organizer)
        {
            victim = assignments.FirstAssignedPawn(VictimRoleId);
            // Snapshot now, before any injury this ritual causes. The mood bonus is decided by this value alone.
            startingHealth = victim?.health.summaryHealth.SummaryHealthPercent ?? 1f;

            foreach (Pawn walker in assignments.SpectatorsForReading)
            {
                if (walker != victim && tramplers.Count < TramplingLayout.MaxWalkers)
                {
                    tramplers.Add(new TramplerState { pawn = walker, slotIndex = tramplers.Count });
                }
            }
            plannedCrossings = tramplers.Count * PassesPerWalker;

            KeepVictimFromEscaping();
            if (TramplingEscort.Needed(victim))
            {
                escort = TramplingEscort.Pick(tramplers.Select(t => t.pawn), victim, selectedTarget.Thing);
                deliveryPending = true;
            }

            if (!TramplingLayout.TryBuild(selectedTarget.Thing, tramplers.Count, out layout, out _))
            {
                layout = null;
            }
        }

        // ---- Queries used by the job givers and drivers --------------------------------------------------------

        private TramplerState StateOf(Pawn pawn)
        {
            return tramplers.FirstOrDefault(t => t.pawn == pawn);
        }

        public bool TryGetSlot(Pawn pawn, out IntVec3 slot)
        {
            TramplerState state = StateOf(pawn);
            if (state == null || layout == null || state.slotIndex >= layout.slots.Count)
            {
                slot = IntVec3.Invalid;
                return false;
            }
            slot = layout.slots[state.slotIndex];
            return true;
        }

        public bool IsActiveWalker(Pawn pawn)
        {
            return pawn != null && pawn == activeWalker && StageIndex == CrossingStage && !failed && !success;
        }

        public bool TryGetNextWaypoint(Pawn pawn, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            TramplerState state = StateOf(pawn);
            if (!IsActiveWalker(pawn) || state == null || layout == null)
            {
                return false;
            }
            List<IntVec3> waypoints = layout.Waypoints(state.slotIndex);
            if (activeWaypoint >= waypoints.Count)
            {
                return false;
            }
            cell = waypoints[activeWaypoint];
            return true;
        }

        public bool TryGetDeliveryJob(Pawn pawn, out Job job)
        {
            job = null;
            if (!deliveryPending || pawn != escort || StageIndex != 0 || failed || layout == null || victim == null)
            {
                return false;
            }
            if (victim.Spawned && victim.Position == layout.victimCell)
            {
                return false;
            }
            job = JobMaker.MakeJob(PoS_JobDefOf.PoS_DeliverVictim, victim, layout.victimCell);
            job.count = 1;
            job.locomotionUrgency = LocomotionUrgency.Walk;
            return true;
        }

        public bool IsActiveTrampler(Pawn pawn)
        {
            TramplerState state = StateOf(pawn);
            return state != null && !state.dropped;
        }

        // ---- Callbacks from the jobs ---------------------------------------------------------------------------

        // Called by the escort the moment the victim is put down, so the delivery job is never handed out twice.
        public void Notify_VictimPlaced()
        {
            if (deliveryPending && victim != null && victim.Spawned && layout != null && victim.Position == layout.victimCell)
            {
                deliveryPending = false;
            }
        }


        public void Notify_WaypointReached(Pawn pawn)
        {
            if (!TryGetNextWaypoint(pawn, out IntVec3 expected) || pawn.Position != expected)
            {
                return;
            }

            if (activeWaypoint == TramplingLayout.VictimWaypointIndex && !activeHit)
            {
                activeHit = true;
                TramplerState state = StateOf(pawn);
                state.passes++;
                crossingsDone++;
                ApplyCrossing();
            }

            activeWaypoint++;
            if (activeWaypoint >= layout.Waypoints(StateOf(pawn).slotIndex).Count)
            {
                activeWalker = null;
            }
        }

        private void ApplyCrossing()
        {
            if (!scarApplied)
            {
                if (!TramplingInjuryUtility.TryApplyScar(victim))
                {
                    Fail("PoS_Trampling_CalledOff_NoScar".Translate(victim.Named("PAWN")));
                    return;
                }
                scarApplied = true;
                // Recorded here, once, and guarded by the saved scarApplied flag.
                JobDriver_Scarify.CreateHistoryEventDef(victim);
            }

            if (victim.Dead)
            {
                Fail("PoS_Trampling_CalledOff_VictimLost".Translate());
                return;
            }

            if (!TramplingInjuryUtility.TryApplyCrush(victim))
            {
                StopEarly();
                return;
            }

            if (victim.Dead)
            {
                Fail("PoS_Trampling_CalledOff_VictimLost".Translate());
            }
        }

        // ---- Ticking -------------------------------------------------------------------------------------------

        // A prisoner outside their cell would otherwise try to escape. Vanilla's prisoner rituals do the same thing,
        // but only once, and this one can run longer than that, so the wait is refreshed for as long as the ritual lasts.
        private void KeepVictimFromEscaping()
        {
            if (victim != null && victim.IsPrisonerOfColony && !victim.Dead)
            {
                victim.guest?.WaitInsteadOfEscapingFor(EscapeGuardTicks);
            }
        }

        public override void LordJobTick()
        {
            base.LordJobTick();
            if (Find.TickManager.TicksGame % EscapeGuardRefreshTicks == 0)
            {
                KeepVictimFromEscaping();
            }
            if (failed)
            {
                return;
            }

            if (StageIndex != phaseStage)
            {
                phaseStage = StageIndex;
                phaseTicks = 0;
            }
            else
            {
                phaseTicks++;
            }

            if (StageIndex < ClosingStage)
            {
                if (layout == null)
                {
                    Fail("PoS_Trampling_CalledOff_Layout".Translate());
                    return;
                }
                if (victim == null || victim.Dead || !(victim.Spawned || victim.ParentHolder is Pawn_CarryTracker))
                {
                    Fail("PoS_Trampling_CalledOff_VictimLost".Translate());
                    return;
                }
                DropInvalidWalkers();
            }

            if (StageIndex == 0)
            {
                TickDelivery();
                TickAssembly();
            }
            else if (StageIndex == CrossingStage)
            {
                TickCrossing();
            }
        }

        private int ActiveCount => tramplers.Count(t => !t.dropped);

        private void TickDelivery()
        {
            if (!deliveryPending || failed)
            {
                return;
            }
            // Someone may have put the victim on the cell without the escort's job reporting it (for example a load).
            if (victim.Spawned && victim.Position == layout.victimCell)
            {
                deliveryPending = false;
                return;
            }

            TramplerState escortState = escort == null ? null : StateOf(escort);
            if (escortState == null || escortState.dropped)
            {
                escort = TramplingEscort.Pick(tramplers.Where(t => !t.dropped).Select(t => t.pawn), victim, selectedTarget.Thing);
                if (escort == null)
                {
                    Fail("PoS_Trampling_CalledOff_NoEscort".Translate(victim.Named("PAWN")));
                }
            }
        }

        private void TickAssembly()
        {
            if (assembled)
            {
                return;
            }

            bool victimReady = victim.Position == layout.victimCell && (victim.Downed || victim.GetPosture() != PawnPosture.Standing);
            bool walkersReady = tramplers.Where(t => !t.dropped).All(t => t.pawn.Position == layout.slots[t.slotIndex]);
            if (victimReady && walkersReady && ActiveCount >= TramplingLayout.MinWalkers)
            {
                assembled = true;
                return;
            }

            if (phaseTicks > AssemblyTimeoutTicks)
            {
                if (deliveryPending)
                {
                    Fail("PoS_Trampling_CalledOff_Assembly".Translate());
                    return;
                }
                foreach (TramplerState state in tramplers.Where(t => !t.dropped && t.pawn.Position != layout.slots[t.slotIndex]).ToList())
                {
                    Drop(state);
                }
                if (!victimReady || ActiveCount < TramplingLayout.MinWalkers)
                {
                    Fail("PoS_Trampling_CalledOff_Assembly".Translate());
                    return;
                }
                assembled = true;
            }
        }

        private void TickCrossing()
        {
            if (success)
            {
                return;
            }

            TickJoy();

            int active = ActiveCount;
            if (active == 0 || (active < TramplingLayout.MinWalkers && crossingsDone == 0))
            {
                if (scarApplied)
                {
                    StopEarly();
                }
                else
                {
                    Fail("PoS_Trampling_CalledOff_TooFewWalkers".Translate());
                }
                return;
            }

            if (activeWalker != null)
            {
                activeTicks++;
                TramplerState state = StateOf(activeWalker);
                if (state == null || state.dropped)
                {
                    activeWalker = null;
                }
                else if (activeTicks > TurnTimeoutTicks)
                {
                    // Stalled path or similar: this trampler is out. Never wait forever.
                    Drop(state);
                }
            }

            if (activeWalker == null)
            {
                PickNextWalker();
            }

            progressBarOverride = CompletedPercent / 100f;
        }

        private void PickNextWalker()
        {
            for (int i = 0; i < tramplers.Count; i++)
            {
                int index = (cursor + i) % tramplers.Count;
                TramplerState state = tramplers[index];
                if (state.dropped || state.passes >= PassesPerWalker)
                {
                    continue;
                }
                activeWalker = state.pawn;
                activeWaypoint = 0;
                activeHit = false;
                activeTicks = 0;
                cursor = index + 1;
                return;
            }

            // Nobody is left with circuits to walk.
            if (!scarApplied)
            {
                Fail("PoS_Trampling_CalledOff_NoOutcome".Translate());
                return;
            }
            success = true;
        }

        private void TickJoy()
        {
            joyTimer++;
            if (joyTimer < JoyIntervalTicks)
            {
                return;
            }
            joyTimer = 0;

            float expectedTicks = Mathf.Max(600f, plannedCrossings * EstimatedTicksPerCrossing);
            float gain = TotalJoyPerRitual * JoyIntervalTicks / expectedTicks;
            foreach (TramplerState state in tramplers)
            {
                if (!state.dropped && state.pawn.Spawned && state.pawn.needs?.joy != null)
                {
                    state.pawn.needs.joy.GainJoy(gain, JoyKindDefOf.Social);
                }
            }
        }

        private void DropInvalidWalkers()
        {
            foreach (TramplerState state in tramplers.Where(t => !t.dropped && !WalkerStillValid(t.pawn)).ToList())
            {
                Drop(state);
            }
        }

        private bool WalkerStillValid(Pawn pawn)
        {
            return pawn != null
                && !pawn.Dead
                && pawn.Spawned
                && !pawn.Downed
                && !pawn.InMentalState
                && lord.ownedPawns.Contains(pawn)
                && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving);
        }

        private void Drop(TramplerState state)
        {
            state.dropped = true;
            droppedCount++;
            if (activeWalker == state.pawn)
            {
                activeWalker = null;
            }
            if (state.pawn != null)
            {
                Messages.Message("PoS_Trampling_WalkerDropped".Translate(state.pawn.Named("PAWN")), state.pawn, MessageTypeDefOf.NegativeEvent, historical: false);
            }
        }

        private void StopEarly()
        {
            if (!scarApplied || success)
            {
                return;
            }
            earlyFinish = true;
            success = true;
            Messages.Message("PoS_Trampling_StoppedEarly".Translate(victim.Named("PAWN")), selectedTarget, MessageTypeDefOf.NeutralEvent, historical: false);
        }

        private void Fail(TaggedString reason)
        {
            if (failed)
            {
                return;
            }
            failed = true;
            Messages.Message(reason, selectedTarget, MessageTypeDefOf.NegativeEvent, historical: false);
        }

        // ---- Overrides -----------------------------------------------------------------------------------------

        protected override bool ShouldBeCalledOff()
        {
            return failed || base.ShouldBeCalledOff();
        }

        // Only a ritual that really marked the victim and reached its planned (or safely shortened) end is a success.
        protected override bool RitualFinished(float progress, bool cancelled)
        {
            return success && scarApplied && victim != null && !victim.Dead && base.RitualFinished(progress, cancelled);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref layout, "layout");
            Scribe_Collections.Look(ref tramplers, "tramplers", LookMode.Deep);
            Scribe_References.Look(ref victim, "victim");
            Scribe_References.Look(ref activeWalker, "activeWalker");
            Scribe_Values.Look(ref startingHealth, "startingHealth", 1f);
            Scribe_References.Look(ref escort, "escort");
            Scribe_Values.Look(ref deliveryPending, "deliveryPending", false);
            Scribe_Values.Look(ref assembled, "assembled", false);
            Scribe_Values.Look(ref scarApplied, "scarApplied", false);
            Scribe_Values.Look(ref earlyFinish, "earlyFinish", false);
            Scribe_Values.Look(ref success, "success", false);
            Scribe_Values.Look(ref failed, "failed", false);
            Scribe_Values.Look(ref plannedCrossings, "plannedCrossings", 0);
            Scribe_Values.Look(ref crossingsDone, "crossingsDone", 0);
            Scribe_Values.Look(ref droppedCount, "droppedCount", 0);
            Scribe_Values.Look(ref activeWaypoint, "activeWaypoint", 0);
            Scribe_Values.Look(ref activeHit, "activeHit", false);
            Scribe_Values.Look(ref activeTicks, "activeTicks", 0);
            Scribe_Values.Look(ref cursor, "cursor", 0);
            Scribe_Values.Look(ref phaseStage, "phaseStage", -1);
            Scribe_Values.Look(ref phaseTicks, "phaseTicks", 0);
            Scribe_Values.Look(ref joyTimer, "joyTimer", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && tramplers == null)
            {
                tramplers = new List<TramplerState>();
            }
        }
    }
}
