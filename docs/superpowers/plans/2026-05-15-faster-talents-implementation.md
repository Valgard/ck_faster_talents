# Faster Talents Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Core Keeper mod that grants one talent point per skill rank instead of the vanilla one per five ranks.

**Architecture:** Two Harmony patches, auto-discovered by Pugstorm's mod loader. Patch A is a `Prefix` that fully replaces `SaveManager.GetAvailableTalentPoints` with a `rank / ranksPerTalentPoint` formula. Patch B is a `Postfix` on `PlayerController.SpawnSkillIncreasePopup` that fires the "new talent point" popup on the ranks vanilla skips. Constants live in a `ModConfig` singleton (no runtime config file — the RoslynCSharp sandbox blocks `System.IO`).

**Tech Stack:** C# / Unity 6000.0.59f2, Pugstorm `CoreKeeperModSDK`, HarmonyLib (`0Harmony.dll`), built via Unity batchmode, deployed on macOS through the fake-mod.io CrossOver workaround.

---

## Conventions for this plan

- **Worktree:** Execution should begin in an isolated worktree created via `superpowers:using-git-worktrees`. The `faster-talents/` repo currently has one commit on `main` (the design spec). Symlinks created by `scripts/link.sh` encode absolute paths, so they self-heal after a worktree switch because `build.sh` re-runs `link.sh`.
- **No automated tests:** This is a Harmony mod with no unit-test harness, exactly like the sibling `disable-durability` mod. "Verification" means (1) the Unity batchmode build compiles, and (2) the manual in-game test plan (Task 13). Two separate C# compiles exist: the Unity build-time compile via the runtime asmdef (Task 12 catches normal errors) and the game-load-time RoslynCSharp sandbox compile (Task 13 catches sandbox violations).
- **Reference mod:** `disable-durability/` is a sibling repo with the same build pattern. Several files below are adapted from it. Its SDK runtime asmdef at `$SDK_PATH/Assets/DisableDurability/DisableDurability.asmdef` is copied verbatim in Task 3.
- **Commit style:** short imperative subject, no emoji, body wrapped ~75 chars. Set identity inline if `git` is unconfigured: `git -c user.name="Sven Pöche" -c user.email="claude@svenpoeche.de" commit`.
- **SDK-side changes are not in this repo:** Tasks 2 and 3 modify the shared `CoreKeeperModSDK/` clone. Those changes are setup artifacts and are not committed to the `faster-talents` repo. Every other task commits.
- **Paths:** `$SDK_PATH` = `/Users/valgard/Projects/private/core_keeper/CoreKeeperModSDK`. The `faster-talents` repo root is the worktree directory.

---

## Task 1: Repository skeleton

**Files:**
- Create: `.gitignore`
- Create: `.envrc.example`
- Create: `src/.gitkeep`, `src/Editor/.gitkeep`, `scripts/.gitkeep`

- [ ] **Step 1: Create `.gitignore`**

```gitignore
# macOS
.DS_Store

# Worktrees
.worktrees/

# Editors / IDEs
.idea/
.vscode/
*.swp

# Build artifacts
bin/
obj/
build/
*.dll
*.pdb

# Unity generated dirs (if ever opened locally)
Library/
Temp/
Logs/
UserSettings/
*.csproj
*.sln

# Env / secrets
.env
.envrc
.envrc.local
```

- [ ] **Step 2: Create `.envrc.example`**

```bash
# Faster Talents — environment variables
#
# Copy this file to `.envrc` and fill in your machine-specific paths.
# `.envrc` is gitignored.
#
# Optional: install direnv (`brew install direnv`) so the file auto-loads
# on `cd` into this directory.

# Path to the Unity Editor binary (must be Unity 6000.0.59f2 per the SDK)
export UNITY_BIN="/Applications/Unity/Hub/Editor/6000.0.59f2/Unity.app/Contents/MacOS/Unity"

# Path to the cloned Pugstorm CoreKeeperModSDK (shared with the sibling mod)
export SDK_PATH="/Users/valgard/Projects/private/core_keeper/CoreKeeperModSDK"

# Directory where Pugstorm's ModBuilder writes its output. The builder
# always appends a `FasterTalents/` subfolder, so the final layout is
# `$MOD_INSTALL_PATH/FasterTalents/{ModManifest.json,Scripts,Bundles}`.
#
# macOS / CrossOver: leave this at the neutral staging path below.
# Pugstorm's loader cannot extract Scripts/ from StreamingAssets under
# Wine, so `./scripts/build.sh` auto-runs `./scripts/install-macos.sh`,
# which copies from this staging dir into the mod.io load path. Set
# SKIP_MACOS_INSTALL=1 to opt out of the auto-install step.
export MOD_INSTALL_PATH="$HOME/Library/Caches/faster-talents-build/"

# macOS only — CrossOver bottle that holds Core Keeper. Override only if
# your bottle has a non-default name.
# export CK_BOTTLE_PATH="$HOME/Library/Application Support/CrossOver/Bottles/Core Keeper"
```

- [ ] **Step 3: Create the directory placeholders**

Run:
```bash
mkdir -p src/Editor scripts
touch src/.gitkeep src/Editor/.gitkeep scripts/.gitkeep
```

- [ ] **Step 4: Commit**

