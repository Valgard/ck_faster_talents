using System;
using HarmonyLib;
using UnityEngine;

namespace FasterTalents
{
    /// <summary>
    /// Patch B. Fires the "New talent point available!" popup, effect, and
    /// bell (PlayerController.SpawnNewSkillPopup) exactly on the skill
    /// levels that grant a talent point under the two-tier formula — and on
    /// no other level.
    ///
    /// Decompiling SaveSkillsSystem.OnUpdate showed the vanilla popup is
    /// driven per skill-change event, not per rank: OnUpdate commits the new
    /// skill value via SaveManager.SetSkillValue, then fires
    /// SpawnNewSkillPopup only when the new level is a multiple of 5.
    ///
    /// This patch hooks SetSkillValue instead. The prefix records the old
    /// level into Harmony __state; the postfix compares
    /// ModConfig.TalentPointsAtLevel before and after and fires
    /// SpawnNewSkillPopup once if the change crossed at least one grant
    /// level. The companion SpawnNewSkillPopupGate suppresses every
    /// SpawnNewSkillPopup call that does not come from this patch, removing
    /// vanilla's every-5th-level popup. SaveManager and PlayerController are
    /// plain managed types, so no Burst handling is required.
    ///
    /// The companion SkillIncreaseAudioPatch (Patch C) re-times the
    /// per-level skill-up twinkle SFX to match: the twinkle plays on
    /// exactly the levels where this patch does not fire the bell.
    /// </summary>
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SetSkillValue))]
    internal static class TalentPopupOnGrantPatch
    {
        // Set only while this patch calls SpawnNewSkillPopup itself, so the
        // SpawnNewSkillPopupGate prefix can tell our call from vanilla's.
        // ThreadStatic guards against any off-main-thread skill writes.
        [ThreadStatic] internal static bool firingOwnPopup;

        static TalentPopupOnGrantPatch()
        {
            Debug.Log("[FasterTalents] TalentPopupOnGrantPatch loaded.");
        }

        [HarmonyPrefix]
        private static void Prefix(SkillID skillId, out int __state)
        {
            // Old level: GetSkillValue still returns the pre-change value.
            int oldValue = Manager.saves.GetSkillValue(skillId);
            __state = SkillExtensions.GetLevelFromSkill(skillId, oldValue);
        }

        [HarmonyPostfix]
        private static void Postfix(SkillID skillId, int value, int __state)
        {
            if (!ModConfig.Instance.enabled) return;

            int newLevel = SkillExtensions.GetLevelFromSkill(skillId, value);
            int gained = ModConfig.Instance.TalentPointsAtLevel(newLevel)
                       - ModConfig.Instance.TalentPointsAtLevel(__state);
            if (gained <= 0) return;

            // Same guard SaveSkillsSystem.OnUpdate uses to keep popups from
            // firing while a save is still loading (no player yet).
            if (Manager.main == null || Manager.main.player == null) return;

            firingOwnPopup = true;
            try { Manager.main.player.SpawnNewSkillPopup(skillId); }
            finally { firingOwnPopup = false; }
        }
    }

    /// <summary>
    /// Gate for SpawnNewSkillPopup: allows only the call made by
    /// TalentPopupOnGrantPatch. Vanilla's own every-5th-level call (from
    /// SaveSkillsSystem.OnUpdate) is suppressed so the popup has a single,
    /// formula-driven source.
    /// </summary>
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.SpawnNewSkillPopup))]
    internal static class SpawnNewSkillPopupGate
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (!ModConfig.Instance.enabled) return true;   // run original
            return TalentPopupOnGrantPatch.firingOwnPopup;  // false => skip original
        }
    }

    /// <summary>
    /// Patch C. Re-times the per-level skill-up twinkle SFX to the two-tier
    /// formula. Vanilla's SaveSkillsSystem.OnUpdate calls
    /// SpawnSkillIncreasePopup with playAudio = (newLevel % 5 != 0): it mutes
    /// the twinkle on every 5th level because the vanilla talent bell plays
    /// there instead. TalentPopupOnGrantPatch moved the bell onto the
    /// formula's grant levels, so without this patch the every-5th levels
    /// that are not grant levels (5, 10, 20, 25, 35, 40, 50, 55, 65, 75, 85,
    /// 95) would lose
    /// the twinkle yet gain no bell — a silent level-up. This prefix
    /// recomputes playAudio as "not a grant level", so the twinkle plays on
    /// exactly the levels where no bell fires. Like vanilla's own flag, the
    /// decision uses only the new level, so a rare multi-level jump may play
    /// both sounds — a harmless cosmetic edge case.
    /// </summary>
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.SpawnSkillIncreasePopup))]
    internal static class SkillIncreaseAudioPatch
    {
        [HarmonyPrefix]
        private static void Prefix(SkillID skillID, ref bool playAudio)
        {
            if (!ModConfig.Instance.enabled) return;

            int level = SkillExtensions.GetLevelFromSkill(
                skillID, Manager.saves.GetSkillValue(skillID));
            playAudio = !ModConfig.Instance.GrantsPointAtLevel(level);
        }
    }
}
