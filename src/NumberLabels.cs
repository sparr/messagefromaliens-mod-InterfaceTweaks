using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InterfaceTweaks
{
    // Once the game shows numbers' values in labels, gives each number one label under all of its
    // glyphs in place of theirs: its value in base 7 as its glyphs write it, then its base 10
    // value in parentheses where that differs. A unit after the number joins the label too, as
    // its translation and the game's value in human units, once the game shows those. While the
    // mouse is over the number or its label, or one of its glyphs' labels is being edited, the
    // glyphs' own labels show instead, so they can still be edited.
    internal static class NumberLabels
    {
        private const int RowWidth = 12;

        private sealed class Number
        {
            // The number's glyphs, then its unit's.
            public readonly List<AlienCharacter> Cells = new List<AlienCharacter>();
            // The glyph whose label shows the whole number: the first of its widest row.
            public AlienCharacter Host;
            public int RowFirst;
            public int RowLast;
            public string Text;
            public bool Combined;
        }

        private static readonly List<Number> numbers = new List<Number>();
        private static readonly Dictionary<AlienCharacter, Number> numberOf = new Dictionary<AlienCharacter, Number>();
        private static readonly HashSet<AlienCharacter> hidden = new HashSet<AlienCharacter>();
        private static Number revealed;

        private static bool Enabled() =>
            Plugin.CombinedNumbers.Value && QuizNumberValues.ValuesShown() && Global.savedata?.dict != null
            && !Global.hideUi && !Global.globalData.hidetranslation && !Global.testword;

        // The game's value tokens, which Global.StringToListString attaches to a number's last
        // glyph, and the unit tokens it attaches to a unit's last glyph, with the quizzes that
        // must be won for the game to show each kind.
        private static bool HasValue(AlienCharacter ac) => ac.extra_info != null && ac.extra_info.EndsWith("number");

        private static string UnitSuffix(AlienCharacter ac)
        {
            var info = ac.extra_info;
            if (info == null)
            {
                return null;
            }
            string kind = info.EndsWith("unit") ? "unit" : info.EndsWith("electricity") ? "electricity" : info.EndsWith("heat") ? "heat" : null;
            if (kind == null)
            {
                return null;
            }
            var unlocked = kind == "unit" ? Global.CountWin("mechanics riddle") >= 5
                : kind == "electricity" ? Global.CountWin("electricity riddle") >= 3
                : Global.CountWin("thermology riddle") >= 2;
            return unlocked ? info.Substring(0, info.Length - kind.Length).Replace("^", " ") : null;
        }

        private static string Translation(string word) =>
            Global.savedata.dict.TryGetValue(word, out var t) && !string.IsNullOrWhiteSpace(t) ? t.Trim() : "?";

        // Glyphs that can be part of a number label: drawn, with a label, outside the menus.
        private static bool Labeled(AlienCharacter ac) =>
            ac != null && ac.gameObject.activeSelf && ac.showtxt && ac.txt != null && ac.txt.gameObject.activeSelf
            && ac.sector != "leftmenu" && ac.sector != "rightmenu";

        // The sentences on screen, each as its glyphs in reading order: rows from the top, cells
        // from the left. Draw.Write numbers a sentence's rows downward with falling line_y.
        private static List<List<AlienCharacter>> Sentences(Draw d)
        {
            var groups = new Dictionary<(string, int, int), List<AlienCharacter>>();
            foreach (var ac in d.alien_slots)
            {
                if (!Labeled(ac) || (ac.sector == "righttextdown" && MinesweeperLayout.OnBoard(d)))
                {
                    continue;
                }
                var key = (ac.sector, ac.page, ac.line);
                if (!groups.TryGetValue(key, out var list))
                {
                    groups[key] = list = new List<AlienCharacter>();
                }
                list.Add(ac);
            }
            var result = new List<List<AlienCharacter>>();
            foreach (var list in groups.Values)
            {
                list.Sort((a, b) => a.line_y != b.line_y ? b.line_y.CompareTo(a.line_y) : a.line_x.CompareTo(b.line_x));
                result.Add(list);
            }
            return result;
        }

        // Whether b is the cell right after a: next on a's row, or first on the next row when a
        // ends a full row, as a wrapped sentence continues.
        private static bool Follows(AlienCharacter a, AlienCharacter b, AlienCharacter rowStart) =>
            (b.line_y == a.line_y && b.line_x == a.line_x + 1)
            || (b.line_y == a.line_y - 1 && a.line_x == RowWidth - 1 && b == rowStart && b.line_x <= 1);

        // Splits a sentence into runs of adjacent cells.
        private static List<List<AlienCharacter>> Runs(List<AlienCharacter> sentence)
        {
            var runs = new List<List<AlienCharacter>>();
            List<AlienCharacter> run = null;
            for (var i = 0; i < sentence.Count; i++)
            {
                var b = sentence[i];
                var rowStart = i == 0 || sentence[i - 1].line_y != b.line_y ? b : null;
                if (run == null || !Follows(run[run.Count - 1], b, rowStart))
                {
                    runs.Add(run = new List<AlienCharacter>());
                }
                run.Add(b);
            }
            return runs;
        }

        private static void Scan(Draw d)
        {
            Clear();
            if (d?.alien_slots == null || !Enabled())
            {
                return;
            }
            var slots = d.slots ?? new List<AlienCharacter>();
            foreach (var sentence in Sentences(d))
            {
                foreach (var run in Runs(sentence))
                {
                    FindNumbers(run, slots);
                }
            }
            foreach (var n in numbers)
            {
                Combine(n);
            }
        }

        private static void FindNumbers(List<AlienCharacter> run, List<AlienCharacter> slots)
        {
            var glyphs = run.ConvertAll(ac => ac.alien_txt);
            var j = 0;
            // Where the last number's digits ended, and whether a unit joined it.
            var lastEnd = -2;
            var lastHadUnit = false;
            while (j < glyphs.Count)
            {
                var end = QuizNumberValues.ReadNumber(glyphs, j, out _, out _);
                if (end < 0)
                {
                    j++;
                    continue;
                }
                // The game's value token ends a number, as where two of its numbers are adjacent.
                for (var k = j; k < end - 1; k++)
                {
                    if (HasValue(run[k]))
                    {
                        end = QuizNumberValues.ReadNumber(glyphs.GetRange(0, k + 1), j, out _, out _);
                        break;
                    }
                }
                if (end <= j)
                {
                    j++;
                    continue;
                }
                var last = run[end - 1];
                var inSlots = false;
                for (var k = j; k < end; k++)
                {
                    inSlots |= slots.Contains(run[k]);
                }
                // Only the numbers the game gives a value, and numbers typed into answer slots.
                if (!HasValue(last) && !inSlots)
                {
                    j = end;
                    continue;
                }
                QuizNumberValues.ReadNumber(glyphs.GetRange(0, end), j, out var value, out var repeats);
                var single = end == j + 1 && QuizNumberValues.IsPlace(glyphs[j], out _, out _);
                var base7 = Base7(glyphs, j, end);
                var base10 = HasValue(last)
                    ? last.extra_info.Substring(0, last.extra_info.Length - "number".Length)
                    : "(" + QuizNumberValues.Format(value, repeats, single) + ")";
                var text = new StringBuilder(base7);
                if (base10 != "(" + base7 + ")")
                {
                    text.Append(' ').Append(base10);
                }
                var n = new Number();
                n.Cells.AddRange(run.GetRange(j, end - j));
                // In "1 to 2 year" the unit belongs to both numbers, so it keeps its own label.
                var shared = j == lastEnd + 1 && Joins(glyphs[j - 1]) && !lastHadUnit;
                lastEnd = end;
                lastHadUnit = false;
                // A unit right after the number, of one glyph or two.
                for (var k = end; !shared && k < Math.Min(end + 2, run.Count); k++)
                {
                    var suffix = UnitSuffix(run[k]);
                    if (suffix != null)
                    {
                        var names = new List<string>();
                        for (var u = end; u <= k; u++)
                        {
                            names.Add(Translation(run[u].alien_txt));
                            n.Cells.Add(run[u]);
                        }
                        text.Append(' ').Append(string.Join(" ", names)).Append(' ').Append(suffix);
                        end = k + 1;
                        lastHadUnit = true;
                        break;
                    }
                }
                n.Text = text.ToString();
                PickHost(n);
                numbers.Add(n);
                foreach (var c in n.Cells)
                {
                    numberOf[c] = n;
                }
                j = end;
            }
        }

        // Whether the glyph joins the numbers on either side of it, as "and", "or", and "to" do:
        // its translation is one of those words, or lists one among alternatives separated by
        // punctuation of any kind.
        private static bool Joins(string glyph)
        {
            if (!Global.savedata.dict.TryGetValue(glyph, out var t) || t == null)
            {
                return false;
            }
            foreach (var part in Alternatives.Split(t))
            {
                var w = part.Trim().ToLowerInvariant();
                if (w == "and" || w == "or" || w == "to")
                {
                    return true;
                }
            }
            return false;
        }

        // Splits a translation at runs of punctuation, keeping spaces, hyphens, and apostrophes
        // inside an alternative, so "near to" is not "to".
        private static readonly System.Text.RegularExpressions.Regex Alternatives = new System.Text.RegularExpressions.Regex(@"[^\p{L}\p{N}\s'-]+");

        // The number in base 7 as its glyphs write it: digits with a point, repeating digits in
        // brackets, and E and the exponent only after the scientific glyph.
        internal static string Base7(List<string> glyphs, int start, int end)
        {
            var i = start;
            var negative = glyphs[i] == QuizNumberValues.Negative;
            if (negative)
            {
                i++;
            }
            var digits = new List<(int Digit, int Place)>();
            var lowest = int.MaxValue;
            while (i < end && QuizNumberValues.IsPlace(glyphs[i], out var digit, out var place) && place < lowest)
            {
                digits.Add((digit, place));
                lowest = place;
                i++;
            }
            if (digits.Count == 0)
            {
                return string.Join(" ", glyphs.GetRange(start, end - start));
            }
            // Every place from the highest written digit, or the ones place, down to the lowest,
            // or the ones place, with 0 where no digit is written.
            var high = Math.Max(digits[0].Place, 0);
            var low = Math.Min(lowest, 0);
            var seq = new int[high - low + 1];
            foreach (var (digit, place) in digits)
            {
                seq[high - place] = digit;
            }
            var s = new StringBuilder();
            if (negative)
            {
                s.Append('-');
            }
            for (var k = 0; k <= high; k++)
            {
                s.Append(seq[k]);
            }
            var fraction = new StringBuilder();
            for (var k = high + 1; k < seq.Length; k++)
            {
                fraction.Append(seq[k]);
            }
            var zeros = i < end ? Array.IndexOf(QuizNumberValues.Repeat, glyphs[i]) : -1;
            if (zeros >= 0 && i + 1 < end && QuizNumberValues.IsDigit(glyphs[i + 1]))
            {
                i++;
                // The repeating digits start this many places below the last written digit after
                // the point, or below the point, as QuizNumberValues.ReadNumber reads them.
                fraction.Append('0', zeros).Append('[');
                while (i < end && QuizNumberValues.IsDigit(glyphs[i]))
                {
                    fraction.Append(glyphs[i]);
                    i++;
                }
                fraction.Append(']');
            }
            if (fraction.Length > 0)
            {
                s.Append('.').Append(fraction);
            }
            // Scientific form only where the glyphs use it.
            if (i + 1 < end && glyphs[i] == QuizNumberValues.Scientific)
            {
                var j = i + 1;
                var negativeExponent = glyphs[j] == QuizNumberValues.Negative;
                if (negativeExponent)
                {
                    j++;
                }
                long e = 0;
                while (j < end && QuizNumberValues.IsPlace(glyphs[j], out var digit, out var place) && place >= 0)
                {
                    e += digit * (long)Math.Pow(7, place);
                    j++;
                }
                s.Append('E').Append(ToBase7(negativeExponent ? -e : e));
            }
            return s.ToString();
        }

        private static string ToBase7(long n)
        {
            if (n == 0)
            {
                return "0";
            }
            var s = new StringBuilder();
            var m = Math.Abs(n);
            while (m > 0)
            {
                s.Insert(0, (char)('0' + m % 7));
                m /= 7;
            }
            return n < 0 ? "-" + s : s.ToString();
        }

        // The label goes under the number's widest row, from that row's first cell.
        private static void PickHost(Number n)
        {
            var bestFirst = 0;
            var bestLast = 0;
            var first = 0;
            for (var i = 1; i <= n.Cells.Count; i++)
            {
                if (i == n.Cells.Count || n.Cells[i].line_y != n.Cells[first].line_y)
                {
                    if (i - 1 - first > bestLast - bestFirst)
                    {
                        bestFirst = first;
                        bestLast = i - 1;
                    }
                    first = i;
                }
            }
            n.RowFirst = bestFirst;
            n.RowLast = bestLast;
            n.Host = n.Cells[bestFirst];
        }

        private static void SetHidden(AlienCharacter ac, bool hide)
        {
            var text = ac?.txt?.textComponent;
            if (text == null)
            {
                return;
            }
            if (hide)
            {
                if (text.enabled)
                {
                    text.enabled = false;
                }
                hidden.Add(ac);
            }
            else if (hidden.Remove(ac))
            {
                text.enabled = true;
            }
        }

        // Places a glyph's label the way Draw.ResetTextPosition does, which is the only place the
        // game moves labels, and which Draw.Update calls on most frames: from a point half a cell
        // left of and one unit below the glyph, a cell wide and a third of a row high, widened to
        // count cells.
        private static void Place(AlienCharacter label, AlienCharacter from, int count)
        {
            var d = Global.manager;
            var cam = Camera.main;
            var field = label?.txt?.transform as RectTransform;
            if (d?.alien_slots == null || cam == null || field == null || from == null)
            {
                return;
            }
            var cell = (Vector2)cam.WorldToScreenPoint(d.alien_slots[1, 1].transform.position) - (Vector2)cam.WorldToScreenPoint(d.alien_slots[0, 0].transform.position);
            field.position = (Vector2)cam.WorldToScreenPoint(from.transform.position + Vector3.down + Vector3.left * 0.5f);
            field.sizeDelta = new Vector2(cell.x * count, cell.y / 3f);
        }

        private static void Combine(Number n)
        {
            n.Combined = true;
            Place(n.Host, n.Cells[n.RowFirst], n.RowLast - n.RowFirst + 1);
            foreach (var c in n.Cells)
            {
                if (c == n.Host)
                {
                    if (!c.txt.isFocused)
                    {
                        c.txt.text = n.Text;
                    }
                }
                else
                {
                    SetHidden(c, true);
                }
            }
        }

        private static void Separate(Number n)
        {
            if (!n.Combined)
            {
                return;
            }
            n.Combined = false;
            Place(n.Host, n.Host, 1);
            foreach (var c in n.Cells)
            {
                SetHidden(c, false);
                c.UpdateEarthText();
            }
        }

        private static void Clear()
        {
            foreach (var n in numbers)
            {
                if (n.Combined)
                {
                    Place(n.Host, n.Host, 1);
                }
            }
            foreach (var ac in hidden)
            {
                if (ac?.txt?.textComponent != null)
                {
                    ac.txt.textComponent.enabled = true;
                }
            }
            hidden.Clear();
            numbers.Clear();
            numberOf.Clear();
            revealed = null;
        }

        private static bool InUse(TMP_InputField field)
        {
            var es = EventSystem.current;
            return field != null && (field.isFocused || (es != null && es.currentSelectedGameObject == field.gameObject));
        }

        private static bool Over(AlienCharacter ac, Vector2 mouse)
        {
            var rect = ac.txt.transform as RectTransform;
            var canvas = ac.txt.GetComponentInParent<Canvas>();
            var cam = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, mouse, cam);
        }

        // The number whose glyphs' own labels should show this frame, if any.
        private static Number Wanted()
        {
            foreach (var n in numbers)
            {
                if (n.Cells.Exists(c => InUse(c.txt)))
                {
                    return n;
                }
            }
            var cam = Camera.main;
            if (cam == null)
            {
                return null;
            }
            Vector2 mouse = Input.mousePosition;
            if (Physics.Raycast(cam.ScreenPointToRay(mouse), out var hit)
                && hit.transform.GetComponentInParent<AlienCharacter>() is AlienCharacter ac && numberOf.TryGetValue(ac, out var over))
            {
                return over;
            }
            foreach (var n in numbers)
            {
                if (n.Cells.Exists(c => c.txt.textComponent.enabled && Over(c, mouse)))
                {
                    return n;
                }
            }
            return null;
        }

        private static void Tick()
        {
            if (numbers.Count == 0)
            {
                return;
            }
            if (!Enabled())
            {
                foreach (var n in numbers)
                {
                    Separate(n);
                }
                Clear();
                return;
            }
            var next = Wanted();
            if (next != revealed)
            {
                if (revealed != null)
                {
                    Combine(revealed);
                }
                revealed = next;
                if (next != null)
                {
                    Separate(next);
                }
            }
            // Another mod may show a hidden label again, as Compound Translations does for glyphs
            // that leave a run.
            foreach (var n in numbers)
            {
                if (n.Combined)
                {
                    foreach (var c in n.Cells)
                    {
                        if (c != n.Host && c.txt.textComponent.enabled)
                        {
                            SetHidden(c, true);
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Draw), nameof(Draw.UpdateText1))]
        private static class UpdateText1Patch
        {
            // Before the redraw reuses the glyphs for other text.
            private static void Prefix()
            {
                try
                {
                    Clear();
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Number labels failed: {e}");
                }
            }

            [HarmonyPriority(Priority.Low)]
            private static void Postfix(Draw __instance)
            {
                try
                {
                    Scan(__instance);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Number labels failed: {e}");
                }
            }
        }

        // The game puts every label back under its own glyph here.
        [HarmonyPatch(typeof(Draw), "ResetTextPosition")]
        private static class ResetTextPositionPatch
        {
            private static void Postfix()
            {
                try
                {
                    foreach (var n in numbers)
                    {
                        if (n.Combined)
                        {
                            Place(n.Host, n.Cells[n.RowFirst], n.RowLast - n.RowFirst + 1);
                        }
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Number labels failed: {e}");
                }
            }
        }

        [HarmonyPatch(typeof(Draw), "Update")]
        private static class UpdatePatch
        {
            private static void Postfix()
            {
                try
                {
                    Tick();
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Number labels failed: {e}");
                }
            }
        }

        // The game rewrites a label whenever its glyph's translation changes. After Quiz Number
        // Values adds its value.
        [HarmonyPatch(typeof(AlienCharacter), nameof(AlienCharacter.UpdateEarthText))]
        private static class UpdateEarthTextPatch
        {
            [HarmonyPriority(Priority.Low)]
            private static void Postfix(AlienCharacter __instance)
            {
                try
                {
                    if (numberOf.TryGetValue(__instance, out var n) && n.Combined)
                    {
                        if (__instance == n.Host)
                        {
                            if (!__instance.txt.isFocused)
                            {
                                __instance.txt.text = n.Text;
                            }
                        }
                        else
                        {
                            SetHidden(__instance, true);
                        }
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Number labels failed: {e}");
                }
            }
        }

    }
}