```bash
git add .gitignore .envrc.example src/.gitkeep src/Editor/.gitkeep scripts/.gitkeep
git -c user.name="Sven Pöche" -c user.email="claude@svenpoeche.de" commit -m "Add repository skeleton"
```

---

## Task 2: Create the SDK mod scaffold (manual Unity wizard)

This is a **manual step the user performs** — the wizard is a Unity GUI action that cannot be scripted. It generates the SDK-side mod folder and the `ModBuilderSettings` asset.

**Files (created by the wizard, in the shared SDK — not this repo):**
- `$SDK_PATH/Assets/FasterTalents/` (folder, `.asmdef`, `Data/`, `.meta` files)
- `$SDK_PATH/Assets/FasterTalents.asset` (the `ModBuilderSettings` ScriptableObject)

- [ ] **Step 1: Run the wizard**

Ask the user to:
1. Open `CoreKeeperModSDK/` in Unity Editor `6000.0.59f2`.
2. Menu: **PugMod → Open Mod SDK Window → Create New Mod**.
3. Mod name: `FasterTalents` (exact casing, no spaces).
4. Confirm. Then **close Unity Editor** (it locks the project against batchmode builds).

- [ ] **Step 2: Verify the scaffold exists**

Run:
```bash
ls -la "$SDK_PATH/Assets/FasterTalents/" "$SDK_PATH/Assets/FasterTalents.asset"
```
Expected: the folder contains `FasterTalents.asmdef` (+ `.meta`); `FasterTalents.asset` exists.

- [ ] **Step 3: Set the manifest fields in `FasterTalents.asset`**

The wizard's Inspector cannot reliably set `displayName`, and its `requiredOn`
"Client and Server" choice writes `-1` ("Everything"). Edit the asset file
directly. Open `$SDK_PATH/Assets/FasterTalents.asset` and ensure the
`metadata:` block reads (leave `guid` at whatever the wizard generated — it
must be non-empty):

```yaml
  metadata:
    guid: <wizard-generated, do not change>
    name: FasterTalents
    displayName: Faster Talents
    skipSafetyChecks: 0
    disableScripts: 0
    accessesExtraAssemblies: 1
    disableHarmonyPatching: 0
    requiredOn: 3
    files: []
    dependencies: []
```

`requiredOn: 3` = ClientAndServer. `disableHarmonyPatching: 0` is critical —
the mod's patches will not run if this is `1`.

- [ ] **Step 4: Verify**

Run:
```bash
grep -E "displayName|requiredOn|disableHarmonyPatching|skipSafetyChecks" "$SDK_PATH/Assets/FasterTalents.asset"
```
Expected: `displayName: Faster Talents`, `requiredOn: 3`, `disableHarmonyPatching: 0`, `skipSafetyChecks: 0`.

No repo commit — this is an SDK-side setup artifact.

---

## Task 3: Customize the runtime asmdef

The wizard-generated `FasterTalents.asmdef` has a minimal reference set that
will not compile patch code against the game DLLs. The sibling mod solved this
by replacing the asmdef with a comprehensive reference list. Copy it verbatim.

**Files (SDK-side, not this repo):**
- Modify: `$SDK_PATH/Assets/FasterTalents/FasterTalents.asmdef`
- Create: `$SDK_PATH/Assets/FasterTalents/FasterTalents.asmdef.wizard-original` (backup)

- [ ] **Step 1: Back up the wizard-generated asmdef**

Run:
```bash
cp "$SDK_PATH/Assets/FasterTalents/FasterTalents.asmdef" \
   "$SDK_PATH/Assets/FasterTalents/FasterTalents.asmdef.wizard-original"
```

- [ ] **Step 2: Copy the sibling's comprehensive asmdef**

Run:
```bash
cp "$SDK_PATH/Assets/DisableDurability/DisableDurability.asmdef" \
   "$SDK_PATH/Assets/FasterTalents/FasterTalents.asmdef"
```

- [ ] **Step 3: Rename the assembly inside the copied asmdef**

The copied file's `"name"` field still says `DisableDurability`. Two asmdefs
with the same assembly name break the project. Change it:

Run:
```bash
sed -i 's/"name": "DisableDurability"/"name": "FasterTalents"/' \
   "$SDK_PATH/Assets/FasterTalents/FasterTalents.asmdef"
```

- [ ] **Step 4: Verify**

Run:
```bash
grep '"name"' "$SDK_PATH/Assets/FasterTalents/FasterTalents.asmdef"
grep -c "0Harmony.dll\|Pug.Other.dll" "$SDK_PATH/Assets/FasterTalents/FasterTalents.asmdef"
```
Expected: `"name": "FasterTalents"` and a count of `2` (both `0Harmony.dll` and `Pug.Other.dll` present in `precompiledReferences`).

No repo commit — SDK-side setup artifact.

---

## Task 4: ModConfig

**Files:**
- Create: `src/ModConfig.cs`

- [ ] **Step 1: Write `src/ModConfig.cs`**

