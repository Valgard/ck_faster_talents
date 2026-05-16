# Tiered Talent-Point Formula Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the mod's linear talent-point formula with a two-tier curve — one point per 3 skill levels up to level 60, then one per 2 levels to 100 (exactly 40 points per skill tree) — and make the talent-point popup fire on exactly those grant levels.

**Architecture:** Three Harmony patches against `CoreKeeperModSDK` game DLLs. `ModConfig.TalentPointsAtLevel` is the single shared formula. Patch A (`SaveManager.GetAvailableTalentPoints`) returns the running total. Patch B hooks `SaveManager.SetSkillValue` (prefix+postfix, Harmony `__state` carries the old level) and fires `SpawnNewSkillPopup` when a change crosses a grant level; a gate prefix on `SpawnNewSkillPopup` suppresses vanilla's every-5th-level popup.

**Tech Stack:** C# (Unity 6000.0.59f2), HarmonyLib, Pugstorm `CoreKeeperModSDK`. Build via `unity -batchmode`. No automated test framework — verification is a Unity batchmode compile plus a manual in-game check.

**Execution note:** Per the repo convention (`../CLAUDE.md`), do this work in a dedicated git worktree under `REPO_ROOT/.worktrees/`. The `superpowers:using-git-worktrees` skill handles creation at execution start.

**Spec:** `docs/superpowers/specs/2026-05-16-tiered-talent-formula-design.md`

---

## File Structure

| File | Change | Responsibility |
|------|--------|----------------|
| `unity/FasterTalents/ModConfig.cs` | Rewrite | Tier constants + `TalentPointsAtLevel` formula. |
| `unity/FasterTalents/TalentPointFormulaPatch.cs` | Modify | Patch A — uses the shared formula. |
| `unity/FasterTalents/TalentPopupEveryRankPatch.cs` → `TalentPopupOnGrantPatch.cs` | Rename + rewrite | Patch B — `SetSkillValue` hook + `SpawnNewSkillPopup` gate. |
| `unity/FasterTalents/FasterTalentsMod.cs` | Modify | Init log references removed `ranksPerTalentPoint`. |
| `CLAUDE.md` | Modify | Architecture docs name `ranksPerTalentPoint` / old class. |
| `README.md` | Modify | Intro, "What it does", config table. |

`.meta` files are GUID carriers (versioned). Renaming a `.cs` requires renaming its `.cs.meta` alongside it so the GUID is preserved.

---

## Task 1: Implement the two-tier formula (all C# changes)

All four source files depend on the `ModConfig` API; they change together in one
compiling commit.

**Files:**
- Modify: `unity/FasterTalents/ModConfig.cs`
- Modify: `unity/FasterTalents/TalentPointFormulaPatch.cs`
- Rename: `unity/FasterTalents/TalentPopupEveryRankPatch.cs` → `TalentPopupOnGrantPatch.cs` (+ `.cs.meta`)
- Modify: `unity/FasterTalents/FasterTalentsMod.cs`

- [ ] **Step 1: Rewrite `ModConfig.cs`**

Replace the entire file with:

