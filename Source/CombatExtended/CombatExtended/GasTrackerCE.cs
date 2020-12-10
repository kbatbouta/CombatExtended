using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace CombatExtended
{
    public class GasTrackerCE : MapComponent
    {
        public List<GasGrid> gases;

        private static Map[] maps = new Map[20];
        private static List<GasDef> allGases;
        private static GasTrackerCE[] comps = new GasTrackerCE[20];
        private static Dictionary<GasDef, GasGrid> quickGasToGrid = new Dictionary<GasDef, GasGrid>();

        public static GasGrid GetGasGrid(Map map, GasDef gasDef)
        {
            var index = map.Index;
            if (index >= 0 && index < comps.Length)
            {
                if (maps[index] == map)
                    return comps[index].GetGridFor(gasDef);
                maps[index] = map;
                comps[index] = map.GetComponent<GasTrackerCE>();
                return comps[index].GetGridFor(gasDef);
            }
            return map.GetComponent<GasTrackerCE>().GetGridFor(gasDef);
        }

        public GasTrackerCE(Map map) : base(map)
        {
        }

        public GasGrid GetGridFor(GasDef gasDef)
        {
            if (quickGasToGrid.TryGetValue(gasDef, out var grid))
                return grid;
            return quickGasToGrid[gasDef] = gases.First(g => g.gas == gasDef);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            foreach (GasGrid grid in gases)
            {
                try
                {
                    grid.Tick();
                }
                catch (Exception er)
                {
                    Log.Error($"CE: a fatal error occured in a GasGrid {grid.gas}, with error: {er.ToString()}");
                }
            }
        }

        public override void MapGenerated()
        {
            base.MapGenerated();
            InitializeGases();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref gases, "gases", LookMode.Deep);
            if (gases == null)
            {
                InitializeGases();
            }
            foreach (GasGrid grid in gases)
            {
                grid.map = map;
            }
        }

        private void InitializeGases()
        {
            gases = new List<GasGrid>();
            if (allGases == null)
            {
                allGases = DefDatabase<GasDef>.AllDefs.ToList();
            }
            foreach (GasDef def in allGases)
            {
                gases.Add(new GasGrid(def));
            }
            foreach (GasGrid grid in gases)
            {
                grid.map = map;
            }
        }
    }
}