```csharp
namespace FasterTalents
{
    /// <summary>
    /// Mod configuration. Values are hardcoded constants: Pugstorm's
    /// RoslynCSharp sandbox blocks System.IO, so a runtime config.json
    /// cannot be read. The singleton shape (ModConfig.Instance.field) is
    /// kept so a future config loader could drop in without touching the
    /// patch classes. Setting ranksPerTalentPoint = 5 restores vanilla.
    /// </summary>
    internal sealed class ModConfig
    {
        // Master switch. When false, both patches early-return.
        public bool enabled = true;

        // Skill ranks needed per earned talent point. Vanilla is 5.
        public int ranksPerTalentPoint = 1;

        // Bonus talent points granted once at the max rank (100). Vanilla is 5.
        public int maxSkillBonusPoints = 5;

        private static readonly ModConfig _instance = new ModConfig();
        public static ModConfig Instance => _instance;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/ModConfig.cs
git -c user.name="Sven Pöche" -c user.email="claude@svenpoeche.de" commit -m "Add ModConfig with hardcoded talent-rate constants"
```

---

## Task 5: Mod bootstrap

**Files:**
- Create: `src/FasterTalentsMod.cs`

- [ ] **Step 1: Write `src/FasterTalentsMod.cs`**

```csharp
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
                $"ranksPerTalentPoint={ModConfig.Instance.ranksPerTalentPoint}, " +
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
```

- [ ] **Step 2: Commit**

```bash
git add src/FasterTalentsMod.cs
git -c user.name="Sven Pöche" -c user.email="claude@svenpoeche.de" commit -m "Add IMod bootstrap"
```

---

## Task 6: Patch A — talent-point formula

**Files:**
- Create: `src/TalentPointFormulaPatch.cs`

Targets the decompiled vanilla `SaveManager.GetAvailableTalentPoints` (global
namespace, `Pug.Other.dll`). All referenced game types (`SaveManager`,
`SkillExtensions`, `SkillID`, `Manager`) are in the global namespace, so the
only `using` directives needed are `HarmonyLib`, `UnityEngine`, and
`System.Collections.Generic` (for `List<int>`).

- [ ] **Step 1: Write `src/TalentPointFormulaPatch.cs`**

```csharp
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
```

- [ ] **Step 2: Commit**

```bash
git add src/TalentPointFormulaPatch.cs
git -c user.name="Sven Pöche" -c user.email="claude@svenpoeche.de" commit -m "Add Patch A: one talent point per skill rank"
```

---

## Task 7: Patch B — popup every rank

**Files:**
- Create: `src/TalentPopupEveryRankPatch.cs`

Targets `PlayerController.SpawnSkillIncreasePopup` (global namespace,
`Pug.Other.dll`). Vanilla calls it on every rank with `playAudio == !flag`,
where `flag` marks the 5-rank steps on which vanilla also fires
`SpawnNewSkillPopup`. The Postfix fires `SpawnNewSkillPopup` exactly on the
ranks vanilla skipped (`playAudio == true`).

- [ ] **Step 1: Write `src/TalentPopupEveryRankPatch.cs`**

```csharp
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
```

- [ ] **Step 2: Commit**

```bash
git add src/TalentPopupEveryRankPatch.cs
git -c user.name="Sven Pöche" -c user.email="claude@svenpoeche.de" commit -m "Add Patch B: talent-point popup on every rank"
```

---

## Task 8: Editor build helper

**Files:**
- Create: `src/Editor/CLIBuildHelper.cs`
- Create: `src/Editor/FasterTalents.Editor.asmdef`

The build helper is editor-only. It must live in its own asmdef because a
combined runtime+editor asmdef cannot reference the editor-only
`ModBuilder` / `ModBuilderSettings` types.

- [ ] **Step 1: Write `src/Editor/FasterTalents.Editor.asmdef`**

```json
{
    "name": "FasterTalents.Editor",
    "rootNamespace": "FasterTalents.Editor",
    "references": [
        "FasterTalents",
        "ModSDK.Editor",
        "PugMod.SDK"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: Write `src/Editor/CLIBuildHelper.cs`**

```csharp
using System;
using System.IO;
using PugMod;
using UnityEditor;
using UnityEngine;

namespace FasterTalents.Editor
{
    /// <summary>
    /// Editor-only helper invoked via
    /// <c>unity -batchmode -executeMethod FasterTalents.Editor.CLIBuildHelper.Build</c>.
    /// Wraps <see cref="ModBuilder.BuildMod"/> and surfaces success/failure
    /// as the Unity process exit code (0 on success, 1/2 on failure).
    /// </summary>
    public static class CLIBuildHelper
    {
        private const string ModName = "FasterTalents";
        // The "Create New Mod" wizard places ModBuilderSettings at the root
        // of Assets/, named after the mod.
        private const string SettingsPath = "Assets/" + ModName + ".asset";

