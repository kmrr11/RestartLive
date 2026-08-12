using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace LifeSim.Data
{
    public static class CsvLoader
    {
        public static List<Dictionary<string, string>> LoadTable(TextAsset asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            return Parse(asset.text);
        }

        public static List<Dictionary<string, string>> Parse(string csvText)
        {
            var rows = new List<Dictionary<string, string>>();
            if (string.IsNullOrWhiteSpace(csvText))
                return rows;

            if (csvText.Length > 0 && csvText[0] == '\uFEFF')
                csvText = csvText.Substring(1);

            var lines = SplitLines(csvText);
            if (lines.Count == 0)
                return rows;

            var headers = ParseLine(lines[0]);
            for (int i = 1; i < lines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                var cols = ParseLine(lines[i]);
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int c = 0; c < headers.Count; c++)
                {
                    var key = headers[c].Trim();
                    if (string.IsNullOrEmpty(key))
                        continue;
                    dict[key] = c < cols.Count ? cols[c] : string.Empty;
                }

                if (dict.Count > 0)
                    rows.Add(dict);
            }

            return rows;
        }

        static List<string> SplitLines(string text)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                    sb.Append(ch);
                    continue;
                }

                if (!inQuotes && (ch == '\n' || ch == '\r'))
                {
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;

                    result.Add(sb.ToString());
                    sb.Length = 0;
                    continue;
                }

                sb.Append(ch);
            }

            if (sb.Length > 0)
                result.Add(sb.ToString());

            return result;
        }

        static List<string> ParseLine(string line)
        {
            var cols = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (ch == ',' && !inQuotes)
                {
                    cols.Add(sb.ToString());
                    sb.Length = 0;
                    continue;
                }

                sb.Append(ch);
            }

            cols.Add(sb.ToString());
            return cols;
        }

        public static string Get(Dictionary<string, string> row, string key, string fallback = "")
        {
            if (row != null && row.TryGetValue(key, out var value) && value != null)
                return value.Trim();
            return fallback;
        }

        public static int GetInt(Dictionary<string, string> row, string key, int fallback = 0)
        {
            var raw = Get(row, key, null);
            if (string.IsNullOrEmpty(raw))
                return fallback;
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                ? v
                : fallback;
        }

        public static float GetFloat(Dictionary<string, string> row, string key, float fallback = 0f)
        {
            var raw = Get(row, key, null);
            if (string.IsNullOrEmpty(raw))
                return fallback;
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v
                : fallback;
        }

        public static bool GetBool(Dictionary<string, string> row, string key, bool fallback = false)
        {
            var raw = Get(row, key, null);
            if (string.IsNullOrEmpty(raw))
                return fallback;

            raw = raw.ToLowerInvariant();
            if (raw == "1" || raw == "true" || raw == "yes" || raw == "y")
                return true;
            if (raw == "0" || raw == "false" || raw == "no" || raw == "n")
                return false;
            return fallback;
        }
    }
}
