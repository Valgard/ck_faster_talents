# Tiered Talent-Point Formula — Design

Date: 2026-05-16

## Goal

Replace the mod's linear talent-point formula (one point per `ranksPerTalentPoint`
skill ranks) with a two-tier (piecewise) formula:

- Skill levels **1–60** grant one talent point every **3** levels → 20 points.
- Skill levels **61–100** grant one talent point every **2** levels → 20 points.
- **Total at level 100: exactly 40 points per skill tree.**

Design rationale: a full talent tree costs exactly 40 points to complete, so a
maxed skill (level 100) yields exactly enough points to fully skill one tree —
no surplus, no shortfall.

## Current behavior (baseline)

- `ModConfig` exposes `enabled`, `ranksPerTalentPoint = 1`, `maxSkillBonusPoints = 5`.
- `TalentPointFormulaPatch` (Prefix on `SaveManager.GetAvailableTalentPoints`)
  computes `earned = level / ranksPerTalentPoint`, plus `maxSkillBonusPoints` at
  the max skill level, minus points already spent.
- `TalentPopupEveryRankPatch` (Postfix on `PlayerController.SpawnSkillIncreasePopup`)
  fires `SpawnNewSkillPopup` on every rank, but only when `ranksPerTalentPoint == 1`.

## New formula

For an arbitrary skill level (the patched getter is polled continuously, so the
formula must be correct at every level, not only at 60 and 100):

```
TalentPointsAtLevel(level):
    if level <= tier1MaxLevel:
        return level / tier1RanksPerPoint
    return tier1MaxLevel / tier1RanksPerPoint
         + (level - tier1MaxLevel) / tier2RanksPerPoint
```

Checks: L60 → 20, L100 → 20 + 40/2 = 40, L75 → 20 + 15/2 = 27, L61 → 20, L62 → 21.

Integer division produces the "every 3 / every 2 levels" staircase: a point only
drops once the threshold is fully reached.

## Decisions

- **Max-rank bonus removed.** `maxSkillBonusPoints` is set to `0`, so the formula
  yields exactly 40 at level 100. The field is kept (not deleted) so the max-rank
  logic stays parameterized and vanilla is a one-line change — consistent with the
  existing "`ranksPerTalentPoint = 5` restores vanilla" convention.
- **Popup fires exactly on point-granting levels.** The "New talent point
  available!" popup, effect, and bell fire on the 40 levels that actually grant a
  point (3, 6, …, 60, 62, 64, …, 100) and on no others. Vanilla's every-5th-level
  cadence is fully suppressed.
- **Config shape: two fixed tier fields.** Not a generic tier list (over-engineered;
  no runtime config loader exists, only two zones are needed) and not a dual-mode
  flag that keeps the old linear formula (dead config path).

## Components

### Component 1 — `ModConfig` (single source of truth for the formula)

Fields:

| Field                | Value | Meaning                                         |
|----------------------|-------|-------------------------------------------------|
| `enabled`            | true  | Master switch (unchanged).                      |
| `tier1MaxLevel`      | 60    | Last level of tier 1.                           |
| `tier1RanksPerPoint` | 3     | Levels per point in tier 1.                     |
| `tier2RanksPerPoint` | 2     | Levels per point in tier 2 (above `tier1MaxLevel`). |
| `maxSkillBonusPoints`| 0     | Bonus points at the max skill level (was 5).    |

`ranksPerTalentPoint` is removed.

One method — **both patches consume it; no formula logic lives in a patch:**

- `int TalentPointsAtLevel(int level)` — the piecewise formula above. Patch A
  calls it directly for the running total; Patch B calls it twice (old level,
  new level) and uses the difference to detect crossed grant levels. A single
  shared formula keeps the total and the popup trigger guaranteed consistent if
  the curve ever changes.

### Component 2 — `TalentPointFormulaPatch` (Prefix, minimal change)

Replace `level / ranksPerTalentPoint` with `ModConfig.Instance.TalentPointsAtLevel(level)`.
The `+= maxSkillBonusPoints` branch at the max skill level stays — it is a no-op
while the value is 0. Spent-points calculation and `__result = earned - spent`
are unchanged.

### Component 3 — popup patches (`SaveManager.SetSkillValue` + `SpawnNewSkillPopup`)

