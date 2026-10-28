using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Path_of_Sacrifice
{
    [DefOf]
    public static class PoS_PreceptDefOf
    {
        public static PreceptDef PoS_Trampling;

        static PoS_PreceptDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PoS_PreceptDefOf));
        }
    }

    /// <summary>
    /// Scarification precepts that actually ask for scars (minor, heavy, extreme) bring the trampling ritual with them.
    /// The vanilla `alsoAdds` field cannot be used: the ritual it creates is never filled from its pattern, so it would
    /// be an empty ritual. The horrible precept asks for no scars and is left alone.
    /// </summary>
    public static class TramplingPrecepts
    {
        public static bool WantsTrampling(Precept precept)
        {
            return precept?.def != null && precept.def.requiredScars > 0 && precept.def.issue?.defName == "Scarification";
        }

        public static bool AnyWantsTrampling(Ideo ideo)
        {
            return ideo.PreceptsListForReading.Any(WantsTrampling);
        }

        public static bool Missing(Ideo ideo)
        {
            return !ideo.HasPrecept(PoS_PreceptDefOf.PoS_Trampling);
        }

        public static Precept_Ritual MakeRitual()
        {
            Precept_Ritual ritual = (Precept_Ritual)PreceptMaker.MakePrecept(PoS_PreceptDefOf.PoS_Trampling);
            return ritual;
        }
    }

    // Editor, reform and any other place a precept is added to a finished ideoligion.
    [HarmonyPatch(typeof(Ideo), nameof(Ideo.AddPrecept))]
    public static class Patch_Ideo_AddPrecept
    {
        public static void Postfix(Ideo __instance, Precept precept, bool init)
        {
            // Without init the ideoligion is still being generated; that path is handled by the foundation patch.
            if (!init || !TramplingPrecepts.WantsTrampling(precept) || !TramplingPrecepts.Missing(__instance))
            {
                return;
            }
            __instance.AddPrecept(TramplingPrecepts.MakeRitual(), init: true, null, PoS_PreceptDefOf.PoS_Trampling.ritualPatternBase);
        }
    }

    // Ideoligion generation: same spot and same steps vanilla uses to add the rituals its memes ask for.
    [HarmonyPatch(typeof(IdeoFoundation), "AddRitualsForMemes")]
    public static class Patch_IdeoFoundation_AddRitualsForMemes
    {
        public static void Postfix(IdeoFoundation __instance, List<Precept> outPrecepts)
        {
            Ideo ideo = __instance.ideo;
            if (ideo == null || !TramplingPrecepts.AnyWantsTrampling(ideo) || !TramplingPrecepts.Missing(ideo))
            {
                return;
            }
            Precept_Ritual ritual = TramplingPrecepts.MakeRitual();
            ideo.AddPrecept(ritual);
            PoS_PreceptDefOf.PoS_Trampling.ritualPatternBase.Fill(ritual);
            ritual.RegenerateName();
            outPrecepts?.Add(ritual);
        }
    }
}
