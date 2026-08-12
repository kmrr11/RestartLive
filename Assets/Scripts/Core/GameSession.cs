using System;
using System.Collections.Generic;
using LifeSim.Data;
using LifeSim.Event;
using UnityEngine;

namespace LifeSim.Core
{
    public enum GamePhase
    {
        Allocate,
        Playing,
        AwaitingChoice,
        Ended
    }

    public sealed class GameSession
    {
        public const int PointPool = 20;
        public const int MaxAge = 80;
        public const int AttrMin = 1;
        public const int AttrMax = 10;

        public PlayerState Player { get; private set; }
        public GamePhase Phase { get; private set; } = GamePhase.Allocate;
        public EventDefinition PendingEvent { get; private set; }
        public IReadOnlyList<BranchDefinition> PendingChoices => _pendingChoices;

        public event Action<string> OnLog;
        public event Action OnStateChanged;
        public event Action OnAwaitingChoice;
        public event Action OnEnded;

        readonly EventDatabase _db = new EventDatabase();
        readonly System.Random _rng = new System.Random();
        EventSelector _selector;
        readonly List<BranchDefinition> _pendingChoices = new List<BranchDefinition>();
        readonly Queue<EventDefinition> _yearQueue = new Queue<EventDefinition>();

        public void Initialize(TextAsset eventsCsv, TextAsset branchesCsv)
        {
            _db.Load(eventsCsv, branchesCsv);
            _selector = new EventSelector(_db, _rng);
            ResetToAllocate();
        }

        public void ResetToAllocate()
        {
            Player = new PlayerState
            {
                Age = 0,
                Alive = true
            };
            Phase = GamePhase.Allocate;
            PendingEvent = null;
            _pendingChoices.Clear();
            _yearQueue.Clear();
            RollAttributes();
        }

        /// <summary>
        /// Randomly distributes PointPool across 4 attrs, each in [AttrMin, AttrMax].
        /// </summary>
        public void RollAttributes()
        {
            if (Phase != GamePhase.Allocate)
                return;

            if (Player == null)
                Player = new PlayerState();

            var values = RollAttributeValues();
            Player.Strength = values[0];
            Player.Intelligence = values[1];
            Player.Luck = values[2];
            Player.Family = values[3];
            RaiseState();
        }

        int[] RollAttributeValues()
        {
            // Guarantee exact sum == PointPool with per-attr clamps.
            int[] values = { AttrMin, AttrMin, AttrMin, AttrMin };
            int remaining = PointPool - AttrMin * 4;
            if (remaining < 0)
                remaining = 0;

            while (remaining > 0)
            {
                int candidates = 0;
                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i] < AttrMax)
                        candidates++;
                }

                if (candidates == 0)
                    break;

