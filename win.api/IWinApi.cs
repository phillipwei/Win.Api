using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;

namespace win.api
{
    public interface IWinApi
    {
        IEnumerable<WindowData> GetWindows(bool includeHidden = false);
        
        Dictionary<Process, IEnumerable<WindowData>> GetWindowsByProcess();
        
        Bitmap CaptureWindow(IntPtr hwnd, PixelFormat pixelFormat = PixelFormat.Format32bppArgb);
        
        bool AdjustWindow(IntPtr handle, int x, int y, int width, int height);
    }
}
