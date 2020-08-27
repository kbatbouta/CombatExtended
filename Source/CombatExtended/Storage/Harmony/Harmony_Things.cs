﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Linq.Mapping;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace CombatExtended.Storage.Harmony
{
    [HarmonyPatch(typeof(Thing), nameof(Thing.Position), MethodType.Setter)]
    public static class Harmony_Thing_Position
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var finished = false;
            for (int i = 0; i < codes.Count; i++)
            {
                if (!finished)
                {
                    if (codes[i].opcode == OpCodes.Ret)
                    {
                        finished = true;
                        var l1 = generator.DefineLabel();
                        yield return codes[i];
                        yield return new CodeInstruction(OpCodes.Ldarg_0) { labels = new List<Label>(new[] { codes[i + 1].labels[0] }) };
                        yield return new CodeInstruction(OpCodes.Ldfld,
                            AccessTools.Field(typeof(Thing), "isPawn"));
                        yield return new CodeInstruction(OpCodes.Brfalse_S, l1);
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Ldfld,
                            AccessTools.Field(typeof(Thing), "positionInt"));
                        yield return new CodeInstruction(OpCodes.Ldarg_1);
                        yield return new CodeInstruction(OpCodes.Call,
                            AccessTools.Method("Harmony_Thing_Position:OnPositionChanged"));
                        codes[i + 1].labels.Clear();
                        codes[i + 1].labels.Add(l1);
                        continue;
                    }
                }
                yield return codes[i];
            }
        }

        public static void OnPositionChanged(Thing thing,
            IntVec3 oldPos,
            IntVec3 newPos)
        {
            if (thing?.Map == null)
                return;
            if (thing?.positionInt == null)
                return;
            if (thing.Destroyed || !thing.Spawned)
                return;
            thing?.Map?.rangeStore?.Notify_ThingPositionChanged(thing);
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
    public static class Harmony_Thing_Destroy
    {
        public static void Prefix(Thing __instance)
        {
            if (__instance.isPawn || __instance.isTurret)
            {
                if (__instance?.Map == null)
                    return;

                __instance?.Map?.rangeStore?.locationCacheX.Clear();
                __instance?.Map?.rangeStore?.locationCacheZ.Clear();

                foreach (Pawn p in __instance?.Map.mapPawns.AllPawns)
                    p.indexValid = false;
            }
        }
    }
}
