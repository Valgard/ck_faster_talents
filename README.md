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

The rank-to-point ratio is a source constant in `unity/FasterTalents/ModConfig.cs`
(`ranksPerTalentPoint`, default `1`; vanilla is `5`). Pugstorm's RoslynCSharp
sandbox blocks runtime file I/O, so there is no `config.json` — change the
constant and rebuild. Setting `ranksPerTalentPoint = 5` restores vanilla
behavior; at any value other than `1` the per-rank popup also reverts to the
vanilla 5-rank cadence.

## Build (developer)

See `CLAUDE.md` for the build and deploy procedure.

## License

Distribution of the compiled mod must comply with the Pugstorm Mod Tool EULA
(non-commercial only).
