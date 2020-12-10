using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using RimWorld;
using UnityEngine;
using Verse;
using Vector3 = UnityEngine.Vector3;

namespace CombatExtended
{
    public class GasGrid : IExposable
    {
        private const int BucketCount = 30;

        private static readonly float[] weightsDefautls = new float[8] { 0.96f, 0.96f, 0.66f, 0.96f, 0.96f, 0.66f, 0.66f, 0.66f };
        private static readonly IntVec3[] adjCellsOffsets = new IntVec3[8] {
            new IntVec3(1, 0, 0),
            new IntVec3(0, 0, 1),
            new IntVec3(1, 0, 1),
            new IntVec3(-1, 0, 0),
            new IntVec3(0, 0, -1),
            new IntVec3(-1, 0, -1),
            new IntVec3(1, 0, -1),
            new IntVec3(-1, 0, 1)
        };

        private class GasParticle : IExposable
        {
            public int gasIDNumber;
            public float density;
            public int positionIndex;
            public IntVec3 position;
            public int lastDrawn;

            public GasParticle()
            {
            }

            public GasParticle(int gasIDNumber, float density, int positionIndex, IntVec3 position)
            {
                this.gasIDNumber = gasIDNumber;
                this.density = density;
                this.positionIndex = positionIndex;
                this.position = position;
            }

            public void ExposeData()
            {
                Scribe_Values.Look(ref gasIDNumber, "gasIDNumber");
                Scribe_Values.Look(ref positionIndex, "positionIndex", 0);
                Scribe_Values.Look(ref density, "density", 0f);
                Scribe_Values.Look(ref position, "position");
            }
        }

        public GasDef gas;
        public Map map;
        public float windAngle;

        private bool loaded = false;
        private float[] windWeights;
        private WeatherTracker weatherTracker;
        private int curBucketIndex = 0;
        private int gasIDCounter = 0;
        private GasParticle[] grid;
        private List<GasParticle> allGases;
        private List<GasParticle>[] buckets = new List<GasParticle>[BucketCount];
        private Vector3 Up = new UnityEngine.Vector3(0, 0, 1);
        private bool[] cacheGrid;
        private int[] cacheTickGrid;

        public GasGrid()
        {
            for (int i = 0; i < BucketCount; i++)
                this.buckets[i] = new List<GasParticle>();
        }

        public GasGrid(GasDef gas)
        {
            this.gas = gas;
            for (int i = 0; i < BucketCount; i++)
                this.buckets[i] = new List<GasParticle>();
        }

        public void LoadData()
        {
            this.loaded = true;
            this.windWeights = new float[8];
            this.cacheGrid = new bool[map.cellIndices.NumGridCells];
            this.cacheTickGrid = new int[map.cellIndices.NumGridCells];
            this.weatherTracker = map.GetComponent<WeatherTracker>();
            this.grid = new GasParticle[map.cellIndices.NumGridCells];
            if (allGases == null)
                allGases = new List<GasParticle>();
            foreach (var particale in allGases)
            {
                grid[particale.positionIndex] = particale;
                buckets[particale.gasIDNumber % BucketCount].Add(particale);
            }
        }

        public void Tick()
        {
            if (!loaded)
            {
                this.loaded = true;
                this.LoadData();
            }
            this.CalculateWindMatrix();
            this.TickBucket();
            this.DrawBucket();
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref gas, "gas");
            Scribe_Values.Look(ref gasIDCounter, "gasIDCounter", 0);
            Scribe_Values.Look(ref curBucketIndex, "curBucketIndex", 0);
            Scribe_Collections.Look(ref allGases, "allGases", LookMode.Deep);
        }

