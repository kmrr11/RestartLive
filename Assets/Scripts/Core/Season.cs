namespace LifeSim.Core
{
    public enum Season
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3
    }

    public static class SeasonUtil
    {
        public static string ToDisplay(Season season)
        {
            switch (season)
            {
                case Season.Spring: return "春";
                case Season.Summer: return "夏";
                case Season.Autumn: return "秋";
                case Season.Winter: return "冬";
                default: return "?";
            }
        }

        public static Season Next(Season season)
        {
            return season == Season.Winter ? Season.Spring : (Season)((int)season + 1);
        }

        /// <summary>
        /// Parses season tokens like 春/夏/秋/冬 or spring/summer/autumn/winter.
        /// Empty means all seasons (mask 0b1111).
        /// </summary>
        public static int ParseMask(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return 0b1111;

            int mask = 0;
            var parts = raw.Split(new[] { ';', '|', ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                switch (part.Trim().ToLowerInvariant())
                {
                    case "春":
                    case "spring":
                    case "0":
                        mask |= 1 << (int)Season.Spring;
                        break;
                    case "夏":
                    case "summer":
                    case "1":
                        mask |= 1 << (int)Season.Summer;
                        break;
                    case "秋":
                    case "autumn":
                    case "fall":
                    case "2":
                        mask |= 1 << (int)Season.Autumn;
                        break;
                    case "冬":
                    case "winter":
                    case "3":
                        mask |= 1 << (int)Season.Winter;
                        break;
                    case "*":
                    case "all":
                    case "全部":
                        return 0b1111;
                }
            }

            return mask == 0 ? 0b1111 : mask;
        }

        public static bool Allows(int seasonMask, Season season)
        {
            return (seasonMask & (1 << (int)season)) != 0;
        }
    }
}
