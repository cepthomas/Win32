using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;



namespace Ephemera.Win32
{
    /// <summary>Native win32 interop.</summary>
    public static class Clipboard
    {
        [Flags]
        public enum KBDLLHOOKSTRUCTFlags : uint
        {
            LLKHF_EXTENDED = 0x01,
            LLKHF_INJECTED = 0x10,
            LLKHF_ALTDOWN = 0x20,
            LLKHF_UP = 0x80,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;    // A virtual-key code in the range 1 to 254.
            public uint scanCode;  // A hardware scan code for the key.
            public KBDLLHOOKSTRUCTFlags flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        /// <summary> https://www.pinvoke.net/default.aspx/Enums/HookType.html </summary>
        // Global hooks are not supported in the.NET Framework except for WH_KEYBOARD_LL and WH_MOUSE_LL.
        public enum HookType : int
        {
            WH_KEYBOARD_LL = 13,
            WH_MOUSE_LL = 14
        }

        /// <summary>Defines the callback for the hook. Apparently you can have multiple typed overloads.</summary>
        public delegate int HookProc(int code, int wParam, ref KBDLLHOOKSTRUCT lParam);


        [DllImport("User32.dll")]
        public static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);

        [DllImport("User32.dll")] //, CharSet = CharSet.Auto)]
        public static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

        [DllImport("user32.dll")]
        public static extern int CallNextHookEx(IntPtr idHook, int nCode, int wParam, ref KBDLLHOOKSTRUCT lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowsHookEx(HookType hookType, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        public static extern bool UnhookWindowsHookEx(IntPtr hInstance);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);


        ///////////////////////////////// TODO old low level ////////////////////////////
        // Clipboard for console applications. Based on https://github.com/MrM40/W-WinClipboard

        #region Definitions
        // https://learn.microsoft.com/en-us/windows/win32/dataxchg/standard-clipboard-formats
        public const int CF_TEXT = 1;         // ANSI Text format. A null character signals the end of the data.
        public const int CF_BITMAP = 2;       // A handle to a bitmap (HBITMAP).
        public const int CF_WAVE = 12;        // Audio data in one of the standard wave formats.
        public const int CF_UNICODETEXT = 13; // Unicode text format. A null character signals the end of the data.
        #endregion

        #region API
        /// <summary>
        /// Get text from clipboard.
        /// </summary>
        /// <returns></returns>
        public static string? GetText()
        {
            IntPtr handle = default;
            IntPtr pointer = default;

            if (!IsClipboardFormatAvailable(CF_UNICODETEXT))
            {
                return null;
            }

            TryOpenClipboard();

            try
            {
                handle = GetClipboardData(CF_UNICODETEXT);
                if (handle == default)
                {
                    return null;
                }

                pointer = GlobalLock(handle);
                if (pointer == default)
                {
                    return null;
                }

                var size = GlobalSize(handle);
                var buff = new byte[size];

                Marshal.Copy(pointer, buff, 0, size);

                return Encoding.Unicode.GetString(buff).TrimEnd('\0');
            }
            finally
            {
                if (pointer != default)
                {
                    GlobalUnlock(handle);
                }

                CloseClipboard();
            }
        }

        /// <summary>
        /// Set text in clipboard.
        /// </summary>
        /// <param name="text"></param>
        public static void SetText(string text)
        {
            TryOpenClipboard();

            EmptyClipboard();
            IntPtr hGlobal = default;

            try
            {
                var bytes = (text.Length + 1) * 2;
                hGlobal = Marshal.AllocHGlobal(bytes);

                if (hGlobal == default)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                var target = GlobalLock(hGlobal);

                if (target == default)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                try
                {
                    Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                }
                finally
                {
                    GlobalUnlock(target);
                }

                if (SetClipboardData(CF_UNICODETEXT, hGlobal) == default)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                hGlobal = default;
            }
            finally
            {
                if (hGlobal != default)
                {
                    Marshal.FreeHGlobal(hGlobal);
                }

                CloseClipboard();
            }
        }
        #endregion

        #region Private methods
        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="Win32Exception"></exception>
        static void TryOpenClipboard()
        {
            var num = 10;
            while (true)
            {
                if (OpenClipboard(default))
                {
                    break;
                }

                if (--num == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                Thread.Sleep(100);
            }
        }
        #endregion

        #region Native Methods
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetClipboardData(uint uFormat, IntPtr data);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        static extern bool EmptyClipboard();

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("Kernel32.dll", SetLastError = true)]
        static extern int GlobalSize(IntPtr hMem);
        #endregion
    }
}