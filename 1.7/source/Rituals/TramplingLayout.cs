using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Path_of_Sacrifice
{
    /// <summary>
    /// Exact cells used by one trampling ritual, solved once and then saved with the lord.
    /// Seen from above, with the altar at the top and "out" pointing away from it:
    ///
    ///         ALTAR
    ///   exit   victim   entry         (the lane; walkers cross left to right in "side" terms)
    ///   outExit outMid  outEntry      (bypass back to the queue, never through the victim cell)
    ///                    slots...     (one fixed waiting cell per trampler, on the entry side)
    /// </summary>
    public class TramplingLayout : IExposable
    {
        public const int MinWalkers = 2;
        public const int MaxWalkers = 6;

        // Index of the victim cell in Waypoints(). Only arriving here counts as a crossing.
        public const int VictimWaypointIndex = 1;

        private const int SlotSearchRadiusSquared = 64;

        public IntVec3 victimCell;
        public IntVec3 entryCell;
        public IntVec3 exitCell;
        public IntVec3 outEntry;
        public IntVec3 outMid;
        public IntVec3 outExit;
        public List<IntVec3> slots = new List<IntVec3>();

        public List<IntVec3> Waypoints(int slotIndex)
        {
            return new List<IntVec3> { entryCell, victimCell, exitCell, outExit, outMid, outEntry, slots[slotIndex] };
        }

        public static bool TryBuild(Thing altar, int walkerCount, out TramplingLayout layout, out string failReason)
        {
            layout = null;
            failReason = "PoS_Trampling_NoLane".Translate();
            Map map = altar?.Map;
            if (altar == null || map == null || !altar.Spawned)
            {
                return false;
            }

            walkerCount = Mathf.Clamp(walkerCount, MinWalkers, MaxWalkers);

            IntVec3 outDir = altar.Rotation.Opposite.FacingCell;
            IntVec3 sideA = new IntVec3(-outDir.z, 0, outDir.x);
            IntVec3 sideB = new IntVec3(outDir.z, 0, -outDir.x);
            CellRect rect = altar.OccupiedRect();

            bool anyLane = false;
            foreach (IntVec3 victimCandidate in FrontCells(rect, outDir))
            {
                foreach (IntVec3 side in new[] { sideA, sideB })
                {
                    if (TryBuildAt(map, victimCandidate, side, outDir, walkerCount, out layout, out bool laneFree))
                    {
                        failReason = null;
                        return true;
                    }
                    anyLane |= laneFree;
                }
            }

            failReason = anyLane
                ? "PoS_Trampling_NoQueueSpace".Translate(walkerCount).Resolve()
                : "PoS_Trampling_NoLane".Translate().Resolve();
            return false;
        }

        private static IEnumerable<IntVec3> FrontCells(CellRect rect, IntVec3 outDir)
        {
            IEnumerable<IntVec3> row;
            if (outDir.z != 0)
            {
                int z = outDir.z > 0 ? rect.maxZ + 1 : rect.minZ - 1;
                row = Enumerable.Range(rect.minX, rect.Width).Select(x => new IntVec3(x, 0, z));
            }
            else
            {
                int x = outDir.x > 0 ? rect.maxX + 1 : rect.minX - 1;
                row = Enumerable.Range(rect.minZ, rect.Height).Select(z => new IntVec3(x, 0, z));
            }
            IntVec3 center = rect.CenterCell;
            return row.OrderBy(c => c.DistanceToSquared(center));
        }

        private static bool TryBuildAt(Map map, IntVec3 victim, IntVec3 side, IntVec3 outDir, int walkerCount, out TramplingLayout layout, out bool laneFree)
        {
            layout = null;
            laneFree = false;

            IntVec3 entry = victim + side;
            IntVec3 exit = victim - side;
            IntVec3 outMid = victim + outDir;
            IntVec3 outEntry = entry + outDir;
            IntVec3 outExit = exit + outDir;
            IntVec3[] core = { victim, entry, exit, outMid, outEntry, outExit };

            foreach (IntVec3 c in core)
            {
                if (!IsOpen(map, c))
                {
                    return false;
                }
            }
            laneFree = true;

            // Flood from the entry bypass cell, staying on the entry side of the victim, so every slot can reach the lane
            // without ever having to cross the victim cell.
            HashSet<IntVec3> blocked = new HashSet<IntVec3>(core);
            Dictionary<IntVec3, int> distance = new Dictionary<IntVec3, int> { { outEntry, 0 } };
            Queue<IntVec3> frontier = new Queue<IntVec3>();
            frontier.Enqueue(outEntry);
            while (frontier.Count > 0)
            {
                IntVec3 current = frontier.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    IntVec3 next = current + GenAdj.CardinalDirections[i];
                    if (distance.ContainsKey(next) || blocked.Contains(next))
                    {
                        continue;
                    }
                    if (next.DistanceToSquared(victim) > SlotSearchRadiusSquared || SideOffset(next, victim, side) < 1 || !IsOpen(map, next))
                    {
                        continue;
                    }
                    distance[next] = distance[current] + 1;
                    frontier.Enqueue(next);
                }
            }

            List<IntVec3> slotCells = distance
                .Where(kv => kv.Key != outEntry)
                .OrderBy(kv => kv.Value)
                .ThenBy(kv => kv.Key.DistanceToSquared(entry))
                .Select(kv => kv.Key)
                .Take(walkerCount)
                .ToList();
            if (slotCells.Count < walkerCount)
            {
                return false;
            }

            layout = new TramplingLayout
            {
                victimCell = victim,
                entryCell = entry,
                exitCell = exit,
                outEntry = outEntry,
                outMid = outMid,
                outExit = outExit,
                slots = slotCells
            };
            return true;
        }

        private static int SideOffset(IntVec3 cell, IntVec3 victim, IntVec3 side)
        {
            return (cell.x - victim.x) * side.x + (cell.z - victim.z) * side.z;
        }

        public static bool IsOpen(Map map, IntVec3 c)
        {
            return c.InBounds(map) && c.Standable(map) && c.GetEdifice(map) == null;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref victimCell, "victimCell");
            Scribe_Values.Look(ref entryCell, "entryCell");
            Scribe_Values.Look(ref exitCell, "exitCell");
            Scribe_Values.Look(ref outEntry, "outEntry");
            Scribe_Values.Look(ref outMid, "outMid");
            Scribe_Values.Look(ref outExit, "outExit");
            Scribe_Collections.Look(ref slots, "slots", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && slots == null)
            {
                slots = new List<IntVec3>();
            }
        }
    }
}
