using System;
using System.Collections.Generic;
using System.IO;

namespace LifeSim.Core
{
    public sealed partial class GameSession
    {
        public int Seed { get; }
        readonly List<LifeCommand> _commands = new List<LifeCommand>();
        readonly List<string> _transcript = new List<string>();
        string _contentHash;
        public IReadOnlyList<string> Transcript => _transcript;

        public GameSession(int? seed = null)
        {
            Seed = seed ?? Guid.NewGuid().GetHashCode();
            _rng = new Random(Seed);
        }

        // Only player input is journaled; nested seasonal transitions stay internal.
        public void Perform(string action, string value = null)
        {
            switch (action)
            {
                case "roll":
                    if (Phase != GamePhase.Allocate) throw new InvalidOperationException("Cannot roll now.");
                    RollAttributes(); break;
                case "start":
                    if (!StartLife()) throw new InvalidOperationException("Cannot start now.");
                    break;
                case "advance":
                    if (Phase != GamePhase.Playing && Phase != GamePhase.InStory)
                        throw new InvalidOperationException("Cannot advance now.");
                    Advance(); break;
                case "confess":
                    if (!CanConfess) throw new InvalidOperationException("Cannot confess now.");
                    BeginConfessSelect(); break;
                case "choose":
                    if (!_pendingChoices.Exists(c => c.ChoiceId == value))
                        throw new InvalidOperationException("Unknown choice.");
                    Choose(value); break;
                default: throw new InvalidDataException("Unknown saved action.");
            }
            _commands.Add(new LifeCommand { Action = action, Value = value });
        }

        public LifeJournal ExportJournal()
        {
            var journal = new LifeJournal { Seed = Seed, ContentHash = _contentHash };
            foreach (var command in _commands)
                journal.Commands.Add(new LifeCommand { Action = command.Action, Value = command.Value });
            journal.Checksum = journal.Digest();
            return journal;
        }

        public void Replay(LifeJournal journal)
        {
            if (_commands.Count != 0 || Phase != GamePhase.Allocate || journal == null ||
                journal.Version != 2 || journal.Seed != Seed || journal.ContentHash != _contentHash ||
                journal.Commands == null || journal.Commands.Count > 20000 ||
                journal.Commands.Exists(c => c == null) || journal.Checksum != journal.Digest())
                throw new InvalidDataException("存档与当前剧情版本不兼容，或存档已损坏。");
            foreach (var command in journal.Commands) Perform(command.Action, command.Value);
        }
    }
}
