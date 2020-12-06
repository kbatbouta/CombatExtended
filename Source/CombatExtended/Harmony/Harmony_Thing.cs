﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using CombatExtended.Utilities;
using HarmonyLib;
using Verse;

namespace CombatExtended.HarmonyCE
{
    [HarmonyPatch(typeof(Thing), "SmeltProducts")]
    public class Harmony_Thing_SmeltProducts
    {
        public static void Postfix(Thing __instance, ref IEnumerable<Thing> __result)
        {
            var ammoUser = (__instance as ThingWithComps)?.TryGetComp<CompAmmoUser>();

            if (ammoUser != null && (ammoUser.HasMagazine && ammoUser.CurMagCount > 0 && ammoUser.CurrentAmmo != null))
            {
                var ammoThing = ThingMaker.MakeThing(ammoUser.CurrentAmmo, null);
                ammoThing.stackCount = ammoUser.CurMagCount;
                __result = __result.AddItem(ammoThing);
            }
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Position), MethodType.Setter)]
    public class Harmony_Thing_Position
    {
        private static FieldInfo fPosition = AccessTools.Field(typeof(Thing), "positionInt");

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var finished = false;
            var l1 = generator.DefineLabel();
            for (int i = 0; i < codes.Count; i++)
            {
                if (!finished)
                {
                    if (codes[i].opcode == OpCodes.Stfld && codes[i].OperandIs(fPosition))
                    {
                        finished = true;
                        yield return codes[i];
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Spawned)));
                        yield return new CodeInstruction(OpCodes.Brfalse_S, l1);

                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map)));
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ThingTracker), nameof(ThingTracker.GetTracker)));
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ThingTracker), nameof(ThingTracker.Notify_PositionChanged)));

                        codes[i + 1].labels.Add(l1);
                        continue;
                    }
                }
                yield return codes[i];
            }
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.DeSpawn))]
    public class Harmony_Thing_DeSpawn
    {
        public static void Prefix(Thing __instance)
        {
            ThingTracker.GetTracker(__instance.Map)?.Notify_DeSpawned(__instance);
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.SpawnSetup))]
    public class Harmony_Thing_SpawnSetup
    {
        public static void Postfix(Thing __instance)
        {
            ThingTracker.GetTracker(__instance.Map)?.Notify_Spawned(__instance);
        }
    }
}
