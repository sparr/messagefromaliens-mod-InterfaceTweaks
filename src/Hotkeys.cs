using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InterfaceTweaks
{
    // Keyboard shortcuts for the game's own controls. Each does what clicking the control does.
    //
    // In the number rule game, 1 to 7 enter digits, as the digit glyphs above the history do.
    //
    // Page Up and Page Down press the previous and next page glyphs of a panel's menu. Both go to
    // a panel whose menu shows either glyph. When both panels' menus do, the panel last used gets
    // the keys: the one the mouse last clicked in, or the left panel if its message has changed
    // since, as when a click on the right opens an explanation on the left. Before either, the
    // right panel gets them.
    //
    // No shortcut works while a text field has focus, or on the frame after one loses it, as a
    // field whose edit Enter ended this frame has already taken the frame's keys. The digits also
    // wait while the calculator is open, as it takes typed digits.
    internal static class Hotkeys
    {
        private const string RulePage = "understand rule";
        private const string Dictionary = "language de book";
        private const string PrevPage = "prevword";
        private const string NextPage = "nextword";

        private static readonly AccessTools.FieldRef<Draw, int> RuleLevel = AccessTools.FieldRefAccess<Draw, int>("understand_lv");
        private static readonly AccessTools.FieldRef<Draw, int> RuleTest = AccessTools.FieldRefAccess<Draw, int>("understand_test");
        private static readonly AccessTools.FieldRef<Draw, List<List<int>>> RuleHistory = AccessTools.FieldRefAccess<Draw, List<List<int>>>("understand_history");
        private static readonly AccessTools.FieldRef<Draw, bool> ScrollToLast = AccessTools.FieldRefAccess<Draw, bool>("right_scrolltolast");
        private static readonly AccessTools.FieldRef<Draw, bool> DictFreePage = AccessTools.FieldRefAccess<Draw, bool>("dict_free_page");
        private static readonly Func<Draw, bool> IsEditNote = AccessTools.MethodDelegate<Func<Draw, bool>>(AccessTools.Method(typeof(Draw), "IsEditNote"));

        private static readonly Type CalculatorPanel = AccessTools.TypeByName("Calculator.Panel");
        private static readonly System.Reflection.FieldInfo CalculatorOpen = CalculatorPanel == null ? null : AccessTools.Field(CalculatorPanel, "open");

        private static bool lastClickLeft;
        // The left panel's message as of the last frame: its place in the history, the history's
        // length, and its entry.
        private static int lastText = -1;
        private static int lastHistoryCount = -1;
        private static string lastEntry;
        private static bool focusedLastFrame;

        private static bool TextFieldFocused()
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            return selected != null && (selected.GetComponent<TMP_InputField>()?.isFocused == true
                || selected.GetComponent<InputField>()?.isFocused == true);
        }

        private static bool CalculatorIsOpen() => CalculatorOpen != null && (bool)CalculatorOpen.GetValue(null);

        // The digit of a 1 to 7 key pressed this frame, on the number row or the keypad, or 0.
        private static int DigitPressed()
        {
            for (var v = 1; v <= 7; v++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + v) || Input.GetKeyDown(KeyCode.Keypad0 + v))
                {
                    return v;
                }
            }
            return 0;
        }

        // As Draw.Update does for a click on a digit glyph: starts a new entry after a complete
        // one or a marker line, and otherwise adds the digit to the entry being written.
        private static void EnterDigit(Draw d, int v)
        {
            var history = RuleHistory(d);
            if (history == null)
            {
                return;
            }
            if (history.Count == 0 || history[history.Count - 1].Count == 3 || history[history.Count - 1][0] < 0)
            {
                history.Add(new List<int> { v });
            }
            else
            {
                history[history.Count - 1].Add(v);
            }
            ScrollToLast(d) = true;
            d.UpdateText();
        }

        private static bool MenuShows(Draw d, string sector, string word)
        {
            foreach (var ac in d.alien_slots)
            {
                if (ac != null && ac.gameObject.activeSelf && ac.sector == sector && ac.alien_txt == word)
                {
                    return true;
                }
            }
            return false;
        }

        // As Draw.Update does for a click on the left menu's page glyph.
        private static void PageLeft(Draw d, bool next)
        {
            var pages = d.currentleftpage;
            if (pages == null || d.currenttext < 0 || d.currenttext >= pages.Count)
            {
                return;
            }
            if (!next && pages[d.currenttext] > 0)
            {
                pages[d.currenttext]--;
                d.UpdateText();
            }
            if (next && pages[d.currenttext] < d.leftpagenum)
            {
                pages[d.currenttext]++;
                d.UpdateText();
            }
        }

        // As Draw.Update does for a click on the right menu's page glyph.
        private static void PageRight(Draw d, bool next)
        {
            if (d.current_page == Dictionary || IsEditNote(d))
            {
                d.currentrightpageup += next ? 1 : -1;
                d.UpdateText();
                DictFreePage(d) = true;
            }
            else if (!next && d.currentrightpage > 0)
            {
                d.currentrightpage--;
                d.UpdateText();
            }
            else if (next && d.currentrightpage < d.rightpagenum)
            {
                d.currentrightpage++;
                d.UpdateText();
            }
        }

        private static bool HasPages(Draw d, string sector) => MenuShows(d, sector, PrevPage) || MenuShows(d, sector, NextPage);

        // Both keys go to the same panel, so on that panel's first or last page the key whose
        // glyph is missing does nothing rather than turning the other panel.
        private static void Page(Draw d, bool next)
        {
            var word = next ? NextPage : PrevPage;
            var left = HasPages(d, "leftmenu");
            var right = HasPages(d, "rightmenu");
            if (left && (!right || lastClickLeft))
            {
                if (MenuShows(d, "leftmenu", word))
                {
                    PageLeft(d, next);
                }
            }
            else if (right && MenuShows(d, "rightmenu", word))
            {
                PageRight(d, next);
            }
        }

        // Gives the keys to the left panel when its message changed since the last frame. A
        // click's change shows up on the frame after the click, so it overrides that click.
        private static void NoteLeftChange(Draw d)
        {
            var history = d?.text_history;
            var count = history?.Count ?? -1;
            var text = d?.currenttext ?? -1;
            var entry = history != null && text >= 0 && text < count ? history[text] : null;
            if (lastHistoryCount >= 0 && (text != lastText || count != lastHistoryCount || entry != lastEntry))
            {
                lastClickLeft = true;
            }
            lastText = text;
            lastHistoryCount = count;
            lastEntry = entry;
        }

        internal static void UpdatePrefix(Draw __instance)
        {
            try
            {
                NoteLeftChange(__instance);
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
                {
                    lastClickLeft = Input.mousePosition.x < Screen.width / 2f;
                }
                var focused = TextFieldFocused();
                var blocked = focused || focusedLastFrame;
                focusedLastFrame = focused;
                if (!Plugin.Hotkeys.Value || blocked || __instance == null)
                {
                    return;
                }
                if (Input.GetKeyDown(KeyCode.PageUp))
                {
                    Page(__instance, false);
                }
                else if (Input.GetKeyDown(KeyCode.PageDown))
                {
                    Page(__instance, true);
                }
                var digit = DigitPressed();
                if (digit != 0 && __instance.current_page == RulePage && RuleLevel(__instance) != 0
                    && RuleTest(__instance) == 0 && !CalculatorIsOpen())
                {
                    EnterDigit(__instance, digit);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Hotkeys failed: {e}");
            }
        }
    }
}
