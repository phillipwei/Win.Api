using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;

namespace Win.Api
{
    public interface IWinApi
    {
        IEnumerable<WindowData> GetWindows(bool includeHidden = false);
        IEnumerable<WindowData> GetWindows(Func<Process, bool> processSelector);
        Dictionary<Process, IEnumerable<WindowData>> GetWindowsByProcess();
        
        /// <summary>
        /// Get's the path for the given Process. Process.Module may fail if crossing bitness of processes. Uses WMI
        /// query so there is a high per invocation cost. Do something else if you are calling this repeatedly or in 
        /// bulk.
        /// </summary>
        /// <param name="processId"></param>
        /// <returns></returns>
        string GetProcessPath(int processId);

        Bitmap CaptureWindow(IntPtr hwnd, PixelFormat pixelFormat = PixelFormat.Format24bppRgb);
        bool AdjustWindow(IntPtr handle, int x, int y, int width, int height);
        void SendLeftClick(IntPtr hwnd, Point point);
        void SendLeftClick(IntPtr hwnd, Point point, TimeSpan clickDownTime);
        void SendDoubleLeftClick(IntPtr hwnd, Point point, TimeSpan clickDownTime);
        void RestoreWindow(IntPtr handle, TimeSpan timeoutPeriod);
        void SetForegroundWindow(IntPtr handle);
        void SendKey(IntPtr intPtr, char c, bool ctrl);
        void SendKeys(IntPtr intPtr, string keys);
        void RefreshNotificationArea();
        void CloseWindow(IntPtr handle);
    }
}