using System;
using System.Collections.Generic;
using System.Globalization;

namespace InterfaceTweaks
{
    // The game's text writes some quantities as a number followed by a unit glyph as a plain word,
    // where elsewhere it writes one token, such as 49Ayyy, that Global.StringToListString turns
    // into the number's glyphs, the unit's glyphs, and a unit token before the unit's last glyph.
    // AlienCharacter.UpdateEarthText adds that token's value in human units to the unit's label
    // once its chapter's quizzes are won. For the plain form, add the unit token the game would
    // have made, so the game labels those units too. A unit glyph with no number before it gets
    // the token for one of the unit.
    internal static class UnitTokens
    {
        private sealed class Unit
        {
            public string[] Glyphs;
            public string Code;
            public string Human;
            public string Kind;
        }

        // The units Global.StringToListString gives a unit token, as its unit glyphs, its unit
        // code, the human unit it shows, and the kind of token, which says which chapter's wins
        // show it. It gives C, A, V, and ohm no token.
        private static readonly Unit[] Units =
        {
            U("kg", "kg", "kg"), U("pa", "pa", "Pa"), U("meter", "m", "m"), U("meter2", "m2", "m\u00b2"),
            U("meter3", "m3", "m\u00b3"), U("mps", "mps", "m/s"), U("sec", "s", "s"), U("minute", "ss", "s"),
            U("hour", "sss", "h"), U("day", "ssss", "d"), U("mps sec-1", "mps2", "m/s\u00b2"),
            U("week", "y", "d"), U("month", "yy", "d"), U("year", "yyy", "y"), U("century", "yyyy", "y"),
            U("newton", "n", "N"), U("joul", "j", "J"), U("watt", "w", "W"), U("kg meter-3", "kgpm3", "kg/m\u00b3"),
            U("newton meter2 kg-2", "nm2pkg2", "Nkg\u207b\u00b2m\u00b2"), U("joul sec", "js", "Js"),
            U("newton meter2 c-2", "nm2pc2", "Nm\u00b2c\u207b\u00b2", "electricity"), U("joul kg-1", "jpkg", "J/kg"),
            U("joul kg-1 cel-1", "jpkgc", "Jkg\u207b\u00b9\u00b0C\u207b\u00b9", "heat"), U("joul cel-1", "jpcel", "J\u00b0C\u207b\u00b9"),
            U("watt meter-2 cel-4", "wpm2k4", "Wm\u207b\u00b2\u00b0C\u207b\u2074", "heat"), U("henry", "h", "H", "electricity"),
            U("henry meter-1", "hpm", "H/m", "electricity"), U("wb", "wb", "Wb", "electricity"),
            U("cel", "cel", "\u00b0C", "heat"), U("k", "k", "K", "heat"), U("mol", "mol", "mol", "heat"),
        };

        private static Unit U(string glyphs, string code, string human, string kind = "unit") =>
            new Unit { Glyphs = glyphs.Split(' '), Code = code, Human = human, Kind = kind };

        // Longest first, so a unit of several glyphs wins over its first glyph alone.
        private static readonly Unit[] ByLength = Sorted();

        private static Unit[] Sorted()
        {
            var a = (Unit[])Units.Clone();
            Array.Sort(a, (x, y) => y.Glyphs.Length.CompareTo(x.Glyphs.Length));
            return a;
        }

        private static readonly System.Text.RegularExpressions.Regex PerUnit = new System.Text.RegularExpressions.Regex(@"^[a-z]+\d?-\d$");

        private static bool IsUnitGlyph(string t) => Array.Exists(Units, u => Array.IndexOf(u.Glyphs, t) >= 0);

        private static bool IsValueToken(string t) => t.StartsWith("(") && t.EndsWith(")" + GameText.ValueKind);

        private static bool IsUnitToken(string t) =>
            t.StartsWith("(") && (t.EndsWith(")unit") || t.EndsWith(")electricity") || t.EndsWith(")heat"));

        // The value in a value token, which Global.FloatToString or IntToString wrote, or a
        // fraction as n/d.
        private static bool Value(string token, out double value)
        {
            var s = token.Substring(1, token.Length - 2 - GameText.ValueKind.Length);
            var slash = s.IndexOf('/');
            if (slash > 0)
            {
                value = 0;
                if (!Parse(s.Substring(0, slash), out var n) || !Parse(s.Substring(slash + 1), out var d) || d == 0)
                {
                    return false;
                }
                value = n / d;
                return true;
            }
            return Parse(s, out value);
        }

        private static bool Parse(string s, out double v) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out v)
            || double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        private static bool Matches(List<string> tokens, int i, Unit u)
        {
            if (i + u.Glyphs.Length > tokens.Count)
            {
                return false;
            }
            for (var k = 0; k < u.Glyphs.Length; k++)
            {
                if (tokens[i + k] != u.Glyphs[k])
                {
                    return false;
                }
            }
            return true;
        }

