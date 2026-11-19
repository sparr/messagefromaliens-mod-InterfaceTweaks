using System;
using System.Collections.Generic;
using HarmonyLib;

namespace InterfaceTweaks
{
    // Puts each chapter's number in front of its name in the two chapter menus, and in the
    // chapter's title with the glyphs that say which menu it is from, in the order of the game's
    // chapter achievements: the chapter glyph for the first menu's chapters, and the second menu's
    // title glyphs, which end in the chapter glyph, for the second menu's.
    internal static class ChapterNumbers
    {
        // The chapter glyph, and the glyph the second chapter menu's title puts before it.
        private const string Chapter = "science";
        private const string Second = "optional";
        internal const string Newline = "newline";
        private const string Newpage = "newpage";
        // A number's glyphs, in base 7 as the game writes it.
        private static string Number(int n) => string.Join(" ", Global.ToBase7(n));

        // The prefix's tokens as the game parses text: each number gets the value token that
        // labels its last glyph, as the game's own numbers do. Value tokens take no cell.
        private static List<string> Parsed(string prefix) => Global.StringToListString(prefix);

        // The glyphs to put before a chapter's name in its title, or null if the name is not a
        // chapter's. In a menu, the menu says which list the chapter is from, so only the number.
        private static string PrefixFor(string name, bool title)
        {
            var i = Global.chapters?.IndexOf(name) ?? -1;
            if (i >= 0)
            {
                return title ? Chapter + " " + Number(i + 1) : Number(i + 1);
            }
            i = Global.chapters2?.IndexOf(name) ?? -1;
            if (i >= 0)
            {
                return title ? Second + " " + Chapter + " " + Number(i + 1) : Number(i + 1);
            }
            return null;
        }

        // Adds the numbers to the chapter lines of a chapter menu, noting each line's drawn text
        // and the name it should open.
        private static string NumberMenu(string text, List<string> chapters, Dictionary<string, string> renamed)
        {
            var tokens = new List<string>(text.Split(' '));
            var start = 0;
            for (var i = 0; i <= tokens.Count; i++)
            {
                if (i < tokens.Count && tokens[i] != Newline && tokens[i] != Newpage)
                {
                    continue;
                }
                var first = -1;
                var name = new List<string>();
                for (var j = start; j < i; j++)
                {
                    if (GameText.TakesCell(tokens[j]))
                    {
                        if (first < 0)
                        {
                            first = j;
                        }
                        name.Add(tokens[j]);
                    }
                }
                var line = string.Join(" ", name);
                var prefix = first >= 0 && chapters.Contains(line) ? PrefixFor(line, false) : null;
                if (prefix != null)
                {
                    var added = Parsed(prefix);
                    tokens.InsertRange(first, added);
                    i += added.Count;
                    renamed[prefix + " " + line] = line;
                }
                start = i + 1;
            }
            return string.Join(" ", tokens);
        }

        [HarmonyPatch]
        private static class WritePatch
        {
            private static System.Reflection.MethodBase TargetMethod() => GameText.WriteString();

            private static void Prefix(Draw __instance, ref string s, string sec, out Dictionary<string, string> __state)
            {
                __state = null;
                try
                {
                    if (!Plugin.NumberChapters.Value || s == null || __instance?.current_page == null)
                    {
                        return;
                    }
                    var page = __instance.current_page;
                    if (sec == "righttextdown" && Global.link_message != null
                        && Global.link_message.TryGetValue(page, out var chapters) && chapters != null)
                    {
                        var renamed = new Dictionary<string, string>();
                        var numbered = NumberMenu(s, chapters, renamed);
                        if (renamed.Count > 0)
                        {
                            s = numbered;
                            __state = renamed;
                        }
                    }
                    else if (sec == "righttitle" && (s == page || s.StartsWith(page + " ")) && PrefixFor(page, true) is string prefix)
                    {
                        s = string.Join(" ", Parsed(prefix)) + " " + s;
                        __state = new Dictionary<string, string> { [prefix + " " + page] = page };
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Write prefix failed: {e}");
                }
            }

            // The game makes each line's link from every glyph drawn on it, so take the numbers
            // back out of the links. The game also highlights a menu line whose page contains the
            // marked word by looking up that link before this runs, so do that with the real name.
            private static void Postfix(Draw __instance, string sec, List<AlienCharacter> __result, Dictionary<string, string> __state)
            {
                if (__state == null || __result == null)
                {
                    return;
                }
                try
                {
                    var marked = __instance?.marked_word;
                    List<string> sources = null;
                    var highlight = sec == "righttextdown" && !string.IsNullOrEmpty(marked)
                        && Global.word_source != null && Global.word_source.TryGetValue(marked, out sources) && sources != null;
                    foreach (var ac in __result)
                    {
                        var link = ac?.line_str;
                        if (string.IsNullOrEmpty(link))
                        {
                            continue;
                        }
                        foreach (var pair in __state)
                        {
                            if (link == pair.Key || link.StartsWith(pair.Key + " "))
                            {
                                ac.line_str = pair.Value + link.Substring(pair.Key.Length);
                                if (highlight && sources.Contains(ac.line_str))
                                {
                                    ac.Highlight(true);
                                }
                                break;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Write postfix failed: {e}");
                }
            }
        }
    }
}
