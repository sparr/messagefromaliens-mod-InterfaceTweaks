using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace InterfaceTweaks
{
    // Puts an image on the Windows clipboard, which Unity has no API for. The image goes on as a
    // device-independent bitmap, which every Windows program reads, and as PNG under the
    // registered "PNG" format, which browsers and chat programs prefer. Under Wine, the X11
    // driver offers the PNG format to Linux programs as image/png, and the bitmap as image/bmp.
    internal static class Clipboard
    {
        private const uint CF_DIB = 8;
        private const uint GMEM_MOVEABLE = 0x0002;
        private const int Attempts = 10;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr owner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint format, IntPtr data);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "RegisterClipboardFormatW")]
        private static extern uint RegisterClipboardFormat(string name);

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr memory);

        private static IntPtr window;
        private static uint process;
        // Kept so the collector can't free the callback while EnumWindows runs.
        private static readonly EnumWindowsProc findWindow = (w, _) =>
        {
            GetWindowThreadProcessId(w, out var owner);
            if (owner == process && IsWindowVisible(w))
            {
                window = w;
                return false;
            }
            return true;
        };

        // The game's window, which must own the clipboard's new data: with no owner,
        // EmptyClipboard leaves the clipboard without one, and SetClipboardData then fails. It is
        // looked up on each copy, so no handle from an earlier copy is used.
        private static IntPtr GameWindow()
        {
            window = IntPtr.Zero;
            process = (uint)Process.GetCurrentProcess().Id;
            EnumWindows(findWindow, IntPtr.Zero);
            return window;
        }

        // Puts an image of width by height pixels, given bottom row first, and its PNG encoding
        // on the clipboard. Returns null, or why it failed.
        internal static string SetImage(int width, int height, Color32[] pixels, byte[] png)
        {
            var owner = GameWindow();
            if (owner == IntPtr.Zero)
            {
                return "the game's window was not found";
            }
            var opened = false;
            for (var i = 0; i < Attempts && !(opened = OpenClipboard(owner)); i++)
            {
                // Another program has the clipboard open.
                Thread.Sleep(10);
            }
            if (!opened)
            {
                return $"the clipboard could not be opened (error {Marshal.GetLastWin32Error()})";
            }
            try
            {
                if (!EmptyClipboard())
                {
                    return $"the clipboard could not be emptied (error {Marshal.GetLastWin32Error()})";
                }
                var dib = Put(CF_DIB, Dib(width, height, pixels));
                var pngFormat = RegisterClipboardFormat("PNG");
                var pngError = pngFormat == 0 ? "the PNG format could not be registered" : Put(pngFormat, png);
                if (dib != null && pngError != null)
                {
                    return $"{dib}; {pngError}";
                }
                if (dib != null || pngError != null)
                {
                    Plugin.Log.LogWarning($"Note image: only one clipboard format was set: {dib ?? pngError}");
                }
                return null;
            }
            finally
            {
                CloseClipboard();
            }
        }

        // Copies the bytes into global memory and hands it to the clipboard, which then owns it.
        private static string Put(uint format, byte[] bytes)
        {
            var memory = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)(ulong)bytes.Length);
            if (memory == IntPtr.Zero)
            {
                return $"{bytes.Length} bytes could not be allocated (error {Marshal.GetLastWin32Error()})";
            }
            var at = GlobalLock(memory);
            if (at == IntPtr.Zero)
            {
                GlobalFree(memory);
                return $"memory could not be locked (error {Marshal.GetLastWin32Error()})";
            }
            Marshal.Copy(bytes, 0, at, bytes.Length);
            GlobalUnlock(memory);
            if (SetClipboardData(format, memory) == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                GlobalFree(memory);
                return $"format {format} could not be set (error {error})";
            }
            return null;
        }

        // A BITMAPINFOHEADER and 32-bit pixels, bottom row first as Unity gives them, in BGRA order.
        private static byte[] Dib(int width, int height, Color32[] pixels)
        {
            const int header = 40;
            var bytes = new byte[header + width * height * 4];
            void Int(int offset, int value) => BitConverter.GetBytes(value).CopyTo(bytes, offset);
            Int(0, header);
            Int(4, width);
            // Positive: bottom row first.
            Int(8, height);
            BitConverter.GetBytes((short)1).CopyTo(bytes, 12);
            BitConverter.GetBytes((short)32).CopyTo(bytes, 14);
            // BI_RGB, then the image size, which may be given for BI_RGB.
            Int(16, 0);
            Int(20, width * height * 4);
            for (var i = 0; i < pixels.Length; i++)
            {
                var o = header + i * 4;
                bytes[o] = pixels[i].b;
                bytes[o + 1] = pixels[i].g;
                bytes[o + 2] = pixels[i].r;
                bytes[o + 3] = 255;
            }
            return bytes;
        }
    }
}
