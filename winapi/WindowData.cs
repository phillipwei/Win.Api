using System;
using System.Drawing;

namespace lib
{
    public class WindowData
    {
        public IntPtr Handle { get; private set; }
        public uint ProcessId { get; private set; }
        public string Title { get; private set; }
        public Rectangle Rectangle { get; private set; }
        public int ZOrder { get; private set; }
        public bool Visible { get; private set; }
        
        public bool HasRectangle { get { return !Equals(Rectangle, Rectangle.Empty); } }

        public WindowData(IntPtr handle, uint processId, string title, Rectangle rectangle, int zOrder, bool visible)
        {
            Handle = handle;
            ProcessId = processId;
            Title = title;
            Rectangle = rectangle;
            ZOrder = zOrder;
            Visible = visible;
        }

        public override string ToString()
        {
            return String.Format("Handle={0}, ProcessId={1}, Title={2}, Rectangle=[{3},{4}:{5}x{6}], Z={7}, Visible={8}",
                Handle, ProcessId, Title,
                Rectangle.Left, Rectangle.Top, Rectangle.Right - Rectangle.Left, Rectangle.Bottom - Rectangle.Top,
                ZOrder, Visible);
        }
    }
}
