namespace FasterTalents
{
    /// <summary>
    /// Mod configuration. Values are hardcoded constants: Pugstorm's
    /// RoslynCSharp sandbox blocks System.IO, so a runtime config.json
    /// cannot be read. The singleton shape (ModConfig.Instance.field) is
    /// kept so a future config loader could drop in without touching the
    /// patch classes. Setting ranksPerTalentPoint = 5 restores vanilla.
    /// </summary>
    internal sealed class ModConfig
    {
        // Master switch. When false, both patches early-return.
        public bool enabled = true;

        // Skill ranks needed per earned talent point. Vanilla is 5.
        public int ranksPerTalentPoint = 1;

        // Bonus talent points granted once at the max rank (100). Vanilla is 5.
        public int maxSkillBonusPoints = 5;

        private static readonly ModConfig _instance = new ModConfig();
        public static ModConfig Instance => _instance;
    }
}
