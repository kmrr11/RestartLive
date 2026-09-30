using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace LifeSim.Core
{
    [Serializable]
    public sealed class LifeCommand
    {
        public string Action;
        public string Value;
    }

    [Serializable]
    public sealed class LifeJournal
    {
        public int Version = 2;
        public int Seed;
        public string ContentHash;
        public string Checksum;
        public List<LifeCommand> Commands = new List<LifeCommand>();

        public static string Hash(string value)
        {
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }

        public string Digest()
        {
            var text = new StringBuilder().Append(Version).Append('|').Append(Seed).Append('|').Append(ContentHash);
            foreach (var command in Commands)
                text.Append('|').Append(command.Action?.Length ?? 0).Append(':').Append(command.Action)
                    .Append('|').Append(command.Value?.Length ?? 0).Append(':').Append(command.Value);
            return Hash(text.ToString());
        }

        public static LifeJournal Read(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > 4 * 1024 * 1024)
                throw new InvalidDataException("存档不存在或文件过大。");
            var journal = JsonUtility.FromJson<LifeJournal>(File.ReadAllText(path));
            if (journal == null || journal.Version != 2 || journal.Commands == null ||
                journal.Commands.Count > 20000 || journal.Commands.Exists(c => c == null) ||
                journal.Checksum != journal.Digest())
                throw new InvalidDataException("存档损坏或版本不兼容。");
            return journal;
        }

        public void Write(string path)
        {
            Checksum = Digest();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(this));
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
    }
}
