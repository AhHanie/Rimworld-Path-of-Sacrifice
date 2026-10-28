using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Path_of_Sacrifice
{
    public class Mod : Verse.Mod
    {
        public Mod(ModContentPack content) : base(content)
        {
            LongEventHandler.QueueLongEvent(Init, "PathofSacrifice.LoadingLabel", doAsynchronously: true, null);
        }

        private void Init()
        {
            new Harmony("sk.pathofsac").PatchAll();
        }
    }
}
