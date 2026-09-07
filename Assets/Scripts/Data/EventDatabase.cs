using System;
using System.Collections.Generic;
using LifeSim.Core;
using UnityEngine;

namespace LifeSim.Data
{
    public sealed class EventDatabase
    {
        public IReadOnlyList<EventDefinition> Events => _events;
        public IReadOnlyList<CharacterDefinition> Characters => _characters;
        public IReadOnlyDictionary<string, BranchDefinition> Branches => _branches;
        public IReadOnlyDictionary<string, StoryDefinition> Stories => _stories;
        public IReadOnlyDictionary<string, BuffDefinition> Buffs => _buffs;

        readonly List<EventDefinition> _events = new List<EventDefinition>();
        readonly Dictionary<string, EventDefinition> _eventsById =
            new Dictionary<string, EventDefinition>(StringComparer.OrdinalIgnoreCase);
        readonly List<CharacterDefinition> _characters = new List<CharacterDefinition>();
        readonly Dictionary<string, CharacterDefinition> _charactersById =
            new Dictionary<string, CharacterDefinition>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, CharacterDefinition> _charactersByMeetTag =
            new Dictionary<string, CharacterDefinition>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, BranchDefinition> _branches =
            new Dictionary<string, BranchDefinition>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, StoryDefinition> _stories =
            new Dictionary<string, StoryDefinition>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, List<StoryStepDefinition>> _storySteps =
            new Dictionary<string, List<StoryStepDefinition>>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, StoryStepDefinition> _stepsById =
            new Dictionary<string, StoryStepDefinition>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, BuffDefinition> _buffs =
            new Dictionary<string, BuffDefinition>(StringComparer.OrdinalIgnoreCase);

        public void Load(TextAsset eventsCsv, TextAsset branchesCsv,
            TextAsset storiesCsv = null, TextAsset storyStepsCsv = null, TextAsset buffsCsv = null,
            TextAsset charactersCsv = null)
        {
            _events.Clear();
            _eventsById.Clear();
            _characters.Clear();
            _charactersById.Clear();
            _charactersByMeetTag.Clear();
            _branches.Clear();
            _stories.Clear();
            _storySteps.Clear();
            _stepsById.Clear();
            _buffs.Clear();

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
                    Tags = CsvLoader.Get(row, "tags"),
                    SeasonMask = SeasonUtil.ParseMask(CsvLoader.Get(row, "season")),
                    StartStory = CsvLoader.Get(row, "startStory")
                };

                ParseIdList(CsvLoader.Get(row, "choices"), def.ChoiceIds);
                if (def.KillChance >= 1f)
                {
                    Debug.LogWarning($"[LifeSim] Event '{id}' killChance={def.KillChance:0.##} looks like a shifted CSV column. Treated as 0. Use effect 'kill' for guaranteed death.");
                    def.KillChance = 0f;
                }
                _events.Add(def);
                _eventsById[id] = def;
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
                        FailEffects = CsvLoader.Get(row, "failEffects"),
                        GotoStep = CsvLoader.Get(row, "gotoStep"),
                        EndStory = CsvLoader.GetBool(row, "endStory")
                    };

