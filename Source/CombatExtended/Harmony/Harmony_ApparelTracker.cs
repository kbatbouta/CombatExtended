﻿using System.Linq;
using Harmony;
using RimWorld;
using Verse;

namespace CombatExtended.Harmony
{
    [HarmonyPatch(typeof(Pawn_ApparelTracker), "Notify_ApparelAdded")]
    internal static class Harmony_ApparelTracker_Notify_ApparelAdded
    {
        internal static void Postfix(Pawn_ApparelTracker __instance, Apparel apparel)
        {
            var hediffDef = apparel.def.GetModExtension<ApparelHediffExtension>()?.hediff;
            if (hediffDef == null)
                return;

            var pawn = __instance.pawn;
            pawn.health.AddHediff(hediffDef);
        }
    }

    [HarmonyPatch(typeof(Pawn_ApparelTracker), "Notify_ApparelRemoved")]
    internal static class Harmony_ApparelTracker_Notify_ApparelRemoved
    {
        internal static void Postfix(Pawn_ApparelTracker __instance, Apparel apparel)
        {
            var hediffDef = apparel.def.GetModExtension<ApparelHediffExtension>()?.hediff;
            if (hediffDef == null)
                return;

            var pawn = __instance.pawn;
            var hediff = pawn.health.hediffSet.hediffs.FirstOrDefault(h => h.def == hediffDef);
            if (hediff == null)
            {
                Log.Warning($"CE :: Apparel {apparel} tried removing hediff {hediffDef} from {pawn} but could not find any");
                return;
            }
            pawn.health.RemoveHediff(hediff);
        }
    }
}