The popup must fire exactly when a skill level-up crosses one or more
point-granting levels. Decompiling `SaveSkillsSystem.OnUpdate` (from
`Pug.Other.dll`) showed the vanilla popup mechanism is **per skill-change event,
not per rank**: `SpawnSkillIncreasePopup` is called once per change with the
cumulative level delta, and `SpawnNewSkillPopup` fires only when the new
level % 5 == 0. `OnUpdate` commits the change via
`Manager.saves.SetSkillValue(skillID, value)` immediately before the popup logic
— a plain `SaveManager` method (no DOTS, no Burst).

Two coordinated patches:

- **Prefix + Postfix on `SaveManager.SetSkillValue(SkillID, int)`** — the only
  place that decides whether a popup fires. The prefix reads the *old* level
  (`GetSkillValue` is not yet overwritten) into Harmony `__state`; the postfix,
  after the commit, reads the *new* level and computes
  `pointsGained = TalentPointsAtLevel(new) − TalentPointsAtLevel(old)` via the
  shared `ModConfig` formula. If `pointsGained > 0` and `Manager.main?.player` is
  non-null, it fires `SpawnNewSkillPopup` once, wrapped in a `[ThreadStatic]`
  reentrancy flag. The `Manager.main.player != null` guard is the same guard
  vanilla's `OnUpdate` uses to suppress popups during save loading.
- **Prefix on `PlayerController.SpawnNewSkillPopup`** — a pure gate: blocks every
  call *not* marked by the reentrancy flag (returns `false` to skip the
  original). This suppresses vanilla's every-5th-level calls entirely; the popup
  then has exactly one source — the `SetSkillValue` postfix.

Result: one decision point using the shared formula, correct for multi-level
jumps (old and new level both available via `__state`), with no tracking
dictionary and no load-time initialization problem. The delta form
(`pointsGained > 0`) also fires correctly when a single jump crosses two grant
levels at once.

The mod's popup patch class is renamed from `TalentPopupEveryRankPatch` to
`TalentPopupOnGrantPatch` — the old name described the now-disproven "every
rank" premise. Its doc comment is rewritten accordingly.

### Component 4 — skill-up twinkle audio (`SpawnSkillIncreasePopup`)

Added as a follow-up after code review of Component 3. Vanilla's
`SaveSkillsSystem.OnUpdate` calls `PlayerController.SpawnSkillIncreasePopup`
once per level-up event with `playAudio = (newLevel % 5 != 0)` — it mutes the
per-level "twinkle" SFX on every 5th level because the vanilla talent bell
plays there instead. Component 3 moved the bell onto the formula's grant
levels, which left the every-5th levels that are not grant levels
(5, 10, 20, 25, 35, 40, 50, 55, 65, 75, 85, 95) with the twinkle muted and no
bell — a silent level-up.

**Patch C — `SkillIncreaseAudioPatch`, a `Prefix` on `SpawnSkillIncreasePopup`**
— recomputes the `playAudio` argument (by `ref`) as `!GrantsPointAtLevel(level)`:
the twinkle plays on exactly the levels where the bell does not. One sound per
level-up — never none, never both. Like vanilla's own flag, the decision uses
only the new level, so a rare multi-level jump may briefly play both sounds — a
harmless cosmetic edge case.

`GrantsPointAtLevel(level)` is added to `ModConfig` (`level > 0 &&
TalentPointsAtLevel(level) > TalentPointsAtLevel(level - 1)`) so the grant-level
predicate stays derived from the one shared formula.

## Documentation updates

`ranksPerTalentPoint` is referenced in: `ModConfig.cs` doc comment, both patch
class doc comments, `faster-talents/CLAUDE.md`, `README.md`. All must be rewritten
for the tier model.

## Non-goals

- Runtime config file (RoslynCSharp sandbox blocks `System.IO`).
- More than two tiers / a generic tier list.
- Keeping the old linear formula as a selectable mode.

## Verification

No automated tests — manual in-game check:

1. Gain skill ranks; confirm the available talent-point count at sample levels:
   L3 → 1, L6 → 2, L60 → 20, L62 → 21, L100 → 40.
2. Confirm the "New talent point available!" popup fires on grant levels (3, 6,
   …, 62, …) and on no non-grant level (e.g. not on L5, L10, L61).
