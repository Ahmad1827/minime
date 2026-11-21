// Win32 calls used by the ForYusef shell: the keyboard hook, staying awake, and the Sticky Keys guard.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace ForYusef
{
    static class Native
    {
        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
        public const uint ES_CONTINUOUS = 0x80000000, ES_SYSTEM_REQUIRED = 0x1, ES_DISPLAY_REQUIRED = 0x2;

        // Accessibility shortcuts (5x Shift = Sticky Keys, hold Shift = Filter Keys, hold NumLock = Toggle Keys)
        [StructLayout(LayoutKind.Sequential)] public struct STICKYKEYS { public uint cbSize, dwFlags; }
        [StructLayout(LayoutKind.Sequential)] public struct TOGGLEKEYS { public uint cbSize, dwFlags; }
        [StructLayout(LayoutKind.Sequential)]
        public struct FILTERKEYS { public uint cbSize, dwFlags, iWaitMSec, iDelayMSec, iRepeatMSec, iBounceMSec; }
        [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint a, uint b, ref STICKYKEYS p, uint w);
        [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint a, uint b, ref TOGGLEKEYS p, uint w);
        [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint a, uint b, ref FILTERKEYS p, uint w);
        public const uint SPI_GETFILTERKEYS = 0x32, SPI_SETFILTERKEYS = 0x33, SPI_GETTOGGLEKEYS = 0x34,
                          SPI_SETTOGGLEKEYS = 0x35, SPI_GETSTICKYKEYS = 0x3A, SPI_SETSTICKYKEYS = 0x3B;
        public const uint FEATURE_ON = 0x1, HOTKEYACTIVE = 0x4, CONFIRMHOTKEY = 0x8;
    }

    class AccessibilityGuard
    {
        Native.STICKYKEYS sk; Native.TOGGLEKEYS tk; Native.FILTERKEYS fk; bool saved;

        public void Disable()
        {
            sk.cbSize = (uint)Marshal.SizeOf(typeof(Native.STICKYKEYS));
            tk.cbSize = (uint)Marshal.SizeOf(typeof(Native.TOGGLEKEYS));
            fk.cbSize = (uint)Marshal.SizeOf(typeof(Native.FILTERKEYS));
            Native.SystemParametersInfo(Native.SPI_GETSTICKYKEYS, sk.cbSize, ref sk, 0);
            Native.SystemParametersInfo(Native.SPI_GETTOGGLEKEYS, tk.cbSize, ref tk, 0);
            Native.SystemParametersInfo(Native.SPI_GETFILTERKEYS, fk.cbSize, ref fk, 0);
            saved = true;
            // Only touch the shortcut flags, and only if the feature itself is off. Not persisted (fWinIni = 0).
            var s = sk; if ((s.dwFlags & Native.FEATURE_ON) == 0) { s.dwFlags &= ~(Native.HOTKEYACTIVE | Native.CONFIRMHOTKEY); Native.SystemParametersInfo(Native.SPI_SETSTICKYKEYS, s.cbSize, ref s, 0); }
            var t = tk; if ((t.dwFlags & Native.FEATURE_ON) == 0) { t.dwFlags &= ~(Native.HOTKEYACTIVE | Native.CONFIRMHOTKEY); Native.SystemParametersInfo(Native.SPI_SETTOGGLEKEYS, t.cbSize, ref t, 0); }
            var f = fk; if ((f.dwFlags & Native.FEATURE_ON) == 0) { f.dwFlags &= ~(Native.HOTKEYACTIVE | Native.CONFIRMHOTKEY); Native.SystemParametersInfo(Native.SPI_SETFILTERKEYS, f.cbSize, ref f, 0); }
        }

        public void Restore()
        {
            if (!saved) return;
            Native.SystemParametersInfo(Native.SPI_SETSTICKYKEYS, sk.cbSize, ref sk, 0);
            Native.SystemParametersInfo(Native.SPI_SETTOGGLEKEYS, tk.cbSize, ref tk, 0);
            Native.SystemParametersInfo(Native.SPI_SETFILTERKEYS, fk.cbSize, ref fk, 0);
            saved = false;
        }
    }
}
