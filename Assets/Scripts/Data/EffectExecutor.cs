using System;
using System.Globalization;
using LifeSim.Core;
using UnityEngine;

namespace LifeSim.Data
{
    /// <summary>
    /// Supports effects like: int+1;str-1;tag:graduate;kill;killChance:0.2
    /// </summary>
    public static class EffectExecutor
    {
        public static void Apply(string effects, PlayerState state, System.Random rng = null)
        {
            if (string.IsNullOrWhiteSpace(effects) || state == null || !state.Alive)
                return;

            rng ??= new System.Random();
            var parts = effects.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var raw in parts)
            {
                var part = raw.Trim();
                if (string.IsNullOrEmpty(part))
                    continue;

                if (part.Equals("kill", StringComparison.OrdinalIgnoreCase))
                {
                    state.Alive = false;
                    continue;
                }

                if (part.StartsWith("killChance:", StringComparison.OrdinalIgnoreCase))
                {
                    if (float.TryParse(part.Substring("killChance:".Length), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out float chance))
                    {
                        chance = Mathf.Clamp01(chance - state.Luck * 0.01f);
                        if (rng.NextDouble() < chance)
                            state.Alive = false;
                    }

                    continue;
                }

                if (part.StartsWith("tag:", StringComparison.OrdinalIgnoreCase))
                {
                    state.AddTag(part.Substring(4));
                    continue;
                }

                if (part.StartsWith("!tag:", StringComparison.OrdinalIgnoreCase) ||
                    part.StartsWith("-tag:", StringComparison.OrdinalIgnoreCase))
                {
                    var tag = part.Substring(part.IndexOf(':') + 1);
                    state.Tags.Remove(tag.Trim());
                    continue;
                }

                ApplyAttrDelta(part, state);
            }
        }

        static void ApplyAttrDelta(string part, PlayerState state)
        {
            int plus = part.IndexOf('+');
            int minus = part.IndexOf('-');
            int idx;
            int sign;

            if (plus > 0 && (minus < 0 || plus < minus))
            {
                idx = plus;
                sign = 1;
            }
            else if (minus > 0)
            {
                idx = minus;
                sign = -1;
            }
            else
            {
                return;
            }

            string attr = part.Substring(0, idx).Trim();
            string numRaw = part.Substring(idx + 1).Trim();
            if (!int.TryParse(numRaw, out int amount))
                return;

            state.AddAttr(attr, sign * amount);
        }

        public static void ApplyTags(string tags, PlayerState state)
        {
            if (string.IsNullOrWhiteSpace(tags) || state == null)
                return;

            var parts = tags.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var tag in parts)
                state.AddTag(tag);
        }

        public static bool RollKill(float killChance, PlayerState state, System.Random rng)
        {
            if (killChance <= 0f || state == null || !state.Alive)
                return false;

            float chance = Mathf.Clamp01(killChance - state.Luck * 0.01f);
            if (rng.NextDouble() < chance)
            {
                state.Alive = false;
                return true;
            }

            return false;
        }
    }
}
