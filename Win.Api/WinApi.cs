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
using System.Management;
using log4net;

namespace Win.Api
{
    public class WinApi : IWinApi
    {
        private static readonly ILog _logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private readonly object _syncRoot = new object();
        public readonly int MaxWindowTitle = 512;

        public Rectangle GetWindowRectangle(IntPtr hWnd)
        {
            var rectangle = new NativeWinApi.Rectangle();
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
            var titleLength = NativeWinApi.GetWindowText(hWnd, title, title.Capacity + 1);
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

        public IEnumerable<WindowData> GetWindows(Func<Process,bool> processSelector)
        {
            return GetWindowsByProcess()
                .Where(kvp => processSelector(kvp.Key))
                .SelectMany(kvp => kvp.Value);
        }

        public string GetProcessPath(int processId)
        {
            var wmiQueryString = "SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = " + processId;

            using (var searcher = new ManagementObjectSearcher(wmiQueryString))
            using (var results = searcher.Get())
            {
                var mo = results.Cast<ManagementObject>().First();
                if (mo == null)
                {
                    return null;
                }
                return (string) mo["ExecutablePath"];
            }
        }

        // NOTE: It's super important the consumer realizes they are responsible for the memory usage of Bitmap -- if
        // you don't dispose, you are hosed. 
        public Bitmap CaptureWindow(IntPtr hwnd, PixelFormat pixelFormat)
        {
            if (!NativeWinApi.IsWindow(hwnd))
            {
                return null;
            }

            // _logger.DebugFormat("CaptureWindow({0},{1})", hwnd, pixelFormat);
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
            var parent = NativeWinApi.GetParent(hWnd);
            
            if (visible || includeHidden)
            {
                list.Add(new WindowData(hWnd, parent, (int)processId, GetWindowTitle(hWnd), GetWindowRectangle(hWnd), list.Count, visible));
            }

            var nextWindow = NativeWinApi.GetWindow(hWnd, NativeWinApi.GetWindowCommand.GW_HWNDNEXT);

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
                var errorMessage = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                Console.WriteLine(errorMessage);
            }
            return success;
        }

        public void SendLeftClick(IntPtr window, Point clientPoint)
        {
            SendLeftClick(window, clientPoint.X, clientPoint.Y, TimeSpan.FromSeconds(0.2));
        }

        public void SendLeftClick(IntPtr window, Point clientPoint, TimeSpan clickDownTime)
        {
            SendLeftClick(window, clientPoint.X, clientPoint.Y, clickDownTime);
        }

        public void SendLeftClick(IntPtr window, int clientX, int clientY, TimeSpan clickDownTime)
        {
            lock (_syncRoot)
            {
                var coord = CreateMouseClickCoordinates(clientX, clientY);
                NativeWinApi.SendMessage(
                    window,
                    NativeWinApi.Messages.WM_LBUTTONDOWN,
                    NativeWinApi.MouseKeyFlags.MK_LBUTTON,
                    coord);
                System.Threading.Thread.Sleep(clickDownTime);
                NativeWinApi.SendMessage(
                    window,
                    NativeWinApi.Messages.WM_LBUTTONUP,
                    IntPtr.Zero,
                    coord);
            }
        }

        public void SendDoubleLeftClick(IntPtr hwnd, Point point, TimeSpan clickDownTime)
        {
            lock (_syncRoot)
            {
                SendLeftClick(hwnd, point, clickDownTime);
                SendLeftClick(hwnd, point, clickDownTime);
            }
        }
        
        private IntPtr CreateMouseClickCoordinates(int x, int y)
        {
            return (IntPtr)((y << 16) | (x & 0xffff));
        }

        // http://stackoverflow.com/questions/10280000/how-to-create-lparam-of-sendmessage-wm-keydown
        private void SendKeyCode(IntPtr hwnd, NativeWinApi.VirtualKeys keyCode, bool extended, 
            NativeWinApi.Messages msgs)
        {
            var scanCode = NativeWinApi.MapVirtualKey((uint)keyCode, 0);
            uint lParam;

            lParam = (0x00000001 | (scanCode << 16));
            if (extended)
            {
                lParam = lParam | 0x01000000;
            }
            NativeWinApi.SendMessage(hwnd, msgs, (IntPtr)keyCode, (IntPtr)lParam);
        }

