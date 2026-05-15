# Faster Talents — Design Spec (V1)

| Field | Value |
|---|---|
| Date | 2026-05-15 |
| Status | Approved, ready for implementation planning |
| Target game | Core Keeper (Windows build, running under CrossOver on macOS) |
| Modding pipeline | Pugstorm `CoreKeeperModSDK` (official) + Harmony |
| Distribution | Personal use, non-commercial (Pugstorm EULA) |
| Sibling mod | `disable-durability/` — shares the SDK clone and the build/deploy pattern |

## 1. Problem statement

In Core Keeper each of the twelve player skills (Mining, Melee, Vitality, …)
ranks up through activity. The player spends **talent points** on each
skill's talent tree. By default a talent point is earned only every **5
ranks**, so progressing a talent tree feels slow relative to the rate at
which skills rank up. The user wants one talent point per *single* rank.

## 2. Goals (V1)

1. One talent point is earned per skill rank instead of per 5 ranks.
2. The rank→point ratio is an adjustable constant in source (not a fixed
   literal), so it can be retuned with a rebuild.
3. The "New talent point available!" popup, effect, and bell fire on **every**
   rank, keeping the visible feedback consistent with the new rate.
4. Behavior is retroactive: existing characters immediately see the talent
   points they would have earned under the new rate.
5. Minimal, isolated patches — easy to remove or revise when the game updates.
6. Built on the officially supported Pugstorm modding pipeline.

## 3. Non-goals (V1)

