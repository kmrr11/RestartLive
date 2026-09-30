using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LifeSim.Core;
using UnityEngine;

static class LiseStoryChecks
{
    static TextAsset Read(string name) => new(File.ReadAllText("Assets/Resources/Data/" + name + ".txt"));
    static GameSession Create()
    {
        var s = new GameSession(42);
        s.Initialize(Read("Events"), Read("Branches"), Read("Stories"), Read("StorySteps"), Read("Buffs"), Read("Characters"));
        return s;
    }
    static void Check(bool value, string message) { if (!value) throw new Exception("Lise: " + message); }
    static void ReplayCheck(GameSession s)
    {
        var copy = Create();
        copy.Replay(s.ExportJournal());
        Check(s.Transcript.SequenceEqual(copy.Transcript), "replay transcript differs");
        Check(s.Phase == copy.Phase && s.AwaitingContinue == copy.AwaitingContinue &&
            s.Player.ActiveStoryStepId == copy.Player.ActiveStoryStepId &&
            s.Player.Age == copy.Player.Age && s.Player.Season == copy.Player.Season &&
            s.Player.Alive == copy.Player.Alive && s.Player.GetFavor("lise") == copy.Player.GetFavor("lise") &&
            s.Player.Tags.SetEquals(copy.Player.Tags) &&
            s.PendingChoices.Select(c => c.ChoiceId).SequenceEqual(copy.PendingChoices.Select(c => c.ChoiceId)),
            "replay state differs");
    }
    public static void Run()
    {
        var adult = new PlayerState { Age = 20, Luck = 10 };
        Check(LifeSim.Data.ConditionParser.Evaluate("age>=16", adult), "age condition must read actual age");
        adult.Age = 15;
        Check(!LifeSim.Data.ConditionParser.Evaluate("age>=16", adult, true), "luck must not waive adult gate");
        foreach (string scenario in new[] { "hide", "return", "return_alt", "direct", "skip", "reject", "existing", "apart", "wander" })
        {
            string route = scenario.Replace("_alt", "");
            bool alternate = scenario.EndsWith("_alt", StringComparison.Ordinal);
            var s = Create();
            s.Perform("start");
            var visited = new List<string>();
            bool configured = false;
            bool injectedState = route == "reject" || route == "existing" || route == "apart";
            bool tracking = false;
            for (int action = 0; action < 900 && !s.Player.CompletedStories.Contains("isekai_arc") && s.Phase != GamePhase.Ended; action++)
            {
                string step = s.PendingStoryStep?.StepId;
                Check(!s.Player.HasTag("lise_war_active") || step == null || !step.StartsWith("iw_rd_", StringComparison.Ordinal),
                    "lighthearted random insert interrupted war branch");
                if (step != null && !visited.Contains(step)) visited.Add(step);
                if (step == "iw_true_page" && !configured)
                {
                    configured = true;
                    tracking = true;
                    if (route == "reject") { s.Player.AddFavor("lise", -100); s.Player.Luck = 0; }
                    if (route == "existing") { s.Player.AddTag("confessed_lise"); s.Player.AddTag("together_lise"); }
                    if (route == "apart") { s.Player.AddTag("confessed_lise"); s.Player.AddTag("together_lise"); s.Player.AddTag("apart_lise"); }
                }
                if (tracking && !injectedState) ReplayCheck(s);
                if (s.PendingChoices.Count == 0) { s.Perform("advance"); continue; }
                string wanted = step switch
                {
                    "iw_class" => "iw_mage",
                    "iw_audience" => route == "wander" ? "iw_refuse_hero" : "iw_accept_hero",
                    "iw_dance" => "iw_dance_yes",
                    "iw_ambush" => "iw_protect",
                    "iw_lise_confession" => route == "skip" ? "lise_confess_wait" : "lise_confess_now",
                    "lise_war_choice" => route == "direct" ? "lise_return_start" : "lise_escape_start",
                    "lise_escape_decision" => route == "hide" ? "lise_keep_hiding" : "lise_rejoin_army",
                    "lise_reorganize" => alternate ? "lise_rally_squads" : "lise_rebuild_supply",
                    "lise_final_assault" => alternate ? "lise_cover_advance" : "lise_break_ward",
                    "lise_last_words" => alternate ? "lise_say_goodbye" : "lise_hold_hand",
                    _ => s.PendingChoices[0].ChoiceId
                };
                Check(s.PendingChoices.Any(c => c.ChoiceId == wanted), "missing choice " + wanted);
                s.Perform("choose", wanted);
            }
            Check(s.Player.CompletedStories.Contains("isekai_arc"), route + " failed to complete");
            if (!injectedState) ReplayCheck(s);
            bool romance = route == "hide" || route == "return" || route == "direct" || route == "existing";
            Check(visited.Contains("lise_war_choice") == romance, route + " incorrect escape gate");
            if (route == "hide" || route == "return" || route == "direct")
            {
                Check(visited.Contains("iw_lise_confession"), "missing in-story confession");
                Check(s.Player.HasTag("confessed_lise"), "confession must be voluntary and recorded");
            }
            if (route == "existing" || route == "apart" || route == "wander")
                Check(!visited.Contains("iw_lise_confession"), "invalid repeated confession offer");
            if (!romance)
            {
                Check(visited.Contains("iw_castle"), "ordinary route must remain reachable");
                Check(!s.Player.HasTag("lise_escape") && !s.Player.HasTag("kingdom_retreat"), "ordinary route polluted");
                if (route != "apart") Check(!s.Player.HasTag("together_lise"), "unrequested romance");
            }
            else
            {
                Check(!visited.Contains("iw_castle") && !visited.Contains("iw_demon"), "war branch leaked into tea ending");
                Check(visited.IndexOf("lise_war_choice") > visited.IndexOf("iw_true_page"), "wrong insertion point");
                if (route != "direct")
                {
                    Check(visited.IndexOf("lise_retreat_news") < visited.IndexOf("lise_refugees") &&
                        visited.IndexOf("lise_refugees") < visited.IndexOf("lise_escape_decision"), "defeats must precede player decision");
                    Check(s.Player.HasTag("kingdom_retreat"), "missing defeat consequence");
                }
                else Check(!visited.Contains("lise_retreat_news"), "direct route falsely reports desertion");
                if (route == "hide")
                {
                    Check(s.Phase == GamePhase.Ended && s.Player.Age >= 80, "hidden life must finish in other world");
                    Check(s.Player.HasTag("lise_hidden_life") && s.Player.HasTag("kingdom_fallen"), "missing hiding consequence");
                    Check(!s.Player.HasTag("lise_farewell_memory") && !s.Player.HasTag("isekai_done") && !s.Player.HasTag("demon_defeated"), "hiding must not defeat demon or return home");
                    Check(s.Player.HasTag("together_lise"), "hiding must preserve relationship");
                    Check(NarrativeRules.BuildBiography(s.Player).Contains("边境"), "hidden ending missing from biography");
                }
                else
                {
                    Check(s.Phase == GamePhase.Playing && s.Player.Alive, "homecoming should resume normal life");
                    Check(s.Player.HasTag("demon_defeated") && s.Player.HasTag("isekai_done") &&
                        s.Player.HasTag("lise_farewell_memory") && s.Player.HasTag("apart_lise") &&
                        !s.Player.HasTag("together_lise") && !s.Player.HasTag("bond_lise"), "victory must lead to separation");
                    Check(visited.IndexOf("lise_victory") < visited.IndexOf("lise_last_words"), "farewell before victory");
                    Check(NarrativeRules.GetRelationshipStage(s.Player, "lise") == "异界离别", "farewell must not read as breakup");
                }
            }
            Console.WriteLine("PASS: Lise main branch " + scenario + (injectedState ? " (gate fixture)" : " and replay"));
        }
    }
}
