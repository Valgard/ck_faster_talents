# Faster Talents

A small Core Keeper mod that replaces the vanilla talent-point curve with a faster two-tier curve — 40 talent points per maxed skill instead of vanilla's 25. Built on the official Pugstorm `CoreKeeperModSDK`.

## What it does

Each player skill earns talent points as it levels up. Vanilla grants one point every 5 skill levels, plus 5 at the max level — 25 total. This mod uses a two-tier curve: one point every 3 levels up to level 60, then one every 2 levels to level 100 — **exactly 40 points per skill tree**, enough to fully skill one talent tree. The "New talent point available!" popup and bell fire on each level that grants a point.

**Faster levelling, too:** the mod also multiplies all skill-XP gain (default **3×**), so your skills — and the talent points they unlock — arrive sooner. This is a separate effect from the talent-point curve above, toggled independently via `xpMultiplier`; it is server-authoritative, so it applies in single-player and when hosting.

The effect is **retroactive**: an existing character immediately sees the talent points it would have earned under the new rate.

**Caveat — removing the mod:** if you spend the extra points and then remove the mod, the vanilla formula yields fewer earned points than you have placed. The game tolerates this — it simply blocks further spending; already-placed talents stay active.

This mod affects player skills only — pets are untouched.

## Requirements

- Core Keeper (Steam, PC build)
- **CoreLib** and **Mod Settings Menu** — mod.io prompts you to install both
  when you subscribe; they host the in-game settings.
- For multiplayer: install on both client and server.

## Configuration

Two settings are live in-game — no config files, no rebuild. Open **Options →
Mod settings**:

- **Enabled** — master switch. When off, every patch falls through and the game
  behaves exactly as vanilla.
- **Skill-XP multiplier** — off, 2×, **3×** (default), 5×, 10×, 20×, or 50×,
  applied to every skill's earned XP. "Off" is vanilla speed, independent of the
  talent-point curve; a per-grant minimum of 1 XP is preserved.

The talent-point curve itself is fixed by four constants in a `ModConfig.cs`
source file. Changing the curve shape means editing those and rebuilding —
Pugstorm's RoslynCSharp sandbox blocks a runtime `config.json`, so there is no
file to edit at play time:

| Constant | Default | Vanilla | Effect |
|----------|---------|---------|--------|
| `tier1MaxLevel` | `60` | — | Last skill level covered by the tier-1 rate. |
| `tier1RanksPerPoint` | `3` | `5` | Skill levels needed per talent point at or below `tier1MaxLevel`. |
| `tier2RanksPerPoint` | `2` | `5` | Skill levels needed per talent point above `tier1MaxLevel`. |
| `maxSkillBonusPoints` | `0` | `5` | Extra talent points granted once when a skill reaches its max level (100). |

Setting `tier1RanksPerPoint` and `tier2RanksPerPoint` both to `5` and
`maxSkillBonusPoints` to `5` restores the vanilla curve.

## License

Personal-use, non-commercial — Pugstorm Core Keeper EULA. Built against the
official `CoreKeeperModSDK`. Source on GitHub; contributions welcome.
