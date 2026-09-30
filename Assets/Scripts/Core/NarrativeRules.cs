using System;
using System.Collections.Generic;
using LifeSim.Data;

namespace LifeSim.Core
{
    public static class NarrativeRules
    {
        public static bool HasConfirmedPartner(PlayerState state)
        {
            if (state == null) return false;
            foreach (var tag in state.Tags)
            {
                if (!tag.StartsWith("confessed_", StringComparison.Ordinal)) continue;
                var id = tag.Substring("confessed_".Length);
                if (state.HasTag("together_" + id) && !state.HasTag("apart_" + id)) return true;
            }
            return false;
        }

        public static bool IsMilestoneDue(EventDefinition evt, PlayerState state)
        {
            if (evt == null || state == null || state.Age != evt.AgeMax || !evt.TriggersOnce)
                return false;
            bool milestone = false;
            foreach (string tag in (evt.Tags ?? string.Empty).Split(new[] { ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                milestone |= string.Equals(tag, "milestone", StringComparison.OrdinalIgnoreCase);
            if (!milestone || !SeasonUtil.AllowsHalfYear(evt.SeasonMask, state.Season))
                return false;
            if ((int)state.Season < 2 && SeasonUtil.AllowsHalfYear(evt.SeasonMask, Season.Autumn)) return false;
            return true;
        }

        public static string GetRelationshipStage(PlayerState state, string characterId)
        {
            if (state == null || string.IsNullOrEmpty(characterId)) return "未相识";
            if (characterId == "lise" && state.HasTag("lise_farewell_memory")) return "异界离别";
            if (characterId == "spouse" && state.HasTag("widow")) return "追忆";
            if (state.HasTag("apart_" + characterId)) return "错过";
            if (state.HasTag("bond_" + characterId) || characterId == "spouse" && state.HasTag("married")) return "伴侣";
            if (state.HasTag("together_" + characterId)) return "交往";
            if (state.GetFavor(characterId) >= 50) return "亲近";
            string meetTag = characterId;
            switch (characterId)
            {
                case "crush": meetTag = "crush_met"; break;
                case "mentor": meetTag = "mentored"; break;
                case "neighbor": meetTag = "kind_neighbor"; break;
                case "spouse": meetTag = "married"; break;
            }
            return state.HasTag(meetTag) || state.GetFavor(characterId) > 0 ? "相识" : "未相识";
        }

        public static string BuildBiography(PlayerState state)
        {
            if (state == null) return string.Empty;
            var lines = new List<string>();
            string title = state.HasTag("iw_shared_memory") ? "把两个世界写进人生的人" :
                state.HasTag("rebounded") ? "跌倒后重新出发的人" :
                state.HasTag("career_mentor") ? "为后来者留灯的人" :
                state.HasTag("graduate") ? "走出书页的人" : "独一无二的普通人生";
            lines.Add(title);
            lines.Add($"你走到了{state.Age}岁{SeasonUtil.ToDisplay(state.Season)}季。");
            if (state.HasTag("college")) lines.Add(state.HasTag("graduate") ? "你完成了学业，也在书本之外找到了方向。" : "录取通知书曾为你打开另一种生活。");
            if (state.HasTag("worker")) lines.Add("你曾早早走上工作岗位，在日常劳作中学会独立。");
            if (state.HasTag("startup_fail")) lines.Add(state.HasTag("rebounded") ? "创业的失利没有成为终点，你重新站稳了脚跟。" : "那次创业的失利，成为你人生里一道真实的伤痕。");
            if (state.HasTag("lise_hidden_life"))
                lines.Add("你选择与莉瑟留在异世界边境，相伴至老。王国陷落的消息与那封求援信，始终留在你们的记忆里。");
            else if (state.HasTag("lise_farewell_memory"))
                lines.Add("你击败魔王后被契约送回原来的世界，与莉瑟在归途之门前分别。没有重逢的承诺，那张地图仍留在身边。");
            else if (state.HasTag("isekai_done")) lines.Add("你带着另一个世界的记忆回到人间，那些相遇并未消失。");
            string favors = state.FormatFavors();
            if (!string.IsNullOrEmpty(favors)) lines.Add(favors);
            lines.Add("人生片段");
            // Preserve the game's own human-readable account instead of exposing internal tags.
            int count = Math.Min(8, state.History.Count);
            for (int i = 0; i < count; i++)
            {
                int index = count <= 1 ? 0 : i * (state.History.Count - 1) / (count - 1);
                lines.Add(state.History[index]);
            }
            return string.Join("\n", lines);
        }
    }
}