                    if (string.IsNullOrEmpty(_branches[choiceId].GotoStep))
                    {
                        var endRaw = CsvLoader.Get(row, "endStory");
                        if (LooksLikeStepId(endRaw))
                        {
                            _branches[choiceId].GotoStep = endRaw;
                            _branches[choiceId].EndStory = false;
                        }
                    }
                }
            }

            if (storiesCsv != null)
            {
                foreach (var row in CsvLoader.LoadTable(storiesCsv))
                {
                    var id = CsvLoader.Get(row, "id");
                    if (string.IsNullOrEmpty(id))
                        continue;

                    _stories[id] = new StoryDefinition
                    {
                        Id = id,
                        Title = CsvLoader.Get(row, "title", id),
                        Once = CsvLoader.GetBool(row, "once", true)
                    };
                }
            }

            if (storyStepsCsv != null)
            {
                foreach (var row in CsvLoader.LoadTable(storyStepsCsv))
                {
                    var storyId = CsvLoader.Get(row, "storyId");
                    var stepId = CsvLoader.Get(row, "stepId");
                    if (string.IsNullOrEmpty(storyId) || string.IsNullOrEmpty(stepId))
                        continue;

                    var step = new StoryStepDefinition
                    {
                        StoryId = storyId,
                        StepId = stepId,
                        Order = CsvLoader.GetInt(row, "order"),
                        Text = CsvLoader.Get(row, "text"),
                        Effects = CsvLoader.Get(row, "effects"),
                        AdvanceSeason = Mathf.Max(0, CsvLoader.GetInt(row, "advanceSeason")),
                        End = CsvLoader.GetBool(row, "end"),
                        NextStepId = CsvLoader.Get(row, "nextStepId"),
                        IsRandom = CsvLoader.GetBool(row, "random"),
                        Weight = Mathf.Max(1, CsvLoader.GetInt(row, "weight", 1)),
                        Require = CsvLoader.Get(row, "require")
                    };
                    ParseIdList(CsvLoader.Get(row, "choices"), step.ChoiceIds);

                    if (!_storySteps.TryGetValue(storyId, out var list))
                    {
                        list = new List<StoryStepDefinition>();
                        _storySteps[storyId] = list;
                    }

                    list.Add(step);
                    if (_stepsById.ContainsKey(stepId))
                        Debug.LogError($"[LifeSim] Duplicate story stepId '{stepId}' (story {storyId}). Jump/next will break.");
                    _stepsById[stepId] = step;

                    if (!_stories.ContainsKey(storyId))
                    {
                        _stories[storyId] = new StoryDefinition
                        {
                            Id = storyId,
                            Title = storyId,
                            Once = true
                        };
                    }
                }

                foreach (var pair in _storySteps)
                    pair.Value.Sort((a, b) => a.Order.CompareTo(b.Order));
            }

            if (buffsCsv != null)
            {
                foreach (var row in CsvLoader.LoadTable(buffsCsv))
                {
                    var id = CsvLoader.Get(row, "id");
                    if (string.IsNullOrEmpty(id))
                        continue;

                    _buffs[id] = new BuffDefinition
                    {
                        Id = id,
                        Name = CsvLoader.Get(row, "name", id),
                        Text = CsvLoader.Get(row, "text"),
                        Seasons = CsvLoader.GetInt(row, "seasons", -1),
                        Strength = CsvLoader.GetInt(row, "str"),
                        Intelligence = CsvLoader.GetInt(row, "int"),
                        Luck = CsvLoader.GetInt(row, "luck"),
                        Family = CsvLoader.GetInt(row, "family"),
                        KillChance = CsvLoader.GetFloat(row, "killChance")
                    };
                }
            }

            if (charactersCsv != null)
            {
                foreach (var row in CsvLoader.LoadTable(charactersCsv))
                {
                    var id = CsvLoader.Get(row, "id");
                    if (string.IsNullOrEmpty(id))
                        continue;

                    var ch = new CharacterDefinition
                    {
                        Id = id,
                        Name = CsvLoader.Get(row, "name", id),
                        MeetTag = CsvLoader.Get(row, "meetTag", id),
                        MinAge = CsvLoader.GetInt(row, "minAge"),
                        ExcludeTag = CsvLoader.Get(row, "excludeTag")
                    };
                    _characters.Add(ch);
                    _charactersById[id] = ch;
                    if (!string.IsNullOrEmpty(ch.MeetTag))
                        _charactersByMeetTag[ch.MeetTag] = ch;
                }
            }

            Debug.Log($"[LifeSim] Loaded {_events.Count} events, {_branches.Count} branches, {_stories.Count} stories, {_stepsById.Count} story steps, {_buffs.Count} buffs, {_characters.Count} characters.");
        }

        static bool LooksLikeStepId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var s = raw.Trim();
            if (s == "0" || s == "1" ||
                s.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("false", StringComparison.OrdinalIgnoreCase))
                return false;

            return s.IndexOf('_') >= 0;
        }

        static void ParseIdList(string raw, List<string> target)
        {
            if (string.IsNullOrEmpty(raw) || target == null)
                return;

            foreach (var c in raw.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = c.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    target.Add(trimmed);
            }
        }

        public bool TryGetEvent(string eventId, out EventDefinition evt)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                evt = null;
                return false;
            }

            return _eventsById.TryGetValue(eventId, out evt);
        }

        public bool TryGetCharacter(string characterId, out CharacterDefinition character)
        {
            if (string.IsNullOrEmpty(characterId))
            {
                character = null;
                return false;
            }

            return _charactersById.TryGetValue(characterId, out character);
        }

        public bool TryGetCharacterByMeetTag(string meetTag, out CharacterDefinition character)
        {
            if (string.IsNullOrEmpty(meetTag))
            {
                character = null;
                return false;
            }

            return _charactersByMeetTag.TryGetValue(meetTag, out character);
        }

        public bool TryGetBuff(string buffId, out BuffDefinition buff)
        {
            if (string.IsNullOrEmpty(buffId))
            {
                buff = null;
                return false;
            }

            return _buffs.TryGetValue(buffId, out buff);
        }

        public bool TryGetBranch(string choiceId, out BranchDefinition branch)
        {
            return _branches.TryGetValue(choiceId, out branch);
        }

        public bool TryGetStory(string storyId, out StoryDefinition story)
        {
            return _stories.TryGetValue(storyId, out story);
        }

        public bool TryGetStoryStep(string stepId, out StoryStepDefinition step)
        {
            return _stepsById.TryGetValue(stepId, out step);
        }

        public StoryStepDefinition GetFirstStoryStep(string storyId)
        {
            if (!_storySteps.TryGetValue(storyId, out var list) || list.Count == 0)
                return null;

            foreach (var step in list)
            {
                if (!step.IsRandom)
                    return step;
            }

            return null;
        }

        public StoryStepDefinition GetNextStoryStep(StoryStepDefinition current)
        {
            if (current == null)
                return null;

            if (!string.IsNullOrEmpty(current.NextStepId) &&
                _stepsById.TryGetValue(current.NextStepId, out var explicitNext))
                return explicitNext;

            if (!_storySteps.TryGetValue(current.StoryId, out var list))
                return null;

            int idx = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], current))
                {
                    idx = i;
                    break;
                }
            }

            if (idx < 0)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].StepId == current.StepId && list[i].Order == current.Order)
                    {
                        idx = i;
                        break;
                    }
                }
            }

            if (idx < 0)
                return null;

            for (int j = idx + 1; j < list.Count; j++)
            {
                if (!list[j].IsRandom)
                    return list[j];
            }

            return null;
        }

        public List<StoryStepDefinition> GetRandomStorySteps(string storyId)
        {
            var result = new List<StoryStepDefinition>();
            if (string.IsNullOrEmpty(storyId) || !_storySteps.TryGetValue(storyId, out var list))
                return result;

            foreach (var step in list)
            {
                if (step.IsRandom)
                    result.Add(step);
            }

            return result;
        }

        public List<BranchDefinition> GetBranchesForEvent(EventDefinition evt)
        {
            return GetBranchesByIds(evt?.ChoiceIds);
        }

        public List<BranchDefinition> GetBranchesForStoryStep(StoryStepDefinition step)
        {
            return GetBranchesByIds(step?.ChoiceIds);
        }

        List<BranchDefinition> GetBranchesByIds(List<string> ids)
        {
            var list = new List<BranchDefinition>();
            if (ids == null)
                return list;

            foreach (var id in ids)
            {
                if (_branches.TryGetValue(id, out var branch))
                    list.Add(branch);
            }

            return list;
        }
    }
}