```csharp
namespace FasterTalents
{
    /// <summary>
    /// Mod configuration. Values are hardcoded constants: Pugstorm's
    /// RoslynCSharp sandbox blocks System.IO, so a runtime config.json
    /// cannot be read. The singleton shape (ModConfig.Instance.field) is
    /// kept so a future config loader could drop in without touching the
    /// patch classes.
    ///
    /// The talent-point curve is a two-tier piecewise formula: one point
    /// per `tier1RanksPerPoint` levels up to `tier1MaxLevel`, then one per
    /// `tier2RanksPerPoint` levels above it. With the defaults a level-100
    /// skill yields exactly 40 points — enough to fully skill one talent
    /// tree. Setting `tier1RanksPerPoint = tier2RanksPerPoint = 5` and
    /// `maxSkillBonusPoints = 5` restores the vanilla curve.
    /// </summary>
    internal sealed class ModConfig
    {
        // Master switch. When false, all patches fall through to vanilla.
        public bool enabled = true;

        // Last skill level covered by the tier-1 rate.
        public int tier1MaxLevel = 60;

        // Tier 1 (levels 1..tier1MaxLevel): levels needed per talent point.
        public int tier1RanksPerPoint = 3;

        // Tier 2 (levels above tier1MaxLevel): levels needed per talent point.
        public int tier2RanksPerPoint = 2;

        // Bonus talent points granted once at the max skill level. Vanilla
        // is 5; 0 keeps the level-100 total at exactly the formula result.
        public int maxSkillBonusPoints = 0;

        private static readonly ModConfig _instance = new ModConfig();
        public static ModConfig Instance => _instance;

        /// <summary>
        /// Total talent points earned at a given skill level under the
        /// two-tier formula. Shared by both patches so the running total
        /// (Patch A) and the popup trigger (Patch B) can never diverge.
        /// Integer division produces the level staircase.
        /// </summary>
        public int TalentPointsAtLevel(int level)
        {
            if (level <= tier1MaxLevel)
                return level / tier1RanksPerPoint;

            return tier1MaxLevel / tier1RanksPerPoint
                 + (level - tier1MaxLevel) / tier2RanksPerPoint;
        }
    }
}
```

- [ ] **Step 2: Rewrite `TalentPointFormulaPatch.cs`**

Replace the entire file with (only the `earned` line and the doc comment differ
from the original):

```csharp
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
            if (!ModConfig.Instance.enabled) return true;   // run original

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
            return false;   // skip original
        }
    }
}
```

- [ ] **Step 3: Rename the popup patch file and its `.meta`**

Run:

```bash
git mv unity/FasterTalents/TalentPopupEveryRankPatch.cs \
       unity/FasterTalents/TalentPopupOnGrantPatch.cs
git mv unity/FasterTalents/TalentPopupEveryRankPatch.cs.meta \
       unity/FasterTalents/TalentPopupOnGrantPatch.cs.meta
```

The `.cs.meta` content (GUID `5263a062e252243f6988796cf7eed024`) is unchanged —
`git mv` preserves it, keeping the Unity asset GUID stable.

- [ ] **Step 4: Rewrite `TalentPopupOnGrantPatch.cs`**

Replace the entire content of the renamed `unity/FasterTalents/TalentPopupOnGrantPatch.cs` with:

```csharp
using System;
using HarmonyLib;
using UnityEngine;

namespace FasterTalents
{
    /// <summary>
    /// Patch B. Fires the "New talent point available!" popup, effect, and
    /// bell exactly on the skill levels that grant a talent point under the
    /// two-tier formula — and nowhere else.
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
}
```

- [ ] **Step 5: Update the Init log in `FasterTalentsMod.cs`**

The `Init()` method logs `ModConfig.Instance.ranksPerTalentPoint`, which no
longer exists. Replace the `Debug.Log(...)` call in `Init()`:

Old:

```csharp
            Debug.Log(
                $"[FasterTalents] Mod initialized. enabled={ModConfig.Instance.enabled}, " +
                $"ranksPerTalentPoint={ModConfig.Instance.ranksPerTalentPoint}, " +
                $"maxSkillBonusPoints={ModConfig.Instance.maxSkillBonusPoints}");
```

New:

```csharp
            Debug.Log(
                $"[FasterTalents] Mod initialized. enabled={ModConfig.Instance.enabled}, " +
                $"tier1MaxLevel={ModConfig.Instance.tier1MaxLevel}, " +
                $"tier1RanksPerPoint={ModConfig.Instance.tier1RanksPerPoint}, " +
                $"tier2RanksPerPoint={ModConfig.Instance.tier2RanksPerPoint}, " +
                $"maxSkillBonusPoints={ModConfig.Instance.maxSkillBonusPoints}");
```

