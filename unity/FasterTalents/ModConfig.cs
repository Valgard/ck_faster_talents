namespace FasterTalents
{
    /// <summary>
    /// Mod configuration. Values are hardcoded constants: Pugstorm's
    /// RoslynCSharp sandbox blocks System.IO, so a runtime config.json
    /// cannot be read. The singleton shape (ModConfig.Instance.field) is
    /// kept so a future config loader could drop in without touching the
    /// patch classes.
    ///
    /// The talent-point curve is a two-tier piecewise formula: one point
    /// per `tier1RanksPerPoint` levels up to `tier1MaxLevel`, then one per
    /// `tier2RanksPerPoint` levels above it. With the defaults a level-100
    /// skill yields exactly 40 points — enough to fully skill one talent
    /// tree. Set `enabled = false` for a clean revert to vanilla behavior;
    /// `tier1RanksPerPoint = tier2RanksPerPoint = 5` with
    /// `maxSkillBonusPoints = 5` reproduces the vanilla point total but
    /// still routes the popup through this mod's formula path.
    /// </summary>
    internal sealed class ModConfig
    {
        // Master switch. When false, all patches fall through to vanilla.
        public bool enabled = true;

        // Last skill level covered by the tier-1 rate.
        public int tier1MaxLevel = 60;

        // Tier 1 (levels 1..tier1MaxLevel): levels needed per talent point.
        public int tier1RanksPerPoint = 3;

        // Tier 2 (levels above tier1MaxLevel): levels needed per talent point.
        public int tier2RanksPerPoint = 2;

        // Bonus talent points granted once at the max skill level. Vanilla
        // is 5; 0 keeps the level-100 total at exactly the formula result.
        public int maxSkillBonusPoints = 0;

        // XP gain multiplier applied to every skill's earned XP. 1.0 = vanilla
        // (boost off). Float so fractional rates (e.g. 2.5) work; a per-grant
        // minimum of 1 XP is preserved. SkillXpBoostPatch reads this. The
        // talent-point curve above is independent and unaffected.
        public float xpMultiplier = 3f;

        private static readonly ModConfig _instance = new ModConfig();
        public static ModConfig Instance => _instance;

        /// <summary>
        /// Total talent points earned at a given skill level under the
        /// two-tier formula. Shared by both patches so the running total
        /// (Patch A) and the popup trigger (Patch B) can never diverge.
        /// Integer division produces the level staircase; both
        /// tier*RanksPerPoint divisors must be > 0.
        /// </summary>
        public int TalentPointsAtLevel(int level)
        {
            if (level <= tier1MaxLevel)
                return level / tier1RanksPerPoint;

            return tier1MaxLevel / tier1RanksPerPoint
                 + (level - tier1MaxLevel) / tier2RanksPerPoint;
        }

        /// <summary>
        /// True when <paramref name="level"/> is a level at which the
        /// two-tier formula grants a new talent point — the running total
        /// steps up from the previous level. Derived from TalentPointsAtLevel
        /// so the grant levels and the totals stay in lockstep.
        /// </summary>
        public bool GrantsPointAtLevel(int level)
        {
            return level > 0
                && TalentPointsAtLevel(level) > TalentPointsAtLevel(level - 1);
        }
    }
}
