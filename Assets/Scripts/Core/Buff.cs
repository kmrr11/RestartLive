namespace LifeSim.Core
{
    public sealed class BuffDefinition
    {
        public string Id;
        public string Name;
        public string Text;
        /// <summary>Seasons remaining. &lt;= 0 means permanent.</summary>
        public int Seasons;
        public int Strength;
        public int Intelligence;
        public int Luck;
        public int Family;
        public float KillChance;
    }

    public sealed class BuffInstance
    {
        public string Id;
        public string Name;
        public string Text;
        public int RemainingSeasons;
        public int Strength;
        public int Intelligence;
        public int Luck;
        public int Family;
        public float KillChance;

        public bool IsPermanent => RemainingSeasons < 0;

        public static BuffInstance From(BuffDefinition def)
        {
            if (def == null)
                return null;

            bool permanent = def.Seasons <= 0;
            return new BuffInstance
            {
                Id = def.Id,
                Name = string.IsNullOrEmpty(def.Name) ? def.Id : def.Name,
                Text = def.Text ?? string.Empty,
                RemainingSeasons = permanent ? -1 : def.Seasons,
                Strength = def.Strength,
                Intelligence = def.Intelligence,
                Luck = def.Luck,
                Family = def.Family,
                KillChance = def.KillChance
            };
        }

        public string FormatLabel()
        {
            if (IsPermanent)
                return Name;
            return $"{Name}({RemainingSeasons}季)";
        }
    }
}
