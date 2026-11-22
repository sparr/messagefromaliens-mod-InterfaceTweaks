using System;
using HarmonyLib;
using UnityEngine;

namespace InterfaceTweaks
{
    // The exit button in the home page's menu calls Draw.ToParentPage, which quits when home is
    // the only page on the stack. Instead, open a page titled with the exit glyph, preceded by
    // the yes/no question glyph once the player has seen it, listing yes and no as menu lines.
    internal static class ExitConfirmation
    {
        private const string ExitWord = "end";
        private const string QuestionWord = "whether";
        // One line for each choice.
        private const string Lines = GameText.Yes + " " + GameText.Newline + " " + GameText.No;
        // The explain button's text, in the form of the game's own: one line per click action.
        private const string Tutorial =
            "left click " + GameText.Yes + " for " + ExitWord + " game " + GameText.Newline + " left click " + GameText.No + " for leave";

        // The confirmation page's name, which the game also shows as its title.
        private static string page;

        internal static bool IsOpen(Draw draw) => page != null && draw != null && draw.current_page == page;

        private static string PageName()
        {
            // Showing a glyph adds it to the player's dictionary, so only use the question glyph
            // once the game has shown it.
            bool seen = Global.savedata?.dict != null && Global.savedata.dict.ContainsKey(QuestionWord);
            return seen ? QuestionWord + " " + ExitWord : ExitWord;
        }

        [HarmonyPatch(typeof(Draw), nameof(Draw.ToParentPage))]
        private static class ToParentPagePatch
        {
            private static bool Prefix(Draw __instance)
            {
                try
                {
                    if (!Plugin.ConfirmExit.Value || __instance.parent_page.Count != 1)
                    {
                        return true;
                    }
                    page = PageName();
                    __instance.ToPage(page);
                    return false;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"ToParentPage prefix failed: {e}");
                    return true;
                }
            }
        }

        // Menu pages open the clicked line as a page; handle yes and no here instead.
        [HarmonyPatch(typeof(Draw), nameof(Draw.ToPage))]
        private static class ToPagePatch
        {
            private static bool Prefix(Draw __instance, string s)
            {
                if (!IsOpen(__instance))
                {
                    return true;
                }
                if (s == GameText.Yes)
                {
                    Application.Quit();
                    return false;
                }
                if (s == GameText.No)
                {
                    __instance.ToParentPage();
                    return false;
                }
                return true;
            }
        }

        // Menu pages underline their lines, show the clickable cursor over them, and open them
        // when clicked.
        [HarmonyPatch(typeof(Draw), nameof(Draw.CurrentPageIsMenu))]
        private static class CurrentPageIsMenuPatch
        {
            private static void Postfix(Draw __instance, ref bool __result)
            {
                if (!__result && IsOpen(__instance))
                {
                    __result = true;
                }
            }
        }

        // The game reads a page's lines, and its explanation, from its text files by page name.
        internal static bool GetInfoPrefix(string category, string s, ref string __result)
        {
            if (page == null || category != GameText.InfoCategory || !IsOpen(Global.manager))
            {
                return true;
            }
            if (s == page)
            {
                __result = Lines;
                return false;
            }
            if (s == page + GameText.ExplanationSuffix)
            {
                __result = Tutorial;
                return false;
            }
            return true;
        }
    }
}
