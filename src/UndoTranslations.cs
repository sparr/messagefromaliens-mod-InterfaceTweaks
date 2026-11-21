using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InterfaceTweaks
{
    // An undo stack for translation edits.
    //
    // Every edit of a translation, by the game or by another mod, ends with Global.SaveGame, so
    // the change is found by comparing Global.savedata.dict with a copy taken at the previous
    // save. Each change is pushed with the navigation state of the last redraw, which is the
    // screen the player edited on, and a fingerprint of the glyphs drawn there.
    //
    // Undo writes the old values back, restores the navigation state, and redraws. If the redraw
    // draws different glyphs in the right panel than the screen had, as after a game's turn or a
    // game's reset, that screen can't be returned to: the undo is reverted, and that change is
    // dropped. If the note editor has another note open, or the note's text changed, only the
    // translation is reverted, so the editor keeps the note it has.
    internal static class UndoTranslations
    {
        private const int MaxChanges = 1000;

        private sealed class Change
        {
            // Each key's value before the change; null for a key that wasn't in the dictionary.
            internal Dictionary<string, string> Before;
            internal Dictionary<string, string> After;
            internal Screen Screen;
        }

        // The navigation state of a redraw, and a fingerprint of what it drew.
        private sealed class Screen
        {
            internal Dictionary<FieldInfo, object> Fields;
            internal List<string> TextHistory;
            internal List<int> LeftPages;
            internal int CurrentText;
            internal string Fingerprint;
            // The note open in the note editor, and its text as the game saved it, which the game
            // replaces with a new string on each edit of the note. Both null when no note is open.
            internal string NoteName;
            internal string NoteText;
            // The dictionary filter as the game saves it, which it restores the filter from.
            internal long Filter;
        }

        // Draw fields that say which page each panel shows, and where in it.
        private static readonly FieldInfo[] NavigationFields = new[]
        {
            "current_page", "parent_page", "parent_index", "currentrightpage", "currentrightpageup",
            "prev_page", "current_category", "current_categoryname", "dict_free_page",
            "selected_character", "hide_unknown_word_indict", "current_notebook", "notebook_pos",
        }.Select(name => AccessTools.Field(typeof(Draw), name) ?? throw new MissingFieldException(nameof(Draw), name)).ToArray();

        private static readonly FieldInfo DictFreePage = AccessTools.Field(typeof(Draw), "dict_free_page");
        private static readonly FieldInfo TextToUpdate = AccessTools.Field(typeof(Draw), "text_to_update");
        // dict_free_page as the redraw began: the redraw itself sets it false.
        private static bool dictFreePage;
        // Set while an undo writes and redraws, whose saves are not new changes.
        private static bool undoing;

        private static readonly List<Change> stack = new List<Change>();
        private static Dictionary<string, string> saved;
        private static Screen lastScreen;

        // The game writes "?" for a word it shows before it is translated, and an emptied label
        // is written as "". Neither is a translation. Other mods' keys, which have a prefix, keep
        // "?" as a value of its own.
        private static string Normalize(string key, string value) =>
            value == null || value.Trim() == "" || (value == "?" && !key.Contains(":")) ? null : value;

        // Keys a mod keeps for its own bookkeeping rather than for the player's text, which undo
        // leaves alone: Mod Settings records there which unlock messages it has shown.
        private static readonly string[] IgnoredPrefixes = { "modsettings." };

        private static bool Ignored(string key) => IgnoredPrefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal));

        private static void Reset()
        {
            stack.Clear();
            var dict = Global.savedata?.dict;
            saved = dict == null ? null : new Dictionary<string, string>(dict);
        }

        private static object Copy(object value) => value switch
        {
            List<string> list => new List<string>(list),
            List<int> list => new List<int>(list),
            _ => value,
        };

        private static Screen Capture(Draw d)
        {
            var fields = NavigationFields.ToDictionary(f => f, f => Copy(f.GetValue(d)));
            fields[DictFreePage] = dictFreePage;
            return new Screen
            {
                Fields = fields,
                TextHistory = new List<string>(d.text_history),
                LeftPages = new List<int>(d.currentleftpage),
                CurrentText = d.currenttext,
                Fingerprint = Fingerprint(d),
                NoteName = OpenNote(d),
                NoteText = NoteText(OpenNote(d)),
                Filter = Global.GetFlag("hide_unknown_word", 0L),
            };
        }

        // The note open in the note editor, or null. The game keeps a note open after the editor
        // is left another way than its back button, and shows it again when the editor is next
        // opened. After the back button closes it, the game still holds its glyphs.
        private static string OpenNote(Draw d) => string.IsNullOrEmpty(d.current_notebook) ? null : d.current_notebook;

        private static string NoteText(string note) =>
            note != null && Global.savedata?.notebook2 != null && Global.savedata.notebook2.TryGetValue(note, out var text) ? text : null;

        // Whether the note editor has the note open that the screen had, with the same saved
        // text, or has none open, as the screen had none.
        private static bool NoteMatches(Draw d, Screen s)
        {
            var note = OpenNote(d);
            return note == s.NoteName && ReferenceEquals(NoteText(note), s.NoteText);
        }

        // The glyphs drawn in each cell of the right panel, without their labels. The left panel
        // is left out: undo restores its history, and other mods, such as Calculator, draw over
        // it. So are the right panel's menu, whose bookmark glyph undo does not restore, the
        // lines drawn around cells, which the minesweeper toggles move, and the raw page text,
        // which marks won lines.
        private static string Fingerprint(Draw d)
        {
            var sb = new StringBuilder();
            if (d.alien_slots != null)
            {
                for (var x = 0; x < d.alien_slots.GetLength(0); x++)
                {
                    for (var y = 0; y < d.alien_slots.GetLength(1); y++)
                    {
                        var ac = d.alien_slots[x, y];
                        if (ac != null && ac.gameObject.activeInHierarchy && ac.sector != null && ac.sector.StartsWith("right") && ac.sector != "rightmenu")
                        {
                            sb.Append(x).Append(',').Append(y).Append(' ').Append(ac.alien_txt).Append(' ')
                                .Append(ac.extra_info).Append('\n');
                        }
                    }
                }
            }
            return sb.ToString();
        }

        // keepForward: keep the left panel's later pages if its history still begins with the
        // screen's, so the player can still go forward to them.
        private static void Restore(Draw d, Screen s, bool keepForward)
        {
            foreach (var kv in s.Fields)
            {
                kv.Key.SetValue(d, Copy(kv.Value));
            }
            var history = d.text_history;
            if (!keepForward || history.Count < s.TextHistory.Count || !history.Take(s.TextHistory.Count).SequenceEqual(s.TextHistory))
            {
                d.text_history = new List<string>(s.TextHistory);
                d.currentleftpage = new List<int>(s.LeftPages);
            }
            else
            {
                // Only the screen's own entries: entries dropped from the history before it leave
                // stale page numbers in its copy of the list.
                for (var i = 0; i < s.TextHistory.Count && i < s.LeftPages.Count && i < d.currentleftpage.Count; i++)
                {
                    d.currentleftpage[i] = s.LeftPages[i];
                }
            }
            d.currenttext = s.CurrentText;
            Global.SetFlag("hide_unknown_word", s.Filter);
        }

        private static void Write(Dictionary<string, string> values)
        {
            var dict = Global.savedata.dict;
            foreach (var kv in values)
            {
                if (kv.Value == null)
                {
                    dict.Remove(kv.Key);
                }
                else
                {
                    dict[kv.Key] = kv.Value;
                }
            }
        }

        private static void Save()
        {
            saved = new Dictionary<string, string>(Global.savedata.dict);
            Global.SaveGame();
        }

        // Redraws now, as Draw.Update does after Draw.UpdateText. UpdateText's tag makes
        // UpdateText1 clear every cell rather than only the right panel's text. UpdateText also
        // schedules a redraw at the end of this Update, which is cleared as Draw.Update clears
        // it, since that redraw would have no tag and clear only part of the screen.
        private static void Redraw(Draw d)
        {
            d.UpdateText();
            d.UpdateText1();
            TextToUpdate.SetValue(d, false);
            Canvas.ForceUpdateCanvases();
            foreach (var ac in UnityEngine.Object.FindObjectsByType<AlienCharacter>())
            {
                ac.UpdatePos();
            }
        }

        private static bool Editing()
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            return selected != null && (selected.GetComponent<TMP_InputField>()?.isFocused == true
                || selected.GetComponent<UnityEngine.UI.InputField>()?.isFocused == true);
        }

        private static void Undo(Draw d)
        {
            if (Global.savedata?.dict == null || stack.Count == 0)
            {
                return;
            }
            var change = stack[stack.Count - 1];
            undoing = true;
            try
            {
                if (!NoteMatches(d, change.Screen))
                {
                    // The editor's note is not the one the change was made with.
                    Write(change.Before);
                    stack.Remove(change);
                    Save();
                    Redraw(d);
                    return;
                }
                var current = Capture(d);
                Write(change.Before);
                Restore(d, change.Screen, keepForward: true);
                try
                {
                    Redraw(d);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Undo redraw failed: {e}");
                }
                stack.Remove(change);
                if (Fingerprint(d) == change.Screen.Fingerprint)
                {
                    Save();
                }
                else
                {
                    Plugin.Log.LogInfo("Undo dropped a change: its screen can no longer be shown");
                    Write(change.After);
                    Restore(d, current, keepForward: false);
                    Save();
                    Redraw(d);
                }
            }
            finally
            {
                undoing = false;
                lastScreen = Capture(d);
            }
        }

        internal static void UpdatePrefix(Draw __instance)
        {
            try
            {
                if (!Plugin.UndoEnabled.Value)
                {
                    // Copied afresh at the first save once undo is turned on again.
                    stack.Clear();
                    saved = null;
                    return;
                }
                if (Plugin.UndoShortcut.Value.IsDown() && !Editing())
                {
                    Undo(__instance);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Undo failed: {e}");
            }
        }

        internal static void UpdateText1Prefix(Draw __instance)
        {
            try
            {
                dictFreePage = (bool)DictFreePage.GetValue(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Undo failed: {e}");
            }
        }

        internal static void UpdateText1Postfix(Draw __instance)
        {
            try
            {
                if (Plugin.UndoEnabled.Value)
                {
                    lastScreen = Capture(__instance);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Undo capture failed: {e}");
            }
        }

        [HarmonyPatch(typeof(Global), nameof(Global.SaveGame))]
        private static class SaveGamePatch
        {
            private static void Prefix()
            {
                try
                {
                    if (!Plugin.UndoEnabled.Value)
                    {
                        return;
                    }
                    var dict = Global.savedata?.dict;
                    if (dict == null || saved == null)
                    {
                        Reset();
                        return;
                    }
                    if (undoing)
                    {
                        // Its own writes, such as a redraw of a solved quiz saving.
                        saved = new Dictionary<string, string>(dict);
                        return;
                    }
                    var before = new Dictionary<string, string>();
                    var after = new Dictionary<string, string>();
                    foreach (var key in dict.Keys.Union(saved.Keys))
                    {
                        if (Ignored(key))
                        {
                            continue;
                        }
                        saved.TryGetValue(key, out var old);
                        dict.TryGetValue(key, out var now);
                        if (Normalize(key, old) != Normalize(key, now))
                        {
                            before[key] = old;
                            after[key] = now;
                        }
                    }
                    saved = new Dictionary<string, string>(dict);
                    if (before.Count == 0 || lastScreen == null)
                    {
                        return;
                    }
                    stack.Add(new Change { Before = before, After = after, Screen = lastScreen });
                    if (stack.Count > MaxChanges)
                    {
                        stack.RemoveAt(0);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Undo SaveGame prefix failed: {e}");
                }
            }
        }

        // A finalizer, so that it runs also when LoadGame throws after replacing the save, as its
        // achievement calls after the load can.
        [HarmonyPatch(typeof(Global), nameof(Global.LoadGame))]
        private static class LoadGamePatch
        {
            private static void Finalizer()
            {
                try
                {
                    Reset();
                    lastScreen = null;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Undo LoadGame finalizer failed: {e}");
                }
            }
        }
    }
}