                int pick = _rng.Next(candidates);
                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i] >= AttrMax)
                        continue;
                    if (pick == 0)
                    {
                        values[i]++;
                        remaining--;
                        break;
                    }

                    pick--;
                }
            }

            // Shuffle assignment so no attribute is biased by index order over many rolls.
            for (int i = values.Length - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }

            return values;
        }

        public bool StartLife()
        {
            if (Phase != GamePhase.Allocate || Player == null)
                return false;

            Phase = GamePhase.Playing;
            Log($"你出生了。力量{Player.Strength} 智力{Player.Intelligence} 运气{Player.Luck} 家境{Player.Family}");
            Player.AppendHistory("出生");
            RaiseState();
            ResolveAgeEvents();
            return true;
        }

        public void AdvanceYear()
        {
            if (Phase != GamePhase.Playing || Player == null || !Player.Alive)
                return;

            if (Player.Age >= MaxAge)
            {
                EndLife("你安详地走到了人生尽头。");
                return;
            }

            Player.Age += 1;
            ResolveAgeEvents();
        }

        void ResolveAgeEvents()
        {
            if (_selector == null || Player == null)
                return;

            var events = _selector.PickForAge(Player, 1);
            _yearQueue.Clear();
            foreach (var e in events)
                _yearQueue.Enqueue(e);

            if (_yearQueue.Count == 0)
            {
                Log($"{Player.Age}岁：平淡的一年。");
                RaiseState();
                CheckEndAfterResolve();
                return;
            }

            ProcessNextInQueue();
        }

        public void ResolvePendingWithoutChoice()
        {
            if (Phase != GamePhase.Playing || PendingEvent == null)
                return;

            ApplyEventBase(PendingEvent);
            PendingEvent = null;
            RaiseState();

            if (!Player.Alive)
            {
                EndLife("命运突然终止了你的人生。");
                return;
            }

            ProcessNextInQueue();
        }

        public void Choose(string choiceId)
        {
            if (Phase != GamePhase.AwaitingChoice || PendingEvent == null)
                return;

            if (!_db.TryGetBranch(choiceId, out var branch))
                return;

            bool success = string.IsNullOrWhiteSpace(branch.Check) ||
                           ConditionParser.Evaluate(branch.Check, Player, luckSoftensThreshold: true);

            string resultText = success ? branch.SuccessText : branch.FailText;
            string effects = success ? branch.SuccessEffects : branch.FailEffects;

            if (string.IsNullOrWhiteSpace(resultText))
                resultText = success ? "事情顺利完成了。" : "事情没有如愿。";

            Log($"{Player.Age}岁：{resultText}");
            Player.AppendHistory($"{Player.Age}岁 · {branch.Label} → {(success ? "成功" : "失败")}");

            // Base tags / once flags still apply after choice.
            MarkEventTriggered(PendingEvent);
            EffectExecutor.ApplyTags(PendingEvent.AddTags, Player);
            EffectExecutor.Apply(effects, Player, _rng);
            EffectExecutor.RollKill(PendingEvent.KillChance, Player, _rng);

            PendingEvent = null;
            _pendingChoices.Clear();
            Phase = GamePhase.Playing;
            RaiseState();

            if (!Player.Alive)
            {
                EndLife("这个选择改变了你的命运，也结束了你的人生。");
                return;
            }

            ProcessNextInQueue();
        }

        public string BuildSummary()
        {
            if (Player == null)
                return string.Empty;

            return $"享年 {Player.Age} 岁\n" +
                   $"力量 {Player.Strength} / 智力 {Player.Intelligence} / 运气 {Player.Luck} / 家境 {Player.Family}\n" +
                   $"经历条目 {Player.History.Count} 条";
        }

        void ProcessNextInQueue()
        {
            if (!Player.Alive)
            {
                EndLife("命运突然终止了你的人生。");
                return;
            }

            if (_yearQueue.Count == 0)
            {
                CheckEndAfterResolve();
                return;
            }

            var evt = _yearQueue.Dequeue();
            PendingEvent = evt;
            Log($"{Player.Age}岁：{evt.Text}");

            var choices = _db.GetBranchesForEvent(evt);
            if (choices.Count > 0)
            {
                _pendingChoices.Clear();
                _pendingChoices.AddRange(choices);
                Phase = GamePhase.AwaitingChoice;
                RaiseState();
                OnAwaitingChoice?.Invoke();
                return;
            }

            ApplyEventBase(evt);
            PendingEvent = null;
            RaiseState();

            if (!Player.Alive)
            {
                EndLife("命运突然终止了你的人生。");
                return;
            }

            ProcessNextInQueue();
        }

        void ApplyEventBase(EventDefinition evt)
        {
            MarkEventTriggered(evt);
            EffectExecutor.ApplyTags(evt.AddTags, Player);
            EffectExecutor.Apply(evt.Effects, Player, _rng);
            bool killed = EffectExecutor.RollKill(evt.KillChance, Player, _rng);
            Player.AppendHistory($"{Player.Age}岁 · {evt.Id}");
            if (killed)
                Log("你没能撑过这一年。");
        }

        void MarkEventTriggered(EventDefinition evt)
        {
            if (evt.Once)
                Player.TriggeredOnceEvents.Add(evt.Id);
        }

        void CheckEndAfterResolve()
        {
            if (!Player.Alive)
            {
                EndLife("命运突然终止了你的人生。");
                return;
            }

            if (Player.Age >= MaxAge)
                EndLife("你安详地走到了人生尽头。");
        }

        void EndLife(string reason)
        {
            Phase = GamePhase.Ended;
            Player.Alive = false;
            Log(reason);
            Log(BuildSummary());
            RaiseState();
            OnEnded?.Invoke();
        }

        void Log(string line)
        {
            OnLog?.Invoke(line);
        }

        void RaiseState()
        {
            OnStateChanged?.Invoke();
        }
    }
}