        public void SendKey(IntPtr window, char c)
        {
            lock (_syncRoot)
            {
                SendKeyCode(window, NativeWinApi.VkKeyScan(c), false, NativeWinApi.Messages.WM_KEYDOWN);
                SendKeyCode(window, NativeWinApi.VkKeyScan(c), false, NativeWinApi.Messages.WM_KEYUP);
            }
        }

        public void SendKey(IntPtr window, char c, bool ctrl)
        {
            lock (_syncRoot)
            {
                if(ctrl) SendKeyCode(window, NativeWinApi.VirtualKeys.Control, false, NativeWinApi.Messages.WM_KEYDOWN);
                SendKey(window, c);
                if (ctrl) SendKeyCode(window, NativeWinApi.VirtualKeys.Control, false, NativeWinApi.Messages.WM_KEYUP);
            }
        }

        public void SendKeys(IntPtr window, string keys)
        {
            lock (_syncRoot)
            {
                foreach (var c in keys)
                {
                    SendKey(window, c);
                }
            }
        }

        public void CloseWindow(IntPtr hWnd)
        {
            if (!NativeWinApi.IsWindow(hWnd)) return;
            NativeWinApi.SendMessage(hWnd, NativeWinApi.Messages.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
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

            var timeOutPoint = DateTime.Now + timeoutPeriod;
            while (DateTime.Now < timeOutPoint)
            {
                if (GetWindows().Any(wd => Equals(wd.Handle, handle) && !wd.IsMinimized))
                {
                    return;
                }
            }
            throw new TimeoutException("Could not restore window");
        }

        public void SetForegroundWindow(IntPtr handle)
        {
            NativeWinApi.SetForegroundWindow(handle);
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
                        var keyboardHookStruct = (NativeWinApi.KbDllHookStruct)Marshal.PtrToStructure(lParam, typeof(NativeWinApi.KbDllHookStruct));
                        var key = (Keys)keyboardHookStruct.vkCode;
                        var released = (keyboardHookStruct.flags & NativeWinApi.KbDllHookStructFlags.LLKHF_UP) == NativeWinApi.KbDllHookStructFlags.LLKHF_UP;
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
            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                var registeredHook = NativeWinApi.SetWindowsHookEx(hookType, proc, NativeWinApi.GetModuleHandle(curModule.ModuleName), 0);
                if (registeredHook == IntPtr.Zero)
                {
                    throw new Exception("Failed to register hook for hooktype " + hookType);
                }
                return registeredHook;
            }
        }

        public void RefreshNotificationArea()
        {
            var startBarHandle = NativeWinApi.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", "");
            var trayHandle = NativeWinApi.FindWindowEx(startBarHandle, IntPtr.Zero, "TrayNotifyWnd", "");
            var notifyHandle = NativeWinApi.FindWindowEx(trayHandle, IntPtr.Zero, "SysPager", "");
            var notifyIconsHandle = NativeWinApi.FindWindowEx(notifyHandle, IntPtr.Zero, "ToolbarWindow32", "Notification Area");
            if (notifyIconsHandle == IntPtr.Zero)
            {
                notifyIconsHandle = NativeWinApi.FindWindowEx(notifyHandle, IntPtr.Zero, "ToolbarWindow32", "User Promoted Notification Area");
            }
            var notifyRectangle = new NativeWinApi.Rectangle();
            NativeWinApi.GetClientRect(notifyIconsHandle, ref notifyRectangle);
            for (var x = notifyRectangle.Left; x < notifyRectangle.Right; x += 5)
            {
                for (var y = notifyRectangle.Top; y < notifyRectangle.Bottom; y += 5)
                {
                    NativeWinApi.SendMessage(notifyIconsHandle, NativeWinApi.Messages.WM_MOUSEMOVE, IntPtr.Zero, CreateMouseClickCoordinates(x, y));
                }
            }
        }
    }
}
