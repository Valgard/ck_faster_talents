# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

A Core Keeper mod that replaces the vanilla talent-point curve (one point per 5 skill levels) with a two-tier curve: one point per 3 levels up to level 60, then one per 2 levels to 100 — 40 points total per skill tree. Four Harmony patches against Pugstorm's `CoreKeeperModSDK`. Single-target, personal-use, non-commercial (Pugstorm EULA).

The parent `../CLAUDE.md` holds the mod-agnostic SDK/CrossOver guidance shared with the sibling `disable-durability` mod.

## Build and deploy

```bash
source .envrc           # exports UNITY_BIN, SDK_PATH, MOD_INSTALL_PATH, MOD_NAME, …
../utils/build.sh      # Unity batchmode build; on Darwin auto-runs install-macos.sh
```

Unity Editor must be closed (it locks the project). `utils/link.sh` symlinks the repo's `unity/` mirror into `$SDK_PATH/Assets/`: one **directory** symlink for `unity/FasterTalents/`, plus three file symlinks for the Assets-level files beside it (`FasterTalents.asset`, `.asset.meta`, `.meta`). `build.sh` invokes it idempotently on every run, so worktree switches and repo moves self-heal.

No automated tests — verification is a manual in-game check: gain skill levels and confirm the available talent-point count and the "new talent point" popup track the two-tier formula (a point every 3 levels up to level 60, then every 2 to level 100).

## Architecture

Six runtime classes in the `FasterTalents` namespace, plus the shared editor helpers symlinked in from `../utils/`:

- **`FasterTalentsMod` (`IMod`)** — bootstrap; logs init. No `BurstDisabler` needed: none of the patch targets are Burst-compiled.
- **`ModConfig`** — hardcoded constants `enabled`, `tier1MaxLevel` (60), `tier1RanksPerPoint` (3), `tier2RanksPerPoint` (2), `maxSkillBonusPoints` (0), plus `TalentPointsAtLevel(level)` and `GrantsPointAtLevel(level)` — the shared two-tier formula and its grant-level predicate. No runtime config file — the RoslynCSharp sandbox blocks `System.IO`.
- **`TalentPointFormulaPatch`** — `Prefix` replacing `SaveManager.GetAvailableTalentPoints`; returns `ModConfig.TalentPointsAtLevel(level)` plus the re-gated max-rank bonus (0 by default).
- **`TalentPopupOnGrantPatch`** — `Prefix`+`Postfix` on `SaveManager.SetSkillValue`; uses Harmony `__state` to compare the talent total before and after the change and fires `SpawnNewSkillPopup` once when a grant level is crossed. The companion **`SpawnNewSkillPopupGate`** (`Prefix` on `PlayerController.SpawnNewSkillPopup`) suppresses vanilla's every-5th-level popup so the popup has one formula-driven source.
- **`SkillIncreaseAudioPatch`** — `Prefix` on `PlayerController.SpawnSkillIncreasePopup`; recomputes the `playAudio` flag to `!GrantsPointAtLevel(level)`, so the per-level skill-up twinkle SFX plays on exactly the levels where `TalentPopupOnGrantPatch` does not fire the bell — no silent level-ups, no double audio.
- **Shared editor helpers** (`../utils/CLIBuildHelper.cs`, `CLIPublishHelper.cs`, `LocalizationGenerator.cs`, namespace `CoreKeeperModUtils`) — `CLIBuildHelper` wraps `ModBuilder.BuildMod` and `CLIPublishHelper` drives the mod.io publish, both for `unity -batchmode -executeMethod`. They are **not** vendored: `utils/link.sh` symlinks them into `unity/FasterTalents/Editor/` when `USE_SHARED_EDITOR_HELPERS=1`, so they compile into the editor-only `FasterTalents.Editor` asmdef (a combined runtime+editor asmdef cannot reference editor-only types). Mod identity comes from `MOD_NAME` in `.envrc`, so one source serves every mod. `LocalizationGenerator` is a no-op here — FasterTalents ships no `localization.yaml`. The `.cs` symlinks and their Unity-generated `.meta` are gitignored (nothing references them by GUID).

`unity/` is the canonical source — a 1:1 mirror of the SDK's `Assets/` tree holding **every** file the Unity Editor generates for the mod: the `.cs` sources, both `.asmdef` files, the ModBuilderSettings `.asset`, and all `.meta` files (GUID carriers — versioned per Unity convention). The SDK clone's `Assets/FasterTalents` is a **directory symlink** into `unity/FasterTalents/` (created by `utils/link.sh`); because it is a directory symlink, any file the Editor adds later is captured automatically. Edit in `unity/`; the SDK picks up the change on the next refresh.

The runtime `FasterTalents.asmdef` is the SDK "Create New Mod" wizard's output used unmodified — the current wizard already emits a comprehensive game-DLL reference set (`Pug.Other.dll`, `0Harmony.dll`, `PugMod.SDK.Runtime.dll`, etc.), so no manual customization is needed. It is versioned here in `unity/FasterTalents/` like every other Editor-generated file.

Patch targets were identified by decompiling the SDK's bundled game DLLs (`Pug.Other.dll`) with `ilspycmd`.

## macOS / CrossOver

The mod is deployed through the fake-mod.io workaround (see parent `../CLAUDE.md`). This mod's fake mod.io ID is **`9999998`** (the sibling `disable-durability` uses `9999999` — they must differ). Do not open the in-game Mods menu while installed; re-run `../utils/build.sh` to restore if the cache is wiped.

## Publishing to mod.io

`../utils/upload.sh` publishes this mod. With
`USE_SHARED_EDITOR_HELPERS=1` it runs the shared Editor class
`CoreKeeperModUtils.CLIPublishHelper.Publish` (symlinked in from
`../utils/`, alongside `CLIBuildHelper`) via Unity batchmode. The
publish reads `MOD_REPO_ROOT` (set in `.envrc`) to locate `CHANGELOG.md`.

- `Editor/FasterTalents.Editor.asmdef` references the mod.io plugin DLL
  via `overrideReferences: true` + `precompiledReferences:
  ["modio.UnityPlugin.dll"]`.
- The published version comes from the topmost `## [x.y.z]` entry of
  `CHANGELOG.md`; bump it before publishing.
- The profile logo is `unity/FasterTalents/Editor/logo.png` (readable,
  uncompressed; min 512×288).
- The real mod ID lives in
  `unity/FasterTalents/Editor/FasterTalents_modio.asset`.
- One-time: log in via the SDK window's "Log in" tab before the first
  publish.

## Conventions

- Commit messages: short imperative subject, no emoji, body wrapped ~75 chars.
- Documentation files (`CLAUDE.md`, `README.md`, `docs/`) are English; chat answers are German.
- The user prefers `git commit --amend` / `git reset --soft` over fix-up commits on a personal branch, and `git rebase` over `git merge`.
