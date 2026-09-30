using System;
using System.IO;
using LifeSim.Core;
using UnityEngine;

static class Program
{
    static TextAsset Read(string name) => new(File.ReadAllText("Assets/Resources/Data/" + name + ".txt"));
    static GameSession Create(int seed)
    {
        var session = new GameSession(seed);
        session.Initialize(Read("Events"),Read("Branches"),Read("Stories"),Read("StorySteps"),Read("Buffs"),Read("Characters"));
        return session;
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Main()
    {
        LiseStoryChecks.Run();
        var db = new LifeSim.Data.EventDatabase();
        db.Load(Read("Events"), Read("Branches"));
        var selector = new LifeSim.Event.EventSelector(db, new Random(1));
        var partner = new PlayerState { Age = 30, Family = 10, Luck = 10 };
        bool CanMarry() => selector.PickForSeason(partner, db.Events.Count).Exists(e => e.Id == "marriage");
        Require(!CanMarry(), "Attributes alone must not create a spouse");
        partner.AddTag("together_lise");
        Require(!CanMarry(), "Relationship without active confession must not create a spouse");
        partner.AddTag("confessed_lise");
        Require(CanMarry(), "Successful active confession must permit marriage");
        partner.AddTag("apart_lise");
        Require(!CanMarry(), "Separated partner must not permit marriage");
        Require(File.ReadAllText("Assets/Data/Events.csv") == Read("Events").text, "Event mirrors differ");
        Console.WriteLine("PASS: marriage requires a successful active confession and an ongoing relationship");
        var timing = new GameSession(7);
        timing.Initialize(new TextAsset("id,ageMin,ageMax,weight,text\n"), new TextAsset("choiceId,eventId\n"));
        timing.Perform("start");
        int age = timing.Player.Age;
        timing.Perform("advance");
        Require(timing.Player.Age == age && timing.Player.Season == Season.Autumn, "First advance must reach second half");
        timing.Perform("advance");
        Require(timing.Player.Age == age + 1 && timing.Player.Season == Season.Spring, "Two advances must complete a year");
        Require(SeasonUtil.AllowsHalfYear(SeasonUtil.ParseMask("夏"), Season.Spring), "Summer events remain reachable");
        Require(SeasonUtil.AllowsHalfYear(SeasonUtil.ParseMask("冬"), Season.Autumn), "Winter events remain reachable");
        Console.WriteLine("PASS: two normal advances per year and seasonal event coverage");
        var session = Create(12345);
        session.Perform("start");
        for (int i=0;i<700 && session.Phase != GamePhase.Ended;i++)
        {
            var save = session.ExportJournal();
            var restored = Create(save.Seed);
            restored.Replay(save);
            Require(string.Join("\n",session.Transcript) == string.Join("\n",restored.Transcript), "Replay logs differ");
            Require(session.Phase == restored.Phase && session.AwaitingContinue == restored.AwaitingContinue, "Replay phase differs");
            if (session.PendingChoices.Count > 0) session.Perform("choose",session.PendingChoices[0].ChoiceId);
            else session.Perform("advance");
        }
        Require(session.Phase == GamePhase.Ended,"Life must terminate");
        foreach (var story in new[] { "gaokao_arc", "isekai_arc", "superpower_arc" })
        {
            var probe = new GameSession(42);
            probe.Initialize(new TextAsset("id,ageMin,ageMax,weight,text,startStory\nentry,0,0,100,entry," + story), Read("Branches"), Read("Stories"), Read("StorySteps"), Read("Buffs"), Read("Characters"));
            probe.Perform("start");
            Require(probe.Player.InStory, "Story failed to enter: " + story);
            for (int i = 0; i < 250 && probe.Player.InStory; i++)
            {
                if (probe.PendingChoices.Count > 0) probe.Perform("choose", probe.PendingChoices[0].ChoiceId);
                else probe.Perform("advance");
            }
            Require(probe.Player.CompletedStories.Contains(story), "Story failed to complete: " + story);
            Console.WriteLine("PASS: story entry through completion: " + story);
        }
        Console.WriteLine("PASS: deterministic life and replay at every action boundary");
    }

}
