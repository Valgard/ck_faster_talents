# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in
this repository.

## What this repo is

A Core Keeper mod that replaces the vanilla talent-point curve (one point per 5 skill
levels) with a two-tier curve: one point per 3 levels up to level 60, then one per 2
levels to 100 — 40 points total per skill tree. Five Harmony patches against Pugstorm's
`CoreKeeperModSDK`. Single-target, personal-use, non-commercial (Pugstorm EULA).

The parent `../CLAUDE.md` holds the mod-agnostic SDK/CrossOver guidance shared with the
sibling `disable-durability` mod.

## Build and deploy

```bash
source .envrc           # exports UNITY_BIN, SDK_PATH, MOD_INSTALL_PATH, MOD_NAME, …
../utils/build.sh      # Unity batchmode build; on Darwin auto-runs install-macos.sh
```

Unity Editor must be closed (it locks the project). `utils/link.sh` symlinks the repo's
`unity/` mirror into `$SDK_PATH/Assets/`: one **directory** symlink for
`unity/FasterTalents/`, plus three file symlinks for the Assets-level files beside it
(`FasterTalents.asset`, `.asset.meta`, `.meta`). `build.sh` invokes it idempotently on
every run, so worktree switches and repo moves self-heal.

No automated tests — verification is a manual in-game check: gain skill levels and
confirm the available talent-point count and the "new talent point" popup track the
two-tier formula (a point every 3 levels up to level 60, then every 2 to level 100).

## Architecture

Seven runtime classes in the `FasterTalents` namespace, plus the shared editor helpers
symlinked in from `../utils/`:

- **`FasterTalentsMod` (`IMod`)** — bootstrap; logs init. Calls
  `BurstDisabler.DisableBurstForSystem<AddSkillValueSystem>()` in `Init()`: the
  talent-curve patch targets are managed (no Burst), but the XP-boost patch
  (`SkillXpBoostPatch`) targets the Burst-compiled `AddSkillValueSystem`, so Burst is
  disabled for that one system. That call is followed by a **manual
  `BurstDisabler.AddWorld` pass over `World.All`**, which is what makes the boost work
  on a **dedicated server**: registering the system only arms the bypass for worlds
  `AddWorld` has already seen, and its sole caller `ECSManager.StartEcs` snapshots what
  is registered at that moment. A dedicated server runs `IMod.Init()` *after*
  `StartEcs`, so without the pass `OnUpdate` keeps running through the Burst path, the
  prefix is never reached, and the boost is silently off exactly where skill XP is
  awarded. No-op in the client ordering (the registry is a set); `EarlyInit()` is not an
  alternative — `TypeManager` is not initialised there yet. See the parent
  `../CLAUDE.md` for the full mechanism.
- **`ModConfig`** — the settings adapter. `enabled` and `xpMultiplier` are now **live
  in-game settings** driven by the Mod Settings Menu framework: `Init` registers a
  Toggle (`enabled`, default on) and a Choice (`xpMultiplier` in {1,2,3,5,10,20,50},
  default 3 — the XP-gain multiplier read by `SkillXpBoostPatch`; `1` = off, independent
  of the talent-point curve), then binds their `SettingHandle`s here (`ModConfig.Bind`)
  so the getters return the live values — a field-to-property change, so the four patch
  classes are unchanged. The talent-curve constants stay hardcoded (they *are* the mod's
  identity): `tier1MaxLevel` (60), `tier1RanksPerPoint` (3), `tier2RanksPerPoint` (2),
  `maxSkillBonusPoints` (0), plus `TalentPointsAtLevel(level)` and
  `GrantsPointAtLevel(level)` — the shared two-tier formula and its grant-level
  predicate. The framework persists the two live values (via CoreLib) to
  `mods/FasterTalents/config.cfg`; the mod's own code still touches no `System.IO` (the
  RoslynCSharp sandbox blocks it).
- **`TalentPointFormulaPatch`** — `Prefix` replacing
  `SaveManager.GetAvailableTalentPoints`; returns `ModConfig.TalentPointsAtLevel(level)`
  plus the re-gated max-rank bonus (0 by default).
- **`SkillXpBoostPatch`** — `Prefix` on the Burst-compiled
  `AddSkillValueSystem.OnUpdate` (Burst disabled in `Init()`). Every skill-XP grant
  (mining, combat, fishing, crafting, cooking, gardening, running, vitality, …) funnels
  through one ECS component, `AddSkillValueCD` (created solely by
  `PlayerController.AddSkill`, consumed by this system). The prefix queries the pending
  `AddSkillValueCD` and multiplies each `amount` by `ModConfig.xpMultiplier` *before*
  the original applies it, so a single multiplier scales **all** skills; the system's
  `level < maxLevel` guard is left intact (no-op at max level). XP grant is
  server-authoritative, so the boost applies in single-player and as host.
- **`TalentPopupOnGrantPatch`** — `Prefix`+`Postfix` on `SaveManager.SetSkillValue`;
  uses Harmony `__state` to compare the talent total before and after the change and
  fires `SpawnNewSkillPopup` once when a grant level is crossed. The companion
  **`SpawnNewSkillPopupGate`** (`Prefix` on `PlayerController.SpawnNewSkillPopup`)
  suppresses vanilla's every-5th-level popup so the popup has one formula-driven source.
