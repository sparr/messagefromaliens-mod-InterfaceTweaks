using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InterfaceTweaks
{
    // While the note editor shows a note in the left panel, typing English searches the
    // player's translations. The typed text and a list of the glyphs whose translations match
    // show at the note's cursor. Up and Down move a selection through the list, and typing more
    // clears it. Tab inserts the selected glyph, or the first if none is selected, at the cursor,
    // as clicking a glyph in the editor's dictionary does, and Enter does that and then starts a
    // new line. Text that matches nothing is highlighted, and Tab and Enter do nothing with it.
    // Backspace deletes the last typed character, and Escape clears the text. With nothing typed,
    // the arrow keys move the note's cursor, Backspace and Delete remove the glyph before or
    // after it, and Enter starts a new line, as the editor's line button does.
    internal static class NotebookTyping
    {
        private const int MaxItems = 8;
        internal const string EditorPage = "page for write";

        private static readonly StringBuilder query = new StringBuilder();
        // Where in the typed text the next typed character goes.
        private static int caret;
        private static readonly List<(string Word, string Text)> matches = new List<(string, string)>();
        private static int selection = -1;
        private static bool focusedLastFrame;
        private static GameObject box;
        private static TextMeshProUGUI text;
        private static Image background;

        private static bool TextFieldFocused()
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            return selected != null && (selected.GetComponent<TMP_InputField>()?.isFocused == true
                || selected.GetComponent<InputField>()?.isFocused == true);
        }

        // The note editor with a note open, shown in the left panel, with no text field taking the
        // keys and the calculator, which also takes typed keys, closed.
        private static bool Active(Draw d) => Plugin.NotebookTyping.Value && ShowsNote(d);

        // The note editor with a note open, shown in the left panel, and the calculator, which
        // covers the left panel and takes typed keys, closed.
        internal static bool ShowsNote(Draw d)
        {
            if (d == null || d.current_page != EditorPage || string.IsNullOrEmpty(d.current_notebook))
            {
                return false;
            }
            var h = d.text_history;
            if (h == null || d.currenttext < 0 || d.currenttext >= h.Count || !h[d.currenttext].StartsWith(EditorPage + "&", StringComparison.Ordinal))
            {
                return false;
            }
            return !CalculatorState.IsOpen();
        }

        private static void Search()
        {
            matches.Clear();
            selection = -1;
            var q = query.ToString().Trim();
            var dict = Global.savedata?.dict;
            if (q.Length == 0 || dict == null || Global.alien_text == null)
            {
                return;
            }
            var ranked = new List<(int Rank, int Length, string Word, string Text)>();
            foreach (var word in Global.alien_text.Keys)
            {
                if (!dict.TryGetValue(word, out var t) || string.IsNullOrWhiteSpace(t) || t.Trim() == "?")
                {
                    continue;
                }
                t = t.Trim();
                var at = t.IndexOf(q, StringComparison.OrdinalIgnoreCase);
                if (at < 0)
                {
                    continue;
                }
                // At the start of the translation, then at the start of any word in it, then
                // anywhere, as in the middle of a word.
                var rank = at == 0 ? 0 : char.IsLetterOrDigit(t[at - 1]) ? 2 : 1;
                var length = t.Length;
                ranked.Add((rank, length, word, t));
            }
            ranked.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank)
                : a.Length != b.Length ? a.Length.CompareTo(b.Length)
                : string.CompareOrdinal(a.Text, b.Text));
            for (var i = 0; i < ranked.Count && i < MaxItems; i++)
            {
                matches.Add((ranked[i].Word, ranked[i].Text));
            }
        }

        // Inserts the glyph at the note's cursor, as the game does for a click on a glyph in the
        // editor's dictionary, so the editor's undo button takes it back.
        private static void Insert(Draw d, string word)
        {
            d.notebookpos_history.Add(d.notebook_pos);
            d.notebook_history.Add(Global.MergeString(d.notebook));
            d.notebook.Insert(d.notebook_pos, word);
            d.notebook_pos++;
            Global.savedata.notebook2[d.current_notebook] = Global.MergeString(d.notebook);
            if (d.text_history[d.currenttext].StartsWith(EditorPage + "&", StringComparison.Ordinal))
            {
                d.text_history[d.currenttext] = EditorPage + "&" + Global.MergeStringWithCursor(d.notebook, d.notebook_pos);
            }
            else
            {
                d.PushLeftText(EditorPage, Global.MergeStringWithCursor(d.notebook, d.notebook_pos));
            }
            d.notebook_reposition = true;
            d.UpdateText();
        }

        // Where the cursor would be drawn before each token of the note, and after the last, as
        // rows and columns: a row holds GameText.RowWidth cells, a row that continues on the next
        // starts one cell in, as Draw.Write wraps text with spacing, and a line break starts a new
        // row.
        private static List<(int Row, int Col)> Layout(List<string> note)
        {
            var at = new List<(int, int)>();
            int row = 0, col = 0;
            foreach (var t in note)
            {
                if (t == GameText.Newline)
                {
                    at.Add((row, col));
                    row++;
                    col = 0;
                    continue;
                }
                if (GameText.TakesCell(t) && col >= GameText.RowWidth)
                {
                    row++;
                    col = 1;
                }
                at.Add((row, col));
                if (GameText.TakesCell(t))
                {
                    col++;
                }
            }
            at.Add((row, col));
            return at;
        }

        // The cursor position on the row above or below, at the closest column.
        private static int Vertical(List<string> note, int pos, int step)
        {
            var at = Layout(note);
            var (row, col) = at[Mathf.Clamp(pos, 0, at.Count - 1)];
            var best = pos;
            var bestDistance = int.MaxValue;
            for (var i = 0; i < at.Count; i++)
            {
                if (at[i].Row != row + step)
                {
                    continue;
                }
                var distance = Math.Abs(at[i].Col - col);
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }
            return best;
        }

        // The first or last cursor position on the cursor's row.
        private static int RowEnd(List<string> note, int pos, bool last)
        {
            var at = Layout(note);
            var row = at[Mathf.Clamp(pos, 0, at.Count - 1)].Row;
            var found = pos;
            for (var i = 0; i < at.Count; i++)
            {
                if (at[i].Row == row)
                {
                    found = i;
                    if (!last)
                    {
                        break;
                    }
                }
            }
            return found;
        }

        private static void MoveCursor(Draw d, int pos)
        {
            pos = Mathf.Clamp(pos, 0, d.notebook.Count);
            if (pos == d.notebook_pos)
            {
                return;
            }
            d.notebook_pos = pos;
            d.text_history[d.currenttext] = EditorPage + "&" + Global.MergeStringWithCursor(d.notebook, d.notebook_pos);
            d.notebook_reposition = true;
            d.UpdateText();
        }

        private static bool Enter => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);

        // Removes the note's glyph at index, as the editor's erase button does for the glyph
        // before the cursor, so the editor's undo button puts it back.
        private static void Remove(Draw d, int index)
        {
            d.notebookpos_history.Add(d.notebook_pos);
            d.notebook_history.Add(Global.MergeString(d.notebook));
            d.notebook.RemoveAt(index);
            if (index < d.notebook_pos)
            {
                d.notebook_pos--;
            }
            Global.savedata.notebook2[d.current_notebook] = Global.MergeString(d.notebook);
            d.text_history[d.currenttext] = EditorPage + "&" + Global.MergeStringWithCursor(d.notebook, d.notebook_pos);
            d.notebook_reposition = true;
            d.UpdateText();
        }

        private static void Clear()
        {
            query.Clear();
            caret = 0;
            matches.Clear();
            selection = -1;
        }

        private static void Keys(Draw d)
        {
            var focused = TextFieldFocused();
            var active = Active(d) && !focused && !focusedLastFrame;
            focusedLastFrame = focused;
            if (!active)
            {
                if (query.Length > 0 && !Active(d))
                {
                    Clear();
                }
                return;
            }
            var typing = query.Length > 0;
            var changed = false;
            foreach (var c in Input.inputString)
            {
                if (c == '\b')
                {
                    if (caret > 0)
                    {
                        query.Remove(--caret, 1);
                        changed = true;
                    }
                }
                else if (!char.IsControl(c) && (query.Length > 0 || c != ' '))
                {
                    query.Insert(caret++, c);
                    changed = true;
                }
            }
            if (typing)
            {
                // Within the typed text, the keys that move a text cursor move the caret.
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    caret = Math.Max(caret - 1, 0);
                }
                if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    caret = Math.Min(caret + 1, query.Length);
                }
                if (Input.GetKeyDown(KeyCode.Home))
                {
                    caret = 0;
                }
                if (Input.GetKeyDown(KeyCode.End))
                {
                    caret = query.Length;
                }
                if (Input.GetKeyDown(KeyCode.Delete) && caret < query.Length)
                {
                    query.Remove(caret, 1);
                    changed = true;
                }
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    query.Clear();
                    caret = 0;
                    changed = true;
                }
            }
            if (changed)
            {
                Search();
            }
            if (!typing && query.Length == 0)
            {
                if (Enter)
                {
                    Insert(d, GameText.Newline);
                }
                else if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    MoveCursor(d, d.notebook_pos - 1);
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    MoveCursor(d, d.notebook_pos + 1);
                }
                else if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    MoveCursor(d, Vertical(d.notebook, d.notebook_pos, -1));
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    MoveCursor(d, Vertical(d.notebook, d.notebook_pos, 1));
                }
                else if (Input.GetKeyDown(KeyCode.Home))
                {
                    MoveCursor(d, RowEnd(d.notebook, d.notebook_pos, false));
                }
                else if (Input.GetKeyDown(KeyCode.Backspace) && d.notebook_pos > 0)
                {
                    Remove(d, d.notebook_pos - 1);
                }
                else if (Input.GetKeyDown(KeyCode.Delete) && d.notebook_pos < d.notebook.Count)
                {
                    Remove(d, d.notebook_pos);
                }
                else if (Input.GetKeyDown(KeyCode.End))
                {
                    MoveCursor(d, RowEnd(d.notebook, d.notebook_pos, true));
                }
            }
            else if (matches.Count > 0)
            {
                if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    selection = Math.Min(selection + 1, matches.Count - 1);
                }
                if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    selection = Math.Max(selection - 1, -1);
                }
                var enter = Enter;
                if (Input.GetKeyDown(KeyCode.Tab) || enter)
                {
                    var word = matches[selection >= 0 ? selection : 0].Word;
                    Clear();
                    Insert(d, word);
                    if (enter)
                    {
                        Insert(d, GameText.Newline);
                    }
                }
            }
        }

        // The note's cursor, a line Draw.Write adds to Draw.selected at the left edge of the
        // cell where the next glyph goes, in the left half of the screen.
        private static bool Cursor(Draw d, out Vector3 top, out Vector3 bottom)
        {
            top = bottom = default;
            if (d.selected == null)
            {
                return false;
            }
            foreach (var line in d.selected)
            {
                if (line == null || !line.gameObject.activeInHierarchy || line.positionCount != 2)
                {
                    continue;
                }
                var a = line.GetPosition(0);
                var b = line.GetPosition(1);
                if (Mathf.Abs(a.x - b.x) < 0.01f && Mathf.Abs(Mathf.Abs(a.y - b.y) - 1f) < 0.01f && a.x < 0f)
                {
                    top = a.y > b.y ? a : b;
                    bottom = a.y > b.y ? b : a;
                    return true;
                }
            }
            return false;
        }

        private static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        private static void Show(Draw d)
        {
            var cam = Camera.main;
            if (query.Length == 0 || cam == null || NoteImage.Capturing || !Active(d) || !Cursor(d, out var top, out var bottom))
            {
                if (box != null && box.activeSelf)
                {
                    box.SetActive(false);
                }
                return;
            }
            if (box == null)
            {
                box = new GameObject("InterfaceTweaks.NotebookTyping", typeof(RectTransform));
                box.transform.SetParent(d.world_canva.transform, false);
                background = box.AddComponent<Image>();
                background.raycastTarget = false;
                var t = new GameObject("Text", typeof(RectTransform));
                t.transform.SetParent(box.transform, false);
                text = t.AddComponent<TextMeshProUGUI>();
                text.font = d.txt_slots[0, 0].fontAsset;
                text.raycastTarget = false;
                text.richText = true;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.alignment = TextAlignmentOptions.TopLeft;
            }
            var front = d.DefaultColor(0);
            var back = d.DefaultColor(1);
            var dim = d.DefaultColor(3);
            var sb = new StringBuilder();
            if (matches.Count == 0)
            {
                // Nothing matches: the typed text is highlighted, and Tab and Enter do nothing.
                // TMP draws a mark over the text, so a translucent one keeps the text readable.
                sb.Append("<color=#").Append(Hex(front)).Append("><mark=#").Append(Hex(front)).Append("55>").Append(WithCaret()).Append("</mark></color>");
            }
            else
            {
                sb.Append("<color=#").Append(Hex(front)).Append('>').Append(WithCaret()).Append("</color>");
            }
            for (var i = 0; i < matches.Count; i++)
            {
                var item = Escape(matches[i].Text);
                sb.Append('\n');
                if (i == selection)
                {
                    sb.Append("<color=#").Append(Hex(front)).Append("><b>> ").Append(item).Append("</b></color>");
                }
                else if (selection < 0 && i == 0)
                {
                    // Tab inserts this one.
                    sb.Append("<color=#").Append(Hex(front)).Append("><u>").Append(item).Append("</u></color>");
                }
                else
                {
                    sb.Append("<color=#").Append(Hex(dim)).Append('>').Append(item).Append("</color>");
                }
            }
            var perUnit = cam.pixelHeight / cam.orthographicSize / 2f;
            text.fontSize = 0.3f * perUnit;
            text.color = front;
            text.text = sb.ToString();
            background.color = new Color(back.r, back.g, back.b, 0.92f);
            var size = text.GetPreferredValues(text.text);
            var pad = 0.08f * perUnit;
            var rt = (RectTransform)box.transform;
            var trt = (RectTransform)text.transform;
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size + Vector2.one * pad * 2f;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.one * pad;
            trt.offsetMax = -Vector2.one * pad;
            // Below the cursor's cell, or above it where the list would run off the screen.
            Vector2 under = cam.WorldToScreenPoint(bottom);
            Vector2 over = cam.WorldToScreenPoint(top);
            var y = under.y - rt.sizeDelta.y < 0f ? over.y + rt.sizeDelta.y : under.y;
            var x = Mathf.Min(under.x, Screen.width - rt.sizeDelta.x);
            rt.position = new Vector2(x, y);
            box.transform.SetAsLastSibling();
            if (!box.activeSelf)
            {
                box.SetActive(true);
            }
        }

        // The typed text with a bar at the caret.
        private static string WithCaret()
        {
            var q = query.ToString();
            return Escape(q.Substring(0, caret)) + "|" + Escape(q.Substring(caret));
        }

        // Typed text and translations shown as typed, not as rich text tags.
        private static string Escape(string s) => s.Replace("<", "<noparse><</noparse>");

        // Before the game's Update, so an insertion redraws in the same frame.
        internal static void UpdatePrefix(Draw __instance)
        {
            try
            {
                Keys(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Notebook typing failed: {e}");
            }
        }

        internal static void UpdatePostfix(Draw __instance)
        {
            try
            {
                Show(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Notebook typing failed: {e}");
            }
        }
    }
}
