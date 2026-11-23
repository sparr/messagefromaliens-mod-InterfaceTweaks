using System;
using System.Collections.Generic;
using UnityEngine;

namespace InterfaceTweaks
{
    // While the game hides translations, moves the underlines Draw.Write draws up from below the
    // empty labels to just below the glyphs. Draw.Write adds each underline to Draw.selected as a
    // horizontal two-point line GameDrop below the center of the glyphs it underlines, which is
    // below the label. A redraw clears Draw.selected first, so each underline is moved once.
    internal static class RaisedUnderlines
    {
        private const float GameDrop = 0.92f;
        private const float RaisedDrop = 0.6f;

        // Draw.selected's length when each Draw.Write in progress started.
        private static readonly Stack<int> starts = new Stack<int>();

        private static bool LabelsHidden() => Global.hideUi || (Global.globalData?.hidetranslation ?? false);

        internal static void WritePrefix(Draw d)
        {
            starts.Push(d?.selected?.Count ?? 0);
        }

        internal static void WriteFinalizer(Draw d)
        {
            if (starts.Count == 0)
            {
                return;
            }
            var start = starts.Pop();
            try
            {
                if (!Plugin.RaiseUnderlines.Value || !LabelsHidden() || d?.selected == null)
                {
                    return;
                }
                var shift = Vector3.up * (GameDrop - RaisedDrop);
                for (var i = start; i < d.selected.Count; i++)
                {
                    var line = d.selected[i];
                    if (line == null || line.positionCount != 2)
                    {
                        continue;
                    }
                    var a = line.GetPosition(0);
                    var b = line.GetPosition(1);
                    if (Mathf.Abs(a.y - b.y) > 0.01f || Mathf.Abs(a.x - b.x) < 0.01f)
                    {
                        continue;
                    }
                    line.SetPosition(0, a + shift);
                    line.SetPosition(1, b + shift);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        }
    }
}
