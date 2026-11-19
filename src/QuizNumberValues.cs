using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace InterfaceTweaks
{
    // Once the game shows numbers' values in the labels of its text, show them for numbers typed
    // into a quiz's answer slots too: the label of a number's last glyph gets the number's value
    // in parentheses, as the game's own labels do.
    internal static class QuizNumberValues
    {
        // The game shows values once the player has won this many of these quizzes.
        private const string ValuesQuizzes = "arithmetic riddle";
        private const int ValuesWins = 11;
        private const string Negative = "negative";
        private const string Scientific = "exp7";
        // Repeat glyphs by the number of zero places between the last written digit, or the
        // point, and the repeating digits. The game's glyph for four or more zero places has no
        // single value, so it ends a number.
        private static readonly string[] Repeat = { "loop", "loop1", "loop2", "loop3" };

        // A digit at a place: "3" is 3, "3e2" is 3 * 7^2, "3e-1" is 3 / 7.
        private static readonly Regex PlaceGlyph = new Regex(@"^([0-6])(?:e(-?\d+))?$");

        // True while Draw.Write runs, when the answer slots are not all drawn yet.
        private static bool writing;

        private static bool Shown() =>
            Plugin.QuizNumberValues.Value && Global.globalData != null && Global.globalData.convert
            && Global.savedata?.win != null && Global.CountWin(ValuesQuizzes) >= ValuesWins;

        private static bool IsPlace(string glyph, out int digit, out int place)
        {
            digit = 0;
            place = 0;
            var m = PlaceGlyph.Match(glyph ?? "");
            if (!m.Success)
            {
                return false;
            }
            digit = m.Groups[1].Value[0] - '0';
            place = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
            return true;
        }

        private static bool IsDigit(string glyph) => glyph != null && glyph.Length == 1 && glyph[0] >= '0' && glyph[0] <= '6';

        // An exact value n / d, with d > 0.
        private readonly struct Ratio
        {
            internal readonly BigInteger N;
            internal readonly BigInteger D;

            internal Ratio(BigInteger n, BigInteger d)
            {
                if (d.Sign < 0)
                {
                    n = -n;
                    d = -d;
                }
                var g = BigInteger.GreatestCommonDivisor(n, d);
                if (!g.IsZero && !g.IsOne)
                {
                    n /= g;
                    d /= g;
                }
                N = n;
                D = d;
            }

            internal static Ratio Power7(int place) =>
                place >= 0 ? new Ratio(BigInteger.Pow(7, place), 1) : new Ratio(1, BigInteger.Pow(7, -place));

            public static Ratio operator +(Ratio a, Ratio b) => new Ratio(a.N * b.D + b.N * a.D, a.D * b.D);
            public static Ratio operator *(Ratio a, Ratio b) => new Ratio(a.N * b.N, a.D * b.D);
            internal Ratio Negate() => new Ratio(-N, D);
            // Exact to a double's precision where both parts fit in one, as they do for any number
            // a quiz shows.
            internal double ToDouble()
            {
                double n = (double)N, d = (double)D;
                return !double.IsInfinity(n) && !double.IsInfinity(d)
                    ? n / d
                    : Math.Exp(BigInteger.Log(BigInteger.Abs(N)) - BigInteger.Log(D)) * N.Sign;
            }
        }

        // Digits at strictly falling places, as the game writes a number. Returns the index after
        // the last one, or start if there are none.
        private static int Places(List<string> glyphs, int start, bool whole, ref Ratio value, out int lowest)
        {
            lowest = int.MaxValue;
            var i = start;
            while (i < glyphs.Count && IsPlace(glyphs[i], out var digit, out var place) && place < lowest && (!whole || place >= 0))
            {
                value += new Ratio(digit, 1) * Ratio.Power7(place);
                lowest = place;
                i++;
            }
            return i;
        }

        // Reads the number starting at glyphs[start]: an optional negative glyph, digits, then
        // optionally a repeat glyph and its digits, then optionally the scientific glyph and a
        // whole exponent. Returns the index after its last glyph, or -1 if none starts there.
        private static int ReadNumber(List<string> glyphs, int start, out Ratio value, out bool repeats)
        {
            value = new Ratio(0, 1);
            repeats = false;
            var i = start;
            var negative = i < glyphs.Count && glyphs[i] == Negative;
            if (negative)
            {
                i++;
            }
            var end = Places(glyphs, i, false, ref value, out var lowest);
            if (end == i)
            {
                return -1;
            }
            i = end;
            var zeros = i < glyphs.Count ? Array.IndexOf(Repeat, glyphs[i]) : -1;
            if (zeros >= 0 && i + 1 < glyphs.Count && IsDigit(glyphs[i + 1]))
            {
                i++;
                BigInteger r = 0;
                var count = 0;
                while (i < glyphs.Count && IsDigit(glyphs[i]))
                {
                    r = r * 7 + (glyphs[i][0] - '0');
                    count++;
                    i++;
                }
                // The repeating digits start below the last written digit after the point, or
                // below the point.
                var first = Math.Min(lowest, 0) - 1 - zeros;
                var period = BigInteger.Pow(7, count) - 1;
                value += new Ratio(r, period) * Ratio.Power7(first + 1);
                repeats = true;
            }
            if (negative)
            {
                value = value.Negate();
            }
            if (i + 1 < glyphs.Count && glyphs[i] == Scientific)
            {
                var j = i + 1;
                var negativeExponent = glyphs[j] == Negative;
                if (negativeExponent)
                {
                    j++;
                }
                var exponent = new Ratio(0, 1);
                var after = Places(glyphs, j, true, ref exponent, out _);
                if (after > j && exponent.D.IsOne && BigInteger.Abs(exponent.N) <= 1000)
                {
                    var e = (int)exponent.N;
                    value *= Ratio.Power7(negativeExponent ? -e : e);
                    i = after;
                }
            }
            return i;
        }

        // How the game writes a value in a label: a fraction where its text gives one, which a
        // repeating number stands for, and otherwise as a decimal.
        private static string Format(Ratio value, bool repeats) =>
            repeats && !value.D.IsOne ? value.N + "/" + value.D : Global.FloatToString(value.ToDouble());

        private static bool IsNumberGlyph(string glyph) =>
            IsPlace(glyph, out _, out _) || glyph == Negative || glyph == Scientific || Array.IndexOf(Repeat, glyph) >= 0;

        // The glyph drawn in the grid cell at x, y if it directly follows or precedes the glyph a
        // in the same row of the same text, or null.
        private static AlienCharacter Neighbor(Draw d, AlienCharacter a, int x, int y, int step)
        {
            var grid = d.alien_slots;
            x += step;
            if (grid == null || x < 0 || x >= grid.GetLength(0))
            {
                return null;
            }
            var b = grid[x, y];
            return b != null && b.gameObject.activeSelf && b.sector == a.sector && b.page == a.page
                && b.line == a.line && b.line_y == a.line_y && b.line_x == a.line_x + step ? b : null;
        }

        // A drawn glyph that joins the answer slots beside it into one number: a number glyph the
        // game's text gives no value of its own, as where the answer is a number's leading digits.
        private static bool Joins(Draw d, AlienCharacter ac) =>
            !d.slots.Contains(ac) && IsNumberGlyph(ac.alien_txt) && (ac.extra_info == null || !ac.extra_info.EndsWith("number"));

        private static bool Find(Draw d, AlienCharacter ac, out int x, out int y)
        {
            var grid = d.alien_slots;
            for (x = 0; grid != null && x < grid.GetLength(0); x++)
            {
                for (y = 0; y < grid.GetLength(1); y++)
                {
                    if (grid[x, y] == ac)
                    {
                        return true;
                    }
                }
            }
            x = y = -1;
            return false;
        }

        // The runs of glyphs in a row that hold answer slots: adjacent slots, and the number glyphs
        // beside them that join them.
        private static List<List<AlienCharacter>> Runs(Draw d)
        {
            var runs = new List<List<AlienCharacter>>();
            var done = new HashSet<AlienCharacter>();
            foreach (var slot in d.slots)
            {
                if (slot == null || done.Contains(slot) || !Find(d, slot, out var x, out var y))
                {
                    continue;
                }
                var run = new List<AlienCharacter> { slot };
                var cur = slot;
                for (var lx = x; Neighbor(d, cur, lx, y, -1) is AlienCharacter b && Joins(d, b); lx--)
                {
                    run.Insert(0, b);
                    cur = b;
                }
                cur = slot;
                for (var rx = x; Neighbor(d, cur, rx, y, 1) is AlienCharacter b && (d.slots.Contains(b) || Joins(d, b)); rx++)
                {
                    run.Add(b);
                    done.Add(b);
                    cur = b;
                }
                runs.Add(run);
            }
            return runs;
        }

        // Adds the value to the label of each number's last glyph in the run that holds the given
        // glyph, or in every run if it is null.
        private static void Annotate(Draw d, AlienCharacter only)
        {
            if (d?.slots == null || d.slots.Count == 0 || !Shown())
            {
                return;
            }
            foreach (var run in Runs(d))
            {
                if (only != null && !run.Contains(only))
                {
                    continue;
                }
                var glyphs = run.ConvertAll(ac => ac?.alien_txt);
                var j = 0;
                while (j < glyphs.Count)
                {
                    var end = ReadNumber(glyphs, j, out var value, out var repeats);
                    if (end < 0)
                    {
                        j++;
                        continue;
                    }
                    var last = run[end - 1];
                    if ((only == null || only == last) && last?.txt != null && !last.txt.isFocused && !string.IsNullOrEmpty(last.txt.text))
                    {
                        last.txt.text += " (" + Format(value, repeats) + ")";
                    }
                    j = end;
                }
            }
        }

        [HarmonyPatch]
        private static class WritePatch
        {
            private static System.Reflection.MethodBase TargetMethod() =>
                AccessTools.Method(typeof(Draw), nameof(Draw.Write), new[]
                {
                    typeof(string), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool),
                    typeof(bool), typeof(float), typeof(string), typeof(float), typeof(bool), typeof(int), typeof(int),
                });

            private static void Prefix() => writing = true;

            // All of the page's answer slots are drawn now.
            private static void Postfix(Draw __instance, string sec)
            {
                writing = false;
                try
                {
                    if (sec == "righttextdown")
                    {
                        Annotate(__instance, null);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Quiz number values failed: {e}");
                }
            }
        }

        // The game rewrites a label whenever its glyph's translation changes.
        [HarmonyPatch(typeof(AlienCharacter), nameof(AlienCharacter.UpdateEarthText))]
        private static class UpdateEarthTextPatch
        {
            private static void Postfix(AlienCharacter __instance)
            {
                try
                {
                    var d = Global.manager;
                    if (!writing && d?.slots != null && d.slots.Count > 0 && __instance.sector == "righttextdown")
                    {
                        Annotate(d, __instance);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Quiz number values failed: {e}");
                }
            }
        }
    }
}
