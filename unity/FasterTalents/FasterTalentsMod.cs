using ModSettingsMenu.Settings;
using PugMod;
using Unity.Entities;
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
        public void EarlyInit() { }

        public void Init()
        {
            BurstDisabler.DisableBurstForSystem<AddSkillValueSystem>();

            // Registering the system is only half of it: the Burst bypass is
            // armed per world by BurstDisabler.AddWorld, whose sole caller is
            // ECSManager.StartEcs, and which snapshots whatever is registered by
            // then. Through CK 1.2 a dedicated server ran IMod.Init() *after*
            // StartEcs, so without this pass OnUpdate kept going through the
            // Burst path, the prefix was never reached, and the XP boost was
            // silently off exactly where it matters — skill XP is
            // server-authoritative. A 1.3.0.5 server runs Init() before that
            // snapshot, but the SDK promises no ordering and the mod still
            // targets 1.2, so the pass stays. Harmless where Init() runs first:
            // AddWorld only arms worlds that contain the system, and on 1.3 the
            // game's own ResetWorlds + AddWorld pass replaces it anyway.
            foreach (var world in World.All)
                BurstDisabler.AddWorld(world);

            // Register faster-talents' settings; ModConfig reads these live handles (patches unchanged).
            // Section uses the default AsDeclared sort, so builder-call order IS render order: the
            // "enabled" master toggle first, then the XP multiplier.
            ModSettings
                .Section(this)
                .Hint("A faster talent-point curve plus a skill-XP boost - fill your talent trees sooner.")
                .Toggle(out var en, "enabled", true)
                .Choice(out var xp, "xpMultiplier", new[] { 1, 2, 3, 5, 10, 20, 50 }, 3)
                .Build();
            ModConfig.Instance.Bind(en, xp);

            Debug.Log(
                $"[FasterTalents] Mod initialized. enabled={ModConfig.Instance.enabled}, "
                    + $"tier1MaxLevel={ModConfig.Instance.tier1MaxLevel}, "
                    + $"tier1RanksPerPoint={ModConfig.Instance.tier1RanksPerPoint}, "
                    + $"tier2RanksPerPoint={ModConfig.Instance.tier2RanksPerPoint}, "
                    + $"maxSkillBonusPoints={ModConfig.Instance.maxSkillBonusPoints}, "
                    + $"xpMultiplier={ModConfig.Instance.xpMultiplier}"
            );
        }

        public void ModObjectLoaded(Object obj) { }

        public void Shutdown() { }

        public void Update() { }
    }
}
