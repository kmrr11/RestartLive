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
                return true;

            if (expr.StartsWith("!tag:", StringComparison.OrdinalIgnoreCase))
                return !state.HasTag(expr.Substring(5));

            if (expr.StartsWith("tag:", StringComparison.OrdinalIgnoreCase))
                return state.HasTag(expr.Substring(4));

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
                return true;

            string left = expr.Substring(0, opIndex).Trim();
            string rightRaw = expr.Substring(opIndex + op.Length).Trim();
            if (!int.TryParse(rightRaw, out int right))
                return false;

            if (luckSoftensThreshold && (op == ">=" || op == ">"))
            {
                // High luck slightly lowers required threshold.
                int soften = state.GetAttr("luck") / 5;
                right = Math.Max(0, right - soften);
            }

            int leftValue = state.GetAttr(left);
            return Compare(leftValue, op, right);
        }

        static bool EvaluateFavor(string spec, PlayerState state, bool luckSoftensThreshold)
        {
            if (string.IsNullOrWhiteSpace(spec) || state == null)
                return true;

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
                return state.GetFavor(spec.Trim()) > 0;

            string id = spec.Substring(0, opIndex).Trim();
            if (!int.TryParse(spec.Substring(opIndex + op.Length).Trim(), out int right))
                return false;

            if (luckSoftensThreshold && (op == ">=" || op == ">"))
                right = Math.Max(0, right - state.GetAttr("luck") / 5);

            return Compare(state.GetFavor(id), op, right);
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
