using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Path_of_Sacrifice
{
    /// <summary>
    /// Altars only (no ritual-spot fallback). An altar without a usable lane in front of it is shown disabled
    /// with the reason, instead of letting the ritual start and fail.
    /// </summary>
    public class RitualObligationTargetWorker_TramplingAltar : RitualObligationTargetWorker_Altar
    {
        public RitualObligationTargetWorker_TramplingAltar()
        {
        }

        public RitualObligationTargetWorker_TramplingAltar(RitualObligationTargetFilterDef def)
            : base(def)
        {
        }

        // Gizmos ask every frame while an altar is selected, so the layout search is cached briefly per altar.
        private const int CacheTicks = 120;
        private static readonly Dictionary<int, (int tick, string reason)> cache = new Dictionary<int, (int tick, string reason)>();

        protected override RitualTargetUseReport CanUseTargetInternal(TargetInfo target, RitualObligation obligation)
        {
            RitualTargetUseReport report = base.CanUseTargetInternal(target, obligation);
            if (!report.canUse)
            {
                return report;
            }

            string reason = LayoutRejection(target.Thing);
            if (reason != null)
            {
                return reason;
            }
            return true;
        }

        private static string LayoutRejection(Thing altar)
        {
            int now = Find.TickManager.TicksGame;
            if (altar != null && cache.TryGetValue(altar.thingIDNumber, out var cached) && now - cached.tick < CacheTicks)
            {
                return cached.reason;
            }

            TramplingLayout.TryBuild(altar, TramplingLayout.MinWalkers, out _, out string reason);
            if (altar != null)
            {
                cache[altar.thingIDNumber] = (now, reason);
            }
            return reason;
        }
    }
}
