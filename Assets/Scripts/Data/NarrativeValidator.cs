using System.Collections.Generic;

namespace LifeSim.Data
{
    public static class NarrativeValidator
    {
        public static List<string> Validate(EventDatabase db)
        {
            var errors = new List<string>();
            if (db == null) { errors.Add("Database is null"); return errors; }
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var e in db.Events) { if (!seen.Add(e.Id)) errors.Add("Duplicate event: " + e.Id); foreach (var id in e.ChoiceIds) if (!db.TryGetBranch(id, out _)) errors.Add($"Event {e.Id} missing branch {id}"); if (!string.IsNullOrEmpty(e.StartStory) && !db.TryGetStory(e.StartStory, out _)) errors.Add($"Event {e.Id} missing story {e.StartStory}"); }
            foreach (var kv in db.Branches) { var b=kv.Value; if (!string.IsNullOrEmpty(b.GotoStep) && !db.TryGetStoryStep(b.GotoStep, out _)) errors.Add($"Branch {b.ChoiceId} missing step {b.GotoStep}"); }
            foreach (var s in db.Stories) if (db.GetFirstStoryStep(s.Key)==null) errors.Add("Story missing first step: " + s.Key);
            return errors;
        }
    }
}
