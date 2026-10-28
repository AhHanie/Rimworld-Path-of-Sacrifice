using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Path_of_Sacrifice
{
    public class RitualOutcomeEffectWorker_Trampling : RitualOutcomeEffectWorker_FromQuality
    {
        private const float GoodHealthThreshold = 0.5f;
        private const float GreatHealthThreshold = 0.8f;

        public RitualOutcomeEffectWorker_Trampling()
        {
        }

        public RitualOutcomeEffectWorker_Trampling(RitualOutcomeEffectDef def)
            : base(def)
        {
        }

        public override IEnumerable<string> BlockingIssues(Precept_Ritual ritual, TargetInfo target, RitualRoleAssignments assignments)
        {
            foreach (string issue in base.BlockingIssues(ritual, target, assignments))
            {
                yield return issue;
            }

            List<Pawn> walkers = assignments.SpectatorsForReading;
            if (walkers.Count < TramplingLayout.MinWalkers)
            {
                yield return "PoS_Trampling_NeedWalkers".Translate(TramplingLayout.MinWalkers);
            }
            else if (walkers.Count > TramplingLayout.MaxWalkers)
            {
                yield return "PoS_Trampling_TooManyWalkers".Translate(TramplingLayout.MaxWalkers);
            }

            Pawn victim = assignments.FirstAssignedPawn(LordJob_Ritual_Trampling.VictimRoleId);
            if (victim != null && walkers.Contains(victim))
            {
                yield return "PoS_Trampling_VictimIsWalker".Translate();
            }

            // The victim role no longer demands reachability: a prisoner is carried by a trampler instead.
            if (victim != null)
            {
                if (TramplingEscort.Needed(victim))
                {
                    if (walkers.Count > 0 && TramplingEscort.Pick(walkers, victim, target.Thing) == null)
                    {
                        yield return "PoS_Trampling_NoEscort".Translate(victim.LabelShort);
                    }
                }
                else if (!victim.CanReach((LocalTargetInfo)target, PathEndMode.Touch, victim.NormalMaxDanger()))
                {
                    yield return "PoS_Trampling_VictimUnreachable".Translate(victim.LabelShort);
                }
            }

            if (walkers.Count >= TramplingLayout.MinWalkers && walkers.Count <= TramplingLayout.MaxWalkers
                && !TramplingLayout.TryBuild(target.Thing, walkers.Count, out _, out string layoutIssue))
            {
                yield return layoutIssue;
            }
        }

        // The vanilla weighted pick scales every positive outcome equally and would erase the quality difference,
        // so the memory is chosen by explicit quality bands instead.
        public override RitualOutcomePossibility GetOutcome(float quality, LordJob_Ritual ritual)
        {
            int band = quality < 0.25f ? 0 : quality < 0.5f ? 1 : quality < 0.75f ? 2 : 3;
            return def.outcomeChances[band];
        }

        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            base.Apply(progress, totalPresence, jobRitual);

            if (!(jobRitual is LordJob_Ritual_Trampling trampling))
            {
                return;
            }
            int bonus = HealthBonus(trampling.StartingHealth);
            if (bonus <= 0)
            {
                return;
            }
            foreach (KeyValuePair<Pawn, int> presence in totalPresence)
            {
                Pawn pawn = presence.Key;
                if (pawn == trampling.Victim || !trampling.IsActiveTrampler(pawn) || pawn.needs?.mood == null)
                {
                    continue;
                }
                Thought_Memory memory = (Thought_Memory)MakeMemory(pawn, jobRitual, PoS_ThoughtDefOf.PoS_Trampling_HealthyVictim);
                memory.moodOffset = bonus;
                pawn.needs.mood.thoughts.memories.TryGainMemory(memory);
            }
        }

        public override string OutcomeQualityBreakdownDesc(float quality, float progress, LordJob_Ritual jobRitual)
        {
            string text = base.OutcomeQualityBreakdownDesc(quality, progress, jobRitual);
            if (jobRitual is LordJob_Ritual_Trampling trampling)
            {
                string health = trampling.StartingHealth.ToStringPercent();
                int bonus = HealthBonus(trampling.StartingHealth);
                text += "\n\n" + (bonus > 0
                    ? "PoS_Trampling_QualityHealth".Translate(health, bonus.ToStringWithSign()).Resolve()
                    : "PoS_Trampling_QualityHealthNone".Translate(health).Resolve());
                if (trampling.EarlyFinish)
                {
                    text += "\n" + "PoS_Trampling_QualityEarlyStop".Translate().Resolve();
                }
                if (trampling.DroppedCount > 0)
                {
                    text += "\n" + "PoS_Trampling_QualityDropped".Translate(trampling.DroppedCount).Resolve();
                }
            }
            return text;
        }

        // Fixed from the health snapshot taken when the ritual started; later injuries never change it.
        public static int HealthBonus(float startingHealth)
        {
            if (startingHealth >= GreatHealthThreshold)
            {
                return 3;
            }
            if (startingHealth >= GoodHealthThreshold)
            {
                return 1;
            }
            return 0;
        }
    }
}
