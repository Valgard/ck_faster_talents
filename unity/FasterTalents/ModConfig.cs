using ModSettingsMenu.Settings;

namespace FasterTalents
{
    /// <summary>
    /// Mod configuration adapter. `enabled` and `xpMultiplier` are now live in-game settings,
    /// read from Mod Settings Menu `SettingHandle`s (bound once in FasterTalentsMod.Init via
    /// Bind); the talent-curve params below stay hardcoded (the curve is the mod's identity).
    /// The singleton shape (ModConfig.Instance.member) let this config source drop in without
    /// touching the patch classes — they still read ModConfig.Instance.enabled / .xpMultiplier
    /// unchanged (field -> property is source-compatible).
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
        // Live handles set once by FasterTalentsMod.Init via Bind(); null only in the brief pre-Bind
        // window at mod load -> the hardcoded defaults below apply. Defensive: patches fire during
        // gameplay, strictly after Bind, and the framework is a hard dependency (never absent).
        private SettingHandle<bool> _enabledHandle;
        private SettingHandle<int> _xpHandle;

        public void Bind(SettingHandle<bool> enabled, SettingHandle<int> xp)
        {
            _enabledHandle = enabled;
            _xpHandle = xp;
        }

        // Master switch (default true). When false, all patches fall through to vanilla.
        public bool enabled => _enabledHandle != null ? _enabledHandle.Value : true;

        // Last skill level covered by the tier-1 rate.
        public int tier1MaxLevel = 60;

        // Tier 1 (levels 1..tier1MaxLevel): levels needed per talent point.
        public int tier1RanksPerPoint = 3;

        // Tier 2 (levels above tier1MaxLevel): levels needed per talent point.
        public int tier2RanksPerPoint = 2;

        // Bonus talent points granted once at the max skill level. Vanilla
        // is 5; 0 keeps the level-100 total at exactly the formula result.
        public int maxSkillBonusPoints = 0;

        // XP gain multiplier applied to every skill's earned XP. 1 = vanilla (boost off — matches
        // SkillXpBoostPatch's `mult == 1f` short-circuit). Backed by the Choice setting
        // [1,2,3,5,10,20,50]; SkillXpBoostPatch reads it each OnUpdate so menu changes apply live.
        // Default 3. The talent-point curve is independent and unaffected.
        public float xpMultiplier => _xpHandle != null ? _xpHandle.Value : 3f;

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

            return tier1MaxLevel / tier1RanksPerPoint + (level - tier1MaxLevel) / tier2RanksPerPoint;
        }

        /// <summary>
        /// True when <paramref name="level"/> is a level at which the
        /// two-tier formula grants a new talent point — the running total
        /// steps up from the previous level. Derived from TalentPointsAtLevel
        /// so the grant levels and the totals stay in lockstep.
        /// </summary>
        public bool GrantsPointAtLevel(int level)
        {
            return level > 0 && TalentPointsAtLevel(level) > TalentPointsAtLevel(level - 1);
        }
    }
}