        // The value token of the number whose glyphs end right before index i: the game puts it
        // before the number's last glyph. Null if no number ends there, or it has no value token.
        private static string NumberValue(List<string> tokens, int i)
        {
            if (i < 1 || !QuizNumberValues.IsNumberGlyph(tokens[i - 1]))
            {
                return null;
            }
            for (var k = i - 1; k >= 0; k--)
            {
                if (IsValueToken(tokens[k]))
                {
                    return tokens[k];
                }
                if (!QuizNumberValues.IsNumberGlyph(tokens[k]))
                {
                    return null;
                }
            }
            return null;
        }

        // The infix arithmetic operators. A number right after one ends an expression, and a unit
        // after it belongs to the whole expression, not to that number.
        private static readonly string[] Operators = { "plus", "minus", "multiply", "divide", "exponent" };

        // Whether the glyph before the number whose glyphs end right before index i, skipping
        // tokens that take no cell, is an operator.
        private static bool AfterOperator(List<string> tokens, int i)
        {
            var k = i - 1;
            while (k >= 0 && (QuizNumberValues.IsNumberGlyph(tokens[k]) || IsValueToken(tokens[k])))
            {
                k--;
            }
            while (k >= 0 && (tokens[k].StartsWith("(") || tokens[k].StartsWith("[")))
            {
                k--;
            }
            return k >= 0 && Array.IndexOf(Operators, tokens[k]) >= 0;
        }

        // The index of the glyph before index i that takes a cell, skipping tokens that are not
        // drawn, or -1 at the start.
        private static int PreviousGlyph(List<string> tokens, int i)
        {
            var k = i - 1;
            while (k >= 0 && (tokens[k].StartsWith("(") || tokens[k].StartsWith("[")))
            {
                k--;
            }
            return k;
        }

        // Glyphs after which a unit belongs to an expression, as in "x sec" or "squareroot sec":
        // the operators, the variables, and roots and powers.
        private static readonly string[] ExpressionGlyphs =
            { "plus", "minus", "multiply", "divide", "exponent", "x", "y", "z", "squareroot", "root3", "exponent2", "exponent3" };

        // Whether the unit starting at index i stands alone, naming one of the unit. Not after a
        // number, an answer slot, another unit, or a glyph of an expression, and not after
        // "terran de", where the glyph names the human unit.
        private static bool Alone(List<string> tokens, int i)
        {
            var p = PreviousGlyph(tokens, i);
            if (p < 0)
            {
                return true;
            }
            var prev = tokens[p];
            if (prev.StartsWith("?") || QuizNumberValues.IsNumberGlyph(prev) || IsUnitGlyph(prev) || PerUnit.IsMatch(prev)
                || Array.IndexOf(ExpressionGlyphs, prev) >= 0)
            {
                return false;
            }
            var before = PreviousGlyph(tokens, p);
            return !(prev == "de" && before >= 0 && tokens[before] == "terran");
        }

        // afterNumbers: give a unit written as a separate word after a number with a value token
        // that number's value. alone: give a unit with no number before it the value of one.
        internal static string AddTokens(string s, bool afterNumbers, bool alone)
        {
            if (s == null || (!alone && !s.Contains(")" + GameText.ValueKind)))
            {
                return s;
            }
            var tokens = new List<string>(s.Split(' '));
            var changed = false;
            for (var i = 0; i < tokens.Count; i++)
            {
                foreach (var u in ByLength)
                {
                    if (!Matches(tokens, i, u))
                    {
                        continue;
                    }
                    var last = i + u.Glyphs.Length - 1;
                    // A unit the game has no code for, such as newtons per square meter, continues
                    // with another unit glyph or a per-unit glyph such as meter-2. Its first glyph
                    // alone would be the wrong unit.
                    if (last + 1 < tokens.Count && (IsUnitGlyph(tokens[last + 1]) || PerUnit.IsMatch(tokens[last + 1])))
                    {
                        break;
                    }
                    // The game already gave it a token, from a token such as 49Ayyy: before its last
                    // glyph, or, for a unit of several glyphs such as m/s², right after this one.
                    if ((last > 0 && IsUnitToken(tokens[last - 1])) || (last + 1 < tokens.Count && IsUnitToken(tokens[last + 1])))
                    {
                        break;
                    }
                    double value = 1;
                    var token = afterNumbers ? NumberValue(tokens, i) : null;
                    bool found;
                    if (token != null)
                    {
                        found = !AfterOperator(tokens, i) && Value(token, out value);
                    }
                    else
                    {
                        found = alone && Alone(tokens, i);
                    }
                    if (found)
                    {
                        var human = value / Global.HumanUnitToAlienUnit(u.Code);
                        if (!double.IsNaN(human) && !double.IsInfinity(human))
                        {
                            tokens.Insert(last, "(" + Global.FloatToString(human) + "^" + u.Human + ")" + u.Kind);
                            changed = true;
                            i = last + 1;
                        }
                    }
                    break;
                }
            }
            return changed ? string.Join(" ", tokens) : s;
        }

        internal static void WritePrefix(ref string s)
        {
            try
            {
                if (Plugin.WordUnits.Value || Plugin.LoneUnits.Value)
                {
                    s = AddTokens(s, Plugin.WordUnits.Value, Plugin.LoneUnits.Value);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Unit tokens failed: {e}");
            }
        }
    }
}
