using RimWorld;
using Verse;

namespace Path_of_Sacrifice
{
    [DefOf]
    public static class PoS_JobDefOf
    {
        public static JobDef PoS_LieAsVictim;
        public static JobDef PoS_AwaitCarry;
        public static JobDef PoS_TramplerHold;
        public static JobDef PoS_TramplerCross;
        public static JobDef PoS_DeliverVictim;

        static PoS_JobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PoS_JobDefOf));
        }
    }

    [DefOf]
    public static class PoS_HediffDefOf
    {
        public static HediffDef PoS_TrampleCrush;

        static PoS_HediffDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PoS_HediffDefOf));
        }
    }

    [DefOf]
    public static class PoS_ThoughtDefOf
    {
        public static ThoughtDef PoS_Trampling_HealthyVictim;

        static PoS_ThoughtDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PoS_ThoughtDefOf));
        }
    }
}
