using System;
using System.Linq;

namespace InterfaceTweaks
{
    // On the game's settings page, push the choices of each "<name> . <choices>" row to the
    // right edge of the row by filling the gap with empty cells.
    internal static class RightAlignSettings
    {
        private const string SettingsPage = "modify game";
        // Glyph cells in one row of the right page.

        private static bool Applies(Draw d) => d?.parent_page != null && d.parent_page.Contains(SettingsPage);

        internal static string Align(string text)
        {
            var tokens = text.Split(' ').ToList();
            var output = new System.Collections.Generic.List<string>(tokens.Count);
            var start = 0;
            for (var i = 0; i <= tokens.Count; i++)
            {
                if (i < tokens.Count && tokens[i] != GameText.Newline)
                {
                    continue;
                }
                var row = tokens.GetRange(start, i - start);
                var gap = row.IndexOf(GameText.Gap);
                var cells = row.Count(GameText.TakesCell);
                // Only rows with choices after the gap, and room to spare.
                if (gap > 0 && gap < row.Count - 1 && cells < GameText.RowWidth)
                {
                    row.InsertRange(gap, Enumerable.Repeat(GameText.Gap, GameText.RowWidth - cells));
                }
                output.AddRange(row);
                if (i < tokens.Count)
                {
                    output.Add(GameText.Newline);
                }
                start = i + 1;
            }
            return string.Join(" ", output);
        }

        internal static void WritePrefix(Draw __instance, ref string s, string sec)
        {
            try
            {
                if (Plugin.RightAlignSettings.Value && sec == GameText.Body && s != null && Applies(__instance))
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
