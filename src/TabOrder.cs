using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InterfaceTweaks
{
    // With keyboard shortcuts on, Tab from a glyph's label moves to the next label through one
    // panel's rows, title row first, then on to the other panel's, and from the end of that
    // panel's last row back to the first panel. Shift+Tab goes the other way. The game's own Tab
    // handler goes row by row across both panels and only forward; it is replaced while the
    // setting is on.
    //
    // Other mods with Tab stops of their own, such as Compound Translations, read Rank and
    // Backward so their stops keep their places in this order. Rank is -1 while the setting is
    // off, when the game's order applies.
    public static class TabOrder
    {
        // A label cell's place in Tab order, from 0, or -1 while the game's order applies.
        public static int Rank(int x, int y)
        {
            var slots = Global.manager?.txt_slots;
            if (!Plugin.Hotkeys.Value || slots == null)
            {
                return -1;
            }
            var width = slots.GetLength(0);
            var height = slots.GetLength(1);
            var half = width / 2;
            var side = x >= half ? 1 : 0;
            return side * half * height + (height - 1 - y) * half + (x - side * half);
        }

        // Whether this frame's Tab goes backward.
        public static bool Backward() =>
            Plugin.Hotkeys.Value && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        // Stands in for the game's Input.GetKeyDown(KeyCode.Tab) in Draw.Update. While the
        // setting is on, moves the selection itself and tells the game no Tab was pressed.
        private static bool GameGetKeyDown(KeyCode key)
        {
            if (key != KeyCode.Tab || !Plugin.Hotkeys.Value)
            {
                return Input.GetKeyDown(key);
            }
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                try
                {
                    MoveSelection();
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Tab order failed: {e}");
                }
            }
            return false;
        }

        // From the selected game label to the next active one in Tab order, wrapping around.
        // A selection that is not a game label is left to whatever owns it.
        private static void MoveSelection()
        {
            var es = EventSystem.current;
            var slots = Global.manager?.txt_slots;
            var selected = es?.currentSelectedGameObject;
            if (slots == null || selected == null)
            {
                return;
            }
            var start = -1;
            var labels = new List<(int Rank, GameObject Go)>();
            for (var x = 0; x < slots.GetLength(0); x++)
            {
                for (var y = 0; y < slots.GetLength(1); y++)
                {
                    var t = slots[x, y];
                    if (t == null)
                    {
                        continue;
                    }
                    if (t.gameObject == selected)
                    {
                        start = Rank(x, y);
                    }
                    else if (t.gameObject.activeSelf)
                    {
                        labels.Add((Rank(x, y), t.gameObject));
                    }
                }
            }
            if (start < 0 || labels.Count == 0)
            {
                return;
            }
            var back = Backward();
            GameObject best = null, wrap = null;
            int bestRank = 0, wrapRank = 0;
            foreach (var (rank, go) in labels)
            {
                var ahead = back ? rank < start : rank > start;
                if (ahead && (best == null || (back ? rank > bestRank : rank < bestRank)))
                {
                    best = go;
                    bestRank = rank;
                }
                if (wrap == null || (back ? rank > wrapRank : rank < wrapRank))
                {
                    wrap = go;
                    wrapRank = rank;
                }
            }
            es.SetSelectedGameObject(best ?? wrap);
        }

        [HarmonyPatch(typeof(Draw), "Update")]
        private static class UpdatePatch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var getKeyDown = AccessTools.Method(typeof(Input), nameof(Input.GetKeyDown), new[] { typeof(KeyCode) });
                var replacement = AccessTools.Method(typeof(TabOrder), nameof(GameGetKeyDown));
                var found = 0;
                foreach (var ins in instructions)
                {
                    if (ins.Calls(getKeyDown))
                    {
                        found++;
                        ins.opcode = OpCodes.Call;
                        ins.operand = replacement;
                    }
                    yield return ins;
                }
                if (found != 1)
                {
                    Plugin.Log.LogWarning($"Tab order: expected 1 Input.GetKeyDown call in Draw.Update, found {found}");
                }
            }
        }
    }
}
