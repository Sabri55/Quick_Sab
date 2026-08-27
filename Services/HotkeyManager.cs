using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Quick_Sab.Models;

namespace Quick_Sab.Services
{
    /// <summary>Global keyboard shortcut (RegisterHotKey) attached to a WPF window.</summary>
    public sealed class HotkeyManager : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 0x5AB1;

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private readonly Window _window;
        private readonly Action _callback;
        private HwndSource _source;
        private IntPtr _hwnd;
        private bool _registered;

        public HotkeyManager(Window window, Action callback)
        {
            _window = window;
            _callback = callback;
        }

        /// <summary>Registers (or re-registers) the shortcut. Returns null on success, otherwise an error message.</summary>
        public string Register(HotkeyConfig cfg)
        {
            EnsureHook();
            Unregister();

            if (cfg == null) return "Missing hotkey configuration.";
            if (!Enum.TryParse<Key>(cfg.Key, true, out var key))
                return "Unknown key: " + cfg.Key;

            uint mods = MOD_NOREPEAT;
            if (cfg.Ctrl) mods |= MOD_CONTROL;
            if (cfg.Alt) mods |= MOD_ALT;
            if (cfg.Shift) mods |= MOD_SHIFT;
            if (cfg.Win) mods |= MOD_WIN;

            if (mods == MOD_NOREPEAT)
                return "The hotkey must include at least one modifier (Ctrl, Alt, Shift or Win).";

            var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            if (!RegisterHotKey(_hwnd, HOTKEY_ID, mods, vk))
                return "Could not register hotkey " + cfg + " (already used by another application?).";

            _registered = true;
            return null;
        }

        public void Unregister()
        {
            if (_registered && _hwnd != IntPtr.Zero)
            {
                UnregisterHotKey(_hwnd, HOTKEY_ID);
                _registered = false;
            }
        }

        private void EnsureHook()
        {
            if (_source != null) return;
            var helper = new WindowInteropHelper(_window);
            _hwnd = helper.EnsureHandle();
            _source = HwndSource.FromHwnd(_hwnd);
            _source?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                _callback?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Unregister();
            _source?.RemoveHook(WndProc);
            _source = null;
        }
    }
}
