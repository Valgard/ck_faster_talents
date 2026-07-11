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

            // Binding spike: prove a separate mod can register with the framework. Behavior is
            // wired through ModConfig in a later step; here the toggle just proves the section
            // appears + persists.
            ModSettings.Section(this)
                .Toggle(out _, "enabled", true)
                .Build();

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
