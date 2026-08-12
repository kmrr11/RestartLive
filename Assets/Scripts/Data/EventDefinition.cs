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
        public string Tags;
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
    }
}
