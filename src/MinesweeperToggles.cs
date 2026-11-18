using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace InterfaceTweaks
{
    // Draws a row of glyphs for each minesweeper setting in the bottom-right corner of the board
    // page, its name and then yes or no, and flips the setting when either glyph is clicked.
    internal static class MinesweeperToggles
    {
        private const string Sector = "interfacetweaks.toggle";
        private const string OnWord = "yes";
        private const string OffWord = "no";

        private sealed class Toggle
        {
            internal ConfigEntry<bool> Entry;
            internal string Name;
            internal readonly List<AlienCharacter> Glyphs = new List<AlienCharacter>();
        }

        // From the bottom row up.
        private static List<Toggle> toggles;

        private static List<Toggle> Toggles() => toggles ??= new List<Toggle>
        {
            new Toggle { Entry = Plugin.CompactMinesweeper, Name = "tohide" },
            new Toggle { Entry = Plugin.CenterMinesweeper, Name = "center" },
        };

        private static Toggle Under(Draw d)
        {
            if (!MinesweeperLayout.OnBoard(d) || !Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out var hit))
            {
                return null;
            }
            var ac = hit.transform.gameObject.GetComponentInParent<AlienCharacter>();
            if (ac == null || !ac.gameObject.activeInHierarchy || ac.sector != Sector)
            {
                return null;
            }
            return Toggles().Find(t => t.Glyphs.Contains(ac));
        }

        [HarmonyPatch(typeof(Draw), nameof(Draw.UpdateText1))]
        private static class UpdateText1Patch
        {
            private static void Postfix(Draw __instance)
            {
                try
                {
                    foreach (var t in Toggles())
                    {
                        t.Glyphs.Clear();
                    }
                    if (!MinesweeperLayout.OnBoard(__instance))
                    {
                        return;
                    }
                    var width = __instance.alien_slots.GetLength(0);
                    var row = 0;
                    foreach (var t in Toggles())
                    {
                        var text = t.Name + " " + (t.Entry.Value ? OnWord : OffWord);
                        t.Glyphs.AddRange(__instance.Write(text, 12, width, row, isLeft: false, isUp: true, showTranslation: true,
                            __instance.text_linewidth, Sector, 1f, nospacing: true));
                        row++;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Minesweeper toggles failed: {e}");
                }
            }
        }

        [HarmonyPatch(typeof(Draw), "Update")]
        private static class UpdatePatch
        {
            private static void Prefix(Draw __instance)
            {
                try
                {
                    if (Input.GetMouseButtonDown(0) && Under(__instance) is Toggle t)
                    {
                        // Config.SettingChanged redraws the page.
                        t.Entry.Value = !t.Entry.Value;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Minesweeper toggle failed: {e}");
                }
            }

        }

        // The game sets the cursor every frame, in the color of nothing clickable for the toggles;
        // give them the cursor of its buttons instead. Setting it again after the game would
        // change the cursor twice every frame.
        [HarmonyPatch(typeof(HardwareCursor), nameof(HardwareCursor.Apply))]
        private static class CursorPatch
        {
            private static void Prefix(ref Color c)
            {
                try
                {
                    var d = Global.manager;
                    if (d != null && c == d.DefaultColor(5) && Under(d) != null)
                    {
                        c = d.DefaultColor(0);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Minesweeper toggle cursor failed: {e}");
                }
            }
        }
    }
}