The class doc comment ("Neither patch target is Burst-compiled…") stays correct:
all three patch targets (`SaveManager.GetAvailableTalentPoints`,
`SaveManager.SetSkillValue`, `PlayerController.SpawnNewSkillPopup`) are plain
managed / MonoBehaviour members.

- [ ] **Step 6: Commit**

```bash
git add unity/FasterTalents/ModConfig.cs \
        unity/FasterTalents/TalentPointFormulaPatch.cs \
        unity/FasterTalents/TalentPopupOnGrantPatch.cs \
        unity/FasterTalents/TalentPopupOnGrantPatch.cs.meta \
        unity/FasterTalents/TalentPopupEveryRankPatch.cs \
        unity/FasterTalents/TalentPopupEveryRankPatch.cs.meta \
        unity/FasterTalents/FasterTalentsMod.cs
git commit -m "Implement two-tier talent-point formula"
```

(Staging the old `TalentPopupEveryRankPatch.cs*` paths records the rename; with
`git mv` already done they appear as deletions.)

---

## Task 2: Build verification

The mod compiles against the game DLLs via a Unity batchmode build — this is the
only compile check available (no standalone test project).

**Files:** none (verification only)

- [ ] **Step 1: Ensure the Unity Editor is closed**

The Editor locks the project; a batchmode build fails while it is open. Quit any
running Unity instance for `CoreKeeperModSDK`.

- [ ] **Step 2: Run the build**

Run from the mod repo root:

```bash
source .envrc
../utils/build.sh
```

Expected: the script refreshes the SDK symlinks, runs the Unity batchmode build
(`FasterTalents.Editor.CLIBuildHelper.Build`), and exits 0. On macOS it then
auto-runs `install-macos.sh`. A compile error in any patched file fails the build
with a `CS####` diagnostic — if that happens, fix the reported file and re-run.

- [ ] **Step 3: Confirm the build output**

Expected: `build.sh` exits with status 0 and the ModBuilder output appears under
`$MOD_INSTALL_PATH/FasterTalents/` (`ModManifest.json`, `Scripts/`, `Bundles/`).
No `CS####` errors in the build log.

---

## Task 3: Update documentation

**Files:**
- Modify: `CLAUDE.md`
- Modify: `README.md`

- [ ] **Step 1: Update `CLAUDE.md` — "What this repo is"**

Old:

```markdown
A Core Keeper mod that grants one talent point per skill rank instead of the vanilla one per five ranks. Two Harmony patches against Pugstorm's `CoreKeeperModSDK`. Single-target, personal-use, non-commercial (Pugstorm EULA).
```

New:

```markdown
A Core Keeper mod that replaces the vanilla talent-point curve (one point per 5 skill levels) with a two-tier curve: one point per 3 levels up to level 60, then one per 2 levels to 100 — 40 points total per skill tree. Three Harmony patches against Pugstorm's `CoreKeeperModSDK`. Single-target, personal-use, non-commercial (Pugstorm EULA).
```

- [ ] **Step 2: Update `CLAUDE.md` — "Architecture" intro line**

Old:

```markdown
Four runtime classes plus one editor helper, all in the `FasterTalents` namespace:
```

New:

```markdown
Five runtime classes plus one editor helper, all in the `FasterTalents` namespace:
```

- [ ] **Step 3: Update `CLAUDE.md` — the `ModConfig` and patch bullets**

Old:

```markdown
- **`ModConfig`** — hardcoded constants `enabled`, `ranksPerTalentPoint` (default 1), `maxSkillBonusPoints` (default 5). No runtime config file — the RoslynCSharp sandbox blocks `System.IO`.
- **`TalentPointFormulaPatch`** — `Prefix` replacing `SaveManager.GetAvailableTalentPoints`; computes `rank / ranksPerTalentPoint` plus the re-gated max-rank bonus.
- **`TalentPopupEveryRankPatch`** — `Postfix` on `PlayerController.SpawnSkillIncreasePopup`; fires `SpawnNewSkillPopup` on the ranks vanilla skips. Only acts when `ranksPerTalentPoint == 1`.
```

