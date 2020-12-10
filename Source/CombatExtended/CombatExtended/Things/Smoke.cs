using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VFESecurity;

namespace CombatExtended
{
    [HarmonyPatch(typeof(TickList), nameof(TickList.RegisterThing))]
    public class Harmony_TickList_RegisterThing
    {
        public static bool Prefix(Thing t)
        {
            if (t is Smoke smoke)
            {
                SmokeTicker.instance.Register(smoke);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(TickList), nameof(TickList.DeregisterThing))]
    public class Harmony_TickList_DeregisterThing
    {
        public static bool Prefix(Thing t)
        {
            if (t is Smoke smoke)
            {
                SmokeTicker.instance.Deregister(smoke);
                return false;
            }
            return true;
        }
    }

    public class SmokeTicker : GameComponent
    {
        private const int bucketCount = 30;

        private struct RemovalRequest
        {
            public int bucketIndex;
            public Smoke item;
        }

        private struct AdditionRequest
        {
            public int bucketIndex;
            public Smoke item;
        }

        private int curBucketIndex = 0;
        private List<RemovalRequest> removals = new List<RemovalRequest>();
        private List<AdditionRequest> additions = new List<AdditionRequest>();
        private HashSet<Smoke>[] buckets = new HashSet<Smoke>[bucketCount];

        public static SmokeTicker instance;

        public SmokeTicker()
        {
            instance = this;
        }

        public SmokeTicker(Game game)
        {
            instance = this;
            for (int i = 0; i < bucketCount; i++)
                buckets[i] = new HashSet<Smoke>();
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            curBucketIndex = (curBucketIndex + 1) % bucketCount;
            this.Tick(curBucketIndex);
            this.CleanUp();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref curBucketIndex, "curBucketIndex", 0);
        }

        public void Register(Smoke smoke)
        {
            int bucketIndex = GetBucket(smoke);
            additions.Add(new AdditionRequest() { bucketIndex = bucketIndex, item = smoke });
        }

        public void Deregister(Smoke smoke)
        {
            int bucketIndex = GetBucket(smoke);
            removals.Add(new RemovalRequest() { bucketIndex = bucketIndex, item = smoke });
        }

        private void Tick(int bucketIndex)
        {
            foreach (var partical in buckets[bucketIndex])
                partical.CustomTick();
        }

        private void CleanUp()
        {
            while (!removals.NullOrEmpty())
            {
                var request = removals.Pop();
                buckets[request.bucketIndex].Remove(request.item);
            }
            while (!additions.NullOrEmpty())
            {
                var request = additions.Pop();
                buckets[request.bucketIndex].Add(request.item);
            }
        }

        private int GetBucket(Smoke smoke) => (smoke.thingIDNumber + 7113) % 30;
    }

    public class SmokeGrid : MapComponent
    {
        private static Map[] maps = new Map[20];
        private static SmokeGrid[] comps = new SmokeGrid[20];

        private bool initialized = false;
        private bool[] cacheGrid;
        private float[] windWeights;
        private Smoke[] smokeGrid;
        private int[] cacheTickGrid;
        private WeatherTracker weatherTracker;

        public static readonly float[] weightsInRoofed = new float[8] { 0.96f, 0.96f, 0.66f, 0.96f, 0.96f, 0.66f, 0.66f, 0.66f };
        public static readonly Vector3[] adjCellsOffsets = new Vector3[8] {
            new Vector3(1, 0, 0),
            new Vector3(0, 0, 1),
            new Vector3(1, 0, 1),
            new Vector3(-1, 0, 0),
            new Vector3(0, 0, -1),
            new Vector3(-1, 0, -1),
            new Vector3(1, 0, -1),
            new Vector3(-1, 0, 1)
        };

        public static SmokeGrid GetGridFor(Map map)
        {
            var index = map.Index;
            if (index >= 0 && index < comps.Length)
            {
                if (maps[index] == map)
                    return comps[index];
                maps[index] = map;
                return comps[index] = map.GetComponent<SmokeGrid>();
            }
            return map.GetComponent<SmokeGrid>();
        }

        public SmokeGrid(Map map) : base(map)
        {
            windWeights = new float[8];
            smokeGrid = new Smoke[map.cellIndices.NumGridCells];
            cacheGrid = new bool[map.cellIndices.NumGridCells];
            cacheTickGrid = new int[map.cellIndices.NumGridCells];
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!initialized)
            {
                initialized = true;
                weatherTracker = map.GetComponent<WeatherTracker>();
                CalculateWindMatrix();
                return;
            }
            if (GenTicks.TicksGame % 5 == 0)
                CalculateWindMatrix();
        }

        public void Register(Smoke smoke)
        {
            IntVec3 position = smoke.Position;
            smokeGrid[map.cellIndices.CellToIndex(position)] = smoke;
        }

        public void Deregister(Smoke smoke)
        {
            IntVec3 position = smoke.Position;
            smokeGrid[map.cellIndices.CellToIndex(position)] = null;
        }

        public Smoke GetAt(IntVec3 position)
        {
            if (!position.InBounds(map))
                return null;

            return smokeGrid[map.cellIndices.CellToIndex(position)];
        }

        public float[] DiffusionWeightAt(IntVec3 pos)
        {
            if (pos.Roofed(map))
                return weightsInRoofed;
            if (weatherTracker.GetWindStrengthAt(pos) < 2.5f)
                return weightsInRoofed;
            return windWeights;
        }

