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