New:

```markdown
- **`ModConfig`** — hardcoded constants `enabled`, `tier1MaxLevel` (60), `tier1RanksPerPoint` (3), `tier2RanksPerPoint` (2), `maxSkillBonusPoints` (0), plus `TalentPointsAtLevel(level)` — the shared two-tier formula. No runtime config file — the RoslynCSharp sandbox blocks `System.IO`.
- **`TalentPointFormulaPatch`** — `Prefix` replacing `SaveManager.GetAvailableTalentPoints`; returns `ModConfig.TalentPointsAtLevel(level)` plus the re-gated max-rank bonus (0 by default).
- **`TalentPopupOnGrantPatch`** — `Prefix`+`Postfix` on `SaveManager.SetSkillValue`; uses Harmony `__state` to compare the talent total before and after the change and fires `SpawnNewSkillPopup` once when a grant level is crossed. The companion **`SpawnNewSkillPopupGate`** (`Prefix` on `PlayerController.SpawnNewSkillPopup`) suppresses vanilla's every-5th-level popup so the popup has one formula-driven source.
```

- [ ] **Step 4: Update `README.md` — intro line (line 3)**

Old:

```markdown
A small Core Keeper mod that grants one talent point per skill rank instead of the vanilla one per five ranks. Built on the official Pugstorm `CoreKeeperModSDK`.
```

New:

```markdown
A small Core Keeper mod that replaces the vanilla talent-point curve with a faster two-tier curve — 40 talent points per maxed skill instead of vanilla's 25. Built on the official Pugstorm `CoreKeeperModSDK`.
```

- [ ] **Step 5: Update `README.md` — "What it does" section**

Old:

```markdown
Each player skill earns talent points as it ranks up. Vanilla grants one talent point every 5 ranks; this mod grants one every rank — five times faster. The "New talent point available!" popup and bell fire on every rank to match.
```

New:

```markdown
Each player skill earns talent points as it levels up. Vanilla grants one point every 5 skill levels, plus 5 at the max level — 25 total. This mod uses a two-tier curve: one point every 3 levels up to level 60, then one every 2 levels to level 100 — **exactly 40 points per skill tree**, enough to fully skill one talent tree. The "New talent point available!" popup and bell fire on each level that grants a point.
```

- [ ] **Step 6: Update `README.md` — the Configuration table**

Old:

```markdown
| Constant | Default | Vanilla | Effect |
|----------|---------|---------|--------|
| `enabled` | `true` | — | Master switch. When `false`, both patches early-return and the game behaves exactly as vanilla. |
| `ranksPerTalentPoint` | `1` | `5` | Skill ranks needed per earned talent point. Setting it to `5` restores vanilla behavior; at any value other than `1` the per-rank popup also reverts to the vanilla 5-rank cadence. |
| `maxSkillBonusPoints` | `5` | `5` | Extra talent points granted once when a skill reaches its max rank. |
```

New:

```markdown
| Constant | Default | Vanilla | Effect |
|----------|---------|---------|--------|
| `enabled` | `true` | — | Master switch. When `false`, all patches fall through and the game behaves exactly as vanilla. |
| `tier1MaxLevel` | `60` | — | Last skill level covered by the tier-1 rate. |
| `tier1RanksPerPoint` | `3` | `5` | Skill levels needed per talent point at or below `tier1MaxLevel`. |
| `tier2RanksPerPoint` | `2` | `5` | Skill levels needed per talent point above `tier1MaxLevel`. |
| `maxSkillBonusPoints` | `0` | `5` | Extra talent points granted once when a skill reaches its max level (100). |

Setting `tier1RanksPerPoint` and `tier2RanksPerPoint` both to `5` and
`maxSkillBonusPoints` to `5` restores the vanilla curve.
```

