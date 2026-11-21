using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace InterfaceTweaks
{
    // When a chapter's wins reach certain counts, the game shows a page for that count in the left
    // panel. No menu links to those pages, so once the left panel's history is gone they cannot be
    // seen again. This lists each one the player has unlocked at the end of the menu that holds
    // the chapter's last quiz, where it opens on the right like the menu's other pages. Pages that
    // Mod Settings adds for unlock messages gated on a page follow, at the end of the menu that
    // holds that page, while one of their lines shows.
    internal static class WinPages
    {
        // A win page is named for its chapter, this word, and the win count that shows it.
        private const string Unlock = "unlock";
        // A quiz page is named for its chapter, this word, and the quiz's number.
        private const string Quiz = "riddle";

        private sealed class Page
        {
            internal string Name;
            internal string Chapter;
            internal int Wins;
        }

        // The category that holds win pages, to which Mod Settings adds pages the game has no
        // text for.
        private const string InfoCategory = "alieninfo";

        // The Global.alien_message the index was built from, and its win pages' category and
        // that category's count of pages then.
        private static object indexed;
        private static object indexedInfo;
        private static int indexedInfoCount = -1;
        private static Dictionary<string, Page> pages = new Dictionary<string, Page>();
        // Win pages by the menu they are listed in, in chapter and win count order.
        private static Dictionary<string, List<Page>> menus = new Dictionary<string, List<Page>>();
        // Pages Mod Settings adds for unlock messages gated on a page, named for that page and
        // Unlock with no count, by the menu that holds the page. Mod Settings decides when they
        // count as won.
        private static Dictionary<string, List<string>> termMenus = new Dictionary<string, List<string>>();

        // Page names hold numbers as the game parses them: a digit, or a digit at a place, as 1e1
        // for 7, so 11 is "1e1 4".
        private const string Number = "([0-6](?:e[0-9]+)?(?: [0-6](?:e[0-9]+)?)*)";
        private static readonly Regex PageName = new Regex("^(.+) " + Unlock + " " + Number + "$");
        // A chapter with one quiz may leave out its number.
        private static readonly Regex QuizName = new Regex("^(.+) " + Quiz + "(?: " + Number + ")?$");

        private static int Value(string number)
        {
            var value = 0;
            foreach (var token in number.Split(' '))
            {
                var e = token.IndexOf('e');
                var place = e < 0 ? 0 : int.Parse(token.Substring(e + 1));
                var digit = token[0] - '0';
                for (var i = 0; i < place; i++)
                {
                    digit *= 7;
                }
                value += digit;
            }
            return value;
        }

        private static void Index()
        {
            var messages = Global.alien_message;
            if (messages == null || Global.link_message == null)
            {
                return;
            }
            messages.TryGetValue(InfoCategory, out var info);
            var infoCount = info?.Count ?? -1;
            if (ReferenceEquals(messages, indexed) && ReferenceEquals(info, indexedInfo) && infoCount == indexedInfoCount)
            {
                return;
            }
            indexed = messages;
            indexedInfo = info;
            indexedInfoCount = infoCount;
            pages = new Dictionary<string, Page>();
            menus = new Dictionary<string, List<Page>>();
            termMenus = new Dictionary<string, List<string>>();
            var lastQuiz = new Dictionary<string, (int Number, string Name)>();
            foreach (var category in messages.Values)
            {
                foreach (var name in category.Keys)
                {
                    var m = PageName.Match(name);
                    if (m.Success)
                    {
                        pages[name] = new Page { Name = name, Chapter = m.Groups[1].Value, Wins = Value(m.Groups[2].Value) };
                        continue;
                    }
                    m = QuizName.Match(name);
                    if (m.Success)
                    {
                        var number = m.Groups[2].Success ? Value(m.Groups[2].Value) : 0;
                        var chapter = m.Groups[1].Value;
                        if (!lastQuiz.TryGetValue(chapter, out var last) || number > last.Number)
                        {
                            lastQuiz[chapter] = (number, name);
                        }
                    }
                }
            }
            // Each menu's lines, gated or not, which the game lists only once at startup. Its other
            // index, of lines by menu, holds only lines whose gates had passed then.
            var menuOf = new Dictionary<string, string>();
            foreach (var kv in Global.link_message)
            {
                foreach (var line in kv.Value)
                {
                    var name = line.Trim();
                    if (!menuOf.ContainsKey(name))
                    {
                        menuOf[name] = kv.Key;
                    }
                }
            }
            var listed = 0;
            var unlisted = 0;
            foreach (var page in pages.Values.OrderBy(p => p.Chapter, StringComparer.Ordinal).ThenBy(p => p.Wins))
            {
                if (!lastQuiz.TryGetValue(page.Chapter, out var last) || !menuOf.TryGetValue(last.Name, out var menu))
                {
                    // Not named, since its name is made of word IDs.
                    unlisted++;
                    continue;
                }
                if (!menus.TryGetValue(menu, out var list))
                {
                    menus[menu] = list = new List<Page>();
                }
                list.Add(page);
                listed++;
            }
            var termPages = 0;
            var termSuffix = " " + Unlock;
            foreach (var name in info?.Keys ?? Enumerable.Empty<string>())
            {
                if (!name.EndsWith(termSuffix, StringComparison.Ordinal) || name.Length == termSuffix.Length)
                {
                    continue;
                }
                termPages++;
                if (!menuOf.TryGetValue(name.Substring(0, name.Length - termSuffix.Length).Trim(), out var menu))
                {
                    unlisted++;
                    continue;
                }
                if (!termMenus.TryGetValue(menu, out var list))
                {
                    termMenus[menu] = list = new List<string>();
                }
                list.Add(name);
                listed++;
            }
            Plugin.Log.LogInfo($"Win pages: {pages.Count} count pages and {termPages} page term pages found, {listed} listed");
            if (unlisted > 0)
            {
                Plugin.Log.LogWarning($"Win pages: {unlisted} have no menu that holds their chapter's last quiz or their page");
            }
        }

        // The game shows a chapter's win page when the chapter's count of wins reaches the page's.
        private static bool Unlocked(Page page) => Global.savedata?.win != null && Global.CountWin(page.Chapter) >= page.Wins;

        private static Page Find(string name)
        {
            if (name == null)
            {
                return null;
            }
            Index();
            return pages.TryGetValue(name, out var page) ? page : null;
        }

        private static string Line(string name) => " " + ChapterNumbers.Newline + " " + string.Join(" ", Global.StringToListString(name));

        // Adds the unlocked win pages to the end of the menu's lines.
        internal static void GetInfoPostfix(string s, ref string __result)
        {
            try
            {
                if (!Plugin.WinPages.Value || string.IsNullOrEmpty(__result) || s == null)
                {
                    return;
                }
                Index();
                var add = "";
                foreach (var page in menus.TryGetValue(s, out var list) ? list : Enumerable.Empty<Page>())
                {
                    // A page has no text when Mod Settings added it and none of its lines
                    // show.
                    if (Unlocked(page) && !string.IsNullOrEmpty(Global.GetInfo(InfoCategory, page.Name)))
                    {
                        // Parse the name as the text files write it, as the game parses
                        // page text, so its number gets the token that shows its value.
                        add += Line(page.Chapter + " " + Unlock + " " + page.Wins);
                    }
                }
                foreach (var name in termMenus.TryGetValue(s, out var names) ? names : Enumerable.Empty<string>())
                {
                    // It has text while Mod Settings shows one of its lines.
                    if (!string.IsNullOrEmpty(Global.GetInfo(InfoCategory, name)))
                    {
                        add += Line(name);
                    }
                }
                __result += add;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Win pages: {e}");
            }
        }

        // An unlocked win page counts as won, so the menu shows it as done and can still be won
        // once its other lines are, and reading it does not add a win that the chapter's count
        // would include.
        [HarmonyPatch(typeof(Global), nameof(Global.GetWin))]
        private static class GetWinPatch
        {
            private static void Postfix(string s, ref bool __result)
            {
                if (__result || s == null || s.IndexOf(Unlock, StringComparison.Ordinal) < 0)
                {
                    return;
                }
                try
                {
                    var page = Find(s) ?? Find(Global.RemoveBracket(s).Trim());
                    if (page != null && Unlocked(page))
                    {
                        __result = true;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Win pages: {e}");
                }
            }
        }

        // Never records a win page as won, in case it is read while still locked.
        [HarmonyPatch(typeof(Global), nameof(Global.SetWin))]
        private static class SetWinPatch
        {
            private static bool Prefix(string s)
            {
                try
                {
                    return s == null || s.IndexOf(Unlock, StringComparison.Ordinal) < 0 || Find(s) == null;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Win pages: {e}");
                    return true;
                }
            }
        }
    }
}
