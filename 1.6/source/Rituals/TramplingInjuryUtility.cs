using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Path_of_Sacrifice
{
    /// <summary>
    /// Owns every health change the ritual makes. Each candidate is checked against the 1.7 health predictors
    /// immediately before it is applied, so the ritual never destroys a part. A scar can never kill; a crush is minor
    /// and is allowed to kill, in which case the lord fails the ritual.
    /// </summary>
    public static class TramplingInjuryUtility
    {
        private const float CrushSeverityMin = 0.5f;
        private const float CrushSeverityMax = 1f;
        private const float ScarSeverity = 1f;

        // A part is skipped once it is already this far down, so repeated steps cannot wear one part away.
        private const float MinPartHealthFraction = 0.5f;

        public static bool TryApplyScar(Pawn victim)
        {
            if (!TryPickPart(victim, HediffDefOf.Scarification, ScarSeverity, scar: true, out BodyPartRecord part))
            {
                return false;
            }

            Hediff scar = HediffMaker.MakeHediff(HediffDefOf.Scarification, victim, part);
            scar.Severity = ScarSeverity;
            HediffComp_GetsPermanent permanent = scar.TryGetComp<HediffComp_GetsPermanent>();
            if (permanent != null)
            {
                permanent.IsPermanent = true;
                permanent.SetPainCategory(JobDriver_Scarify.InjuryPainCategories.RandomElementByWeight(e => e.weight).category);
            }
            victim.health.AddHediff(scar);
            return true;
        }

        public static bool TryApplyCrush(Pawn victim)
        {
            if (victim.Dead)
            {
                return false;
            }

            float severity = Rand.Range(CrushSeverityMin, CrushSeverityMax);
            if (!CandidateParts(victim, scar: false)
                .Where(p => !victim.health.WouldLosePartAfterAddingHediff(PoS_HediffDefOf.PoS_TrampleCrush, p, severity))
                .TryRandomElement(out BodyPartRecord part))
            {
                return false;
            }

            Hediff crush = HediffMaker.MakeHediff(PoS_HediffDefOf.PoS_TrampleCrush, victim, part);
            crush.Severity = severity;
            victim.health.AddHediff(crush);
            return true;
        }

        private static bool TryPickPart(Pawn pawn, HediffDef def, float severity, bool scar, out BodyPartRecord part)
        {
            return CandidateParts(pawn, scar)
                .Where(p => IsSafe(pawn, def, p, severity))
                .TryRandomElement(out part);
        }

        private static IEnumerable<BodyPartRecord> CandidateParts(Pawn pawn, bool scar)
        {
            List<BodyPartRecord> external = pawn.health.hediffSet.GetNotMissingParts()
                .Where(p => p.depth == BodyPartDepth.Outside
                    && !pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(p)
                    && HealthyEnough(pawn, p)
                    && (!scar || p.def.canScarify))
                .ToList();

            List<BodyPartRecord> limbs = external.Where(IsInLimb).ToList();
            if (limbs.Count > 0 || !scar)
            {
                return limbs;
            }
            // A scar may fall back to other external skin, but never the head or neck.
            return external.Where(p => p.def.defName != "Head" && p.def.defName != "Neck" && !IsInside(p, "Head"));
        }

        private static bool HealthyEnough(Pawn pawn, BodyPartRecord part)
        {
            return pawn.health.hediffSet.GetPartHealth(part) >= part.def.GetMaxHealth(pawn) * MinPartHealthFraction;
        }

        private static bool IsInLimb(BodyPartRecord part)
        {
            return IsInside(part, "Arm") || IsInside(part, "Leg");
        }

        private static bool IsInside(BodyPartRecord part, string ancestorDefName)
        {
            for (BodyPartRecord p = part; p != null; p = p.parent)
            {
                if (p.def.defName == ancestorDefName)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsSafe(Pawn pawn, HediffDef def, BodyPartRecord part, float severity)
        {
            return !pawn.health.WouldDieAfterAddingHediff(def, part, severity)
                && !pawn.health.WouldLosePartAfterAddingHediff(def, part, severity);
        }
    }
}
