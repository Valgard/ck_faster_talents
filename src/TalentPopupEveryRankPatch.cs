using HarmonyLib;
using UnityEngine;

namespace FasterTalents
{
    /// <summary>
    /// Patch B. Makes the "New talent point available!" popup, effect, and
    /// bell fire on every rank. Vanilla SaveSkillsSystem.OnUpdate calls
    /// PlayerController.SpawnSkillIncreasePopup on every rank with
    /// playAudio == !flag, and only fires SpawnNewSkillPopup itself when
    /// flag is true (every 5th rank). This Postfix fires SpawnNewSkillPopup
    /// on the other ranks (playAudio == true), so it never double-fires on
    /// the 5-rank steps. PlayerController is a MonoBehaviour — always
    /// managed, so no Burst handling is required.
    ///
    /// Only acts at the 1:1 ratio: "popup on every rank" is only meaningful
    /// when ranksPerTalentPoint == 1. For other values the popup keeps its
    /// vanilla 5-rank cadence.
    /// </summary>
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.SpawnSkillIncreasePopup))]
    internal static class TalentPopupEveryRankPatch
    {
        static TalentPopupEveryRankPatch()
        {
            Debug.Log("[FasterTalents] TalentPopupEveryRankPatch loaded.");
        }

        [HarmonyPostfix]
        private static void Postfix(PlayerController __instance, SkillID skillID, bool playAudio)
        {
            if (!ModConfig.Instance.enabled) return;
            if (ModConfig.Instance.ranksPerTalentPoint != 1) return;
            if (!playAudio) return;   // playAudio == false => vanilla 5-rank step; popup already fired
            __instance.SpawnNewSkillPopup(skillID);
        }
    }
}
