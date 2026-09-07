using System.Collections.Generic;

namespace LifeSim.Core
{
    public sealed class PlayerState
    {
        public int Age { get; set; }
        public Season Season { get; set; } = Season.Spring;
        public string ActiveStoryId { get; set; }
        public string ActiveStoryStepId { get; set; }
        public HashSet<string> CompletedStories { get; } = new HashSet<string>();
        public int Strength { get; set; }

        public bool InStory => !string.IsNullOrEmpty(ActiveStoryId);
        public int Intelligence { get; set; }
        public int Luck { get; set; }
        public int Family { get; set; }
        public bool Alive { get; set; } = true;
        /// <summary>Filled when life ends so the ending panel can show why.</summary>
        public string DeathCause { get; set; }
        public HashSet<string> Tags { get; } = new HashSet<string>();
        public HashSet<string> TriggeredOnceEvents { get; } = new HashSet<string>();
        public List<string> History { get; } = new List<string>();
        public List<BuffInstance> Buffs { get; } = new List<BuffInstance>();
        /// <summary>Queued character id; next season event is forced to that confession.</summary>
        public string PendingConfessId { get; set; }
        public string PendingConfessName { get; set; }

        readonly List<string> _buffLogs = new List<string>();
        readonly Dictionary<string, int> _favors =
            new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> _favorNames =
            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

        public int GetBaseAttr(string key)
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

        public int GetAttr(string key)
        {
            return ClampAttr(GetBaseAttr(key) + GetBuffMod(key));
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
            SetAttr(key, GetBaseAttr(key) + delta);
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

        public bool HasBuff(string id)
        {
            return FindBuff(id) != null;
        }

        public BuffInstance FindBuff(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            for (int i = 0; i < Buffs.Count; i++)
            {
                if (string.Equals(Buffs[i].Id, id.Trim(), System.StringComparison.OrdinalIgnoreCase))
                    return Buffs[i];
            }

            return null;
        }

        public BuffInstance AddOrRefreshBuff(BuffDefinition def)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.Id))
                return null;

            var existing = FindBuff(def.Id);
            if (existing != null)
            {
                if (!existing.IsPermanent && def.Seasons > 0)
                    existing.RemainingSeasons = def.Seasons;
                EnqueueBuffLog($"【{existing.Name}】被重新唤起。");
                return existing;
            }

            var inst = BuffInstance.From(def);
            Buffs.Add(inst);
            string dur = inst.IsPermanent ? "永久" : $"{inst.RemainingSeasons}季";
            string extra = string.IsNullOrEmpty(inst.Text) ? string.Empty : inst.Text;
            EnqueueBuffLog($"获得buff：{inst.Name}（{dur}）{extra}");
            return inst;
        }

        public bool RemoveBuff(string id)
        {
            var existing = FindBuff(id);
            if (existing == null)
                return false;

            Buffs.Remove(existing);
            EnqueueBuffLog($"【{existing.Name}】消散了。");
            return true;
        }

        public List<BuffInstance> TickBuffs()
        {
            var expired = new List<BuffInstance>();
            for (int i = Buffs.Count - 1; i >= 0; i--)
            {
                var buff = Buffs[i];
                if (buff.IsPermanent)
                    continue;

                buff.RemainingSeasons--;
                if (buff.RemainingSeasons > 0)
                    continue;

                Buffs.RemoveAt(i);
                expired.Add(buff);
            }

            return expired;
        }

        public int GetBuffMod(string key)
        {
            int sum = 0;
            switch (NormalizeAttr(key))
            {
                case "str":
                case "strength":
                case "力量":
                    for (int i = 0; i < Buffs.Count; i++)
                        sum += Buffs[i].Strength;
                    break;
                case "int":
                case "intelligence":
                case "智力":
                    for (int i = 0; i < Buffs.Count; i++)
                        sum += Buffs[i].Intelligence;
                    break;
                case "luck":
                case "运气":
                    for (int i = 0; i < Buffs.Count; i++)
                        sum += Buffs[i].Luck;
                    break;
                case "family":
                case "家境":
                    for (int i = 0; i < Buffs.Count; i++)
                        sum += Buffs[i].Family;
                    break;
            }

            return sum;
        }

        public float GetKillChanceMod()
        {
            float sum = 0f;
            for (int i = 0; i < Buffs.Count; i++)
                sum += Buffs[i].KillChance;
            return sum;
        }

        public string FormatBuffs()
        {
            if (Buffs.Count == 0)
                return string.Empty;

            var parts = new List<string>(Buffs.Count);
            for (int i = 0; i < Buffs.Count; i++)
                parts.Add(Buffs[i].FormatLabel());
            return "buff：" + string.Join("  ", parts);
        }

        public int GetFavor(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                return 0;
            return _favors.TryGetValue(characterId.Trim(), out var value) ? value : 0;
        }

        public void EnsureFavor(string characterId, string displayName, int initial = 20)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                return;

            var id = characterId.Trim();
            if (!string.IsNullOrWhiteSpace(displayName))
                _favorNames[id] = displayName.Trim();

            if (_favors.ContainsKey(id))
                return;

            int value = ClampFavor(initial);
            _favors[id] = value;
            string name = GetFavorName(id);
            EnqueueBuffLog($"开始留意【{name}】（好感{value}）");
        }

        public void AddFavor(string characterId, int delta, string displayName = null)
        {
            if (string.IsNullOrWhiteSpace(characterId) || delta == 0)
                return;

            var id = characterId.Trim();
            if (!string.IsNullOrWhiteSpace(displayName))
                _favorNames[id] = displayName.Trim();

            int next = ClampFavor(GetFavor(id) + delta);
            _favors[id] = next;
            string sign = delta > 0 ? "+" + delta : delta.ToString();
            EnqueueBuffLog($"【{GetFavorName(id)}】好感 {sign} → {next}");
        }

        public string FormatFavors()
        {
            if (_favors.Count == 0)
                return string.Empty;

            var parts = new List<string>(_favors.Count);
            foreach (var pair in _favors)
                parts.Add($"{GetFavorName(pair.Key)}{pair.Value}");
            return "好感：" + string.Join("  ", parts);
        }

        string GetFavorName(string id)
        {
            if (_favorNames.TryGetValue(id, out var name) && !string.IsNullOrEmpty(name))
                return name;
            return id;
        }

        static int ClampFavor(int value)
        {
            if (value < 0) return 0;
            if (value > 100) return 100;
            return value;
        }

        public void EnqueueBuffLog(string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
                _buffLogs.Add(line);
        }

        public List<string> DrainBuffLogs()
        {
            if (_buffLogs.Count == 0)
                return new List<string>();

            var copy = new List<string>(_buffLogs);
            _buffLogs.Clear();
            return copy;
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
