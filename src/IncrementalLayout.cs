using System;
using System.Collections.Generic;
using System.Linq;

namespace InterfaceTweaks
{
    // On the incremental game's page, puts each row's parts in fixed columns, so the glyphs stay in
    // place as the numbers grow. The game writes the first row as "<money> money . <time> sec" and
    // each building's row as "<count> <building> . <price> price", with "?" before a building the
    // player can afford. This writes "money <money>" with the time from the seventh cell, and
    // "<building> <count>" with the price glyph in the fifth cell and its number after it. A part
    // too long for its columns pushes the rest of its row right.
    internal static class IncrementalLayout
    {
        internal const string Page = "money enlarge";
        internal const string Gate = "[geometry-riddle-3]";
        private const string Gap = ".";
        private const string Newline = "newline";
        private const string MoneyWord = "money";
        private const string PriceWord = "price";
        // Zero-based cells where the time and the price glyph start.
        private const int TimeCell = 6;
        private const int PriceCell = 4;

        private static readonly HashSet<string> Buildings = new HashSet<string>
        {
            "farm", "market", "factory", "lab", "reactor", "blackhole", "dimension",
        };

        private static int Cells(IEnumerable<string> tokens) => tokens.Count(GameText.TakesCell);

        private static IEnumerable<string> Gaps(int n) => Enumerable.Repeat(Gap, Math.Max(0, n));

        // The money row: the amount's tokens, the money glyph, an optional gap, and the time's
        // tokens ending in its unit.
        private static List<string> MoneyRow(List<string> row)
        {
            var money = row.IndexOf(MoneyWord);
            if (money < 1 || money == row.Count - 1)
            {
                return null;
            }
            var amount = row.GetRange(0, money);
            var time = row.Skip(money + 1).SkipWhile(t => t == Gap).ToList();
            if (time.Count == 0)
            {
                return null;
            }
            var output = new List<string> { MoneyWord };
            output.AddRange(amount);
            output.AddRange(Gaps(TimeCell - Cells(output)));
            output.AddRange(time);
            return output;
        }

        // A building's row: the count's tokens, the building glyph, a gap, the price's tokens, and
        // the price glyph.
        private static List<string> BuildingRow(List<string> row)
        {
            var gap = row.IndexOf(Gap);
            if (gap < 2 || row[row.Count - 1] != PriceWord || !Buildings.Contains(row[gap - 1].TrimStart('?')))
            {
                return null;
            }
            var price = row.GetRange(gap + 1, row.Count - gap - 2).SkipWhile(t => t == Gap).ToList();
            if (price.Count == 0)
            {
                return null;
            }
            var output = new List<string> { row[gap - 1] };
            output.AddRange(row.GetRange(0, gap - 1));
            output.AddRange(Gaps(PriceCell - Cells(output)));
            output.Add(PriceWord);
            output.AddRange(price);
            return output;
        }

        internal static string Arrange(string text)
        {
            var tokens = text.Split(' ').Where(t => t != "").ToList();
            var output = new List<string>(tokens.Count + 16);
            var start = 0;
            for (var i = 0; i <= tokens.Count; i++)
            {
                if (i < tokens.Count && tokens[i] != Newline)
                {
                    continue;
                }
                var row = tokens.GetRange(start, i - start);
                output.AddRange((row.Count > 0 ? MoneyRow(row) ?? BuildingRow(row) : null) ?? row);
                if (i < tokens.Count)
                {
                    output.Add(Newline);
                }
                start = i + 1;
            }
            return string.Join(" ", output);
        }

        internal static void WritePrefix(Draw __instance, ref string s, string sec)
        {
            try
            {
                if (Plugin.IncrementalColumns.Value && sec == "righttextdown" && s != null
                    && __instance != null && __instance.current_page == Page)
                {
                    s = Arrange(s);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Write prefix failed: {e}");
            }
        }
    }
}
