using System;
using System.Drawing;

namespace Win.Api
{
    public class WindowData
    {
        public IntPtr Parent { get; private set; }
        public IntPtr Handle { get; private set; }
        public int ProcessId { get; private set; }
        public string Title { get; private set; }
        public Rectangle Rectangle { get; private set; }
        public int ZOrder { get; private set; }
        public bool Visible { get; private set; }

        public Size Size { get { return Rectangle.Size; } }
        public bool HasHandle { get { return !Equals(Handle, IntPtr.Zero); } }
        public bool HasTitle { get { return Title != String.Empty; } }
        public bool HasRectangle { get { return !Equals(Rectangle, Rectangle.Empty); } }
        public bool HasZOrder { get { return ZOrder != -1; } }
        public bool IsMinimized { get { return Rectangle.X == -32000 && Rectangle.Y == -32000; } }

        public WindowData(IntPtr handle, IntPtr parent, int processId, string title, Rectangle rectangle, int zOrder, bool visible)
        {
            Handle = handle;
            Parent = parent;
            ProcessId = processId;
            Title = title;
            Rectangle = rectangle;
            ZOrder = zOrder;
            Visible = visible;
        }

        public override string ToString()
        {
            return String.Format("Handle={0}, Parent={1}, ProcessId={2}, Title={3}, Rectangle=[{4},{5}:{6}x{7}], Z={8}, Visible={9}",
                Handle, Parent, ProcessId, Title,
                Rectangle.Left, Rectangle.Top, Rectangle.Right - Rectangle.Left, Rectangle.Bottom - Rectangle.Top,
                ZOrder, Visible);
        }
    }
}
