using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace Path_of_Sacrifice
{
    /// <summary>Ends the gather stage once the victim lies on the exact cell and every trampler waits at their cell.</summary>
    public class StageEndTrigger_TramplingAssembled : StageEndTrigger
    {
        public override Trigger MakeTrigger(LordJob_Ritual ritual, TargetInfo spot, IEnumerable<TargetInfo> foci, RitualStage stage)
        {
            if (ritual is LordJob_Ritual_Trampling trampling)
            {
                return new Trigger_Custom(_ => trampling.AssemblyDone);
            }
            Log.Error("Used StageEndTrigger_TramplingAssembled on a non LordJob_Ritual_Trampling ritual job");
            return null;
        }
    }

    /// <summary>
    /// Ends the crossing stage when all circuits are walked, or a safe early finish was recorded.
    /// Never fires without a permanent scar.
    /// </summary>
    public class StageEndTrigger_TramplingComplete : StageEndTrigger
    {
        public override Trigger MakeTrigger(LordJob_Ritual ritual, TargetInfo spot, IEnumerable<TargetInfo> foci, RitualStage stage)
        {
            if (ritual is LordJob_Ritual_Trampling trampling)
            {
                return new Trigger_Custom(_ => trampling.ReadyToFinish);
            }
            Log.Error("Used StageEndTrigger_TramplingComplete on a non LordJob_Ritual_Trampling ritual job");
            return null;
        }
    }
}
