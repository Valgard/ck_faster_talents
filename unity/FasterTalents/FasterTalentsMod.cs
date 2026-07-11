using ModSettingsMenu.Settings;
using PugMod;
using UnityEngine;

namespace FasterTalents
{
    /// <summary>
    /// Mod bootstrap. The Pugstorm mod loader instantiates this class on
    /// game start and calls the IMod lifecycle methods. The Harmony patch
    /// classes are auto-discovered by the loader — there is no PatchAll()
    /// call. The talent-curve patch targets are not Burst-compiled (SaveManager
    /// is a plain managed class, PlayerController is a MonoBehaviour). The
    /// XP-boost patch (SkillXpBoostPatch) is the exception: it targets the
    /// Burst-compiled AddSkillValueSystem, so Init disables Burst for that one
    /// system.
    /// </summary>
    public sealed class FasterTalentsMod : IMod
    {
        public void EarlyInit()
        {
        }

        public void Init()
        {
            BurstDisabler.DisableBurstForSystem<AddSkillValueSystem>();

            // Register faster-talents' settings; ModConfig reads these live handles (patches unchanged).
            ModSettings.Section(this)
                .Hint("Talent + XP tuning")
                .Choice(out var xp, "xpMultiplier", new[] { 1, 2, 3, 5, 10, 20, 50 }, 3)
                .Toggle(out var en, "enabled", true)
                .Build();
            ModConfig.Instance.Bind(en, xp);

            Debug.Log(
                $"[FasterTalents] Mod initialized. enabled={ModConfig.Instance.enabled}, " +
                $"tier1MaxLevel={ModConfig.Instance.tier1MaxLevel}, " +
                $"tier1RanksPerPoint={ModConfig.Instance.tier1RanksPerPoint}, " +
                $"tier2RanksPerPoint={ModConfig.Instance.tier2RanksPerPoint}, " +
                $"maxSkillBonusPoints={ModConfig.Instance.maxSkillBonusPoints}, " +
                $"xpMultiplier={ModConfig.Instance.xpMultiplier}");
        }

        public void ModObjectLoaded(Object obj)
        {
        }

        public void Shutdown()
        {
        }

        public void Update()
        {
        }
    }
}
