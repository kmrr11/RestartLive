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
        InStory,
        StoryAwaitingChoice,
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
        public StoryStepDefinition PendingStoryStep { get; private set; }
        public IReadOnlyList<BranchDefinition> PendingChoices => _pendingChoices;
        public bool InStoryMode => Player != null && Player.InStory;
        public bool AwaitingContinue => _afterChoiceContinue != null;
        public bool PickingConfess => _pickingConfess;
        public string ConfessPrompt => _pickingConfess ? "你想向谁告白？" : null;
        public bool CanConfess =>
            Phase == GamePhase.Playing &&
            Player != null &&
            Player.Alive &&
            !Player.InStory &&
            !AwaitingContinue &&
            !_pickingConfess &&
            string.IsNullOrEmpty(Player.PendingConfessId);

        public event Action<string> OnLog;
        public event Action OnStateChanged;
        public event Action OnAwaitingChoice;
        public event Action OnEnded;

        readonly EventDatabase _db = new EventDatabase();
        readonly System.Random _rng = new System.Random();
        EventSelector _selector;
        readonly List<BranchDefinition> _pendingChoices = new List<BranchDefinition>();
        readonly Queue<EventDefinition> _yearQueue = new Queue<EventDefinition>();
        string _resumeAfterRandom;
        Action _afterChoiceContinue;
        bool _pickingConfess;

        public void Initialize(TextAsset eventsCsv, TextAsset branchesCsv,
            TextAsset storiesCsv = null, TextAsset storyStepsCsv = null, TextAsset buffsCsv = null,
            TextAsset charactersCsv = null)
        {
            _db.Load(eventsCsv, branchesCsv, storiesCsv, storyStepsCsv, buffsCsv, charactersCsv);
            _selector = new EventSelector(_db, _rng);
            ResetToAllocate();
        }

        public void ResetToAllocate()
        {
            Player = new PlayerState
            {
                Age = 0,
                Season = Season.Spring,
                Alive = true
            };
            Phase = GamePhase.Allocate;
            PendingEvent = null;
            PendingStoryStep = null;
            _pendingChoices.Clear();
            _yearQueue.Clear();
            _afterChoiceContinue = null;
            _pickingConfess = false;
            RollAttributes();
        }

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
            ResolveSeasonEvents();
            return true;
        }

        /// <summary>
        /// Normal mode: advance one season.
        /// Story mode: continue to the next story beat (may stay in the same season).
        /// </summary>
        public void Advance()
        {
            if (ConsumeAfterChoiceContinue())
                return;

            if (Player != null && Player.InStory)
                ContinueStory();
            else
                AdvanceSeason();
        }

        bool ConsumeAfterChoiceContinue()
        {
            if (_afterChoiceContinue == null)
                return false;

            var next = _afterChoiceContinue;
            _afterChoiceContinue = null;
            next();
            return true;
        }

        public void AdvanceSeason()
        {
            if (Phase != GamePhase.Playing || Player == null || !Player.Alive || Player.InStory)
                return;

            if (Player.Age > MaxAge || (Player.Age == MaxAge && Player.Season == Season.Winter))
            {
                EndLife(FormatOldAgeDeath());
                return;
            }

            AdvanceSeasonInternal(1);
            ResolveSeasonEvents();
        }

        public void AdvanceYear()
        {
            Advance();
        }

        public void BeginConfessSelect()
        {
            if (!CanConfess)
                return;

            var targets = GetConfessTargets();
            if (targets.Count == 0)
            {
                Log("你还没有可以告白的人。先去认识他们吧。");
                RaiseState();
                return;
            }

            _pickingConfess = true;
            PendingEvent = null;
            _pendingChoices.Clear();
            for (int i = 0; i < targets.Count; i++)
            {
                var ch = targets[i];
                _pendingChoices.Add(new BranchDefinition
                {
                    ChoiceId = "cp:" + ch.Id,
                    Label = $"{ch.Name}（好感{Player.GetFavor(ch.Id)}）"
                });
            }

            _pendingChoices.Add(new BranchDefinition
            {
                ChoiceId = "confess_cancel",
                Label = "再想想"
            });

            Phase = GamePhase.AwaitingChoice;
            RaiseState();
            OnAwaitingChoice?.Invoke();
        }

        List<CharacterDefinition> GetConfessTargets()
        {
            var list = new List<CharacterDefinition>();
            if (Player == null)
                return list;

            foreach (var ch in _db.Characters)
            {
                if (ch == null || string.IsNullOrEmpty(ch.Id))
                    continue;
                if (Player.Age < ch.MinAge)
                    continue;
                if (!string.IsNullOrEmpty(ch.MeetTag) && !Player.HasTag(ch.MeetTag))
                    continue;
                if (!string.IsNullOrEmpty(ch.ExcludeTag) && Player.HasTag(ch.ExcludeTag))
                    continue;
                list.Add(ch);
            }

            return list;
        }

        void HandleConfessPick(string choiceId)
        {
            _pickingConfess = false;
            _pendingChoices.Clear();
            Phase = GamePhase.Playing;

            if (string.IsNullOrEmpty(choiceId) || choiceId == "confess_cancel")
            {
                Log("你把那句话又咽了回去。");
                RaiseState();
                return;
            }

            string id = choiceId.StartsWith("cp:", StringComparison.OrdinalIgnoreCase)
                ? choiceId.Substring(3)
                : choiceId;
            if (!_db.TryGetCharacter(id, out var ch) || ch == null)
            {
                RaiseState();
                return;
            }

            Player.PendingConfessId = ch.Id;
            Player.PendingConfessName = ch.Name;
            Log($"你决定下一季向{ch.Name}告白。");
            Player.AppendHistory($"{FormatMoment()} · 决定向{ch.Name}告白");
            RaiseState();
        }

        bool TryEnqueuePendingConfess()
        {
            var pendingId = Player.PendingConfessId;
            if (string.IsNullOrEmpty(pendingId))
                return false;

            Player.PendingConfessId = null;
            Player.PendingConfessName = null;
            if (!_db.TryGetCharacter(pendingId, out var ch) || ch == null)
                return false;

            if (!string.IsNullOrEmpty(ch.ExcludeTag) && Player.HasTag(ch.ExcludeTag))
            {
                Log($"你想向{ch.Name}告白，可那个人已经不在了。这一季风很轻。");
                return false;
            }

            bool success = _rng.Next(100) < RollConfessChance(ch);
            string eventId = success ? $"confess_{ch.Id}_ok" : $"confess_{ch.Id}_no";
            if (!_db.TryGetEvent(eventId, out var evt) || evt == null)
            {
                Debug.LogWarning($"[LifeSim] Missing confession event '{eventId}'.");
                Log($"你想向{ch.Name}告白，话却卡在喉咙里。");
                return false;
            }

            _yearQueue.Enqueue(evt);
            return true;
        }

        int RollConfessChance(CharacterDefinition ch)
        {
            int favor = Player.GetFavor(ch.Id);
            int luck = Player.GetAttr("luck");
            int chance = 18 + favor * 6 / 10 + luck * 3;
            if (Player.HasTag("together_" + ch.Id))
                chance += 20;
            if (Player.HasTag("married") && !string.Equals(ch.Id, "spouse", StringComparison.OrdinalIgnoreCase))
                chance -= 18;
            if (string.Equals(ch.Id, "puku", StringComparison.OrdinalIgnoreCase))
                chance += 12;
            return Mathf.Clamp(chance, 8, 92);
        }

        void AdvanceSeasonInternal(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (Player.Season == Season.Winter)
                {
                    Player.Season = Season.Spring;
                    Player.Age += 1;
                }
                else
                {
                    Player.Season = SeasonUtil.Next(Player.Season);
                }

                foreach (var expired in Player.TickBuffs())
                    Log($"【{expired.Name}】的影响渐渐淡去。");
            }
        }

        void ResolveSeasonEvents()
        {
            if (_selector == null || Player == null || Player.InStory)
                return;

            _yearQueue.Clear();
            if (TryEnqueuePendingConfess())
            {
                ProcessNextInQueue();
                return;
            }

            var events = _selector.PickForSeason(Player, 1);
            foreach (var e in events)
                _yearQueue.Enqueue(e);

            if (_yearQueue.Count == 0)
            {
                Log($"{FormatMoment()}：平淡的一季。");
                RaiseState();
                CheckEndAfterResolve();
                return;
            }

            ProcessNextInQueue();
        }

        string FormatMoment()
        {
            return $"{Player.Age}岁·{SeasonUtil.ToDisplay(Player.Season)}";
        }

        public void Choose(string choiceId)
        {
            if (Phase == GamePhase.StoryAwaitingChoice)
            {
                ChooseStory(choiceId);
                return;
            }

            if (Phase == GamePhase.AwaitingChoice && _pickingConfess)
            {
                HandleConfessPick(choiceId);
                return;
            }

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

            Log($"{FormatMoment()}：{resultText}");
            Player.AppendHistory($"{FormatMoment()} · {branch.Label} → {(success ? "成功" : "失败")}");

            var sourceEvent = PendingEvent;
            MarkEventTriggered(sourceEvent);
            EffectExecutor.ApplyTags(sourceEvent.AddTags, Player, _db);
            bool killedByEvent = EffectExecutor.RollKill(sourceEvent.KillChance, Player, _rng);
            if (Player.Alive)
                ApplyEffects(effects);
            bool killedByChoice = !Player.Alive && !killedByEvent;

            string startStory = sourceEvent.StartStory;
            PendingEvent = null;
            _pendingChoices.Clear();
            Phase = GamePhase.Playing;

            if (!Player.Alive)
            {
                EndLife(killedByChoice
                    ? FormatChoiceDeath(sourceEvent, branch, resultText)
                    : FormatEventDeath(sourceEvent));
                return;
            }

            _afterChoiceContinue = () => ContinueAfterWorldChoice(startStory);
            RaiseState();
        }

        void ContinueAfterWorldChoice(string startStory)
        {
            if (!string.IsNullOrEmpty(startStory) && TryBeginStory(startStory))
                return;

            ProcessNextInQueue();
            if (Phase == GamePhase.Playing && Player != null && Player.Alive && !Player.InStory)
                AdvanceSeason();
        }

        void ChooseStory(string choiceId)
        {
            if (PendingStoryStep == null || !_db.TryGetBranch(choiceId, out var branch))
                return;

            bool success = string.IsNullOrWhiteSpace(branch.Check) ||
                           ConditionParser.Evaluate(branch.Check, Player, luckSoftensThreshold: true);

            string resultText = success ? branch.SuccessText : branch.FailText;
            string effects = success ? branch.SuccessEffects : branch.FailEffects;
            if (string.IsNullOrWhiteSpace(resultText))
                resultText = success ? "事情顺利完成了。" : "事情没有如愿。";

            Log($"{FormatMoment()}：{resultText}");
            Player.AppendHistory($"{FormatMoment()} · {branch.Label} → {(success ? "成功" : "失败")}");

            ApplyEffects(effects);
            _pendingChoices.Clear();
            Phase = GamePhase.InStory;

            if (!Player.Alive)
            {
                var step = PendingStoryStep;
                ClearStory();
                EndLife(FormatChoiceDeath(null, branch, resultText, step));
                return;
            }

            var chosen = branch;
            var fromStep = PendingStoryStep;
            _afterChoiceContinue = () => ContinueAfterStoryChoice(chosen, fromStep);
            RaiseState();
        }

        void ContinueAfterStoryChoice(BranchDefinition branch, StoryStepDefinition fromStep)
        {
            if (branch != null && branch.EndStory)
            {
                FinishStory();
                return;
            }

            if (branch != null && !string.IsNullOrEmpty(branch.GotoStep) &&
                _db.TryGetStoryStep(branch.GotoStep, out var gotoStep))
            {
                if (fromStep != null && fromStep.AdvanceSeason > 0)
                {
                    AdvanceSeasonInternal(fromStep.AdvanceSeason);
                    Log($"（时光流转至 {FormatMoment()}）");
                }

                var allowed = AllowStepOrNext(gotoStep);
                if (allowed == null)
                {
                    FinishStory();
                    return;
                }

                ShowStoryStep(allowed, allowRandom: false);
                return;
            }

            ResolveStoryStepAftermath(fromStep, forceEnd: false);
        }

        public string BuildSummary()
        {
            if (Player == null)
                return string.Empty;

            string death = string.IsNullOrEmpty(Player.DeathCause)
                ? string.Empty
                : Player.DeathCause + "\n\n";

            string favors = Player.FormatFavors();
            return $"享年 {Player.Age} 岁 · {SeasonUtil.ToDisplay(Player.Season)}\n\n" +
                   death +
                   $"力量 {Player.GetAttr("str")} / 智力 {Player.GetAttr("int")} / 运气 {Player.GetAttr("luck")} / 家境 {Player.GetAttr("family")}\n" +
                   (string.IsNullOrEmpty(favors) ? string.Empty : favors + "\n") +
                   (Player.Buffs.Count > 0 ? Player.FormatBuffs() + "\n" : string.Empty) +
                   $"经历条目 {Player.History.Count} 条";
        }

        void ProcessNextInQueue()
        {
            if (!Player.Alive)
            {
                EndLife(Player.DeathCause ?? FormatUnknownDeath());
                return;
            }

            if (Player.InStory)
                return;

            if (_yearQueue.Count == 0)
            {
                CheckEndAfterResolve();
                return;
            }

            var evt = _yearQueue.Dequeue();
            PendingEvent = evt;
            Log($"{FormatMoment()}：{evt.Text}");

            var choices = _db.GetBranchesForEvent(evt);
            if (choices.Count > 0)
            {
                MarkEventTriggered(evt);
                _pendingChoices.Clear();
                _pendingChoices.AddRange(choices);
                Phase = GamePhase.AwaitingChoice;
                RaiseState();
                OnAwaitingChoice?.Invoke();
                return;
            }

            ApplyEventBase(evt);
            string startStory = evt.StartStory;
            PendingEvent = null;
            RaiseState();

            if (!Player.Alive)
            {
                EndLife(Player.DeathCause ?? FormatEventDeath(evt));
                return;
            }

            if (!string.IsNullOrEmpty(startStory) && TryBeginStory(startStory))
                return;

            ProcessNextInQueue();
        }

        void ApplyEventBase(EventDefinition evt)
        {
            MarkEventTriggered(evt);
            EffectExecutor.ApplyTags(evt.AddTags, Player, _db);
            bool killed = EffectExecutor.RollKill(evt.KillChance, Player, _rng);
            if (Player.Alive)
                ApplyEffects(evt.Effects);
            Player.AppendHistory($"{FormatMoment()} · {evt.Id}");
            if (killed || !Player.Alive)
                Player.DeathCause = FormatEventDeath(evt);
        }

        void ApplyEffects(string effects)
        {
            EffectExecutor.Apply(effects, Player, _rng, _db);
            FlushBuffLogs();
        }

        void FlushBuffLogs()
        {
            if (Player == null)
                return;

            foreach (var line in Player.DrainBuffLogs())
                Log(line);
        }

        void MarkEventTriggered(EventDefinition evt)
        {
            if (evt != null && evt.TriggersOnce)
                Player.TriggeredOnceEvents.Add(evt.Id);
        }

        bool TryBeginStory(string storyId)
        {
            if (string.IsNullOrEmpty(storyId) || !_db.TryGetStory(storyId, out var story))
                return false;

            if (story.Once && Player.CompletedStories.Contains(storyId))
                return false;

            var first = _db.GetFirstStoryStep(storyId);
            if (first == null)
                return false;

            _yearQueue.Clear();
            PendingEvent = null;
            _resumeAfterRandom = null;
            Player.ActiveStoryId = storyId;
            Log($"—— 进入剧情线：{story.Title} ——");
            Player.AppendHistory($"剧情开始 · {story.Title}");
            ShowStoryStep(first);
            return true;
        }

        void ContinueStory()
        {
            if (Phase != GamePhase.InStory || PendingStoryStep == null || !Player.Alive)
                return;

            // Reaching here means the current step had no choices and was already shown;
            // "继续" applies aftermath and moves forward.
            ResolveStoryStepAftermath(PendingStoryStep, forceEnd: PendingStoryStep.End);
        }

        void ShowStoryStep(StoryStepDefinition step)
        {
            ShowStoryStep(step, allowRandom: true);
        }

        void ShowStoryStep(StoryStepDefinition step, bool allowRandom)
        {
            if (step == null)
                return;

            if (allowRandom && !step.IsRandom && !step.End)
            {
                var inserted = TryPickRandomStoryBeat(step);
                if (inserted != null)
                {
                    _resumeAfterRandom = step.StepId;
                    step = inserted;
                }
            }

            PendingStoryStep = step;
            Player.ActiveStoryStepId = step.StepId;
            Log($"{FormatMoment()}：{step.Text}");

            var choices = _db.GetBranchesForStoryStep(step);
            if (choices.Count > 0)
            {
                _pendingChoices.Clear();
                _pendingChoices.AddRange(choices);
                Phase = GamePhase.StoryAwaitingChoice;
                RaiseState();
                OnAwaitingChoice?.Invoke();
                return;
            }

            ApplyEffects(step.Effects);
            Player.AppendHistory($"{FormatMoment()} · story:{step.StepId}");
            Phase = GamePhase.InStory;
            RaiseState();

            if (!Player.Alive)
            {
                var deadStep = step;
                ClearStory();
                EndLife(FormatStoryDeath(deadStep));
            }
        }

        StoryStepDefinition TryPickRandomStoryBeat(StoryStepDefinition planned)
        {
            if (Player == null || string.IsNullOrEmpty(Player.ActiveStoryId))
                return null;
            if (!string.IsNullOrEmpty(_resumeAfterRandom))
                return null;
            if (_rng.Next(100) >= 18)
                return null;

            var pool = _db.GetRandomStorySteps(Player.ActiveStoryId);
            var eligible = new List<StoryStepDefinition>();
            int total = 0;
            foreach (var beat in pool)
            {
                if (beat == null || beat.StepId == planned.StepId)
                    continue;
                if (Player.TriggeredOnceEvents.Contains("sr:" + beat.StepId))
                    continue;
                if (!ConditionParser.Evaluate(beat.Require, Player))
                    continue;
                eligible.Add(beat);
                total += beat.Weight;
            }

            if (eligible.Count == 0 || total <= 0)
                return null;

            int roll = _rng.Next(total);
            int acc = 0;
            StoryStepDefinition picked = eligible[eligible.Count - 1];
            foreach (var beat in eligible)
            {
                acc += beat.Weight;
                if (roll < acc)
                {
                    picked = beat;
                    break;
                }
            }

            Player.TriggeredOnceEvents.Add("sr:" + picked.StepId);
            Log("—— 旅途中的插曲 ——");
            return picked;
        }

        void ResolveStoryStepAftermath(StoryStepDefinition step, bool forceEnd)
        {
            if (step == null)
                return;

            if (step.AdvanceSeason > 0)
            {
                AdvanceSeasonInternal(step.AdvanceSeason);
                Log($"（时光流转至 {FormatMoment()}）");
            }

            if (forceEnd || step.End)
            {
                FinishStory();
                return;
            }

            if (step.IsRandom && !string.IsNullOrEmpty(_resumeAfterRandom) &&
                _db.TryGetStoryStep(_resumeAfterRandom, out var resume))
            {
                _resumeAfterRandom = null;
                var allowed = AllowStepOrNext(resume);
                if (allowed == null)
                {
                    FinishStory();
                    return;
                }

                ShowStoryStep(allowed, allowRandom: false);
                return;
            }

            var next = FindNextAllowedStep(step);
            if (next == null)
            {
                FinishStory();
                return;
            }

            ShowStoryStep(next);
        }

        StoryStepDefinition AllowStepOrNext(StoryStepDefinition step)
        {
            if (step != null && !step.IsRandom && ConditionParser.Evaluate(step.Require, Player))
                return step;
            return FindNextAllowedStep(step);
        }

        StoryStepDefinition FindNextAllowedStep(StoryStepDefinition step)
        {
            var next = _db.GetNextStoryStep(step);
            int guard = 0;
            while (next != null && guard++ < 48)
            {
                if (!next.IsRandom && ConditionParser.Evaluate(next.Require, Player))
                    return next;
                next = _db.GetNextStoryStep(next);
            }

            return null;
        }

        void FinishStory()
        {
            var storyId = Player.ActiveStoryId;
            if (!string.IsNullOrEmpty(storyId))
            {
                Player.CompletedStories.Add(storyId);
                if (_db.TryGetStory(storyId, out var story))
                    Log($"—— 剧情线结束：{story.Title} ——");
                else
                    Log("—— 剧情线结束 ——");
                Player.AppendHistory($"剧情结束 · {storyId}");
            }

            ClearStory();
            Phase = GamePhase.Playing;
            RaiseState();
            CheckEndAfterResolve();
        }

        void ClearStory()
        {
            Player.ActiveStoryId = null;
            Player.ActiveStoryStepId = null;
            PendingStoryStep = null;
            _resumeAfterRandom = null;
            _pendingChoices.Clear();
            _afterChoiceContinue = null;
        }

        void CheckEndAfterResolve()
        {
            if (!Player.Alive)
            {
                EndLife(Player.DeathCause ?? FormatUnknownDeath());
                return;
            }

            if (Player.Age >= MaxAge && Player.Season == Season.Winter)
                EndLife(FormatOldAgeDeath());
        }

        void EndLife(string reason)
        {
            if (Phase == GamePhase.Ended)
                return;

            Phase = GamePhase.Ended;
            Player.Alive = false;
            if (string.IsNullOrWhiteSpace(reason))
                reason = FormatUnknownDeath();
            Player.DeathCause = reason;
            ClearStory();
            _afterChoiceContinue = null;
            Log("—— 人生结束 ——");
            Log(reason);
            RaiseState();
            OnEnded?.Invoke();
        }

        string FormatOldAgeDeath()
        {
            return $"死因：年满 {MaxAge} 岁，在{FormatMoment()}安然走到了人生尽头。";
        }

        string FormatUnknownDeath()
        {
            return WithBuffRisk($"死因：{FormatMoment()}，身体突然撑不住了。");
        }

        string FormatEventDeath(EventDefinition evt)
        {
            string what = evt != null && !string.IsNullOrWhiteSpace(evt.Text)
                ? TrimStop(evt.Text)
                : "这一季的变故";
            return WithBuffRisk($"死因：{FormatMoment()}，没能撑过「{what}」。");
        }

        string FormatStoryDeath(StoryStepDefinition step)
        {
            string storyTitle = "这段经历";
            if (step != null && _db.TryGetStory(step.StoryId, out var story) &&
                !string.IsNullOrEmpty(story.Title))
                storyTitle = story.Title;

            string what = step != null && !string.IsNullOrWhiteSpace(step.Text)
                ? TrimStop(step.Text)
                : "途中的变故";
            return WithBuffRisk($"死因：{FormatMoment()}，在「{storyTitle}」中没能撑过「{what}」。");
        }

        string FormatChoiceDeath(EventDefinition evt, BranchDefinition branch, string resultText,
            StoryStepDefinition step = null)
        {
            string choice = branch != null && !string.IsNullOrWhiteSpace(branch.Label)
                ? branch.Label
                : "那个选择";
            string scene;
            if (evt != null && !string.IsNullOrWhiteSpace(evt.Text))
                scene = TrimStop(evt.Text);
            else if (step != null && !string.IsNullOrWhiteSpace(step.Text))
                scene = TrimStop(step.Text);
            else
                scene = "那件事";

            string outcome = string.IsNullOrWhiteSpace(resultText)
                ? string.Empty
                : $"结果是：{TrimStop(resultText)}。";
            return WithBuffRisk($"死因：{FormatMoment()}，面对「{scene}」时选择了「{choice}」。{outcome}你因此没能活下来。");
        }

        string WithBuffRisk(string reason)
        {
            if (Player == null || Player.Buffs == null || Player.Buffs.Count == 0)
                return reason;

            var names = new List<string>();
            for (int i = 0; i < Player.Buffs.Count; i++)
            {
                var buff = Player.Buffs[i];
                if (buff != null && buff.KillChance > 0f)
                    names.Add(buff.Name);
            }

            if (names.Count == 0)
                return reason;

            return reason + $" 当时你带着【{string.Join("】【", names)}】，让这次更加危险。";
        }

        static string TrimStop(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var s = text.Trim();
            while (s.EndsWith("。") || s.EndsWith(".") || s.EndsWith("！") || s.EndsWith("!") ||
                   s.EndsWith("？") || s.EndsWith("?"))
                s = s.Substring(0, s.Length - 1).TrimEnd();
            return s;
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
