#if UNITY_EDITOR
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
            db.Load(events, branches);

            int missing = 0;
            foreach (var evt in db.Events)
            {
                foreach (var choiceId in evt.ChoiceIds)
                {
                    if (!db.TryGetBranch(choiceId, out _))
                    {
                        Debug.LogError($"[LifeSim] Event '{evt.Id}' references missing choice '{choiceId}'");
                        missing++;
                    }
                }
            }

            EditorUtility.DisplayDialog(
                "LifeSim",
                $"Events: {db.Events.Count}\nBranches: {db.Branches.Count}\nMissing choices: {missing}",
                "OK");
        }
    }
}
#endif
