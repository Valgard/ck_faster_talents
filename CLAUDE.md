# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

A Core Keeper mod that grants one talent point per skill rank instead of the vanilla one per five ranks. Two Harmony patches against Pugstorm's `CoreKeeperModSDK`. Single-target, personal-use, non-commercial (Pugstorm EULA).

The parent `../CLAUDE.md` holds the mod-agnostic SDK/CrossOver guidance shared with the sibling `disable-durability` mod.

## Build and deploy

```bash
source .envrc           # exports UNITY_BIN, SDK_PATH, MOD_INSTALL_PATH, MOD_NAME, …
../utils/build.sh      # Unity batchmode build; on Darwin auto-runs install-macos.sh
```

Unity Editor must be closed (it locks the project). `utils/link.sh` symlinks the repo's `unity/` mirror into `$SDK_PATH/Assets/`: one **directory** symlink for `unity/FasterTalents/`, plus three file symlinks for the Assets-level files beside it (`FasterTalents.asset`, `.asset.meta`, `.meta`). `build.sh` invokes it idempotently on every run, so worktree switches and repo moves self-heal.

No automated tests — verification is a manual in-game check: gain a skill rank, confirm the available talent-point count rises by one and the "new talent point" popup fires.

## Architecture

Four runtime classes plus one editor helper, all in the `FasterTalents` namespace:

- **`FasterTalentsMod` (`IMod`)** — bootstrap; logs init. No `BurstDisabler` needed: neither patch target is Burst-compiled.
- **`ModConfig`** — hardcoded constants `enabled`, `ranksPerTalentPoint` (default 1), `maxSkillBonusPoints` (default 5). No runtime config file — the RoslynCSharp sandbox blocks `System.IO`.
- **`TalentPointFormulaPatch`** — `Prefix` replacing `SaveManager.GetAvailableTalentPoints`; computes `rank / ranksPerTalentPoint` plus the re-gated max-rank bonus.
- **`TalentPopupEveryRankPatch`** — `Postfix` on `PlayerController.SpawnSkillIncreasePopup`; fires `SpawnNewSkillPopup` on the ranks vanilla skips. Only acts when `ranksPerTalentPoint == 1`.
- **`Editor/CLIBuildHelper`** — wraps `ModBuilder.BuildMod` for `unity -batchmode -executeMethod`. Own asmdef (`unity/FasterTalents/Editor/FasterTalents.Editor.asmdef`) because editor-only types cannot be referenced from a combined asmdef.

`unity/` is the canonical source — a 1:1 mirror of the SDK's `Assets/` tree holding **every** file the Unity Editor generates for the mod: the `.cs` sources, both `.asmdef` files, the ModBuilderSettings `.asset`, and all `.meta` files (GUID carriers — versioned per Unity convention). The SDK clone's `Assets/FasterTalents` is a **directory symlink** into `unity/FasterTalents/` (created by `utils/link.sh`); because it is a directory symlink, any file the Editor adds later is captured automatically. Edit in `unity/`; the SDK picks up the change on the next refresh.

The runtime `FasterTalents.asmdef` is the SDK "Create New Mod" wizard's output used unmodified — the current wizard already emits a comprehensive game-DLL reference set (`Pug.Other.dll`, `0Harmony.dll`, `PugMod.SDK.Runtime.dll`, etc.), so no manual customization is needed. It is versioned here in `unity/FasterTalents/` like every other Editor-generated file.

Patch targets were identified by decompiling the SDK's bundled game DLLs (`Pug.Other.dll`) with `ilspycmd`.

## macOS / CrossOver

The mod is deployed through the fake-mod.io workaround (see parent `../CLAUDE.md`). This mod's fake mod.io ID is **`9999998`** (the sibling `disable-durability` uses `9999999` — they must differ). Do not open the in-game Mods menu while installed; re-run `../utils/build.sh` to restore if the cache is wiped.

## Conventions

- Commit messages: short imperative subject, no emoji, body wrapped ~75 chars.
- Documentation files (`CLAUDE.md`, `README.md`, `docs/`) are English; chat answers are German.
- The user prefers `git commit --amend` / `git reset --soft` over fix-up commits on a personal branch, and `git rebase` over `git merge`.
