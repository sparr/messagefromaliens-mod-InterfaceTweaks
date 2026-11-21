using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InterfaceTweaks
{
    // A notes page in the left panel for each right panel page. While the left panel shows the
    // explanation the right panel's explain button opens, that button becomes a notes button,
    // which opens the page's notes. While the notes show, the explain button is back. While the
    // left panel shows a page's notes, opening another page on the right shows that page's notes.
    //
    // A notes page is a left panel history entry titled with the page's name and the notes
    // glyph, with no text. Its area holds one English text field, as Glyph Notes has on a word's
    // information panel. Notes are stored in Global.savedata.dict under KeyPrefix + the page.
    internal static class PageNotes
    {
        internal const string KeyPrefix = "interfacetweaks.pagenote:";
        // The game's explain button, and the glyph that takes its place.
        private const string ExplainButton = "tutorial";
        private const string NotesButton = "draw";
        private const string TitleSuffix = " de " + NotesButton;

        // The left panel's text rows and columns, below its title row.
        private const int FirstRow = 1;
        private const int Rows = 8;
        private const int Columns = 12;
        private const int CharacterLimit = 20000;
        private const float Inset = 0.12f;
        private const float Padding = 0.1f;
        private const float FontSize = 0.3f;
        private const float MinFontSize = 0.02f;

        private static TMP_InputField field;
        private static TMP_SelectionCaret caret;
        // The page whose notes the field edits, and its note as last saved.
        private static string page;
        private static string saved;
        private static bool dirty = true;
        private static bool loggedError;
        // Whether the right menu drawn last has the notes button in place of the explain button.
        private static bool buttonShown;

        private static bool Enabled => Plugin.PageNotes.Value;

        private static string Entry(string p) => p + TitleSuffix + "&";

        // The page whose notes the history entry is, or null.
        private static string NotesPage(string entry) =>
            entry != null && entry.EndsWith(TitleSuffix + "&", StringComparison.Ordinal)
                ? entry.Substring(0, entry.Length - TitleSuffix.Length - 1)
                : null;

        private static string CurrentEntry(Draw d) =>
            d?.text_history != null && d.currenttext >= 0 && d.currenttext < d.text_history.Count ? d.text_history[d.currenttext] : null;

        // The explanation Draw.GetHelp opens for the current page, or null if it opens none.
        private static string HelpTitle(Draw d)
        {
            var p = d.current_page ?? "";
            // A page whose name starts with a token Draw.Write does not draw is no game page, such
            // as Mod Settings' pages, whose explanations it opens by this rule whatever their names.
            if (p.StartsWith("["))
            {
                return Global.GetInfo("alieninfo", p + " de tutorial") != "" ? p + " de tutorial" : null;
            }
            var later = Global.CountWin("geometry riddle") >= 5 && !Global.isDemo;
            if (p.Contains("riddle") && p != "bonus arithmetic riddle" && p != "last riddle")
            {
                return later ? "riddle de tutorial 2" : "riddle de tutorial";
            }
            if (d.CurrentPageIsMessage())
            {
                return later ? "message de tutorial 2" : "message de tutorial";
            }
            if (p == "hero de arena game")
            {
                return "hero de arena de tutorial";
            }
            return Global.GetInfo("alieninfo", p + " de tutorial") != "" ? p + " de tutorial" : null;
        }

        // Whether the left panel shows the current page's explanation.
        private static bool ShowsHelp(Draw d)
        {
            var entry = CurrentEntry(d);
            var help = HelpTitle(d);
            return entry != null && help != null && entry.StartsWith(help + "&", StringComparison.Ordinal);
        }

        private static string Key(string p) => KeyPrefix + p;

        private static string Get(string p)
        {
            var dict = Global.savedata?.dict;
            return dict != null && dict.TryGetValue(Key(p), out var t) ? t : "";
        }

        private static void Open(Draw d)
        {
            d.PushLeftText(d.current_page + TitleSuffix, "");
            d.UpdateText();
        }

        // While the left panel shows another page's notes, show the current page's instead. The
        // entry is replaced rather than a new one pushed, so changing pages does not fill the
        // history with notes.
        private static void Follow(Draw d)
        {
            var entry = CurrentEntry(d);
            var shown = NotesPage(entry);
            if (shown == null || d.current_page == null || shown == d.current_page)
            {
                return;
            }
            d.text_history[d.currenttext] = Entry(d.current_page);
            if (d.currenttext < d.currentleftpage.Count)
            {
                d.currentleftpage[d.currenttext] = 0;
            }
        }

        // Tokens Draw.Write draws a glyph for: not value tokens or bracketed tokens, which begin
        // the names Mod Settings opens its pages under.
        private static bool Drawn(string token) => token != "" && !token.StartsWith("(") && !token.StartsWith("[");

        // The words of the left panel's title as drawn, or null when it shows none.
        private static string DrawnTitle(Draw d)
        {
            if (d.alien_left_title == null)
            {
                return null;
            }
            var words = new List<string>();
            foreach (var ac in d.alien_left_title)
            {
                if (ac != null && ac.gameObject.activeInHierarchy && ac.sector == "lefttitle")
                {
                    words.Add(ac.alien_txt);
                }
            }
            return words.Count > 0 ? string.Join(" ", words) : null;
        }

        internal static void Tick()
        {
            try
            {
                var d = Global.manager;
                if (dirty)
                {
                    dirty = false;
                    string p = null;
                    if (Enabled && d != null)
                    {
                        p = NotesPage(CurrentEntry(d));
                        // Another mod, such as Calculator, may draw over the left panel without a
                        // history entry of its own, so the drawn title must be the entry's.
                        if (p != null && DrawnTitle(d) != string.Join(" ", Global.StringToListString(p + TitleSuffix, modify: false).Where(Drawn)))
                        {
                            p = null;
                        }
                    }
                    Show(d, p);
                }
                Position();
            }
            catch (Exception e)
            {
                if (!loggedError)
                {
                    Plugin.Log.LogError($"Page notes: {e}");
                    loggedError = true;
                }
            }
        }

        private static void Show(Draw d, string p)
        {
            if (p == null || d == null)
            {
                if (field != null && field.gameObject.activeSelf)
                {
                    // Deactivating ends the edit, which saves it.
                    field.gameObject.SetActive(false);
                    ReleaseSelection();
                }
                return;
            }
            if (field == null)
            {
                field = CreateInput(d);
            }
            if (!(field.isFocused && p == page))
            {
                if (field.isFocused)
                {
                    field.DeactivateInputField();
                }
                ReleaseSelection();
                page = p;
                saved = Get(p);
                field.SetTextWithoutNotify(saved);
            }
            field.textComponent.color = d.DefaultColor(0);
            field.gameObject.SetActive(true);
        }

        // Writes each change to the save data, so the note survives whatever ends the edit.
        private static void OnValueChanged(string s)
        {
            var dict = Global.savedata?.dict;
            if (dict == null || page == null)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(s))
            {
                dict.Remove(Key(page));
            }
            else
            {
                dict[Key(page)] = s;
            }
        }

        private static void Save()
        {
            if (page == null)
            {
                return;
            }
            var now = Get(page);
            if (now != saved)
            {
                saved = now;
                Global.SaveGame();
            }
        }

        // An edit that ends without a click, such as on Escape, leaves the field selected, so the
        // next key would act on it.
        private static void ReleaseSelection()
        {
            var es = EventSystem.current;
            if (field != null && es != null && !es.alreadySelecting && !field.isFocused && es.currentSelectedGameObject == field.gameObject)
            {
                es.SetSelectedGameObject(null);
            }
        }

        // TMP shows the text with a zero-width space after it, which auto sizing skips, so an
        // empty last line would not count toward the size. A word joiner counts.
        private sealed class TrailingJoiner : ITextPreprocessor
        {
            public string PreprocessText(string text) =>
                text != null && text.EndsWith("\u200b", StringComparison.Ordinal)
                    ? text.Substring(0, text.Length - 1) + "\u2060"
                    : text;
        }

        private static TMP_InputField CreateInput(Draw d)
        {
            var go = UnityEngine.Object.Instantiate(d.text_prefab);
            go.name = "InterfaceTweaks.PageNotes";
            go.transform.SetParent(d.world_canva.transform);
            var input = go.GetComponent<TMP_InputField>();
            // The labels' font, which differs from the prefab's for some languages.
            input.fontAsset = d.txt_slots[0, 0].fontAsset;
            input.characterLimit = CharacterLimit;
            input.richText = false;
            input.onFocusSelectAll = false;
            input.resetOnDeActivation = true;
            input.lineType = TMP_InputField.LineType.MultiLineNewline;
            // Arrow keys stay in the field instead of moving the selection to a nearby label.
            var nav = input.navigation;
            nav.mode = Navigation.Mode.None;
            input.navigation = nav;
            input.onValueChanged.AddListener(OnValueChanged);
            input.onEndEdit.AddListener(_ =>
            {
                Save();
                ReleaseSelection();
            });
            var t = input.textComponent;
            t.parseCtrlCharacters = false;
            t.textPreprocessor = new TrailingJoiner();
            t.alignment = TextAlignmentOptions.TopLeft;
            input.scrollSensitivity = 0f;
            // Clips text that does not fit even at the smallest size.
            go.AddComponent<RectMask2D>();
            go.SetActive(false);
            return input;
        }

        // The left panel's text rows, in world space.
        private static void Area(Draw d, out Vector2 min, out Vector2 max)
        {
            min = d.PosToScreenPos(0, FirstRow + Rows, false);
            max = d.PosToScreenPos(Columns, FirstRow, false);
        }

        // Places the field over the left panel's text rows. Its pivot is its bottom-left corner.
        private static void Position()
        {
            if (field == null || !field.gameObject.activeSelf)
            {
                return;
            }
            var cam = Camera.main;
            var d = Global.manager;
            if (cam == null || d == null)
            {
                return;
            }
            Area(d, out var min, out var max);
            Vector2 a = cam.WorldToScreenPoint(min + Vector2.one * (Inset + Padding));
            Vector2 b = cam.WorldToScreenPoint(max - Vector2.one * (Inset + Padding));
            field.transform.position = a;
            ((RectTransform)field.transform).sizeDelta = b - a;
            var perUnit = cam.pixelHeight / cam.orthographicSize / 2f;
            var t = field.textComponent;
            t.fontSizeMax = FontSize * perUnit;
            t.fontSizeMin = MinFontSize * perUnit;
            // The text, and the caret drawn over it, stay at the top instead of scrolling.
            if (t.rectTransform.anchoredPosition != Vector2.zero)
            {
                t.rectTransform.anchoredPosition = Vector2.zero;
            }
            if (caret == null)
            {
                caret = field.GetComponentInChildren<TMP_SelectionCaret>();
            }
            if (caret != null && caret.rectTransform.anchoredPosition != Vector2.zero)
            {
                caret.rectTransform.anchoredPosition = Vector2.zero;
            }
        }

        internal static void UpdateText1Prefix(Draw __instance)
        {
            try
            {
                if (Enabled)
                {
                    Follow(__instance);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Page notes failed: {e}");
            }
        }

        internal static void UpdateText1Postfix() => dirty = true;

        // The right menu's explain button becomes the notes button while the left panel shows
        // the explanation.
        internal static void WritePrefix(Draw __instance, ref string s, string sec)
        {
            try
            {
                if (sec != "rightmenu" || s == null)
                {
                    return;
                }
                buttonShown = false;
                if (!Enabled || !ShowsHelp(__instance))
                {
                    return;
                }
                var tokens = s.Split(' ');
                var i = Array.IndexOf(tokens, ExplainButton);
                if (i >= 0)
                {
                    tokens[i] = NotesButton;
                    s = string.Join(" ", tokens);
                    buttonShown = true;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Page notes button failed: {e}");
            }
        }

        // A left click on the notes button opens the page's notes. The game's own click handling
        // does nothing for that glyph in the right menu.
        internal static void UpdatePrefix(Draw __instance)
        {
            try
            {
                if (!buttonShown || !Enabled || !Input.GetMouseButtonDown(0))
                {
                    return;
                }
                var cam = Camera.main;
                if (cam == null || !Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out var hit))
                {
                    return;
                }
                var ac = hit.transform.GetComponentInParent<AlienCharacter>();
                if (ac != null && ac.sector == "rightmenu" && ac.alien_txt == NotesButton)
                {
                    Open(__instance);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Page notes click failed: {e}");
            }
        }

        internal static void UpdatePostfix() => Tick();
    }
}
