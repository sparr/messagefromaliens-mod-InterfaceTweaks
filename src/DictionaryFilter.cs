using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InterfaceTweaks
{
    // On the dictionary page, Ctrl+F starts typing a filter: the dictionary's categories and
    // words are those whose translations contain the typed text, as Global.UpdateDict lists
    // them, so the upper panel shows only categories that hold a match and the lower panel only
    // a category's matching words. A button glyph at the left end of the right menu starts it
    // too. While typing is taken, the glyph's label is hidden under an empty box that shows the
    // typed text. Ctrl+F or the glyph again clears the text and goes on taking typing, and Escape
    // clears it, stops, and shows the label again. Leaving the dictionary page clears it.
    internal static class DictionaryFilter
    {
        // Global.UpdateDict's most words in one category entry.
        private const int CategorySize = 48;

        private static readonly AccessTools.FieldRef<Draw, string> CategoryName =
            AccessTools.FieldRefAccess<Draw, string>("current_categoryname");
        private static readonly AccessTools.FieldRef<Draw, bool> DictFreePage =
            AccessTools.FieldRefAccess<Draw, bool>("dict_free_page");

        private static bool listening;
        private static readonly StringBuilder query = new StringBuilder();
        // Set when the filter changes, so the next listing picks the category to show.
        private static bool changed;
        private static bool focusedLastFrame;
        private static bool matchesAny;
        // The button's glyph.
        internal const string Button = "find";
        private const string Glyph = Button;
        private static bool glyphShown;
        // The glyph whose label is hidden under the box.
        private static AlienCharacter hidden;
        private static readonly List<(Image Image, bool Vertical)> edges = new List<(Image, bool)>();
        private static GameObject box;
        private static TextMeshProUGUI text;
        private static Image background;

        private static bool OnDictionary(Draw d) => d != null && d.current_page == GameText.Dictionary;

        private static bool Ctrl => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        private static bool Alt => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        private static void Reset(Draw d, bool listen)
        {
            var had = query.Length > 0;
            query.Clear();
            listening = listen;
            if (had)
            {
                Redraw(d);
            }
        }

        private static void Redraw(Draw d)
        {
            changed = true;
            d.UpdateText();
        }

        private static void Keys(Draw d)
        {
            var focused = NotebookTyping.TextFieldFocused();
            var free = !focused && !focusedLastFrame;
            focusedLastFrame = focused;
            if (!Plugin.DictionaryFilter.Value || !OnDictionary(d))
            {
                // Its text applies only to the dictionary page it was typed on.
                query.Clear();
                listening = false;
                return;
            }
            // The calculator takes typed keys while it is open.
            if (!free || CalculatorState.IsOpen())
            {
                return;
            }
            if ((Ctrl && Input.GetKeyDown(KeyCode.F) && glyphShown) || Clicked())
            {
                Reset(d, listen: true);
                return;
            }
            if (!listening)
            {
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Reset(d, listen: false);
                return;
            }
            if (Ctrl || Alt)
            {
                return;
            }
            var edited = false;
            foreach (var c in Input.inputString)
            {
                if (c == '\b')
                {
                    if (query.Length > 0)
                    {
                        query.Remove(query.Length - 1, 1);
                        edited = true;
                    }
                }
                else if (!char.IsControl(c) && (query.Length > 0 || c != ' '))
                {
                    query.Append(c);
                    edited = true;
                }
            }
            if (edited)
            {
                Redraw(d);
            }
        }

        private static bool Matches(string word, string q) =>
            Global.savedata?.dict != null && Global.savedata.dict.TryGetValue(word, out var t)
            && t != null && t.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

        // Keeps the matching words of each category, splitting them into entries of at most
        // CategorySize as Global.UpdateDict does, and drops categories left with none.
        [HarmonyPatch(typeof(Global), nameof(Global.UpdateDict))]
        private static class UpdateDictPatch
        {
            private static void Postfix()
            {
                try
                {
                    var d = Global.manager;
                    if (!Plugin.DictionaryFilter.Value || !OnDictionary(d) || Global.dict_up == null || Global.dict_down == null)
                    {
                        return;
                    }
                    if (query.Length > 0)
                    {
                        Filter(query.ToString());
                    }
                    if (changed)
                    {
                        changed = false;
                        Select(d);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Dictionary filter: {e}");
                }
            }
        }

        private static void Filter(string q)
        {
            var up = new List<string>();
            var down = new List<List<string>>();
            var inDict = new List<string>();
            for (var i = 0; i < Global.dict_up.Count; i++)
            {
                var name = Global.dict_up[i];
                var words = Global.dict_down[i].Where(w => Matches(w, q)).ToList();
                foreach (var w in words)
                {
                    // The category's entries follow one another, so a full last entry of it is
                    // topped up before a new one starts.
                    if (up.Count == 0 || up[up.Count - 1] != name || down[down.Count - 1].Count == CategorySize)
                    {
                        up.Add(name);
                        down.Add(new List<string>());
                    }
                    down[down.Count - 1].Add(w);
                    inDict.Add(w);
                }
            }
            Global.dict_up = up;
            Global.dict_down = down;
            Global.word_in_dict = inDict;
            matchesAny = up.Count > 0;
        }

        // After the filter changes, shows the category shown before if it is still listed, or else
        // the first, and keeps the dictionary from switching to the category of the word the left
        // panel shows, which may no longer be listed.
        private static void Select(Draw d)
        {
            var index = Global.dict_up.IndexOf(CategoryName(d) ?? "");
            if (index < 0)
            {
                index = Global.dict_up.Count > 0 ? 0 : -1;
            }
            d.current_category = index;
            if (index >= 0)
            {
                CategoryName(d) = Global.dict_up[index];
                d.currentrightpageup = index / CategorySize;
            }
            else
            {
                d.currentrightpageup = 0;
            }
            DictFreePage(d) = true;
        }

        private static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        // At the right menu's left end, while the title row has a cell for it.
        internal static void WritePrefix(Draw __instance, ref string s, string sec)
        {
            try
            {
                if (sec != GameText.RightMenu || s == null)
                {
                    return;
                }
                glyphShown = false;
                var d = __instance;
                if (!Plugin.DictionaryFilter.Value || !OnDictionary(d))
                {
                    return;
                }
                var cells = s.Split(' ').Count(GameText.TakesCell) + (d.right_title ?? "").Split(' ').Count(GameText.TakesCell);
                if (cells >= GameText.RowWidth)
                {
                    return;
                }
                s = Glyph + " " + s;
                glyphShown = true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Dictionary filter: {e}");
            }
        }

        private static AlienCharacter Shown(Draw d)
        {
            if (!glyphShown || d?.alien_slots == null)
            {
                return null;
            }
            foreach (var ac in d.alien_slots)
            {
                if (ac != null && ac.gameObject.activeInHierarchy && ac.sector == GameText.RightMenu && ac.alien_txt == Glyph)
                {
                    return ac;
                }
            }
            return null;
        }

        private static bool Clicked()
        {
            if (!glyphShown || !Input.GetMouseButtonDown(0))
            {
                return false;
            }
            var cam = Camera.main;
            if (cam == null || !Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out var hit))
            {
                return false;
            }
            var ac = hit.transform.GetComponentInParent<AlienCharacter>();
            return ac != null && ac.sector == GameText.RightMenu && ac.alien_txt == Glyph;
        }

        // Shows the glyph's label again, also when the game has since moved the glyph object to
        // another cell or page.
        private static void Unhide()
        {
            if (hidden != null && hidden.txt != null && hidden.txt.textComponent != null)
            {
                hidden.txt.textComponent.enabled = true;
            }
            hidden = null;
        }

        // A side of the box's frame: x and y give the side, -1, 0, or 1 from the middle.
        private static void Edge(Transform parent, int x, int y)
        {
            var o = new GameObject("Edge", typeof(RectTransform));
            o.transform.SetParent(parent, false);
            var image = o.AddComponent<Image>();
            image.raycastTarget = false;
            var rt = (RectTransform)o.transform;
            var at = new Vector2((x + 1) / 2f, (y + 1) / 2f);
            rt.anchorMin = new Vector2(x == 0 ? 0f : at.x, y == 0 ? 0f : at.y);
            rt.anchorMax = new Vector2(x == 0 ? 1f : at.x, y == 0 ? 1f : at.y);
            rt.pivot = at;
            rt.anchoredPosition = Vector2.zero;
            edges.Add((image, x != 0));
        }

        private static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // While taking typing, the glyph's label is hidden under an empty box, as the quiz answer
        // slots are boxes, in which the typed text shows, highlighted while nothing matches. The
        // box takes clicks, so the hidden label cannot be edited.
        private static void Show(Draw d)
        {
            var ac = listening ? Shown(d) : null;
            if (ac != hidden)
            {
                Unhide();
            }
            if (ac == null || ac.txt == null || ac.txt.textComponent == null)
            {
                if (box != null && box.activeSelf)
                {
                    box.SetActive(false);
                }
                return;
            }
            ac.txt.textComponent.enabled = false;
            hidden = ac;
            if (box == null)
            {
                box = new GameObject("InterfaceTweaks.DictionaryFilter", typeof(RectTransform));
                box.transform.SetParent(d.world_canva.transform, false);
                background = box.AddComponent<Image>();
                background.raycastTarget = true;
                var t = new GameObject("Text", typeof(RectTransform));
                t.transform.SetParent(box.transform, false);
                text = t.AddComponent<TextMeshProUGUI>();
                text.font = d.txt_slots[0, 0].fontAsset;
                text.raycastTarget = false;
                text.richText = true;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
                text.alignment = TextAlignmentOptions.Midline;
                Edge(box.transform, 0, -1);
                Edge(box.transform, 0, 1);
                Edge(box.transform, -1, 0);
                Edge(box.transform, 1, 0);
            }
            var cam = Camera.main;
            var perUnit = cam == null ? 80f : cam.pixelHeight / cam.orthographicSize / 2f;
            var front = d.DefaultColor(0);
            var back = d.DefaultColor(1);
            var typed = query.ToString().Replace("<", "<noparse><</noparse>") + "|";
            text.text = typed;
            text.fontSize = 0.3f * perUnit;
            text.color = front;
            background.color = query.Length > 0 && !matchesAny ? new Color(front.r, front.g, front.b, 0.33f) : new Color(back.r, back.g, back.b, 1f);
            var thickness = Mathf.Max(1f, 0.04f * perUnit);
            foreach (var (image, vertical) in edges)
            {
                image.color = front;
                image.rectTransform.sizeDelta = vertical ? new Vector2(thickness, 0f) : new Vector2(0f, thickness);
            }
            // Over the label, a cell wide and a line of text high, and wider to the left for
            // longer text, so it stays clear of the buttons to its right.
            var corners = new Vector3[4];
            ((RectTransform)ac.txt.transform).GetWorldCorners(corners);
            var center = (corners[0] + corners[2]) / 2f;
            var cell = perUnit;
            var width = Mathf.Max(cell * 0.9f, text.GetPreferredValues(text.text).x + 0.2f * perUnit);
            var height = 0.42f * perUnit;
            var rt = (RectTransform)box.transform;
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.position = new Vector3(center.x + cell * 0.45f, center.y, center.z);
            Fill((RectTransform)text.transform);
            box.transform.SetAsLastSibling();
            if (!box.activeSelf)
            {
                box.SetActive(true);
            }
        }

        // Before the game's Update, so a change redraws in the same frame.
        internal static void UpdatePrefix(Draw __instance)
        {
            try
            {
                Keys(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Dictionary filter: {e}");
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
                Plugin.Log.LogError($"Dictionary filter: {e}");
            }
        }
    }
}