- **`SkillIncreaseAudioPatch`** — `Prefix` on
  `PlayerController.SpawnSkillIncreasePopup`; recomputes the `playAudio` flag to
  `!GrantsPointAtLevel(level)`, so the per-level skill-up twinkle SFX plays on exactly
  the levels where `TalentPopupOnGrantPatch` does not fire the bell — no silent
  level-ups, no double audio.
- **Shared editor helpers** (`../utils/CLIBuildHelper.cs`, `CLIPublishHelper.cs`,
  `LocalizationGenerator.cs`, namespace `CoreKeeperModUtils`) — `CLIBuildHelper` wraps
  `ModBuilder.BuildMod` and `CLIPublishHelper` drives the mod.io publish, both for
  `unity -batchmode -executeMethod`. They are **not** vendored: `utils/link.sh` symlinks
  them into `unity/FasterTalents/Editor/`, so they compile into the editor-only
  `FasterTalents.Editor` asmdef (a combined runtime+editor asmdef cannot reference
  editor-only types). Mod identity comes from `MOD_NAME` in `.envrc`, so one source
  serves every mod. `LocalizationGenerator` templates the mod's
  `localization/localization.yaml` (EN/DE for the settings section label, the Choice
  option labels, and the section hint) into native TextDataBlock assets under
  `unity/FasterTalents/Localization/Generated/` at build, driven by
  `LOC_YAML`/`LOC_OUT`/`LOC_TABLE` in `.envrc`. The `.cs` symlinks and their
  Unity-generated `.meta` are gitignored (nothing references them by GUID).

`unity/` is the canonical source — a 1:1 mirror of the SDK's `Assets/` tree holding
**every** file the Unity Editor generates for the mod: the `.cs` sources, both `.asmdef`
files, the ModBuilderSettings `.asset`, and all `.meta` files (GUID carriers — versioned
per Unity convention). The SDK clone's `Assets/FasterTalents` is a **directory symlink**
into `unity/FasterTalents/` (created by `utils/link.sh`); because it is a directory
symlink, any file the Editor adds later is captured automatically. Edit in `unity/`; the
SDK picks up the change on the next refresh.

The runtime `FasterTalents.asmdef` starts from the SDK "Create New Mod" wizard's output
— the wizard already emits a comprehensive game-DLL reference set (`Pug.Other.dll`,
`0Harmony.dll`, `PugMod.SDK.Runtime.dll`, etc.), so no game-DLL wiring is needed — plus
two added references, `CoreLib` and `ModSettingsMenu`, for the settings integration.
**FasterTalents hard-depends on Mod Settings Menu (+ CoreLib):** both deps are declared
in the asmdef `references` *and* in the ModBuilderSettings `.asset` `dependencies:` list
(each `required: 1`), so the loader refuses to load the mod without them. It is
versioned here in `unity/FasterTalents/` like every other Editor-generated file.

Patch targets were identified by decompiling the SDK's bundled game DLLs
(`Pug.Other.dll`) with `ilspycmd`.

## macOS / CrossOver

The mod is deployed through the fake-mod.io workaround (see parent `../CLAUDE.md`). This
mod's fake mod.io ID is **`9999998`**; the siblings use distinct IDs
(`disable-durability` `9999999`, `item-checklist` `9999997`, `caveling-divining-rod`
`9999996`, `simple-crafting-pool-extender` `9999995`, `faster-pet-talents` `9999994`,
`reusable-cattle-box` `9999993`, `rebalance-key-crafting` `9999992`, `mod-settings-menu`
`9999991` — they must differ). Do not open the in-game Mods menu while installed; re-run
`../utils/build.sh` to restore if the cache is wiped.

## Publishing to mod.io

`../utils/upload.sh` publishes this mod. It runs the shared Editor class
`CoreKeeperModUtils.CLIPublishHelper.Publish` (symlinked in from `../utils/`, alongside
`CLIBuildHelper`) via Unity batchmode. The publish reads `MOD_REPO_ROOT` (set in
`.envrc`) to locate `CHANGELOG.md`.

- `Editor/FasterTalents.Editor.asmdef` references the mod.io plugin DLL
  via `overrideReferences: true` + `precompiledReferences:
  ["modio.UnityPlugin.dll"]`.
- The published version comes from the topmost `## [x.y.z]` entry of `CHANGELOG.md`;
  bump it before publishing.
- The profile logo is `unity/FasterTalents/Editor/logo.png` (readable,
  uncompressed; min 512×288).
- The real mod ID lives in `unity/FasterTalents/Editor/FasterTalents_modio.asset`.
- One-time: log in via the SDK window's "Log in" tab before the first publish.

## Conventions

- Commit messages: short imperative subject, no emoji, body wrapped ~75 chars.
- Documentation files (`CLAUDE.md`, `README.md`, `docs/`) are English; chat answers are German.
- The user prefers `git commit --amend` / `git reset --soft` over fix-up commits on a
  personal branch, and `git rebase` over `git merge`.
