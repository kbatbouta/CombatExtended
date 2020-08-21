﻿using System.Text;
using RimWorld;
using Verse;

namespace CombatExtended
{
    public static class AmmoUtility
    {
        /// <summary>
        ///     Generates a readout text for a projectile with the damage amount, type, secondary explosion and other CE stats for
        ///     display in info-box
        /// </summary>
        /// <param name="projectileDef">The projectile's ThingDef</param>
        /// <returns>Formatted string listing projectile stats</returns>
        public static string GetProjectileReadout(this ThingDef projectileDef, Thing weapon)
        {
            // Append ammo stats
            var props = projectileDef?.projectile as ProjectilePropertiesCE;
            if (props == null)
            {
                Log.Error("CE tried getting projectile readout with null props");
                return "";
            }

            var stringBuilder = new StringBuilder();

            // Damage type/amount
            var dmgList = "   " + "CE_DescDamage".Translate() + ": ";
            if (!props.secondaryDamage.NullOrEmpty())
            {
                // If we have multiple damage types, put every one in its own line
                stringBuilder.AppendLine(dmgList);
                stringBuilder.AppendLine("   " + GenText.ToStringByStyle(props.GetDamageAmount(weapon), ToStringStyle.Integer) + " (" + props.damageDef.LabelCap + ")");
                foreach (var sec in props.secondaryDamage)
                {
                    stringBuilder.AppendLine("   " + GenText.ToStringByStyle(sec.amount, ToStringStyle.Integer) + " (" + sec.def.LabelCap + ")");
                }
            }
            else
            {
                stringBuilder.AppendLine(dmgList + GenText.ToStringByStyle(props.GetDamageAmount(weapon), ToStringStyle.Integer) + " (" + props.damageDef.LabelCap + ")");
            }
            // Explosion radius
            if (props.explosionRadius > 0)
            {
                stringBuilder.AppendLine("   " + "CE_DescExplosionRadius".Translate() + ": " + props.explosionRadius.ToStringByStyle(ToStringStyle.FloatOne));
            }

            // Secondary explosion
            var secExpProps = projectileDef.GetCompProperties<CompProperties_ExplosiveCE>();
            if (secExpProps != null)
            {
                if (secExpProps.explosiveRadius > 0)
                {
                    stringBuilder.AppendLine("   " + "CE_DescSecondaryExplosion".Translate() + ":");
                    stringBuilder.AppendLine("   " + "   " + "CE_DescExplosionRadius".Translate() + ": " + secExpProps.explosiveRadius.ToStringByStyle(ToStringStyle.FloatOne));
                    stringBuilder.AppendLine("   " + "   " + "CE_DescDamage".Translate() + ": " +
                                             secExpProps.damageAmountBase.ToStringByStyle(ToStringStyle.Integer) + " (" + secExpProps.explosiveDamageType.LabelCap + ")");
                }
              /* Fragrange never did anything
                if (secExpProps.fragRange > 0)
                {
                    stringBuilder.AppendLine("   " + "CE_DescFragRange".Translate() + ": " + secExpProps.fragRange.ToStringByStyle(ToStringStyle.FloatTwo));
                }*/
            }

            // CE stats
            stringBuilder.AppendLine("   " + "CE_DescSharpPenetration".Translate() + ": " + props.armorPenetrationSharp.ToStringByStyle(ToStringStyle.FloatTwo) + " " + "CE_mmRHA".Translate());
            stringBuilder.AppendLine("   " + "CE_DescBluntPenetration".Translate() + ": " + props.armorPenetrationBlunt.ToStringByStyle(ToStringStyle.FloatTwo) + " " + "CE_MPa".Translate());

            if (props.pelletCount > 1)
            {
                stringBuilder.AppendLine("   " + "CE_DescPelletCount".Translate() + ": " + GenText.ToStringByStyle(props.pelletCount, ToStringStyle.Integer));
            }
            if (props.spreadMult != 1)
            {
                stringBuilder.AppendLine("   " + "CE_DescSpreadMult".Translate() + ": " + props.spreadMult.ToStringByStyle(ToStringStyle.PercentZero));
            }

            return stringBuilder.ToString();
        }

        public static bool IsShell(ThingDef def)
        {
            var ammo = ThingDefOf.Turret_Mortar.building.turretGunDef.GetCompProperties<CompProperties_AmmoUser>();
            return ammo?.ammoSet.ammoTypes.Any(l => l.ammo == def) ?? false;
        }

        public static bool IsAmmoSystemActive(AmmoDef def)
		{
            if (Controller.settings.EnableAmmoSystem) return true;
            return (def != null && def.isMortarAmmo);
		}

        public static bool IsAmmoSystemActive(AmmoSetDef ammoSet)
		{
            if (Controller.settings.EnableAmmoSystem) return true;
            return (ammoSet != null && ammoSet.isMortarAmmoSet);
		}
    }
}