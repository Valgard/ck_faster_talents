# Changelog

All notable changes to this mod are documented in this file. The format is
loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
without strict adherence — entries describe what shipped per release, not
every commit. The topmost `## [x.y.z]` entry is the version `upload.sh`
publishes.

## [1.3.0]

- **In-game settings** under Options -> Mod Settings (via the Mod Settings Menu
  framework): switch the whole mod on/off and pick the skill-XP multiplier
  (off, 2x, 3x, 5x, 10x, 20x, 50x) live.
- Now requires the **Mod Settings Menu** mod and CoreLib, which host the settings.

## [1.2.0]

- Added an XP-gain multiplier (default 3×) applied to every skill, so skill
  levels — and the talent points they unlock — are reached faster. Set
  `xpMultiplier` in ModConfig to 1.0 to disable. The talent-point curve is
  unchanged and each skill's max level is still respected.

## [1.1.0]

- Replaced the flat one-point-per-rank rate with a two-tier talent-point
  curve: one point per 3 skill levels up to level 60, then one per 2 to
  level 100 — exactly 40 points per skill tree.
- The talent-point popup is now formula-driven; vanilla's every-5th-level
  popup is suppressed so there is a single source.
- Skill-up twinkle SFX re-timed to the talent formula: the per-level
  twinkle now plays on exactly the levels that do not grant a talent
  point, so no level-up is silent and the talent bell never doubles up.

## [1.0.0]

- Initial release: talent points granted one per skill rank.
- "New talent point available!" popup fires on every rank that grants a
  point.
