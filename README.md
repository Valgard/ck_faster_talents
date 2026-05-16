# Faster Talents — Core Keeper Mod

A small Core Keeper mod that grants one talent point per skill rank instead of the vanilla one per five ranks. Built on the official Pugstorm `CoreKeeperModSDK`.

## What it does

Each player skill earns talent points as it ranks up. Vanilla grants one talent point every 5 ranks; this mod grants one every rank — five times faster. The "New talent point available!" popup and bell fire on every rank to match.

The effect is **retroactive**: an existing character immediately sees the talent points it would have earned under the new rate.

**Caveat — removing the mod:** if you spend the extra points and then remove the mod, the vanilla formula yields fewer earned points than you have placed. The game tolerates this — it simply blocks further spending; already-placed talents stay active.

This mod changes only the player skill talents. Pet talents are untouched.

## Requirements

- Core Keeper (Steam, PC build)
- Pugstorm `CoreKeeperModSDK` toolchain to build (developer-side only)
- For multiplayer: install on both client and server.

## Configuration

There is no runtime `config.json` — Pugstorm's RoslynCSharp sandbox blocks file
I/O. Configuration lives in three source constants in
`unity/FasterTalents/ModConfig.cs`; edit them and rebuild to change behavior:

| Constant | Default | Vanilla | Effect |
|----------|---------|---------|--------|
| `enabled` | `true` | — | Master switch. When `false`, both patches early-return and the game behaves exactly as vanilla. |
| `ranksPerTalentPoint` | `1` | `5` | Skill ranks needed per earned talent point. Setting it to `5` restores vanilla behavior; at any value other than `1` the per-rank popup also reverts to the vanilla 5-rank cadence. |
| `maxSkillBonusPoints` | `5` | `5` | Extra talent points granted once when a skill reaches its max rank. |

## Build (developer)

See `CLAUDE.md` for the build and deploy procedure.

## License

Distribution of the compiled mod must comply with the Pugstorm Mod Tool EULA
(non-commercial only).
