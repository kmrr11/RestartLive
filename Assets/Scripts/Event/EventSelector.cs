using System.Collections.Generic;
using LifeSim.Core;
using LifeSim.Data;

namespace LifeSim.Event
{
    public sealed class EventSelector
    {
        readonly EventDatabase _db;
        readonly System.Random _rng;

        public EventSelector(EventDatabase db, System.Random rng = null)
        {
            _db = db;
            _rng = rng ?? new System.Random();
        }

        public List<EventDefinition> PickForAge(PlayerState state, int count = 1)
        {
            var result = new List<EventDefinition>();
            if (state == null || count <= 0)
                return result;

            var pool = BuildPool(state);
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                var picked = WeightedPick(pool);
                if (picked == null)
                    break;

                result.Add(picked);
                pool.RemoveAll(e => e.Id == picked.Id);
            }

            return result;
        }

        List<EventDefinition> BuildPool(PlayerState state)
        {
            var pool = new List<EventDefinition>();
            foreach (var evt in _db.Events)
            {
                if (state.Age < evt.AgeMin || state.Age > evt.AgeMax)
                    continue;

                if (evt.Once && state.TriggeredOnceEvents.Contains(evt.Id))
                    continue;

                if (!ConditionParser.Evaluate(evt.Require, state))
                    continue;

                if (HasExcludedTag(evt.ExcludeTags, state))
                    continue;

                pool.Add(evt);
            }

            return pool;
        }

        static bool HasExcludedTag(string excludeTags, PlayerState state)
        {
            if (string.IsNullOrWhiteSpace(excludeTags))
                return false;

            var parts = excludeTags.Split(new[] { ';', '|' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var tag in parts)
            {
                if (state.HasTag(tag))
                    return true;
            }

            return false;
        }

        EventDefinition WeightedPick(List<EventDefinition> pool)
        {
            int total = 0;
            foreach (var evt in pool)
                total += GetEffectiveWeight(evt);

            if (total <= 0)
                return null;

            int roll = _rng.Next(total);
            int acc = 0;
            foreach (var evt in pool)
            {
                acc += GetEffectiveWeight(evt);
                if (roll < acc)
                    return evt;
            }

            return pool[pool.Count - 1];
        }

        int GetEffectiveWeight(EventDefinition evt)
        {
            // Reserved for family/resource weight bias later.
            return evt.Weight;
        }
    }
}
