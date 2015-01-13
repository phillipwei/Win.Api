using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace Win.Api
{
    public class WinApi : IWinApi
    {
        public readonly int MaxWindowTitle = 512;

        public Rectangle GetWindowRectangle(IntPtr hWnd)
        {
            NativeWinApi.Rectangle rectangle = new NativeWinApi.Rectangle();
            NativeWinApi.GetWindowRect(hWnd, ref rectangle);

            return new Rectangle(
                rectangle.Left,
                rectangle.Top,
                Math.Abs(rectangle.Right - rectangle.Left),
                Math.Abs(rectangle.Bottom - rectangle.Top)
            );
        }

        public string GetWindowTitle(IntPtr hWnd)
        {
            var title = new StringBuilder(this.MaxWindowTitle);
            int titleLength = NativeWinApi.GetWindowText(hWnd, title, title.Capacity + 1);
            title.Length = titleLength;
            return title.ToString();
        }

        public IEnumerable<WindowData> GetWindows(bool includeHidden = false)
        {
            var returnValue = new List<WindowData>();
            PopulateNextWindow(returnValue, NativeWinApi.GetTopWindow(IntPtr.Zero), includeHidden);
            return returnValue;
        }

        public Dictionary<Process, IEnumerable<WindowData>> GetWindowsByProcess()
        {
            return GetWindows(false)
                .GroupBy(w => w.ProcessId)
                .ToDictionary(g => Process.GetProcessById((int)g.Key), g => g as IEnumerable<WindowData>);
        }

        public Bitmap CaptureWindow(IntPtr hwnd, PixelFormat pixelFormat)
        {
            var rectangle = new NativeWinApi.Rectangle();
            NativeWinApi.GetWindowRect(hwnd, ref rectangle);
            var bmp = new Bitmap(rectangle.Width, rectangle.Height, pixelFormat);
            
            using (var gfxBmp = Graphics.FromImage(bmp))
            {
                var hdcBitmap = gfxBmp.GetHdc();
                NativeWinApi.PrintWindow(hwnd, hdcBitmap, NativeWinApi.PrintWindowFlag.PW_DEFAULT);
                gfxBmp.ReleaseHdc(hdcBitmap);
            }

            return bmp;
        }

        private void PopulateNextWindow(List<WindowData> list, IntPtr hWnd, bool includeHidden)
        {
            var visible = NativeWinApi.IsWindowVisible(hWnd);
            uint processId;
            var threadId = NativeWinApi.GetWindowThreadProcessId(hWnd, out processId);

            if (visible || includeHidden)
            {
                list.Add(new WindowData(hWnd, processId, GetWindowTitle(hWnd), GetWindowRectangle(hWnd), list.Count, visible));
            }

            IntPtr nextWindow = NativeWinApi.GetWindow(hWnd, NativeWinApi.GetWindowCommand.GW_HWNDNEXT);

            if (nextWindow != IntPtr.Zero)
            {
                PopulateNextWindow(list, nextWindow, includeHidden);
            }
        }

        public bool AdjustWindow(IntPtr handle, int x, int y, int width, int height)
        {
            var success = NativeWinApi.MoveWindow(handle, x, y, width, height, true);
            if(!success)
            {
                string errorMessage = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                Console.WriteLine(errorMessage);
            }
            return success;
        }

        public void RestoreWindow(IntPtr handle, TimeSpan timeoutPeriod)
        {
            var first = GetWindows().First(wd => Equals(wd.Handle, handle));
            
            if (first == null)
            {
                throw new InvalidOperationException("Cannot ShowWindow(" + handle + ") -- does not exist");
            }

            if (first.IsMinimized)
            {
                NativeWinApi.SendMessage(
                    handle,
                    NativeWinApi.Messages.WM_SYSCOMMAND,
                    new IntPtr((int)NativeWinApi.SysCommands.SC_RESTORE),
                    IntPtr.Zero);
                // NativeWinApi.ShowWindow(handle, NativeWinApi.WindowShowStyle.Restore);
            }

            DateTime timeOutPoint = DateTime.Now + timeoutPeriod;
            while (DateTime.Now < timeOutPoint)
            {
                if (GetWindows().Any(wd => Equals(wd.Handle, handle) && !wd.IsMinimized))
                {
                    return;
                }
            }
            throw new TimeoutException("Could not restore window");
        }

        private static object _blockInputSyncRoot = new object();
        private static bool _blocked = false;

        public static bool BlockInput()
        {
            lock (_blockInputSyncRoot)
            {
                if (_blocked)
                {
                    return false;
                }
                return NativeWinApi.BlockInput(true);
            }
        }

        public static bool UnblockInput()
        {
            lock (_blockInputSyncRoot)
            {
                if (!_blocked)
                {
                    return false;
                }
                return NativeWinApi.BlockInput(false);
            }
        }

        public static void SuspendDrawing(Control parent)
        {
            NativeWinApi.SendMessage(parent.Handle, NativeWinApi.Messages.WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
        }

        public static void ResumeDrawing(Control parent)
        {
            NativeWinApi.SendMessage(parent.Handle, NativeWinApi.Messages.WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
            parent.Refresh();
        }

        internal static void SetMouseHook(NativeWinApi.HookProc proc)
        {
            SetHook(NativeWinApi.HookType.WH_MOUSE_LL, proc);
        }

        private class KeyboardHookWrapper : IDisposable
        {
            object _objLock = new object();
            List<KeyboardHookWrapper> _list;

            PressedKeysEventHandler _keyboardHandler;
            bool _registered;
            IntPtr _registeredHookId;
            HashSet<Keys> _pressedKeys;

            public KeyboardHookWrapper(PressedKeysEventHandler keyboardHandler)
            {
                _keyboardHandler = keyboardHandler;
                _registered = false;
                _pressedKeys = new HashSet<Keys>();
            }

            public IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
            {
                if (nCode >= 0)
                {
                    lock (_objLock)
                    {
                        NativeWinApi.KbDllHookStruct keyboardHookStruct = (NativeWinApi.KbDllHookStruct)Marshal.PtrToStructure(lParam, typeof(NativeWinApi.KbDllHookStruct));
                        Keys key = (Keys)keyboardHookStruct.vkCode;
                        bool released = (keyboardHookStruct.flags & NativeWinApi.KbDllHookStructFlags.LLKHF_UP) == NativeWinApi.KbDllHookStructFlags.LLKHF_UP;
                        if (released && _pressedKeys.Contains(key))
                        {
                            _pressedKeys.Remove(key);
                            _keyboardHandler(new HashSet<Keys>(_pressedKeys));
                        }
                        else
                        {
                            _pressedKeys.Add(key);
                            _keyboardHandler(new HashSet<Keys>(_pressedKeys));
                        }
                    }
                }
                return NativeWinApi.CallNextHookEx(NativeWinApi.HookType.WH_KEYBOARD_LL, nCode, wParam, lParam);
            }

            public void Register(List<KeyboardHookWrapper> list)
            {
                if (_registered)
                {
                    throw new NotImplementedException("Already registered");
                }
                _list = list;
                _registeredHookId = SetHook(NativeWinApi.HookType.WH_KEYBOARD_LL, HookProc);
                _list.Add(this);
                _registered = true;
            }

            public void Unregister()
            {
                if (_registered)
                {
                    NativeWinApi.UnhookWindowsHookEx(_registeredHookId);
                    _list.Remove(this);
                    _registered = false;
                }
            }

            private bool _disposed = false;

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!_disposed)
                {
                    if (disposing)
                    {
                        if (_registered)
                        {
                            Unregister();
                        }
                    }
                    _disposed = true;
                }
            }
        }

        private static List<KeyboardHookWrapper> KeyboardHooks = new List<KeyboardHookWrapper>();

        public delegate void PressedKeysEventHandler(HashSet<Keys> pressedKeys);

        public static void SetKeyboardHook(PressedKeysEventHandler keyHandler)
        {
            (new KeyboardHookWrapper(keyHandler)).Register(KeyboardHooks);
        }

        private static IntPtr SetHook(NativeWinApi.HookType hookType, NativeWinApi.HookProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                IntPtr registeredHook = NativeWinApi.SetWindowsHookEx(hookType, proc, NativeWinApi.GetModuleHandle(curModule.ModuleName), 0);
                if (registeredHook == IntPtr.Zero)
                {
                    throw new Exception("Failed to register hook for hooktype " + hookType);
                }
                return registeredHook;
            }
        }

        public static void MouselessClick(IntPtr window, Point clientPoint)
        {
            MouselessClick(window, clientPoint.X, clientPoint.Y);
        }

        private static IntPtr MakeMouseClickParam(int x, int y)
        {
            return (IntPtr)((y << 16) | (x & 0xffff));
        }

        public static void MouselessClick(IntPtr window, int clientX, int clientY)
        {
            // NativeWinApi.SetForegroundWindow(window);
            IntPtr coord = MakeMouseClickParam(clientX, clientY);
            NativeWinApi.SendMessage(
                window,
                NativeWinApi.Messages.WM_LBUTTONDOWN,
                IntPtr.Zero,
                // (IntPtr)helper.win32.NativeWinApi.MouseKeyFlags.MK_RBUTTON,
                coord);
            NativeWinApi.SendMessage(
                window,
                NativeWinApi.Messages.WM_LBUTTONUP,
                IntPtr.Zero,
                // (IntPtr)helper.win32.NativeWinApi.MouseKeyFlags.MK_RBUTTON,
                coord);
        }

        public static void LeftClick()
        {
            NativeWinApi.mouse_event((int)NativeWinApi.MouseEventMessages.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
            NativeWinApi.mouse_event((int)NativeWinApi.MouseEventMessages.MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        }

        public static void RefreshNotificationArea()
        {
            IntPtr startBarHandle = NativeWinApi.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", "");
            IntPtr trayHandle = NativeWinApi.FindWindowEx(startBarHandle, IntPtr.Zero, "TrayNotifyWnd", "");
            IntPtr notifyHandle = NativeWinApi.FindWindowEx(trayHandle, IntPtr.Zero, "SysPager", "");
            IntPtr notifyIconsHandle = NativeWinApi.FindWindowEx(notifyHandle, IntPtr.Zero, "ToolbarWindow32", "Notification Area");
            if (notifyIconsHandle == IntPtr.Zero)
            {
                notifyIconsHandle = NativeWinApi.FindWindowEx(notifyHandle, IntPtr.Zero, "ToolbarWindow32", "User Promoted Notification Area");
            }
            NativeWinApi.Rectangle notifyRectangle = new NativeWinApi.Rectangle();
            NativeWinApi.GetClientRect(notifyIconsHandle, ref notifyRectangle);
            for (int x = notifyRectangle.Left; x < notifyRectangle.Right; x += 5)
            {
                for (int y = notifyRectangle.Top; y < notifyRectangle.Bottom; y += 5)
                {
                    NativeWinApi.SendMessage(notifyIconsHandle, NativeWinApi.Messages.WM_MOUSEMOVE, IntPtr.Zero, MakeMouseClickParam(x, y));
                }
            }
        }
    }
}
