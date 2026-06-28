using HarmonyLib;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace FasterTalents
{
    /// <summary>
    /// Patch C. Multiplies player skill-XP gain by ModConfig.xpMultiplier.
    ///
    /// Every skill-XP grant (mining, combat, fishing, crafting, cooking,
    /// gardening, …) funnels through one ECS component, AddSkillValueCD,
    /// created solely by PlayerController.AddSkill. That component is consumed
    /// by the Burst system AddSkillValueSystem, which does
    /// `skillBuffer.Value += amount` behind a `level &lt; maxLevel` guard. The
    /// producers run inside Burst-compiled simulation code, so the only robust
    /// interception point is the consumer system. FasterTalentsMod.Init disables
    /// Burst for it so this managed Prefix can run; the Prefix inflates the
    /// pending AddSkillValueCD.amount values before the original OnUpdate applies
    /// them, leaving the system's max-level guard intact (the boost is a no-op
    /// at max level).
    /// </summary>
    [HarmonyPatch(typeof(AddSkillValueSystem), "OnUpdate")]
    internal static class SkillXpBoostPatch
    {
        static SkillXpBoostPatch()
        {
            Debug.Log("[FasterTalents] SkillXpBoostPatch loaded.");
        }

        [HarmonyPrefix]
        private static void Prefix(ref SystemState state)
        {
            float mult = ModConfig.Instance.xpMultiplier;
            if (mult == 1f) return;   // boost off — leave amounts untouched

            EntityManager em = state.EntityManager;
            EntityQuery query = state.GetEntityQuery(ComponentType.ReadWrite<AddSkillValueCD>());
            NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                AddSkillValueCD cd = em.GetComponentData<AddSkillValueCD>(entities[i]);
                int boosted = (int)(cd.amount * mult + 0.5f);
                if (boosted < 1) boosted = 1;
                cd.amount = boosted;
                em.SetComponentData(entities[i], cd);
            }
            entities.Dispose();
        }
    }
}
