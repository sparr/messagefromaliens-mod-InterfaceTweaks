using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace InterfaceTweaks
{
    // Changes to how the minesweeper board page draws the board.
    //
    // Compact: hides the labels of the board's glyphs and closes the gaps between the board's
    // rows. The game spaces rows 1.5 units apart to leave room for labels, and hard-codes that
    // spacing in the glyph slots' positions, the cells' frames, and the click areas. The glyphs and
    // frames are moved after each redraw, and the mouse's world point is moved back to the game's
    // spacing before the game tests it against the cells. The solution the game shows in the left
    // panel after a wrong answer gets the same treatment; it has no frames or click areas.
    //
    // Center: moves the board 3.5 cells right, to the middle of the right panel, and, with the
    // compact board, 1 cell down, away from the line under the page's text. The glyphs and frames
    // are moved, and the mouse position is moved back before the game tests it against the cells.
    internal static class MinesweeperLayout
    {
        internal const string BoardPage = "2e1 detrap de variant";
        // The token before the line that opens the board page in the game's menu.
        internal const string BoardGate = "[geography-riddle-1]";
        private const string SolutionTitle = "correct answer";
        private const float GamePitch = 1.5f;
        private const float CompactPitch = 1f;
        private const float CenterRight = 3.5f;
        private const float CompactCenterDown = 1f;
        // The center of the top left cell's frame, and the board's width and height in cells.
        private const float FrameX = 0.5f;
        private const float FrameY = 0.25f;
        private const int BoardSize = 5;

        // Glyph slots moved, with their positions before the move.
        private static readonly Dictionary<AlienCharacter, Vector3> moved = new Dictionary<AlienCharacter, Vector3>();

        internal static bool OnBoard(Draw d) =>
            d != null && d.current_page == BoardPage && !string.IsNullOrEmpty(MV.current_variant);

        private static bool Compact(Draw d) => Plugin.CompactMinesweeper.Value && OnBoard(d);

        private static Vector3 Offset(Draw d)
        {
            if (!Plugin.CenterMinesweeper.Value || !OnBoard(d))
            {
                return Vector3.zero;
            }
            return new Vector3(CenterRight, Plugin.CompactMinesweeper.Value ? -CompactCenterDown : 0f, 0f);
        }

        // Maps a point on the board as drawn to where it would be on the game's board, which spaces
        // rows GamePitch apart. Rows below the first are moved down by the gaps compact closed.
        private static Vector3 ToGameBoard(Draw d, Vector3 point)
        {
            point -= Offset(d);
            if (Compact(d))
            {
                var row = Mathf.FloorToInt(FrameY + 0.5f - point.y);
                if (row > 0)
                {
                    point.y -= (GamePitch - CompactPitch) * row;
                }
            }
            return point;
        }

        // Moves the board's cell frames, which Draw.UpdateText1 adds to Draw.selected as 1 by 1
        // rectangles centered on (FrameX + column, FrameY - row * GamePitch). A redraw clears
        // Draw.selected first, so each frame is moved once.
        private static void MoveFrames(Draw d, bool compact, Vector3 offset)
        {
            if (d.selected == null || (!compact && offset == Vector3.zero))
            {
                return;
            }
            foreach (var frame in d.selected)
            {
                if (frame == null || frame.positionCount != 5)
                {
                    continue;
                }
                var corner = frame.GetPosition(0);
                var opposite = frame.GetPosition(2);
                var center = (corner + opposite) / 2f;
                var column = Mathf.RoundToInt(center.x - FrameX);
                var row = Mathf.RoundToInt((FrameY - center.y) / GamePitch);
                if (!Near(corner.x - opposite.x, 1f) || !Near(corner.y - opposite.y, 1f)
                    || column < 0 || column >= BoardSize || row < 0 || row >= BoardSize
                    || !Near(center.x, FrameX + column) || !Near(center.y, FrameY - row * GamePitch))
                {
                    continue;
                }
                var shift = offset + (compact ? Vector3.up * ((GamePitch - CompactPitch) * row) : Vector3.zero);
                for (var i = 0; i < frame.positionCount; i++)
                {
                    frame.SetPosition(i, frame.GetPosition(i) + shift);
                }
            }
        }

        private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.01f;

        // The left panel's page that the game opens with the solution after a wrong answer. Another
        // mod, such as Calculator, may draw over the left panel without a history entry of its
        // own, so the drawn title must be the solution's too.
        private static bool ShowsSolution(Draw d) =>
            d != null && d.currenttext >= 0 && d.currenttext < d.text_history.Count
            && d.text_history[d.currenttext].StartsWith(SolutionTitle + "&")
            && d.alien_left_title != null
            && string.Join(" ", d.alien_left_title.Where(a => a != null && a.gameObject.activeInHierarchy && a.sector == GameText.LeftTitle).Select(a => a.alien_txt)) == SolutionTitle;

        // Moves a board's glyphs by the offset, and if compact, hides their labels and moves each
        // row up to close the gap above it.
        private static void Move(List<AlienCharacter> glyphs, bool compact, Vector3 offset)
        {
            if (glyphs == null || (!compact && offset == Vector3.zero))
            {
                return;
            }
            foreach (var ac in glyphs)
            {
                if (ac == null || moved.ContainsKey(ac))
                {
                    continue;
                }
                moved[ac] = ac.transform.position;
                ac.transform.position += offset;
                if (compact)
                {
                    ac.ShowText(false);
                    ac.transform.position += Vector3.up * ((GamePitch - CompactPitch) * ac.line);
                }
            }
        }

        private static void Restore()
        {
            foreach (var kv in moved)
            {
                if (kv.Key != null)
                {
                    kv.Key.transform.position = kv.Value;
                }
            }
            moved.Clear();
        }

        internal static void UpdateText1Prefix()
        {
            try
            {
                Restore();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Minesweeper layout restore failed: {e}");
            }
        }

        internal static void UpdateText1Postfix(Draw __instance)
        {
            try
            {
                if (Plugin.CompactMinesweeper.Value && ShowsSolution(__instance))
                {
                    Move(__instance.alien_left_text, true, Vector3.zero);
                }
                if (OnBoard(__instance))
                {
                    Move(__instance.alien_right_text_down, Plugin.CompactMinesweeper.Value, Offset(__instance));
                    MoveFrames(__instance, Plugin.CompactMinesweeper.Value, Offset(__instance));
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Minesweeper layout failed: {e}");
            }
        }

        // Whether the game's own Draw.Update is running: after every other mod's prefix and before
        // every other mod's postfix, which may read the mouse for their own purposes.
        private static bool inGameUpdate;

        internal static void GameUpdateStarts() => inGameUpdate = true;

        internal static void GameUpdateEnds() => inGameUpdate = false;

        // Draw.Update tests the mouse against the board's cells, where the game draws them, with the
        // world point from Camera.ScreenToWorldPoint. Its raycasts for the glyph under the mouse
        // use other methods, so they still see the moved glyphs. Off the board, nothing changes.
        [HarmonyPatch(typeof(Camera), nameof(Camera.ScreenToWorldPoint), typeof(Vector3))]
        private static class ScreenToWorldPointPatch
        {
            private static void Postfix(ref Vector3 __result)
            {
                if (!inGameUpdate)
                {
                    return;
                }
                try
                {
                    var d = Global.manager;
                    if (OnBoard(d))
                    {
                        __result = ToGameBoard(d, __result);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"ScreenToWorldPoint postfix failed: {e}");
                }
            }
        }
    }
}