- [ ] **Step 7: Commit**

```bash
git add CLAUDE.md README.md
git commit -m "Sync docs with the two-tier talent formula"
```

---

## Task 4: In-game manual verification

No automated tests exist; behavior is verified in a running game. Deploy is
already done by Task 2 (`build.sh` auto-runs `install-macos.sh` on macOS).

**Files:** none (verification only)

- [ ] **Step 1: Launch Core Keeper and load a world**

Do NOT open the in-game Mods menu (it triggers a mod.io sync that wipes the
fake-ID install — see `../CLAUDE.md`). If the main menu shows the incompatible-mod
warning dialog, choose **Load Anyway**.

- [ ] **Step 2: Verify the talent-point total at sample levels**

Gain skill ranks (or use an existing character) and confirm the available
talent-point count for a skill matches `TalentPointsAtLevel`:

| Skill level | Expected available points (unspent) |
|-------------|--------------------------------------|
| 3           | 1                                    |
| 6           | 2                                    |
| 60          | 20                                   |
| 62          | 21                                   |
| 100         | 40                                   |

- [ ] **Step 3: Verify the popup fires on grant levels only**

Gain individual skill levels and confirm the "New talent point available!" popup,
effect, and bell fire when crossing a grant level (3, 6, 9, …, 60, 62, 64, …) and
do **not** fire on a non-grant level (e.g. levels 1, 2, 4, 5, 61).

- [ ] **Step 4: Verify no spurious popups on save load**

Reload an existing world with a high-level skill. Expected: no burst of talent
popups during loading. (Patch B fires `SpawnNewSkillPopup` from the
`SetSkillValue` postfix; the `Manager.main.player == null` guard — vanilla's own
load guard — should suppress it during load. If a popup burst does appear, save
deserialization is routing through `SetSkillValue` after the player exists; the
fix is to bracket the popup with a `[ThreadStatic]` flag set by a
prefix/postfix on `SaveSkillsSystem.OnUpdate` so the popup only fires inside the
gameplay update.)

- [ ] **Step 5: Confirm `enabled = false` restores vanilla (optional)**

Set `ModConfig.enabled = false`, rebuild, and confirm the talent count and popup
cadence revert to vanilla (one point per 5 levels). Restore `enabled = true`
afterward.

---

## Self-Review

**Spec coverage:**
- New formula (`TalentPointsAtLevel`, two tiers) → Task 1 Step 1. ✓
- Max-rank bonus removed (`maxSkillBonusPoints = 0`, field kept) → Task 1 Step 1. ✓
- Patch A uses the shared formula → Task 1 Step 2. ✓
- Patch B: `SetSkillValue` prefix+postfix with `__state` + `SpawnNewSkillPopup` gate → Task 1 Steps 3–4. ✓
- Single shared formula consumed by both patches → `TalentPointsAtLevel`, Task 1. ✓
- Doc updates (`ModConfig.cs`, patch doc comments, `CLAUDE.md`, `README.md`) → in-file comments in Task 1; `CLAUDE.md`/`README.md` in Task 3. ✓
- Non-goals (no runtime config, no generic tier list, no linear mode) → respected. ✓
- Verification (build + manual in-game checks) → Tasks 2 and 4. ✓

**Placeholder scan:** No TBD/TODO; every code step shows complete file content or
exact old/new strings.

**Type consistency:** `ModConfig.TalentPointsAtLevel(int) : int` is defined in
Task 1 Step 1 and called identically in Steps 2 and 4. `TalentPopupOnGrantPatch.firingOwnPopup`
is defined in Step 4 and read in the same file by `SpawnNewSkillPopupGate`.
Harmony `__state` is `out int` in the prefix and `int` in the postfix (Step 4).
`SetSkillValue` parameter names `skillId` / `value` match the decompiled game
signature.
