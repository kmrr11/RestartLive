#if UNITY_EDITOR
using System.Collections.Generic;
using LifeSim.Data;
using UnityEditor;
using UnityEngine;

namespace LifeSim.EditorTools
{
    public static class LifeSimDataValidator
    {
        [MenuItem("LifeSim/Validate CSV Data")]
        public static void Validate()
        {
            var events = Resources.Load<TextAsset>("Data/Events");
            var branches = Resources.Load<TextAsset>("Data/Branches");
            if (events == null || branches == null)
            {
                EditorUtility.DisplayDialog("LifeSim", "找不到 Resources/Data 下的 Events/Branches。", "OK");
                return;
            }

            var db = new EventDatabase();
            db.Load(events, branches,
                Resources.Load<TextAsset>("Data/Stories"),
                Resources.Load<TextAsset>("Data/StorySteps"),
                Resources.Load<TextAsset>("Data/Buffs"),
                Resources.Load<TextAsset>("Data/Characters"));

            int missing = 0;
            int lethalShift = 0;
            int dupSteps = 0;
            int missingConfess = 0;
            var seenSteps = new HashSet<string>();
            foreach (var evt in db.Events)
            {
                if (evt.KillChance >= 1f)
                {
                    Debug.LogError($"[LifeSim] Event '{evt.Id}' has killChance={evt.KillChance} (100% death). Check CSV column alignment.");
                    lethalShift++;
                }

                foreach (var choiceId in evt.ChoiceIds)
                {
                    if (!db.TryGetBranch(choiceId, out _))
                    {
                        Debug.LogError($"[LifeSim] Event '{evt.Id}' references missing choice '{choiceId}'");
                        missing++;
                    }
                }
            }

            foreach (var ch in db.Characters)
            {
                if (!db.TryGetEvent($"confess_{ch.Id}_ok", out _))
                {
                    Debug.LogError($"[LifeSim] Character '{ch.Id}' missing confess_{ch.Id}_ok");
                    missingConfess++;
                }
                if (!db.TryGetEvent($"confess_{ch.Id}_no", out _))
                {
                    Debug.LogError($"[LifeSim] Character '{ch.Id}' missing confess_{ch.Id}_no");
                    missingConfess++;
                }
            }

            // Story step ids are unique keys for jumps.
            var stepsAsset = Resources.Load<TextAsset>("Data/StorySteps");
            if (stepsAsset != null)
            {
                foreach (var row in CsvLoader.LoadTable(stepsAsset))
                {
                    var stepId = CsvLoader.Get(row, "stepId");
                    if (string.IsNullOrEmpty(stepId))
                        continue;
                    if (!seenSteps.Add(stepId))
                    {
                        Debug.LogError($"[LifeSim] Duplicate story stepId '{stepId}'");
                        dupSteps++;
                    }
                }
            }

            EditorUtility.DisplayDialog(
                "LifeSim",
                $"Events: {db.Events.Count}\nBranches: {db.Branches.Count}\nCharacters: {db.Characters.Count}\nMissing choices: {missing}\nMissing confess events: {missingConfess}\n100% killChance rows: {lethalShift}\nDuplicate stepIds: {dupSteps}",
                "OK");
        }
    }
}
#endif