        public static void Build()
        {
            try
            {
                var settings = AssetDatabase.LoadAssetAtPath<ModBuilderSettings>(SettingsPath);
                if (settings == null)
                {
                    Debug.LogError(
                        $"[CLIBuildHelper] Could not load ModBuilderSettings at {SettingsPath}");
                    EditorApplication.Exit(1);
                    return;
                }

                var exportPath = Environment.GetEnvironmentVariable("MOD_INSTALL_PATH");
                if (string.IsNullOrEmpty(exportPath))
                {
                    Debug.LogError("[CLIBuildHelper] MOD_INSTALL_PATH not set");
                    EditorApplication.Exit(1);
                    return;
                }

                Directory.CreateDirectory(exportPath);

                Debug.Log($"[CLIBuildHelper] Building {ModName} → {exportPath}");
                ModBuilder.BuildMod(settings, exportPath, ok =>
                {
                    Debug.Log($"[CLIBuildHelper] Build {(ok ? "succeeded" : "FAILED")}");
                    EditorApplication.Exit(ok ? 0 : 1);
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[CLIBuildHelper] Exception: {e}");
                EditorApplication.Exit(2);
            }
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add src/Editor/FasterTalents.Editor.asmdef src/Editor/CLIBuildHelper.cs
git -c user.name="Sven Pöche" -c user.email="claude@svenpoeche.de" commit -m "Add editor-only CLI build helper"
```

---

## Task 9: Symlink script

**Files:**
- Create: `scripts/link.sh`

- [ ] **Step 1: Write `scripts/link.sh`**

```bash
#!/usr/bin/env bash
# scripts/link.sh — Idempotently create symlinks from the SDK clone's
# Assets/FasterTalents/ folder back to this repo's src/.
#
# Required env vars (set in .envrc):
#   SDK_PATH   Path to the cloned Pugstorm CoreKeeperModSDK
#
# Preconditions:
#   - SDK_PATH/Assets/FasterTalents/ must already exist (created by the
#     PugMod → Open Mod SDK Window → "Create New Mod" wizard).
#
# Symlinks use absolute paths, so re-run after moving either repo (or after
# a worktree switch). build.sh invokes this on every run.

set -euo pipefail

: "${SDK_PATH:?must be set in .envrc}"

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SDK_MOD_DIR="$SDK_PATH/Assets/FasterTalents"

if [ ! -d "$SDK_MOD_DIR" ]; then
    echo "ERROR: SDK mod dir not found: $SDK_MOD_DIR" >&2
    echo "Create it first via PugMod → Open Mod SDK Window → Create New Mod." >&2
    exit 1
fi

cd "$SDK_MOD_DIR"
mkdir -p Editor

# -s symbolic, -f force overwrite, -n don't dereference an existing dir-link
ln -sfn "$REPO_ROOT/src/FasterTalentsMod.cs"          FasterTalentsMod.cs
ln -sfn "$REPO_ROOT/src/ModConfig.cs"                 ModConfig.cs
ln -sfn "$REPO_ROOT/src/TalentPointFormulaPatch.cs"   TalentPointFormulaPatch.cs
ln -sfn "$REPO_ROOT/src/TalentPopupEveryRankPatch.cs" TalentPopupEveryRankPatch.cs
ln -sfn "$REPO_ROOT/src/Editor/CLIBuildHelper.cs"             Editor/CLIBuildHelper.cs
ln -sfn "$REPO_ROOT/src/Editor/FasterTalents.Editor.asmdef"   Editor/FasterTalents.Editor.asmdef

echo "✓ Symlinks created in $SDK_MOD_DIR:"
ls -la FasterTalentsMod.cs ModConfig.cs TalentPointFormulaPatch.cs \
       TalentPopupEveryRankPatch.cs Editor/CLIBuildHelper.cs \
       Editor/FasterTalents.Editor.asmdef
```

- [ ] **Step 2: Make it executable and commit**

```bash
chmod +x scripts/link.sh
git add scripts/link.sh
git -c user.name="Sven Pöche" -c user.email="claude@svenpoeche.de" commit -m "Add link.sh to symlink src into the SDK clone"
```

---

## Task 10: Build script

**Files:**
- Create: `scripts/build.sh`

- [ ] **Step 1: Write `scripts/build.sh`**

```bash
#!/usr/bin/env bash
# scripts/build.sh — Build the Faster Talents mod via Unity batchmode.
#
# Required env vars (set in .envrc):
#   UNITY_BIN          Path to the Unity Editor binary (Unity 6000.0.59f2)
#   SDK_PATH           Path to the cloned Pugstorm CoreKeeperModSDK
#   MOD_INSTALL_PATH   Destination folder Pugstorm's ModBuilder writes to
#
# On macOS, this also runs scripts/install-macos.sh after the build to apply
# the CrossOver/Wine workaround. Set SKIP_MACOS_INSTALL=1 to opt out.
#
# Exit codes:
#   0  Build succeeded (and on macOS, install step also succeeded)
#   1  Env var missing or invalid path
#   2  Unity returned non-zero (build failure or Unity crash)
#   3  macOS install step failed

set -euo pipefail

: "${UNITY_BIN:?must be set in .envrc — see .envrc.example}"
: "${SDK_PATH:?must be set in .envrc}"
: "${MOD_INSTALL_PATH:?must be set in .envrc}"

if [ ! -x "$UNITY_BIN" ]; then
    echo "ERROR: \$UNITY_BIN is not executable: $UNITY_BIN" >&2
    exit 1
fi

if [ ! -d "$SDK_PATH/Assets" ]; then
    echo "ERROR: \$SDK_PATH does not look like a Unity project: $SDK_PATH" >&2
    exit 1
fi

mkdir -p "$MOD_INSTALL_PATH"

# Refresh symlinks into the SDK clone. Idempotent; self-heals after worktree
# switches or repo moves where existing symlinks would dangle.
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
"$SCRIPT_DIR/link.sh" >/dev/null

echo "Building FasterTalents mod..."
echo "  SDK:     $SDK_PATH"
echo "  Install: $MOD_INSTALL_PATH"

if "$UNITY_BIN" \
        -batchmode \
        -nographics \
        -projectPath "$SDK_PATH" \
        -executeMethod FasterTalents.Editor.CLIBuildHelper.Build \
        -logFile - \
        -quit; then
    echo "✓ Build complete."
else
    echo "✗ Build failed. Check Unity log output above for errors." >&2
    exit 2
fi

if [ "$(uname -s)" = "Darwin" ] && [ -z "${SKIP_MACOS_INSTALL:-}" ]; then
    echo
    if "$SCRIPT_DIR/install-macos.sh"; then
        echo "✓ macOS install complete. Launch Core Keeper to load."
        echo "  Reminder: do NOT open the in-game Mod menu."
    else
        echo "✗ macOS install step failed." >&2
        exit 3
    fi
else
    echo "  Restart Core Keeper to load."
fi
```

- [ ] **Step 2: Make it executable and commit**

```bash
chmod +x scripts/build.sh
git add scripts/build.sh
git -c user.name="Sven Pöche" -c user.email="claude@svenpoeche.de" commit -m "Add build.sh for Unity batchmode builds"
```

---

## Task 11: macOS install script

**Files:**
- Create: `scripts/install-macos.sh`

Adapted from the sibling mod. The only project-specific differences are the
constants block: a **distinct** fake mod.io ID (`9999998` — `9999999` is the
sibling's, and two fake mods must not collide) and the mod name strings.

- [ ] **Step 1: Write `scripts/install-macos.sh`**

```bash
#!/usr/bin/env bash
# scripts/install-macos.sh — Workaround installer for macOS / CrossOver.
#
# Pugstorm's mod loader fails to extract Scripts/ from locally built mods
# under Wine (a `\\?\C:\…` long-path bug in RemoveDirectoryRecursive). Mods
# from mod.io load via a different codepath that avoids the bug. This script
# makes a locally built mod look mod.io-installed by populating three places:
#
#   1. mod.io/<game_id>/mods/<mod_id>_<modfile_id>/   (extracted)
#   2. <Temp>/Pugstorm/Core Keeper/<game_id>/<mod_id>_<modfile_id>.zip (cache)
#   3. mod.io/<game_id>/state.json — subscribedMods + mods.<mod_id> entry
#
# Required env vars (set in .envrc):
#   MOD_INSTALL_PATH   Directory containing the built `FasterTalents/` folder.
#
# Optional env vars:
#   CK_BOTTLE_PATH     CrossOver bottle path. Defaults to the standard
#                      "Core Keeper" bottle.
#
# Idempotent — safe to re-run after each ./scripts/build.sh.
#
# IMPORTANT: after running this, launch Core Keeper but DO NOT open the
# in-game Mod menu — it triggers a mod.io API sync that deletes the cache.

set -euo pipefail

: "${MOD_INSTALL_PATH:?must be set in .envrc — see .envrc.example}"

# --- Project-specific constants ----------------------------------------------

GAME_ID="5289"             # Core Keeper's mod.io game ID.
FAKE_MOD_ID="9999998"      # Distinct from the sibling mod's 9999999.
FAKE_MODFILE_ID="1"
MOD_NAME="FasterTalents"
MOD_NAME_ID="faster-talents"
MOD_DISPLAY_NAME="Faster Talents"
MOD_SUMMARY="One talent point per skill rank instead of every five."

# --- Resolve bottle path and derive loader paths -----------------------------

CK_BOTTLE_PATH="${CK_BOTTLE_PATH:-$HOME/Library/Application Support/CrossOver/Bottles/Core Keeper}"

if [ ! -d "$CK_BOTTLE_PATH" ]; then
    echo "ERROR: CrossOver bottle not found at:" >&2
    echo "       $CK_BOTTLE_PATH" >&2
    echo "       Set CK_BOTTLE_PATH in .envrc if your bottle has a different name." >&2
    exit 1
fi

WINE_USER="crossover"

SRC="$MOD_INSTALL_PATH/$MOD_NAME"
MODIO_BASE="$CK_BOTTLE_PATH/drive_c/users/Public/mod.io/$GAME_ID"
MODIO_DST="$MODIO_BASE/mods/${FAKE_MOD_ID}_${FAKE_MODFILE_ID}"
ZIP_DIR="$CK_BOTTLE_PATH/drive_c/users/$WINE_USER/AppData/Local/Temp/Pugstorm/Core Keeper/$GAME_ID"
ZIP_DST="$ZIP_DIR/${FAKE_MOD_ID}_${FAKE_MODFILE_ID}.zip"
STATE_JSON="$MODIO_BASE/state.json"
MODLOADER_CACHE="$CK_BOTTLE_PATH/drive_c/users/$WINE_USER/AppData/Local/Temp/Pugstorm/Core Keeper/ModLoader/$MOD_NAME"

# --- Sanity check on the built mod -------------------------------------------

if [ ! -f "$SRC/ModManifest.json" ]; then
    echo "ERROR: no built mod at $SRC/ModManifest.json" >&2
    echo "       Run ./scripts/build.sh first." >&2
    exit 1
fi

echo "Installing $MOD_NAME for macOS / CrossOver…"
echo "  Source:    $SRC"
echo "  mod.io:    $MODIO_DST"
echo "  Cache zip: $ZIP_DST"

# --- 1. Copy extracted mod into mod.io path ----------------------------------

rm -rf "$MODIO_DST"
mkdir -p "$MODIO_DST"
cp -R "$SRC/ModManifest.json" "$MODIO_DST/"
[ -d "$SRC/Scripts" ] && cp -R "$SRC/Scripts" "$MODIO_DST/"
[ -d "$SRC/Bundles" ] && cp -R "$SRC/Bundles" "$MODIO_DST/"

xattr -rc "$MODIO_DST/" 2>/dev/null || true

# --- 2. Build the ZIP at the loader's expected cache path --------------------

mkdir -p "$ZIP_DIR"
rm -f "$ZIP_DST"
( cd "$SRC" && zip -qr "$ZIP_DST" Bundles Scripts ModManifest.json )

# --- 3. Patch state.json to register our fake mod ----------------------------

if [ ! -f "$STATE_JSON" ]; then
    echo "ERROR: $STATE_JSON not found. Has the game ever launched with mod.io enabled?" >&2
    exit 1
fi

[ -f "$STATE_JSON.macos-backup" ] || cp "$STATE_JSON" "$STATE_JSON.macos-backup"

USER_ID="$(jq -r '.existingUsers | keys[0]' "$STATE_JSON")"
if [ -z "$USER_ID" ] || [ "$USER_ID" = "null" ]; then
    echo "ERROR: could not find a user under existingUsers in $STATE_JSON." >&2
    exit 1
fi

jq --arg user "$USER_ID" \
   --arg modid "$FAKE_MOD_ID" \
   --argjson modidNum "$FAKE_MOD_ID" \
   --argjson modfileNum "$FAKE_MODFILE_ID" \
   --arg name "$MOD_NAME" \
   --arg nameId "$MOD_NAME_ID" \
   --arg displayName "$MOD_DISPLAY_NAME" \
   --arg summary "$MOD_SUMMARY" \
   '
   (.existingUsers[$user].subscribedMods) |=
       (if index($modid) then . else . + [$modid] end)
   |
   .mods[$modid] = {
       currentModfile: {
           id: $modfileNum,
           mod_id: $modidNum,
           version: "1.0.0",
           filename: ($name + ".zip")
       },
       modObject: {
           id: $modidNum,
           game_id: 5289,
           status: 1,
           visible: 1,
           name: $name,
           name_id: $nameId,
           summary: $summary,
           modfile: { id: $modfileNum, mod_id: $modidNum }
       }
   }
   ' "$STATE_JSON" > "$STATE_JSON.tmp"
mv "$STATE_JSON.tmp" "$STATE_JSON"

# --- 4. Clean the ModLoader cache for this mod -------------------------------

rm -rf "$MODLOADER_CACHE"

echo "✓ Install complete."
echo
echo "  Next: launch Core Keeper. Do NOT open the in-game Mod menu — that"
echo "  triggers a mod.io API sync that will delete this fake entry."
```

- [ ] **Step 2: Make it executable and commit**

```bash
chmod +x scripts/install-macos.sh
git add scripts/install-macos.sh
git -c user.name="Sven Pöche" -c user.email="claude@svenpoeche.de" commit -m "Add macOS CrossOver install workaround script"
```

---

## Task 12: First build (compile validation)

This task runs the Unity batchmode build, which compiles the runtime asmdef
(catching ordinary C# errors) and packages the mod via `ModBuilder.BuildMod`.

**Files:** none created — this validates Tasks 2–11.

- [ ] **Step 1: Create `.envrc` from the example**

Run:
```bash
cp .envrc.example .envrc
```
Then ask the user to confirm the three paths in `.envrc` (`UNITY_BIN`,
`SDK_PATH`, `MOD_INSTALL_PATH`) are correct for this machine. `SDK_PATH`
should already match the sibling mod's.

- [ ] **Step 2: Confirm Unity Editor is closed**

The Editor locks the project against batchmode builds. Run:
```bash
pgrep -fl "Unity.app/Contents/MacOS/Unity" || echo "Unity not running — OK"
```
If a Unity process is listed, ask the user to close the Editor.

- [ ] **Step 3: Run the build**

Run:
```bash
source .envrc && ./scripts/build.sh
```
Expected: `✓ Build complete.` then `✓ macOS install complete.`, exit code 0.

- [ ] **Step 4: If the build fails**

The Unity log is printed inline. Common causes and fixes:
- `CS0246: type or namespace not found` for a game type → the runtime asmdef
  is missing a reference; re-check Task 3 (the copy must have all
  `precompiledReferences`, especially `Pug.Other.dll` and `0Harmony.dll`).
- `CS0103: 'FasterTalents.Editor.CLIBuildHelper' not found` → the editor
  asmdef (Task 8) or its symlink (Task 9) is missing; re-run
  `./scripts/link.sh` and check `ls -la "$SDK_PATH/Assets/FasterTalents/Editor/"`.
- `Could not load ModBuilderSettings at Assets/FasterTalents.asset` → Task 2
  did not complete; the wizard scaffold is missing.
- Symlink shows no `->` in `link.sh` output → a `src/` file is missing or
  misnamed.

Fix the cause, re-run Step 3. If a `src/` file needed correcting, commit the
fix:
```bash
git add src/<fixed-file>
git -c user.name="Sven Pöche" -c user.email="claude@svenpoeche.de" commit -m "Fix <short description> compile error"
```

- [ ] **Step 5: Verify the build output**

Run:
```bash
ls -la "$MOD_INSTALL_PATH/FasterTalents/"
```
Expected: `ModManifest.json` and a `Scripts/` folder containing the four
runtime `.cs` files.

---

## Task 13: In-game verification (manual test plan)

Implements spec §12. The RoslynCSharp sandbox compiles the mod's `Scripts/`
at game load — this task is where a sandbox violation would surface as
`mod load error: CompileFailed`. The mod uses no `System.IO` and no
transpiler, so it should compile cleanly.

**Files:** none.

- [ ] **Step 1: Launch Core Keeper and locate the log**

Ask the user to launch Core Keeper through CrossOver (do NOT open the in-game
Mods menu). Then find the player log:
```bash
find ~/Library/Application\ Support/CrossOver/Bottles/Core\ Keeper/drive_c/users/ \
     -name "Player.log" -path "*Pugstorm*" 2>/dev/null
```

- [ ] **Step 2: Verify the mod loaded (Test 1)**

Run (substitute the path from Step 1):
```bash
grep -E "FasterTalents|CompileFailed" "<player-log-path>"
```
Expected: `[FasterTalents] Mod initialized. enabled=True, ranksPerTalentPoint=1, maxSkillBonusPoints=5`,
plus the two patch-loaded lines. **No `CompileFailed`.** If `CompileFailed`
appears, the log names the offending file/line — fix and rebuild (Task 12).

- [ ] **Step 3: One point per rank (Test 2)**

In a world: open a skill's talent tree, note the available-points number.
Gain one rank in that skill (e.g. mine a few blocks for Mining). Re-open the
tree.
Pass: the available count increased by exactly 1.

- [ ] **Step 4: Popup every rank (Test 3)**

Gain a single rank that is not a multiple of 5.
Pass: the yellow "New talent point available!" chat line, the effect, and the
bell fire on that rank.

- [ ] **Step 5: Spend works (Test 4)**

Open the talent tree, place a talent point.
Pass: the available count decrements; the talent's effect applies.

- [ ] **Step 6: Retroactive (Test 5)**

Load a character that already has a high skill rank from before the mod.
Pass: the available count is `rank − spent` (e.g. rank 40 → ~40 available),
not the vanilla `floor(rank/5) − spent`.

- [ ] **Step 7: Max-rank bonus (Test 6)**

Inspect a rank-100 skill's talent tree (if available).
Pass: available count is `100 + 5 − spent` (105 minus spent).

- [ ] **Step 8: Save persistence (Test 7)**

Spend points, save and quit, reload the character.
Pass: counts and placed talents are consistent across the reload.

- [ ] **Step 9: Record results**

If any test fails, debug via the player log, fix in `src/`, rebuild (Task 12),
re-test. Commit fixes as separate commits. If all pass, proceed to Task 14.

---

## Task 14: Documentation

**Files:**
- Create: `README.md`
- Create: `CLAUDE.md`

- [ ] **Step 1: Write `README.md`**

```markdown
# Faster Talents — Core Keeper Mod

A small Core Keeper mod that grants one talent point per skill rank instead of the vanilla one per five ranks. Built on the official Pugstorm `CoreKeeperModSDK`.

## What it does

Each of the twelve player skills earns talent points as it ranks up. Vanilla grants one talent point every 5 ranks; this mod grants one every rank — five times faster. The "New talent point available!" popup and bell fire on every rank to match.

The effect is **retroactive**: an existing character immediately sees the talent points it would have earned under the new rate.

**Caveat — removing the mod:** if you spend the extra points and then remove the mod, the vanilla formula yields fewer earned points than you have placed. The game tolerates this — it simply blocks further spending; already-placed talents stay active.

This mod changes only the player skill talents. Pet talents are untouched.

## Requirements

- Core Keeper (Steam, PC build)
- Pugstorm `CoreKeeperModSDK` toolchain to build (developer-side only)
- For multiplayer: install on both client and server.

## Configuration

The rank-to-point ratio is a source constant in `src/ModConfig.cs`
(`ranksPerTalentPoint`, default `1`; vanilla is `5`). Pugstorm's RoslynCSharp
sandbox blocks runtime file I/O, so there is no `config.json` — change the
constant and rebuild. Setting `ranksPerTalentPoint = 5` restores vanilla
behavior; at any value other than `1` the per-rank popup also reverts to the
vanilla 5-rank cadence.

## Build (developer)

See `docs/superpowers/specs/2026-05-15-faster-talents-design.md` §8 and
`docs/superpowers/plans/2026-05-15-faster-talents-implementation.md`.

## License

Distribution of the compiled mod must comply with the Pugstorm Mod Tool EULA
(non-commercial only).
```

- [ ] **Step 2: Write `CLAUDE.md`**

```markdown
# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

A Core Keeper mod that grants one talent point per skill rank instead of the vanilla one per five ranks. Two Harmony patches against Pugstorm's `CoreKeeperModSDK`. Single-target, personal-use, non-commercial (Pugstorm EULA).

The design spec is `docs/superpowers/specs/2026-05-15-faster-talents-design.md` and the implementation plan is `docs/superpowers/plans/2026-05-15-faster-talents-implementation.md`. Read those before changing scope, patch targets, or the build pipeline. The parent `../CLAUDE.md` holds the mod-agnostic SDK/CrossOver guidance shared with the sibling `disable-durability` mod.

## Build and deploy

```bash
source .envrc           # exports UNITY_BIN, SDK_PATH, MOD_INSTALL_PATH
./scripts/build.sh      # Unity batchmode build; on Darwin auto-runs install-macos.sh
```

Unity Editor must be closed (it locks the project). `scripts/link.sh` symlinks `src/` into `$SDK_PATH/Assets/FasterTalents/`; `build.sh` invokes it idempotently so worktree switches self-heal.

No automated tests. Verification is the manual test plan in the spec §12 / plan Task 13.

## Architecture

Four runtime classes plus one editor helper, all in the `FasterTalents` namespace:

- **`FasterTalentsMod` (`IMod`)** — bootstrap; logs init. No `BurstDisabler` needed: neither patch target is Burst-compiled.
- **`ModConfig`** — hardcoded constants `enabled`, `ranksPerTalentPoint` (default 1), `maxSkillBonusPoints` (default 5). No runtime config file — the RoslynCSharp sandbox blocks `System.IO`.
- **`TalentPointFormulaPatch`** — `Prefix` replacing `SaveManager.GetAvailableTalentPoints`; computes `rank / ranksPerTalentPoint` plus the re-gated max-rank bonus.
- **`TalentPopupEveryRankPatch`** — `Postfix` on `PlayerController.SpawnSkillIncreasePopup`; fires `SpawnNewSkillPopup` on the ranks vanilla skips. Only acts when `ranksPerTalentPoint == 1`.
- **`Editor/CLIBuildHelper`** — wraps `ModBuilder.BuildMod` for `unity -batchmode -executeMethod`. Own asmdef (`src/Editor/FasterTalents.Editor.asmdef`) because editor-only types cannot be referenced from a combined asmdef.

`src/` is canonical. The SDK clone's `Assets/FasterTalents/` holds symlinks back to `src/`. The SDK's runtime `FasterTalents.asmdef` is customized in place (copied from the sibling mod for the full game-DLL reference set) with a `.wizard-original` backup — this customization lives only in the SDK clone, not this repo.

Patch targets were identified by decompiling the SDK's bundled game DLLs (`Pug.Other.dll`) with `ilspycmd`; the decompiled vanilla reference code is in spec §6.

## macOS / CrossOver

The mod is deployed through the fake-mod.io workaround (see parent `../CLAUDE.md`). This mod's fake mod.io ID is **`9999998`** (the sibling `disable-durability` uses `9999999` — they must differ). Do not open the in-game Mods menu while installed; re-run `./scripts/build.sh` to restore if the cache is wiped.

## Conventions

- Commit messages: short imperative subject, no emoji, body wrapped ~75 chars.
- Documentation files (`CLAUDE.md`, `README.md`, `docs/`) are English; chat answers are German.
- The user prefers `git commit --amend` / `git reset --soft` over fix-up commits on a personal branch, and `git rebase` over `git merge`.
```

- [ ] **Step 3: Commit**

```bash
git add README.md CLAUDE.md
git -c user.name="Sven Pöche" -c user.email="claude@svenpoeche.de" commit -m "Add README and mod-specific CLAUDE.md"
```

---

## Task 15: Finalize the development branch

**Files:** none.

- [ ] **Step 1: Confirm all work is committed**

Run:
```bash
git status --short
git log --oneline main..HEAD
```
Expected: clean working tree; the commit list shows Tasks 1, 4–11, 14 (plus any Task 12 fix commits).

- [ ] **Step 2: Integrate the worktree branch**

Use the `superpowers:finishing-a-development-branch` skill to decide how to
integrate. The user's global preference: fast-forward / rebase the worktree
branch into `main` (linear history), then remove the worktree. Per the parent
`../CLAUDE.md`, change the Bash working directory out of the worktree
**before** `git worktree remove`.

- [ ] **Step 3: Final state check**

Run:
```bash
git -C /Users/valgard/Projects/private/core_keeper/faster-talents log --oneline
```
Expected: spec commit, then all implementation commits, on `main`.

---

## Self-review notes

- **Spec coverage:** Goal 1 (one point per rank) → Task 6. Goal 2 (adjustable constant) → Task 4 (`ranksPerTalentPoint`). Goal 3 (popup every rank) → Task 7. Goal 4 (retroactive) → inherent in Patch A's live computation, verified Task 13 Step 6. Goal 5 (minimal patches) → Tasks 6–7. Goal 6 (official pipeline) → Tasks 2–3, 8–11. Spec §8 build/scaffold → Tasks 2, 3, 9–12. Spec §12 test plan → Task 13.
- **Burst:** spec §6.3 — no `BurstDisabler`; reflected in Task 5's bootstrap (no such call) and the class comments.
- **Sandbox:** spec §4 — no `System.IO`, no transpiler in runtime code; Tasks 6–7 use only `HarmonyLib` + `System.Collections.Generic` + game types. `skipSafetyChecks` stays `0` (Task 2 Step 3).
- **Distinct fake ID:** spec §8.2 — `9999998` in Task 11, called out vs the sibling's `9999999`.