        public void PumpAt(IntVec3 pos, float density)
        {
            if (!pos.InBounds(map))
                return;
            if (this.TryGetAt(pos, out var particle))
            {
                particle.density = Mathf.Clamp(particle.density + density, 0, gas.maxDensity);
                return;
            }
            if (!this.CanDiffuseTo(pos))
                return;
            this.CreateAt(density, pos);
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

        private void DrawBucket()
        {
            for (int i = 0; i < buckets[curBucketIndex].Count; i++)
                Draw(buckets[curBucketIndex][i]);
        }

        private void TickBucket()
        {
            for (int i = 0; i < buckets[curBucketIndex].Count; i++)
            {
                GasParticle particle = buckets[curBucketIndex][i];
                if (!CanDiffuseTo(particle.position) || particle.density <= 1f)
                {
                    Destroy(particle); i--;
                    return;
                }
            }
            for (int i = 0; i < buckets[curBucketIndex].Count; i++)
                Decay(buckets[curBucketIndex][i]);
            for (int i = 0; i < buckets[curBucketIndex].Count; i++)
                Diffuse(buckets[curBucketIndex][i]);
            for (int i = 0; i < buckets[curBucketIndex].Count; i++)
                ApplyHediffs(buckets[curBucketIndex][i].position);
            curBucketIndex = (curBucketIndex + 1) % BucketCount;
        }

        private void CalculateWindMatrix()
        {
            Vector3 windDirection = weatherTracker.WindDirection;
            windAngle = Mathf.Acos(Vector3.Dot(windDirection, Up));
            for (int i = 0; i < 8; i++)
                windWeights[i] = Mathf.Max(Vector3.Dot(adjCellsOffsets[i].ToVector3(), windDirection) * 1 / 4f * weightsDefautls[i], 0f);
        }

        private void Draw(GasParticle particle)
        {
            bool roofed = particle.position.Roofed(map);
            if (Rand.Chance(roofed ? 0.25f : 0.75f))
                return;
            if (GenTicks.TicksGame - particle.lastDrawn <= (roofed ? 75 : 150))
                return;
            particle.lastDrawn = GenTicks.TicksGame;
            MoteThrown obj = (MoteThrown)ThingMaker.MakeThing(ThingDefOf.Mote_Smoke);
            obj.Scale = Rand.Range(1.5f, 2.5f) * Rand.Range(0.6f, 1.5f) * Mathf.Clamp(particle.density / gas.maxDensity, 0.5f, 2.2f) + (!roofed ? 1.5f : 0.5f);
            obj.rotationRate = Rand.Range(-30f, 30f);
            obj.exactPosition = particle.position.ToVector3() + new Vector3(Rand.Range(0f, 0.5f), Rand.Range(0f, 0.5f), Rand.Range(0f, 0.5f));
            obj.SetVelocity(windAngle, Rand.Range(0.4f, 0.75f));
            Color color;
            if (roofed)
            {
                color = gas.colorInSide;
            }
            else
            {
                color = gas.colorOutSide;
            }
            color.a = Mathf.Clamp(particle.density / gas.maxDensity, 0.1f, !particle.position.Roofed(map) ? gas.opacityOutDoors : gas.opacityOutDoors);
            obj.instanceColor = color;
            GenSpawn.Spawn(obj, particle.position, map);
        }

        private void ApplyHediffs(IntVec3 pos)
        {
            if (gas.hediffGivers == null)
                return;
            foreach (var hediffGiver in gas.hediffGivers)
                hediffGiver.TryApply(pos.GetFirstPawn(map));
        }

        private void Diffuse(GasParticle particle)
        {
            float[] weights = WeightsAt(particle.position);
            for (int i = 0; i < 8; i++)
            {
                IntVec3 cell = particle.position + adjCellsOffsets[i];
                if (!cell.InBounds(map))
                    continue;
                if (!CanDiffuseTo(cell))
                    continue;
                DiffuseTo(particle, cell, weights[i]);
            }
        }

        private float[] WeightsAt(IntVec3 pos)
        {
            if (!gas.affectedByWind)
                return weightsDefautls;
            if (pos.Roofed(map))
                return weightsDefautls;
            float strength = weatherTracker.GetWindStrengthAt(pos);
            if (strength < 1.5f)
                return weightsDefautls;
            return windWeights.Select(w => w * Mathf.Sqrt(strength / 1.5f)).ToArray();
        }

        private void DiffuseTo(GasParticle particle, GasParticle other, float weight)
        {
            float diff = (particle.density - other.density) / 2f * weight;
            particle.density = Mathf.Clamp(particle.density - diff, 0, gas.maxDensity);
            other.density = Mathf.Clamp(particle.density + diff, 0, gas.maxDensity);
        }

        private void DiffuseTo(GasParticle particle, IntVec3 cell, float weight)
        {
            if (TryGetAt(cell, out var other))
            {
                DiffuseTo(particle, other, weight);
                return;
            }
            float diff = particle.density / 7f * weight;
            CreateAt(diff, cell);
            particle.density = Mathf.Clamp(particle.density - diff, 0, gas.maxDensity);
        }

        private void Decay(GasParticle particle)
        {
            float amount = gas.decayRateRoofed;
            if (!particle.position.Roofed(map))
            {
                amount = gas.decayRateUnroofed;
                float strength = weatherTracker.GetWindStrengthAt(particle.position);
                if (strength > 2.5)
                    amount *= Mathf.Sqrt(strength);
            }
            particle.density = Mathf.Clamp(particle.density - amount * (BucketCount / 60f), 0, gas.maxDensity);
        }

        private bool TryGetAt(IntVec3 cell, out GasParticle particle)
        {
            GasParticle nullableParticale = grid[map.cellIndices.CellToIndex(cell)];
            if (nullableParticale != null)
            {
                particle = nullableParticale;
                return true;
            }
            particle = null;
            return false;
        }

        private GasParticle CreateAt(float initialDensity, IntVec3 position)
        {
            if (initialDensity <= 1f)
                return null;

            GasParticle other = grid[map.cellIndices.CellToIndex(position)];
            if (other != null)
                throw new InvalidOperationException();

            int positionIndex = map.cellIndices.CellToIndex(position);
            GasParticle particle = new GasParticle(gasIDCounter, initialDensity, positionIndex, position);
            allGases.Add(particle);
            buckets[gasIDCounter % BucketCount].Add(particle);
            grid[positionIndex] = particle;
            gasIDCounter++;
            return particle;
        }

        private void Destroy(GasParticle particle)
        {
            allGases.Remove(particle);
            grid[particle.positionIndex] = null;
            buckets[particle.gasIDNumber % BucketCount].Remove(particle);
        }
    }
}
