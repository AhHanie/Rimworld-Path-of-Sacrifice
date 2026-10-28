using RimWorld;

namespace Path_of_Sacrifice
{
    /// <summary>Quality from the share of planned crossings that were actually walked.</summary>
    public class RitualOutcomeComp_CompletedCrossings : RitualOutcomeComp_Quality
    {
        public override bool DataRequired => false;

        public override float Count(LordJob_Ritual ritual, RitualOutcomeComp_Data data)
        {
            return (ritual as LordJob_Ritual_Trampling)?.CompletedPercent ?? 0;
        }
    }
}
