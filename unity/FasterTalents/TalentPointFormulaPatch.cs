using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FasterTalents
{
    /// <summary>
    /// Patch A. Replaces SaveManager.GetAvailableTalentPoints so a talent
    /// point is earned every `ranksPerTalentPoint` skill ranks (default 1)
    /// instead of the vanilla one per 5. The vanilla max-rank bonus is
    /// preserved but re-gated on the true max rank — the vanilla check was
    /// `floor(rank/5) >= 20`, which would misfire at rank 20 once the
    /// divisor changes. SaveManager is a plain managed class, so no Burst
    /// handling is required.
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
            if (!ModConfig.Instance.enabled) return true;   // run original

            int skillValue = Manager.saves.GetSkillValue(skillTreeID);
            int level = SkillExtensions.GetLevelFromSkill(skillTreeID, skillValue);

            int earned = level / ModConfig.Instance.ranksPerTalentPoint;
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
            return false;   // skip original
        }
    }
}
