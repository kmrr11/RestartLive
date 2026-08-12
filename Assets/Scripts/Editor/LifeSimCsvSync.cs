#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LifeSim.EditorTools
{
    /// <summary>
    /// Designers edit Assets/Data/*.csv, then sync into Resources as .txt (Unity TextAsset).
    /// </summary>
    public static class LifeSimCsvSync
    {
        const string SourceDir = "Assets/Data";
        const string DestDir = "Assets/Resources/Data";

        [MenuItem("LifeSim/Sync CSV To Resources")]
        public static void Sync()
        {
            Directory.CreateDirectory(DestDir.Replace('\\', '/'));
            int count = 0;
            foreach (var src in Directory.GetFiles(SourceDir, "*.csv"))
            {
                var name = Path.GetFileNameWithoutExtension(src) + ".txt";
                var dst = Path.Combine(DestDir, name);
                File.Copy(src, dst, true);
                count++;
                Debug.Log($"[LifeSim] Synced {src} -> {dst}");
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("LifeSim", $"已同步 {count} 个 CSV 到 Resources/Data。", "OK");
        }

        [InitializeOnLoadMethod]
        static void AutoSyncOnCsvChange()
        {
            // Manual menu is enough for prototype; keep hook free of heavy watchers.
        }
    }
}
#endif
