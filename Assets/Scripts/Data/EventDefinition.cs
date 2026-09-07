using System;
using System.Collections.Generic;

namespace LifeSim.Data
{
    public sealed class EventDefinition
    {
        public string Id;
        public int AgeMin;
        public int AgeMax;
        public int Weight = 1;
        public string Text;
        public string Require;
        public string ExcludeTags;
        public string AddTags;
        public string Effects;
        public List<string> ChoiceIds = new List<string>();
        public float KillChance;
        public bool Once;
        public bool HasChoices => ChoiceIds != null && ChoiceIds.Count > 0;
        /// <summary>Choice popups are always once-per-life, even if CSV once=0.</summary>
        public bool TriggersOnce => Once || HasChoices;
        public string Tags;
        /// <summary>Not picked by the seasonal pool; injected by systems such as confession.</summary>
        public bool IsForced
        {
            get
            {
                if (string.IsNullOrEmpty(Tags))
                    return false;

                var parts = Tags.Split(new[] { ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    if (string.Equals(parts[i].Trim(), "forced", StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return false;
            }
        }
        /// <summary>Bit mask for Spring/Summer/Autumn/Winter. Default all.</summary>
        public int SeasonMask = 0b1111;
        /// <summary>Optional long storyline id started after this event resolves.</summary>
        public string StartStory;
    }

    public sealed class StoryDefinition
    {
        public string Id;
        public string Title;
        public bool Once = true;
    }

    public sealed class StoryStepDefinition
    {
        public string StoryId;
        public string StepId;
        public int Order;
        public string Text;
        public List<string> ChoiceIds = new List<string>();
        public string Effects;
        /// <summary>How many seasons to advance after this step resolves (0 = stay in same season).</summary>
        public int AdvanceSeason;
        public bool End;
        /// <summary>Optional next step id; empty means next by order.</summary>
        public string NextStepId;
        /// <summary>If true, this beat is a once-per-run random insert, not part of the main order.</summary>
        public bool IsRandom;
        public int Weight = 1;
        public string Require;
    }

    public sealed class BranchDefinition
    {
        public string ChoiceId;
        public string EventId;
        public string Label;
        public string Check;
        public string SuccessText;
        public string FailText;
        public string SuccessEffects;
        public string FailEffects;
        /// <summary>Jump to this story step after the choice (story mode).</summary>
        public string GotoStep;
        public bool EndStory;
    }
}
