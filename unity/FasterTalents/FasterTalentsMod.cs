using PugMod;
using UnityEngine;

namespace FasterTalents
{
    /// <summary>
    /// Mod bootstrap. The Pugstorm mod loader instantiates this class on
    /// game start and calls the IMod lifecycle methods. The Harmony patch
    /// classes are auto-discovered by the loader — there is no PatchAll()
    /// call. Neither patch target is Burst-compiled (SaveManager is a plain
    /// managed class, PlayerController is a MonoBehaviour), so unlike the
    /// sibling durability mod no BurstDisabler call is needed.
    /// </summary>
    public sealed class FasterTalentsMod : IMod
    {
        public void EarlyInit()
        {
        }

        public void Init()
        {
            Debug.Log(
                $"[FasterTalents] Mod initialized. enabled={ModConfig.Instance.enabled}, " +
                $"tier1MaxLevel={ModConfig.Instance.tier1MaxLevel}, " +
                $"tier1RanksPerPoint={ModConfig.Instance.tier1RanksPerPoint}, " +
                $"tier2RanksPerPoint={ModConfig.Instance.tier2RanksPerPoint}, " +
                $"maxSkillBonusPoints={ModConfig.Instance.maxSkillBonusPoints}");
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
