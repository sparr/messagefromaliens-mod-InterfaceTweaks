using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace InterfaceTweaks
{
    // On the dictionary page, a button at the left end of the right menu lists in the left panel
    // every sentence the player can see that holds the word whose definition the left panel
    // shows.
    //
    // The pages the player can see are found as the game shows them: from the home menu through
    // every menu line whose gate passes, with each such page's explanation, rule, and skill graph
    // pages, the message and quiz explanations the game would open, the welcome message, the win
    // pages the player has unlocked, and the player's notes once the note editor is unlocked.
    // Their text comes from Global.GetInfo, as the game draws it, so lines other mods add to it
    // are included.
    internal static class DictionarySearch
    {
        // The button's glyph, which the game's own click handling ignores in the right menu.
        internal const string Button = "see";
        // The results' title: these glyphs, then the word.
        private const string TitlePrefix = "sentence with";
        private const string Home = "home";
        private const string Welcome = "welcome de message";
        // Pages the explanation, rule, and skill graph buttons open: the page's name and one of
        // these.
        private static readonly string[] HelpSuffixes = { GameText.ExplanationSuffix, " de rule", " de skill graph" };
        // The explanations Draw.GetHelp opens on message and quiz pages, before and after its
        // count of geometry quiz wins.
        private const string MessageHelp = "message de tutorial";
        private const string QuizHelp = "riddle de tutorial";
        private const string LaterHelp = " 2";
        private const string LaterHelpChapter = "geometry riddle";
        private const int LaterHelpWins = 5;
        // Draw.UpdateText shows this page's text in place of the settings page's own until any of
        // these chapters' quiz wins reach their counts.
        private const string SettingsPage = "modify game";
        private const string EarlySettingsText = "modify game 0";
        private static readonly (string Chapter, int Wins)[] SettingsWins =
        {
            ("arithmetic riddle", 11), ("mechanics riddle", 5), ("electricity riddle", 3), ("thermology riddle", 2),
        };

        private static readonly string[] Breaks = { GameText.Newline, GameText.Newpage };

        private static bool buttonShown;
        // A left click on the button this frame, handled after the game's Update.
        private static bool clicked;

        // The word whose definition the left panel shows, or whose results it shows.
        private static string SelectedWord(Draw d)
        {
            var title = d.left_title;
            if (string.IsNullOrEmpty(title) || Global.alien_text == null)
            {
                return null;
            }
            title = Global.RemoveBracket(title, removeminus: false);
            if (title.StartsWith(TitlePrefix + " ", StringComparison.Ordinal))
            {
                title = title.Substring(TitlePrefix.Length + 1);
            }
            return Global.alien_text.ContainsKey(title) ? title : null;
        }

        // At the right menu's left end, while the title row has a cell for it.
        internal static void WritePrefix(Draw __instance, ref string s, string sec)
        {
            try
            {
                if (sec != GameText.RightMenu || s == null)
                {
                    return;
                }
                buttonShown = false;
                var d = __instance;
                if (!Plugin.SearchButton.Value || d == null || d.current_page != GameText.Dictionary || SelectedWord(d) == null)
                {
                    return;
                }
                var cells = s.Split(' ').Count(GameText.TakesCell) + (d.right_title ?? "").Split(' ').Count(GameText.TakesCell);
                if (cells >= GameText.RowWidth)
                {
                    return;
                }
                s = Button + " " + s;
                buttonShown = true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Dictionary search: {e}");
            }
        }

        internal static void UpdatePrefix(Draw __instance)
        {
            clicked = false;
            try
            {
                if (!buttonShown || !Input.GetMouseButtonDown(0) || __instance?.current_page != GameText.Dictionary)
                {
                    return;
                }
                var cam = Camera.main;
                if (cam == null || !Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out var hit))
                {
                    return;
                }
                var ac = hit.transform.GetComponentInParent<AlienCharacter>();
                clicked = ac != null && ac.sector == GameText.RightMenu && ac.alien_txt == Button;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Dictionary search: {e}");
            }
        }

        // After the game's Update, while Calculator still counts the frame as a click, so a push
        // of the entry under its calculator closes it.
        internal static void UpdatePostfix(Draw __instance)
        {
            if (!clicked)
            {
                return;
            }
            clicked = false;
            try
            {
                var d = __instance;
                var word = d == null ? null : SelectedWord(d);
                if (word == null)
                {
                    return;
                }
                var found = Sentences().Where(line => line.Contains(word)).Select(line => string.Join(" ", line)).Distinct().ToList();
                Plugin.Log.LogInfo($"Dictionary search: {found.Count} sentence(s)");
                d.PushLeftText(TitlePrefix + " " + word, found.Count == 0 ? GameText.Gap : string.Join(" " + GameText.Newline + " ", found));
                d.UpdateText();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Dictionary search: {e}");
            }
        }

        // Every line of text the player can see, as the tokens to draw it with.
        private static IEnumerable<List<string>> Sentences()
        {
            foreach (var page in VisiblePages())
            {
                foreach (var line in Lines(PageText(page), gated: true))
                {
                    yield return line;
                }
            }
            if (Global.savedata?.notebook2 != null && Global.GetWin("last-riddle"))
            {
                foreach (var note in Global.savedata.notebook2.Values)
                {
                    foreach (var line in Lines(note, gated: false))
                    {
                        yield return line;
                    }
                }
            }
        }

        // The pages in the order the player meets them, each page's help pages after it.
        private static List<string> VisiblePages()
        {
            var seen = new HashSet<string>();
            var order = new List<string>();
            var queue = new Queue<string>();
            void Add(string page)
            {
                if (!string.IsNullOrEmpty(page) && seen.Add(page))
                {
                    order.Add(page);
                    queue.Enqueue(page);
                }
            }
            Add(Welcome);
            Add(Home);
            var quizzes = false;
            while (queue.Count > 0)
            {
                var page = queue.Dequeue();
                quizzes |= IsQuiz(page);
                foreach (var suffix in HelpSuffixes)
                {
                    if (!string.IsNullOrEmpty(Global.GetInfo(GameText.InfoCategory, page + suffix)))
                    {
                        Add(page + suffix);
                    }
                }
                if (Global.link_message == null || !Global.link_message.ContainsKey(page))
                {
                    continue;
                }
                foreach (var line in Lines(PageText(page), gated: true))
                {
                    Add(Global.RemoveBracket(string.Join(" ", line)));
                }
            }
            var later = Global.CountWin(LaterHelpChapter) >= LaterHelpWins && !Global.isDemo ? LaterHelp : "";
            Add(MessageHelp + later);
            if (quizzes)
            {
                Add(QuizHelp + later);
            }
            foreach (var page in WinPages.Shown())
            {
                Add(page);
            }
            return order;
        }

        // As Draw.CurrentPageIsRiddle and Draw.GetHelp tell quiz pages.
        private static bool IsQuiz(string page) =>
            page.Contains("riddle") && page != "bonus arithmetic riddle" && page != "last riddle";

        // A page's text as Draw.UpdateText gets it for the right panel.
        private static string PageText(string page)
        {
            if (page == SettingsPage && SettingsWins.All(w => Global.CountWin(w.Chapter) < w.Wins))
            {
                page = EarlySettingsText;
            }
            return Global.GetInfo(GameText.InfoCategory, Global.MergeString(Global.ReverseParse(page))) ?? "";
        }

        // The lines of the text with a glyph to draw, as tokens, leaving out lines whose gate has
        // not passed, as Draw.Write does: every line after the first whose first token is a gate.
        // Gates and button marks are left out, and answer slots become empty cells, since the
        // game adds every slot it draws to the quiz's slots.
        private static IEnumerable<List<string>> Lines(string text, bool gated)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }
            var lines = new List<List<string>> { new List<string>() };
            foreach (var token in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Breaks.Contains(token))
                {
                    lines.Add(new List<string>());
                }
                else
                {
                    lines[lines.Count - 1].Add(token);
                }
            }
            for (var index = 0; index < lines.Count; index++)
            {
                var tokens = lines[index];
                if (tokens.Count == 0)
                {
                    continue;
                }
                var first = tokens[0];
                if (gated && index > 0 && first.StartsWith("[") && first.Length >= 2 && !Global.GetWin(first.Substring(1, first.Length - 2)))
                {
                    continue;
                }
                var line = new List<string>();
                foreach (var token in tokens)
                {
                    if (token[0] == '[' || token[0] == '*' || token == GameText.Cursor)
                    {
                        continue;
                    }
                    line.Add(token[0] == '?' ? GameText.Gap : token);
                }
                if (line.Any(t => GameText.TakesCell(t) && t != GameText.Gap))
                {
                    yield return line;
                }
            }
        }
    }
}
