using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FasterTalents
{
    /// <summary>
    /// Patch A. Replaces SaveManager.GetAvailableTalentPoints with the
    /// two-tier formula in ModConfig.TalentPointsAtLevel — one point per
    /// 3 skill levels up to level 60, one per 2 levels to 100 (40 total).
    /// The vanilla max-rank bonus is preserved but re-gated on the true max
    /// level; with maxSkillBonusPoints == 0 (the default) that branch is a
    /// no-op. SaveManager is a plain managed class, so no Burst handling is
    /// required.
    /// </summary>
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.GetAvailableTalentPoints))]
    internal static class TalentPointFormulaPatch
    {
        static TalentPointFormulaPatch()
        {
            Debug.Log("[FasterTalents] TalentPointFormulaPatch loaded.");
        }

        [HarmonyPrefix]
        private static bool Prefix(SkillID skillTreeID, ref int __result)
        {
            if (!ModConfig.Instance.enabled)
                return true; // run original

            int skillValue = Manager.saves.GetSkillValue(skillTreeID);
            int level = SkillExtensions.GetLevelFromSkill(skillTreeID, skillValue);

            int earned = ModConfig.Instance.TalentPointsAtLevel(level);
            if (level >= SkillExtensions.GetMaxSkillLevel(skillTreeID))
                earned += ModConfig.Instance.maxSkillBonusPoints;

            int spent = 0;
            List<int> points = Manager.saves.GetSkillTalentTreesPoints(skillTreeID);
            if (points != null)
            {
                for (int i = 0; i < points.Count; i++)
                    spent += points[i];
            }

            __result = earned - spent;
            return false; // skip original
        }
    }
}
