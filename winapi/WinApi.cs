using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Drawing.Imaging;

namespace lib
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

        public IEnumerable<WindowData> ListWindowData(bool includeHidden)
        {
            var returnValue = new List<WindowData>();
            PopulateNextWindow(returnValue, NativeWinApi.GetTopWindow(IntPtr.Zero), includeHidden);
            return returnValue;
        }

        public Dictionary<Process, IEnumerable<WindowData>> ListWindowDataByProcess()
        {
            return ListWindowData(false)
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
    }
}
