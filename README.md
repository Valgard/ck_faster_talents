# Faster Talents — Core Keeper Mod

A small Core Keeper mod that replaces the vanilla talent-point curve with a faster two-tier curve — 40 talent points per maxed skill instead of vanilla's 25. Built on the official Pugstorm `CoreKeeperModSDK`.

## What it does

Each player skill earns talent points as it levels up. Vanilla grants one point every 5 skill levels, plus 5 at the max level — 25 total. This mod uses a two-tier curve: one point every 3 levels up to level 60, then one every 2 levels to level 100 — **exactly 40 points per skill tree**, enough to fully skill one talent tree. The "New talent point available!" popup and bell fire on each level that grants a point.

The effect is **retroactive**: an existing character immediately sees the talent points it would have earned under the new rate.

**Caveat — removing the mod:** if you spend the extra points and then remove the mod, the vanilla formula yields fewer earned points than you have placed. The game tolerates this — it simply blocks further spending; already-placed talents stay active.

This mod changes only the player skill talents. Pet talents are untouched.

## Requirements

- Core Keeper (Steam, PC build)
- Pugstorm `CoreKeeperModSDK` toolchain to build (developer-side only)
- For multiplayer: install on both client and server.

## Configuration

There is no runtime `config.json` — Pugstorm's RoslynCSharp sandbox blocks file
I/O. Configuration lives in five source constants in
`unity/FasterTalents/ModConfig.cs`; edit them and rebuild to change behavior:

| Constant | Default | Vanilla | Effect |
|----------|---------|---------|--------|
| `enabled` | `true` | — | Master switch. When `false`, all patches fall through and the game behaves exactly as vanilla. |
| `tier1MaxLevel` | `60` | — | Last skill level covered by the tier-1 rate. |
| `tier1RanksPerPoint` | `3` | `5` | Skill levels needed per talent point at or below `tier1MaxLevel`. |
| `tier2RanksPerPoint` | `2` | `5` | Skill levels needed per talent point above `tier1MaxLevel`. |
| `maxSkillBonusPoints` | `0` | `5` | Extra talent points granted once when a skill reaches its max level (100). |

Setting `tier1RanksPerPoint` and `tier2RanksPerPoint` both to `5` and
`maxSkillBonusPoints` to `5` restores the vanilla curve.

## Build (developer)

See `CLAUDE.md` for the build and deploy procedure.

## Publishing

Publish a new version to mod.io:

1. Bump the topmost `## [x.y.z]` entry in `CHANGELOG.md`.
2. One-time only: open the Pugstorm Mod SDK window in Unity and log in via
   the "Log in" tab.
3. Close the Unity Editor, then run:

   ```bash
   source .envrc
   ../utils/upload.sh            # or: ../utils/upload.sh --dry-run
   ```

A newly created mod.io profile is hidden — open its profile page and set it
visible once you have reviewed it.

## License

Distribution of the compiled mod must comply with the Pugstorm Mod Tool EULA
(non-commercial only).
