using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;
using RimWorld;
using UnityEngine;

namespace CombatExtended.HarmonyCE
{
	[HarmonyPatch(typeof(MechClusterGenerator))]
	[HarmonyPatch("GetBuildingDefsForCluster_NewTemp")]
	[HarmonyPatch(new Type[] { typeof(float), typeof(IntVec2), typeof(bool), typeof(float?), typeof(bool) })]
    public static class Harmony_MechClusterGenerator_GetBuildingDefsForCluster
    {
		private static ThingDef CE_MechAmmoBeacon = DefDatabase<ThingDef>.GetNamed("CombatExtended_MechAmmoBeacon");

        [HarmonyPostfix]
        public static void PostFix(float points, ref List<ThingDef> __result)
		{
			if (Controller.settings.EnableAmmoSystem
                && __result.Any(x => x.building.IsTurret
                    && !x.building.IsMortar
					&& x.building.turretGunDef != null
					&& x.building.turretGunDef.GetCompProperties<CompProperties_AmmoUser>()?.ammoSet != null))
			{
				int count = 1;
				if (points > 3000)
				{
					__result.Add(CE_MechAmmoBeacon);
					count++;
				}
				if (points > 7000)
				{
					__result.Add(CE_MechAmmoBeacon);
					count++;
				}
				__result.Add(CE_MechAmmoBeacon);
			}
        }
    }
}
