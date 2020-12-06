using Verse;

namespace CombatExtended.Compatibility
{
    class Patches
    {
        public static void Init()
        {
            //if (EDShields.CanInstall())
            //{
            //    EDShields.Install();
            //}

            if (VanillaFurnitureExpandedShields.CanInstall())
            {
                VanillaFurnitureExpandedShields.Install();
            }

            if (ProjectRimFactoryCompat.CanInstall())
            {
                ProjectRimFactoryCompat.Install();
            }
        }
    }
}
