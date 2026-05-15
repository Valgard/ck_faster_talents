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

`src/` is canonical. The SDK clone's `Assets/FasterTalents/` holds symlinks back to `src/`. The SDK's runtime `FasterTalents.asmdef` is used exactly as the "Create New Mod" wizard generated it — the current SDK wizard already emits a comprehensive game-DLL reference set (`Pug.Other.dll`, `0Harmony.dll`, `PugMod.SDK.Runtime.dll`, etc.), so no manual customization is needed. That asmdef lives only in the SDK clone, not this repo.

Patch targets were identified by decompiling the SDK's bundled game DLLs (`Pug.Other.dll`) with `ilspycmd`; the decompiled vanilla reference code is in spec §6.

## macOS / CrossOver

The mod is deployed through the fake-mod.io workaround (see parent `../CLAUDE.md`). This mod's fake mod.io ID is **`9999998`** (the sibling `disable-durability` uses `9999999` — they must differ). Do not open the in-game Mods menu while installed; re-run `./scripts/build.sh` to restore if the cache is wiped.

## Conventions

- Commit messages: short imperative subject, no emoji, body wrapped ~75 chars.
- Documentation files (`CLAUDE.md`, `README.md`, `docs/`) are English; chat answers are German.
- The user prefers `git commit --amend` / `git reset --soft` over fix-up commits on a personal branch, and `git rebase` over `git merge`.
