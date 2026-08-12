using System;
using System.Collections.Generic;
using UnityEngine;

namespace LifeSim.Data
{
    public sealed class EventDatabase
    {
        public IReadOnlyList<EventDefinition> Events => _events;
        public IReadOnlyDictionary<string, BranchDefinition> Branches => _branches;

        readonly List<EventDefinition> _events = new List<EventDefinition>();
        readonly Dictionary<string, BranchDefinition> _branches =
            new Dictionary<string, BranchDefinition>(StringComparer.OrdinalIgnoreCase);

        public void Load(TextAsset eventsCsv, TextAsset branchesCsv)
        {
            _events.Clear();
            _branches.Clear();

            foreach (var row in CsvLoader.LoadTable(eventsCsv))
            {
                var id = CsvLoader.Get(row, "id");
                if (string.IsNullOrEmpty(id))
                    continue;

                var def = new EventDefinition
                {
                    Id = id,
                    AgeMin = CsvLoader.GetInt(row, "ageMin"),
                    AgeMax = CsvLoader.GetInt(row, "ageMax", 999),
                    Weight = Mathf.Max(1, CsvLoader.GetInt(row, "weight", 1)),
                    Text = CsvLoader.Get(row, "text"),
                    Require = CsvLoader.Get(row, "require"),
                    ExcludeTags = CsvLoader.Get(row, "excludeTags"),
                    AddTags = CsvLoader.Get(row, "addTags"),
                    Effects = CsvLoader.Get(row, "effects"),
                    KillChance = CsvLoader.GetFloat(row, "killChance"),
                    Once = CsvLoader.GetBool(row, "once"),
                    Tags = CsvLoader.Get(row, "tags")
                };

                var choices = CsvLoader.Get(row, "choices");
                if (!string.IsNullOrEmpty(choices))
                {
                    foreach (var c in choices.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = c.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                            def.ChoiceIds.Add(trimmed);
                    }
                }

                _events.Add(def);
            }

            if (branchesCsv != null)
            {
                foreach (var row in CsvLoader.LoadTable(branchesCsv))
                {
                    var choiceId = CsvLoader.Get(row, "choiceId");
                    if (string.IsNullOrEmpty(choiceId))
                        continue;

                    _branches[choiceId] = new BranchDefinition
                    {
                        ChoiceId = choiceId,
                        EventId = CsvLoader.Get(row, "eventId"),
                        Label = CsvLoader.Get(row, "label"),
                        Check = CsvLoader.Get(row, "check"),
                        SuccessText = CsvLoader.Get(row, "successText"),
                        FailText = CsvLoader.Get(row, "failText"),
                        SuccessEffects = CsvLoader.Get(row, "successEffects"),
                        FailEffects = CsvLoader.Get(row, "failEffects")
                    };
                }
            }

            Debug.Log($"[LifeSim] Loaded {_events.Count} events, {_branches.Count} branches.");
        }

        public bool TryGetBranch(string choiceId, out BranchDefinition branch)
        {
            return _branches.TryGetValue(choiceId, out branch);
        }

        public List<BranchDefinition> GetBranchesForEvent(EventDefinition evt)
        {
            var list = new List<BranchDefinition>();
            if (evt == null)
                return list;

            foreach (var id in evt.ChoiceIds)
            {
                if (_branches.TryGetValue(id, out var branch))
                    list.Add(branch);
            }

            return list;
        }
    }
}
