using System;
using System.Linq;
using HarmonyLib;

namespace InterfaceTweaks
{
    // On the game's settings page, push the choices of each "<name> . <choices>" row to the
    // right edge of the row by filling the gap with empty cells.
    internal static class RightAlignSettings
    {
        private const string SettingsPage = "modify game";
        private const string Gap = ".";
        private const string Newline = "newline";
        // Glyph cells in one row of the right page.
        private const int RowWidth = 12;

        private static bool Applies(Draw d) => d?.parent_page != null && d.parent_page.Contains(SettingsPage);

        // Draw.Write gives these tokens no cell of their own.
        private static bool TakesCell(string token) =>
            token != "" && token[0] != '(' && token[0] != '[' && token[0] != '*' && token[0] != '@';

        internal static string Align(string text)
        {
            var tokens = text.Split(' ').ToList();
            var output = new System.Collections.Generic.List<string>(tokens.Count);
            var start = 0;
            for (var i = 0; i <= tokens.Count; i++)
            {
                if (i < tokens.Count && tokens[i] != Newline)
                {
                    continue;
                }
                var row = tokens.GetRange(start, i - start);
                var gap = row.IndexOf(Gap);
                var cells = row.Count(TakesCell);
                // Only rows with choices after the gap, and room to spare.
                if (gap > 0 && gap < row.Count - 1 && cells < RowWidth)
                {
                    row.InsertRange(gap, Enumerable.Repeat(Gap, RowWidth - cells));
                }
                output.AddRange(row);
                if (i < tokens.Count)
                {
                    output.Add(Newline);
                }
                start = i + 1;
            }
            return string.Join(" ", output);
        }

        [HarmonyPatch]
        private static class WritePatch
        {
            private static System.Reflection.MethodBase TargetMethod() => GameText.WriteString();

            private static void Prefix(Draw __instance, ref string s, string sec)
            {
                try
                {
                    if (Plugin.RightAlignSettings.Value && sec == "righttextdown" && s != null && Applies(__instance))
                    {
                        s = Align(s);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Write prefix failed: {e}");
                }
            }
        }
    }
}
