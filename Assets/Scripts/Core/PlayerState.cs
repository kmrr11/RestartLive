using System.Collections.Generic;

namespace LifeSim.Core
{
    public sealed class PlayerState
    {
        public int Age { get; set; }
        public int Strength { get; set; }
        public int Intelligence { get; set; }
        public int Luck { get; set; }
        public int Family { get; set; }
        public bool Alive { get; set; } = true;
        public HashSet<string> Tags { get; } = new HashSet<string>();
        public HashSet<string> TriggeredOnceEvents { get; } = new HashSet<string>();
        public List<string> History { get; } = new List<string>();

        public int GetAttr(string key)
        {
            switch (NormalizeAttr(key))
            {
                case "str":
                case "strength":
                case "力量":
                    return Strength;
                case "int":
                case "intelligence":
                case "智力":
                    return Intelligence;
                case "luck":
                case "运气":
                    return Luck;
                case "family":
                case "家境":
                    return Family;
                default:
                    return 0;
            }
        }

        public void SetAttr(string key, int value)
        {
            value = ClampAttr(value);
            switch (NormalizeAttr(key))
            {
                case "str":
                case "strength":
                case "力量":
                    Strength = value;
                    break;
                case "int":
                case "intelligence":
                case "智力":
                    Intelligence = value;
                    break;
                case "luck":
                case "运气":
                    Luck = value;
                    break;
                case "family":
                case "家境":
                    Family = value;
                    break;
            }
        }

        public void AddAttr(string key, int delta)
        {
            SetAttr(key, GetAttr(key) + delta);
        }

        public void AddTag(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag))
                Tags.Add(tag.Trim());
        }

        public bool HasTag(string tag)
        {
            return !string.IsNullOrWhiteSpace(tag) && Tags.Contains(tag.Trim());
        }

        public void AppendHistory(string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
                History.Add(line);
        }

        static string NormalizeAttr(string key)
        {
            return (key ?? string.Empty).Trim().ToLowerInvariant();
        }

        static int ClampAttr(int value)
        {
            if (value < 0) return 0;
            if (value > 20) return 20;
            return value;
        }
    }
}