- Runtime configuration via a `config.json` file — Pugstorm's RoslynCSharp
  sandbox blocks `System.IO`, so the adjustable knob is a source constant, not
  a runtime file. (See §6 of the sibling mod's lived experience.)
- In-game UI or hotkey to change the ratio.
- Changing the **pet talent** system — explicitly out of scope; pet talents
  use a separate formula and are untouched.
- Changing how skills *rank up* (XP curves, the `gainOneSkillPointPerLevel`
  dev toggle) — only the talent-point *award rate* changes.
- A talent-point *refund* or tree-reset feature.
- Automated tests (Harmony patches are verified manually, as with the sibling
  mod).
- mod.io upload.

## 4. Constraints

- **Pugstorm EULA**: non-commercial distribution only.
- **RoslynCSharp sandbox**: mods are compiled at load time inside a
  default-deny sandbox. `System.IO`, `System.Diagnostics.Process`, and
  `System.Reflection.Emit` fail the compile. The mod uses none of these.
  Both patches are plain `Prefix`/`Postfix` methods using only HarmonyLib
  attributes and game types — no transpiler, so `System.Reflection.Emit`
  (which `CodeInstruction.opcode` would drag in) is never referenced. This
  is why `skipSafetyChecks` stays `false`.
- **Build toolchain**: Unity Editor `6000.0.59f2` with Linux Build Support
  (Mono); already installed for the sibling mod.
- **Shared SDK**: `CoreKeeperModSDK/` is the same clone the sibling mod uses.
  The Steamworks macOS fix and "Update Game Files" are already applied.
- **Architecture**: Core Keeper uses Unity DOTS/ECS. Both patch targets were
  identified by decompiling the SDK's bundled game DLLs with `ilspycmd`.

## 5. Architecture

### 5.1 High-level summary

Two Harmony patches, auto-discovered by Pugstorm's loader:

1. **`TalentPointFormulaPatch`** — a `Prefix` that fully replaces
   `SaveManager.GetAvailableTalentPoints`, recomputing earned points as
   `rank / ranksPerTalentPoint` instead of `rank / 5`.
2. **`TalentPopupEveryRankPatch`** — a `Postfix` on
   `PlayerController.SpawnSkillIncreasePopup` that fires the "new talent
   point" popup on the ranks where vanilla would not.

A `ModConfig` holds the adjustable constants. A `FasterTalentsMod : IMod`
bootstrap logs initialization. No `BurstDisabler` is needed — see §6.3.

### 5.2 Components

| Component | Responsibility | Visibility |
|---|---|---|
| `FasterTalentsMod` | `IMod` bootstrap; logs init. No Burst handling needed. | public sealed |
| `ModConfig` | Holds hardcoded constants `enabled`, `ranksPerTalentPoint`, `maxSkillBonusPoints`. Singleton shape mirrors the sibling mod. | internal/public |
| `TalentPointFormulaPatch` | Harmony `Prefix` replacing `SaveManager.GetAvailableTalentPoints`. | internal static |
| `TalentPopupEveryRankPatch` | Harmony `Postfix` on `PlayerController.SpawnSkillIncreasePopup`. | internal static |
| `Editor/CLIBuildHelper` | Editor-only wrapper around `PugMod.ModBuilder.BuildMod(...)` for `unity -batchmode -executeMethod`. | public static (Editor-only) |

Each component has one responsibility. The patch classes do not know how
config is stored; the connection point is a read of `ModConfig`.

### 5.3 Source repository layout

```
faster-talents/                                  # this git repo
├── .gitignore
├── .envrc.example                               # machine paths template
├── CLAUDE.md                                    # mod-specific guidance
├── README.md
├── docs/superpowers/specs/
│   └── 2026-05-15-faster-talents-design.md
├── src/                                         # canonical source of truth
│   ├── FasterTalentsMod.cs
│   ├── ModConfig.cs
│   ├── TalentPointFormulaPatch.cs
│   ├── TalentPopupEveryRankPatch.cs
│   └── Editor/
│       ├── FasterTalents.Editor.asmdef
│       └── CLIBuildHelper.cs
└── scripts/
    ├── link.sh                                  # symlinks src/ into the SDK
    ├── build.sh                                 # unity batchmode build
    └── install-macos.sh                         # fake-mod.io deploy step
```

No `config/config.json` is shipped (unlike the sibling mod): the sandbox
prevents reading it at runtime, so a config file would only mislead. The
adjustable values live solely in `ModConfig.cs`.

### 5.4 SDK integration

`src/` is canonical. `scripts/link.sh` symlinks the `.cs` files and the
`.Editor.asmdef` into the SDK clone's `Assets/FasterTalents/` folder, which
the "Create New Mod" wizard creates (see §8). Symlinks encode absolute paths
and dangle after a repo move, so `build.sh` re-runs `link.sh` idempotently.

## 6. Patch strategy

Both targets were located by decompiling `Pug.Other.dll` / `Pug.Base.dll`
(`ilspycmd`). The reference code below is the **decompiled vanilla** as of the
current game build — it is the contract our patches replace.

### 6.1 Patch A — talent-point formula

Vanilla `SaveManager.GetAvailableTalentPoints` (`Pug.Other.dll`):

```csharp
public int GetAvailableTalentPoints(SkillID skillTreeID)
{
    int skillValue = Manager.saves.GetSkillValue(skillTreeID);
    int num = (int)math.floor((float)SkillExtensions.GetLevelFromSkill(skillTreeID, skillValue) / 5f);
    if (num >= 20)               // num == 20 only at rank 100
        num += 5;                // Constants.kAdditionalTalentPointWhenMaxSkill
    int num2 = 0;
    List<int> points = Manager.saves.GetSkillTalentTreesPoints(skillTreeID);
    if (points != null)
        for (int i = 0; i < points.Count; i++)
            num2 += points[i];
    return num - num2;           // earned − spent
}
```

Notes:
- The `5f` is an inline literal, **not** a reference to
  `Constants.kSkillPointsPerTalentPoint` (`const int = 5`) — that named
  constant is defined but unused here, and being a `const` it is inlined at
  the game's compile time and cannot be changed at runtime anyway.
- `GetMaxSkillLevel` returns `100` for every skill, so the max-skill bonus
  triggers exactly at rank 100.
- This method is the single authority: `SkillTalentTreeUI` uses it both to
  display the available count and to gate spending.

Patch shape — a `Prefix` that fully replaces the method:

```csharp
[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.GetAvailableTalentPoints))]
internal static class TalentPointFormulaPatch
{
    [HarmonyPrefix]
    static bool Prefix(SkillID skillTreeID, ref int __result)
    {
        if (!ModConfig.Instance.enabled) return true;            // run original

        int skillValue = Manager.saves.GetSkillValue(skillTreeID);
        int level = SkillExtensions.GetLevelFromSkill(skillTreeID, skillValue);

        int earned = level / ModConfig.Instance.ranksPerTalentPoint;   // integer div == floor for level >= 0
        if (level >= SkillExtensions.GetMaxSkillLevel(skillTreeID))
            earned += ModConfig.Instance.maxSkillBonusPoints;

        int spent = 0;
        var points = Manager.saves.GetSkillTalentTreesPoints(skillTreeID);
        if (points != null)
            for (int i = 0; i < points.Count; i++)
                spent += points[i];

        __result = earned - spent;
        return false;                                            // skip original
    }
}
```

With `ranksPerTalentPoint = 1`, `earned == level` (0–100). The max-skill
bonus is preserved but re-gated on the *true* max rank
(`level >= GetMaxSkillLevel`) rather than the vanilla `num >= 20` check, which
would otherwise misfire at rank 20 once the divisor changes.

### 6.2 Patch B — popup every rank

Vanilla `SaveSkillsSystem.OnUpdate` (`Pug.Other.dll`) decides, once per update
tick in which a skill's value changed, which popups to show:

```csharp
int levelFromSkill2 = SkillExtensions.GetLevelFromSkill(skillID, item.get_Item(i).Value);
bool flag = levelFromSkill2 % 5 == 0;
if (levelFromSkill != 0 || levelFromSkill2 != 3)
    Manager.main.player.SpawnSkillIncreasePopup(skillID, !flag);
if (flag)
    Manager.main.player.SpawnNewSkillPopup(skillID);   // "New talent point!" popup + bell
```

`SpawnNewSkillPopup` shows the yellow talent-point chat line, plays
`gainTalentEffect`, and plays `successTone`. The key observation:
`SpawnSkillIncreasePopup` is called on **every** rank (subject to the tutorial
guard), and its `playAudio` argument is exactly `!flag` — `false` on the
5-rank steps where vanilla *also* fires `SpawnNewSkillPopup`, `true` on every
other rank. It has a single caller (`SaveSkillsSystem.OnUpdate`), confirmed by
decompilation.

So Patch B is a **`Postfix`** on `PlayerController.SpawnSkillIncreasePopup`
that fires `SpawnNewSkillPopup` exactly on the ranks vanilla skipped — no
transpiler, no `System.Reflection.Emit`, sandbox stays on:

```csharp
[HarmonyPatch(typeof(PlayerController), nameof(PlayerController.SpawnSkillIncreasePopup))]
internal static class TalentPopupEveryRankPatch
{
    [HarmonyPostfix]
    static void Postfix(PlayerController __instance, SkillID skillID, bool playAudio)
    {
        if (!ModConfig.Instance.enabled) return;
        if (ModConfig.Instance.ranksPerTalentPoint != 1) return;  // popup-every-rank only meaningful at 1:1
        if (!playAudio) return;   // playAudio == false => vanilla 5-rank step; SpawnNewSkillPopup already fired
        __instance.SpawnNewSkillPopup(skillID);
    }
}
```

Known cosmetic edge cases (Patch A always awards the points correctly
regardless):
- **First skill-up 0→3:** the vanilla guard `if (levelFromSkill != 0 ||
  levelFromSkill2 != 3)` suppresses `SpawnSkillIncreasePopup` entirely for the
  first jump to rank 3, so the Postfix does not run and no talent popup shows
  for that one initial multi-rank jump.
- **Multi-rank jumps:** a large XP gain crossing several ranks in one tick
  produces a single popup (vanilla behavior — `OnUpdate` evaluates once per
  tick, not once per rank).
- **`ranksPerTalentPoint != 1`:** Patch B no-ops; the popup keeps its vanilla
  5-rank cadence while Patch A still changes the formula.

### 6.3 Burst

Neither target is Burst-compiled, so **no `BurstDisabler` call is needed**
(unlike the sibling durability mod):

- `SaveManager.GetAvailableTalentPoints` is a plain method on the managed
  `SaveManager` singleton.
- `PlayerController.SpawnSkillIncreasePopup` is a method on a `MonoBehaviour`;
  `MonoBehaviour` code is always managed.

The implementation should still confirm during the first build that both
patches bind (the init log lines appear).

## 7. Configuration

The sandbox forbids a runtime config file, so configuration is a set of
hardcoded constants in `ModConfig.cs`, exposed through a singleton whose shape
mirrors the sibling mod's `ModConfig`:

| Field | Default | Vanilla equiv. | Meaning |
|---|---|---|---|
| `enabled` | `true` | — | Master switch. When false, both patches early-return and vanilla behavior applies. |
| `ranksPerTalentPoint` | `1` | `5` | Ranks needed per earned talent point. Drives both patches. |
| `maxSkillBonusPoints` | `5` | `5` | Bonus points granted once at the true max rank (100). |

Both patches honor `enabled` at runtime via an early-return at the top of the
`Prefix`/`Postfix`. Patch B additionally acts only when
`ranksPerTalentPoint == 1` — "popup on every rank" is only meaningful at the
1:1 ratio. For any other `ranksPerTalentPoint` value Patch B does nothing and
the popup keeps its vanilla 5-rank cadence, while Patch A still applies the
new formula. This is documented in the README.

## 8. Build, scaffold & install

The shared SDK and machine-level setup (Unity Editor, SDK clone, Steamworks
fix, "Update Game Files") are already done for the sibling mod and do **not**
repeat. Two mod-specific items:

### 8.1 Mod scaffold — one manual Unity step

The new mod needs a scaffold inside the SDK that only the wizard generates.
The user runs it once:

1. Open `CoreKeeperModSDK/` in Unity Editor `6000.0.59f2`.
2. **PugMod → Open Mod SDK Window → Create New Mod**.
3. Mod name: `FasterTalents`.
4. The wizard creates `Assets/FasterTalents/` (folder, `.asmdef`, `Data/`,
   `.meta` files with fresh GUIDs) and `Assets/FasterTalents.asset` (the
   `ModBuilderSettings` ScriptableObject — the SDK stores the manifest data
   *inside* this asset; there is no separate `ModManifest.json` file).
5. In the Inspector of `Assets/FasterTalents.asset`, set:
   - `displayName` → `Faster Talents` (the wizard/Inspector cannot always set
     this reliably; verify it directly).
   - `requiredOn` → `ClientAndServer` (value `3`). The Editor dropdown's
     "Client and Server" choice writes `-1` ("Everything"); set `3` directly.
   - `disableHarmonyPatching` → `false`, `skipSafetyChecks` → `false`.
6. Close Unity Editor (it locks the project against batchmode builds).

After this, `scripts/link.sh` symlinks `src/` into `Assets/FasterTalents/`,
keeping a `.wizard-original` backup of the generated `.asmdef` as the sibling
mod does.

### 8.2 macOS deploy — distinct fake mod.io ID

`scripts/install-macos.sh` mirrors the sibling mod's fake-mod.io workaround
for the CrossOver/Wine long-path bug. The fake mod.io ID **must differ** from
the sibling mod's `9999999`, or the two mods collide under
`mod.io/5289/mods/<id>_1/`. This mod uses **`9999998`**.

The standard caveat applies: do not open the in-game Mods menu while a
fake-ID mod is installed — it triggers a mod.io sync that deletes the local
files. Re-run `build.sh` to restore.

### 8.3 Build pipeline

`source .envrc` then `./scripts/build.sh` — Unity batchmode build via
`-executeMethod CLIBuildHelper.Build`, then on macOS auto-runs
`install-macos.sh`. `.envrc` exports `UNITY_BIN`, `SDK_PATH`,
`MOD_INSTALL_PATH` (machine paths; `.envrc` is gitignored, `.envrc.example`
committed).

## 9. Retroactivity & disabling

- **Retroactive**: the available count is computed live from skill rank,
  nothing is stored. An existing character at e.g. Mining rank 50 immediately
  sees `50 − spent` available points the next time the talent UI queries. No
  save migration.
- **Disabling after spending**: if the mod is removed (or
  `ranksPerTalentPoint` reset to `5`) *after* the extra points were spent, the
  vanilla formula yields `earned − spent` which can go **negative**. The game
  does not crash — `SkillTalentTreeUI` already handles a negative available
  count and simply blocks further spending; already-placed talents stay
  active. This caveat is documented in the README.

## 10. Multiplayer

`requiredOn = ClientAndServer` (manifest value `3`), matching the sibling mod.
Talent spending and `GetAvailableTalentPoints` evaluate on the client against
local character data, so client-only installation would likely suffice;
`ClientAndServer` is chosen as the conservative, sibling-consistent default.
In singleplayer the player is both client and server, so this is automatically
satisfied. Whether Pugstorm enforces `requiredOn` is verified during optional
multiplayer testing.

## 11. Error handling

| Failure | Response | User-visible signal |
|---|---|---|
| Harmony cannot locate `GetAvailableTalentPoints` or `SpawnSkillIncreasePopup` (game update renamed it) | Loader logs; that patch class is skipped; rest of mod continues | log entry |
| Patch A throws at runtime | Harmony default: catch, log, fall back to original | log entry |
| Patch B throws at runtime | Harmony default: catch, log; the skill-increase popup still showed (Postfix runs after the original) | log entry |

A static constructor / init log line (`[FasterTalents] …`) marks
initialization so the game log can confirm the patches loaded.

Deliberately **not** implemented: try/catch around patch bodies (Harmony's own
catch suffices), config validation (constants are trivial), telemetry.

## 12. Test plan

All tests are manual. No automated harness.

| # | Test | Steps | Pass condition |
|---|---|---|---|
| 1 | Patches load | Build, install, launch, grep log | `[FasterTalents]` init line present |
| 2 | One point per rank | Note a skill's rank + tree's available points; gain one rank | available count increases by 1 |
| 3 | Popup every rank | Gain a single (non-multiple-of-5) rank | "New talent point available!" popup + bell fires on that rank |
| 4 | Spend works | Open the talent tree, place a point | available count decrements; talent applies |
| 5 | Retroactive | Load a character with an already-high skill | available count reflects `rank − spent`, not `floor(rank/5) − spent` |
| 6 | Max-rank bonus | Inspect a rank-100 skill's tree | available count is `100 + 5 − spent` |
| 7 | Save persistence | Spend points, save, reload | counts and placed talents consistent |
| 8 (optional) | Multiplayer | Host + client both with mod | both sides consistent |

## 13. Open questions carried into implementation

1. Exact `displayName` / `requiredOn` persistence behavior of the wizard's
   Inspector — verified by reading `Assets/FasterTalents.asset` after the
   wizard run.
2. Whether client-only installation is sufficient in multiplayer — resolved by
   optional test 8 if multiplayer is in scope.

## 14. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Game update renames `GetAvailableTalentPoints` or `SpawnSkillIncreasePopup` | Both targets documented here with decompiled reference; a missing target makes Harmony skip that one patch (logged) without breaking the other. |
| Another mod patches the same talent formula | Patch A is a full `Prefix` replacement; conflicts log clearly. Add `[HarmonyPriority(Priority.Last)]` if a conflict is observed. |
| Patch B fires a duplicate popup on 5-rank steps | The `playAudio` argument distinguishes vanilla's 5-rank steps (`false`) from other ranks (`true`); the Postfix acts only on `true`. |
| Player removes mod after spending extra points → negative count | Documented caveat; game already tolerates negative available counts. |
| Pugstorm SDK / game version drift | Shared SDK clone is pinned alongside the sibling mod; bump deliberately. |

## 15. Out-of-scope (V2+ candidates)

- Runtime-editable config (blocked by the sandbox unless `skipSafetyChecks`).
- In-game UI / hotkey to tune the ratio.
- Pet talent rate changes.
- Talent refund / tree reset.
- mod.io publication.
- Automated tests / CI.
