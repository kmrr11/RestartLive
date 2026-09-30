using System;
namespace UnityEngine
{
    public class TextAsset { public string text; public TextAsset(string value) { text = value; } }
    public static class Mathf
    {
        public static int Clamp(int v,int min,int max) => Math.Clamp(v,min,max);
        public static float Clamp01(float v) => Math.Clamp(v,0,1);
        public static int Max(int a,int b) => Math.Max(a,b);
    }
    public static class Debug
    {
        public static void Log(object value) { }
        public static void LogWarning(object value) { }
        public static void LogError(object value) { Console.Error.WriteLine(value); }
    }
    public static class JsonUtility
    {
        static readonly System.Text.Json.JsonSerializerOptions Options = new() { IncludeFields = true };
        public static string ToJson(object value, bool prettyPrint = false) => System.Text.Json.JsonSerializer.Serialize(value, Options);
        public static T FromJson<T>(string value) => System.Text.Json.JsonSerializer.Deserialize<T>(value, Options);
    }
}