        public bool CanDiffuseTo(IntVec3 pos)
        {
            if (!pos.InBounds(map))
                return false;

            var index = map.cellIndices.CellToIndex(pos);
            if (cacheTickGrid[index] == GenTicks.TicksGame)
                return cacheGrid[index];

            cacheTickGrid[index] = GenTicks.TicksGame;
            return cacheGrid[index] = (false
                    || !pos.Filled(map)
                    || (pos.GetDoor(map)?.Open ?? false)
                    || (pos.GetFirstThing<Building_Vent>(map) is Building_Vent vent && vent.TryGetComp<CompFlickable>().SwitchIsOn));
        }

        private void CalculateWindMatrix()
        {
            Vector3 windDirection = weatherTracker.WindDirection;
            for (int i = 0; i < 8; i++)
            {
                windWeights[i] = Mathf.Max(Vector3.Dot(adjCellsOffsets[i], windDirection) * 1 / 4f * weightsInRoofed[i], 0f);
            }
            var message = "";
            for (int i = 0; i < 8; i++)
                message += $" {windWeights[i]}";
            Log.Message($"{message}");
        }
    }

    public class Smoke : Gas
    {
        private const float InhalationPerSec = 0.045f * 60 / GenTicks.TicksPerRealSecond;
        private const float MaxDensity = 12800f;
        private const float BugConfusePercent = 0.15f;
        private const float MinDensity = 1f;
        private const float MinDiffusionDensity = 10f;
        private int ticksToDestroy = 100;
        private WeatherTracker weatherTracker;
        private float density;
        private SmokeGrid grid;
        private float[] diffusionWeights = new float[8];

        public float Density
        {
            get => density;
        }

        public WeatherTracker WeatherTracker
        {
            get
            {
                if (weatherTracker == null)
                    weatherTracker = Map.GetComponent<WeatherTracker>();
                return weatherTracker;
            }
        }

        public SmokeGrid Grid
        {
            get
            {
                if (grid == null)
                    grid = SmokeGrid.GetGridFor(Map);
                return grid;
            }
        }

        public override string LabelNoCount
        {
            get => base.LabelNoCount + " (" + Mathf.RoundToInt(density) + " ppm)";
        }

        public Smoke()
        {
        }

        public Smoke(float density)
        {
            this.density = density;
        }

        public void CustomTick()
        {
            if (Destroyed || !Spawned)
                return;
            if (!CanDiffuseTo(Position) || ticksToDestroy <= 0)
            {
                Destroy();
                return;
            }
            if (density <= MinDensity)
            {
                ticksToDestroy--;
                return;
            }

            IntVec3 position = Position;
            float diffusionBudget = density / 2f;
            diffusionWeights = Grid.DiffusionWeightAt(position);
            for (int i = 0; i < 8; i++)
                DiffuseTo(SmokeGrid.adjCellsOffsets[i].ToIntVec3() + position, diffusionBudget, diffusionWeights[i]);

            if (!Map.roofGrid.Roofed(Map.cellIndices.CellToIndex(Position)))
                UpdateDensityBy(-90f * weatherTracker.GetWindStrengthAt(position) / 4.5f - 15f);

            ApplyHediffs();
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            weatherTracker = map.GetComponent<WeatherTracker>();
            Grid.Register(this);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            Grid.Deregister(this);
            base.DeSpawn(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref density, "density");
        }

        public void UpdateDensityBy(float diff)
        {
            density = Mathf.Clamp(density + diff, 0, MaxDensity);
        }

        public float GetOpacity()
        {
            return (0.05f + (0.95f * (density / MaxDensity))) * (Position.Roofed(Map) ? 1f : 0.5f);
        }

        private bool CanDiffuseTo(IntVec3 pos)
        {
            return Grid.CanDiffuseTo(pos);
        }

        private void DiffuseTo(IntVec3 cell, float amount, float weight = 1f)
        {
            if (!CanDiffuseTo(cell))
                return;
            weight = Mathf.Pow(weight, 0.5f);
            if (amount * weight < MinDiffusionDensity)
                return;

            Smoke other = grid.GetAt(cell);
            if (other != null)
            {
                float densityDiff = (this.density - other.density) * 2 * weight;
                DiffuseTo(other, densityDiff / 2);
            }
            else
            {
                Smoke newSmokeCloud = (Smoke)GenSpawn.Spawn(CE_ThingDefOf.Gas_BlackSmoke, cell, Map);
                DiffuseTo(newSmokeCloud, amount * weight);
            }
        }

        private void DiffuseTo(Smoke other, float value)
        {
            this.UpdateDensityBy(-value);
            other.UpdateDensityBy(value);
        }

        private void ApplyHediffs()
        {
            if (!Position.InBounds(Map))
                return;

            var pawns = Position.GetThingList(Map).Where(t => t is Pawn).ToList();

            foreach (Pawn pawn in pawns)
            {
                if (pawn.RaceProps.FleshType == FleshTypeDefOf.Insectoid)
                {
                    if (density > MaxDensity * BugConfusePercent)
                        pawn.mindState.mentalStateHandler.TryStartMentalState(CE_MentalStateDefOf.WanderConfused);
                    continue;
                }

                var severity = InhalationPerSec * Mathf.Pow(density / MaxDensity, 2) * pawn.GetStatValue(CE_StatDefOf.SmokeSensitivity);
                HealthUtility.AdjustSeverity(pawn, CE_HediffDefOf.SmokeInhalation, severity);
            }
        }
    }
}
