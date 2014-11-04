using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;

namespace lib
{
    public interface IWinApi
    {
        IEnumerable<WindowData> ListWindowData(bool includeHidden = false);
        Dictionary<Process, IEnumerable<WindowData>> ListWindowDataByProcess();
        Bitmap CaptureWindow(IntPtr hwnd, PixelFormat pixelFormat = PixelFormat.Format32bppArgb);
        void AdjustWindow(IntPtr handle, int x, int y, int width, int height);
    }
}
