using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace InterfaceTweaks
{
    // Copies an image of the whole note the note editor shows to the clipboard, all of its left
    // panel pages one below the other, with the glyphs' labels as they are drawn.
    //
    // The game draws only the left panel page it shows, so each page is drawn in turn, without
    // the note's cursor, and read from the screen at the end of the frame. Then, before the frame
    // is shown, the left half of the screen as it was before the copy is drawn over it, so the
    // panel looks unchanged throughout. The page shown before comes back after. A button at the
    // left end of the editor's right menu, in the cell the menu leaves empty, starts it.
    internal static class NoteImage
    {
        // The button's glyph, which the game's own click handling treats as any other editor
        // button it has no action for: it shows the note in the left panel again.
        internal const string Button = "picture";
        private const string MenuSector = "rightmenu";
        private const int MenuCells = 12;

        // The left panel's text area in world units: 12 cells wide, 8 rows of 1.5 below the line
        // under its title row, at y = 5.25, and left of the line between the panels, at x = 0.
        private const float Left = -12f;
        private const float Right = 0f;
        private const float Top = 5.25f;
        private const float RowHeight = 1.5f;
        private const int Rows = 8;
        // Kept clear of the lines along the area's top and right.
        private const float Inset = 0.1f;
        // Frames after the frame that redraws a page, for labels that other tweaks lay out once
        // TMP has laid out the glyphs' labels.
        private const int SettleFrames = 2;

        // While pages are being drawn for the image.
        internal static bool Capturing;
        private static bool buttonShown;

        private struct Shot
        {
            public int Width;
            public int Height;
            public Color32[] Pixels;
        }

        private static bool IsEditNote(Draw d) =>
            d != null && d.current_page == NotebookTyping.EditorPage && !string.IsNullOrEmpty(d.current_notebook);

        private static int Page(Draw d) =>
            d.currentleftpage != null && d.currenttext >= 0 && d.currenttext < d.currentleftpage.Count ? d.currentleftpage[d.currenttext] : 0;

        private static void Start(Draw d)
        {
            if (Capturing || Plugin.Instance == null)
            {
                return;
            }
            Capturing = true;
            Plugin.Instance.StartCoroutine(Run(d));
        }

        // The left half of the screen, up to the line between the panels, in screen pixels.
        private static RectInt Half()
        {
            var cam = Camera.main;
            var right = Mathf.Clamp(Mathf.CeilToInt(cam.WorldToScreenPoint(new Vector3(Right, 0f)).x) + 1, 0, Screen.width);
            return new RectInt(0, 0, right, Screen.height);
        }

        // The left half of the screen as the frame drew it.
        private static Texture2D Snapshot(RectInt half)
        {
            // Drawn back at its own size, so each pixel lands on the one it came from. In linear
            // color space, its colors are decoded and encoded again on the way, which can round
            // them by a step or two of 255.
            var tex = new Texture2D(half.width, half.height, TextureFormat.RGB24, false);
            tex.filterMode = FilterMode.Point;
            tex.ReadPixels(new Rect(half.x, half.y, half.width, half.height), 0, 0, false);
            tex.Apply(false);
            return tex;
        }

        // Draws the snapshot over the frame, at the end of the frame, before it is shown.
        private static void Cover(Texture2D cover, RectInt half)
        {
            if (cover == null || half.width != Half().width || Screen.height != half.height)
            {
                return;
            }
            GL.PushMatrix();
            try
            {
                GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
                Graphics.DrawTexture(new Rect(half.x, Screen.height - half.y - half.height, half.width, half.height), cover);
            }
            finally
            {
                GL.PopMatrix();
            }
        }

        // Keeps the snapshot over the panel while the page shown before is drawn again and its
        // labels settle, then frees it.
        private static IEnumerator Uncover(Texture2D cover, RectInt half)
        {
            try
            {
                for (var f = 0; f <= SettleFrames + 1; f++)
                {
                    yield return new WaitForEndOfFrame();
                    Cover(cover, half);
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(cover);
            }
        }

        private static IEnumerator Run(Draw d)
        {
            // The click's own redraw, which shows the note again if the left panel showed
            // something else, is done in the frame's Update, so the frame shows its result.
            yield return new WaitForEndOfFrame();
            var index = d.currenttext;
            var note = d.current_notebook;
            string original = null;
            string plain = null;
            var page = 0;
            Texture2D cover = null;
            var half = default(RectInt);
            var shots = new List<Shot>();
            var complete = false;
            try
            {
                if (!NotebookTyping.ShowsNote(d))
                {
                    Plugin.Log.LogWarning("Note image: the left panel does not show the note.");
                    yield break;
                }
                half = Half();
                cover = Snapshot(half);
                page = Page(d);
                original = d.text_history[index];
                // The note without its cursor. The position tokens before each glyph, which clicks
                // in the note use, are kept, so the glyphs are drawn as they were.
                plain = NotebookTyping.EditorPage + "&" + Global.MergeStringWithCursor(d.notebook, -1);
                d.text_history[index] = plain;
                for (var p = 0; ; p++)
                {
                    d.currentleftpage[index] = p;
                    d.notebook_reposition = false;
                    d.UpdateText();
                    // The next Update draws the page. Later frames let labels that other tweaks
                    // lay out after TMP's layout settle.
                    for (var f = 0; f < SettleFrames; f++)
                    {
                        yield return new WaitForEndOfFrame();
                        Cover(cover, half);
                    }
                    yield return new WaitForEndOfFrame();
                    if (!Unchanged(d, index, note, plain) || index >= d.currentleftpage.Count || d.currentleftpage[index] != p)
                    {
                        Plugin.Log.LogWarning("Note image: the note or its panel changed while it was being copied.");
                        yield break;
                    }
                    var last = p >= d.leftpagenum;
                    var shot = Read(d, last);
                    Cover(cover, half);
                    if (shot.Pixels != null)
                    {
                        shots.Add(shot);
                    }
                    if (last)
                    {
                        break;
                    }
                }
                complete = true;
            }
            finally
            {
                // The entry without the cursor is put back as it was, unless something replaced it,
                // as a click in the note does. The cover stays until the redraw settles only if the
                // panel still shows the note.
                if (plain != null && index < d.text_history.Count && ReferenceEquals(d.text_history[index], plain))
                {
                    var shown = Unchanged(d, index, note, plain);
                    d.text_history[index] = original;
                    d.currentleftpage[index] = page;
                    d.notebook_reposition = false;
                    d.UpdateText();
                    if (shown && cover != null)
                    {
                        Plugin.Instance.StartCoroutine(Uncover(cover, half));
                        cover = null;
                    }
                }
                if (cover != null)
                {
                    UnityEngine.Object.Destroy(cover);
                }
                Capturing = false;
            }
            if (complete)
            {
                try
                {
                    Copy(d, shots);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Note image: {e}");
                }
            }
        }

        private static bool Unchanged(Draw d, int index, string note, string entry) =>
            d.current_page == NotebookTyping.EditorPage && d.current_notebook == note && d.currenttext == index
            && index < d.text_history.Count && d.text_history[index] == entry && NotebookTyping.ShowsNote(d);

        // The drawn page's part of the left panel: every row, or on the last page, the rows down to
        // the lowest glyph, so blank rows at the end of a page are kept unless the note ends there.
        private static Shot Read(Draw d, bool last)
        {
            var rows = Rows;
            if (last)
            {
                rows = 0;
                foreach (var ac in d.alien_left_text ?? Enumerable.Empty<AlienCharacter>())
                {
                    if (ac != null && ac.gameObject.activeInHierarchy)
                    {
                        var row = Mathf.FloorToInt((Top - ac.transform.position.y) / RowHeight) + 1;
                        rows = Math.Max(rows, Mathf.Clamp(row, 1, Rows));
                    }
                }
                if (rows == 0)
                {
                    return default;
                }
            }
            var cam = Camera.main;
            Vector2 a = cam.WorldToScreenPoint(new Vector3(Left, Top - Inset));
            Vector2 b = cam.WorldToScreenPoint(new Vector3(Right - Inset, Top - RowHeight * rows));
            var x0 = Mathf.Clamp(Mathf.RoundToInt(a.x), 0, Screen.width);
            var x1 = Mathf.Clamp(Mathf.RoundToInt(b.x), 0, Screen.width);
            var y0 = Mathf.Clamp(Mathf.RoundToInt(b.y), 0, Screen.height);
            var y1 = Mathf.Clamp(Mathf.RoundToInt(a.y), 0, Screen.height);
            if (x1 <= x0 || y1 <= y0)
            {
                return default;
            }
            var tex = new Texture2D(x1 - x0, y1 - y0, TextureFormat.RGB24, false);
            try
            {
                tex.ReadPixels(new Rect(x0, y0, x1 - x0, y1 - y0), 0, 0, false);
                return new Shot { Width = tex.width, Height = tex.height, Pixels = tex.GetPixels32() };
            }
            finally
            {
                UnityEngine.Object.Destroy(tex);
            }
        }

        // Stacks the pages, first at the top, each below a strip of the background as tall as the
        // inset cut from its top, so rows keep their spacing across pages, and copies the result.
        private static void Copy(Draw d, List<Shot> shots)
        {
            if (shots.Count == 0)
            {
                Plugin.Log.LogWarning("Note image: the note is empty.");
                return;
            }
            var cam = Camera.main;
            var pad = Mathf.RoundToInt(cam.WorldToScreenPoint(new Vector3(0f, Inset)).y - cam.WorldToScreenPoint(Vector3.zero).y);
            var width = shots.Max(s => s.Width);
            var height = shots.Sum(s => s.Height + pad);
            var pixels = new Color32[width * height];
            Color32 back = d.DefaultColor(1);
            back.a = 255;
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = back;
            }
            // Rows run bottom to top, so the last page goes in first.
            var y = 0;
            for (var s = shots.Count - 1; s >= 0; s--)
            {
                var shot = shots[s];
                for (var r = 0; r < shot.Height; r++)
                {
                    Array.Copy(shot.Pixels, r * shot.Width, pixels, (y + r) * width, shot.Width);
                }
                y += shot.Height + pad;
            }
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            byte[] png;
            try
            {
                image.SetPixels32(pixels);
                png = image.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.Destroy(image);
            }
            var error = Clipboard.SetImage(width, height, pixels, png);
            if (error != null)
            {
                Plugin.Log.LogError($"Note image: {error}");
            }
            else
            {
                Plugin.Log.LogInfo($"Note image: copied {shots.Count} page(s), {width} by {height} pixels.");
            }
        }

        // In the note editor, the button goes at the right menu's left end, in the cell it leaves
        // empty, so the game's buttons stay where they are.
        internal static void WritePrefix(Draw __instance, ref string s, string sec)
        {
            try
            {
                if (sec != MenuSector || s == null)
                {
                    return;
                }
                buttonShown = false;
                if (!Plugin.NoteImageButton.Value || !IsEditNote(__instance))
                {
                    return;
                }
                if (s.Split(' ').Count(GameText.TakesCell) >= MenuCells)
                {
                    return;
                }
                s = Button + " " + s;
                buttonShown = true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Note image: {e}");
            }
        }

        // Starts a copy on a left click on the button.
        internal static void UpdatePrefix(Draw __instance)
        {
            try
            {
                if (Capturing || !buttonShown || !Input.GetMouseButtonDown(0) || !IsEditNote(__instance))
                {
                    return;
                }
                var cam = Camera.main;
                if (cam == null || !Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out var hit))
                {
                    return;
                }
                var ac = hit.transform.GetComponentInParent<AlienCharacter>();
                if (ac != null && ac.sector == MenuSector && ac.alien_txt == Button)
                {
                    Start(__instance);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Note image: {e}");
            }
        }
    }
}
