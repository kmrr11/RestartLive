using System;
using LifeSim.Core;

namespace LifeSim.Data
{
    /// <summary>
    /// Supports expressions like: str>=5;family&lt;8;tag:student;!tag:dead
    /// Comparison ops: >=, <=, !=, >, <, =
    /// </summary>
    public static class ConditionParser
    {
        public static bool Evaluate(string expression, PlayerState state, bool luckSoftensThreshold = false)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return true;

            var parts = expression.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (!EvaluateSingle(part.Trim(), state, luckSoftensThreshold))
                    return false;
            }

            return true;
        }

        static bool EvaluateSingle(string expr, PlayerState state, bool luckSoftensThreshold)
        {
            if (string.IsNullOrEmpty(expr))
                return false;

            if (expr.StartsWith("!tag:", StringComparison.OrdinalIgnoreCase))
                return !state.HasTag(expr.Substring(5));

            if (expr.StartsWith("tag:", StringComparison.OrdinalIgnoreCase))
                return state != null && !string.IsNullOrWhiteSpace(expr.Substring(4)) && state.HasTag(expr.Substring(4));

            if (expr.StartsWith("!buff:", StringComparison.OrdinalIgnoreCase))
                return !state.HasBuff(expr.Substring(6));

            if (expr.StartsWith("buff:", StringComparison.OrdinalIgnoreCase))
                return state.HasBuff(expr.Substring(5));

            if (expr.StartsWith("favor:", StringComparison.OrdinalIgnoreCase))
                return EvaluateFavor(expr.Substring(6), state, luckSoftensThreshold);

            string op = null;
            int opIndex = -1;
            string[] ops = { ">=", "<=", "!=", ">", "<", "=" };
            foreach (var candidate in ops)
            {
                int idx = expr.IndexOf(candidate, StringComparison.Ordinal);
                if (idx > 0)
                {
                    op = candidate;
                    opIndex = idx;
                    break;
                }
            }

            if (op == null)
                return false;

            string left = expr.Substring(0, opIndex).Trim();
            string rightRaw = expr.Substring(opIndex + op.Length).Trim();
            if (!int.TryParse(rightRaw, out int right))
                return false;

            if (string.Equals(left, "age", StringComparison.OrdinalIgnoreCase) || left == "年龄")
                return state != null && Compare(state.Age, op, right);

            if (luckSoftensThreshold && (op == ">=" || op == ">"))
            {
                // High luck slightly lowers required threshold.
                int soften = state.GetAttr("luck") / 5;
                right = Math.Max(0, right - soften);
            }

            if (state == null || !IsKnownAttribute(left)) return false;
            int leftValue = state.GetAttr(left);
            return Compare(leftValue, op, right);
        }

        static bool EvaluateFavor(string spec, PlayerState state, bool luckSoftensThreshold)
        {
            if (string.IsNullOrWhiteSpace(spec) || state == null) return false;

            string op = null;
            int opIndex = -1;
            string[] ops = { ">=", "<=", "!=", ">", "<", "=" };
            foreach (var candidate in ops)
            {
                int idx = spec.IndexOf(candidate, StringComparison.Ordinal);
                if (idx > 0)
                {
                    op = candidate;
                    opIndex = idx;
                    break;
                }
            }

            if (op == null)
                return !string.IsNullOrWhiteSpace(spec) && state.GetFavor(spec.Trim()) > 0;

            string id = spec.Substring(0, opIndex).Trim();
            if (!int.TryParse(spec.Substring(opIndex + op.Length).Trim(), out int right))
                return false;

            if (luckSoftensThreshold && (op == ">=" || op == ">"))
                right = Math.Max(0, right - state.GetAttr("luck") / 5);

            return Compare(state.GetFavor(id), op, right);
        }

        static bool IsKnownAttribute(string key)
        {
            switch ((key ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "str": case "strength": case "力量": case "int": case "intelligence": case "智力":
                case "luck": case "运气": case "family": case "家境": case "age": case "年龄": return true;
                default: return false;
            }
        }

        static bool Compare(int leftValue, string op, int right)
        {
            switch (op)
            {
                case ">=": return leftValue >= right;
                case "<=": return leftValue <= right;
                case "!=": return leftValue != right;
                case ">": return leftValue > right;
                case "<": return leftValue < right;
                case "=": return leftValue == right;
                default: return true;
            }
        }
    }
}
