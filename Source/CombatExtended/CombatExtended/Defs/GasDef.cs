using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace CombatExtended
{
    public class GasDef : Def
    {
        public float decayRateRoofed = 90;

        public float decayRateUnroofed = 15;

        public float maxDensity = 12800;

        public float minEffectiveDensity = 100f;

        public float opacityOutDoors = 1.0f;

        public float opacityInDoors = 0.4f;

        public bool isBad = false;

        public bool blockLineOfSight = false;

        public bool affectedByWind = false;

        public Def decayInto = ThingDefOf.Filth_Ash;

        public Color colorOutSide = Color.gray;

        public Color colorInSide = Color.black;

        public List<HediffGiver> hediffGivers;
    }
}
