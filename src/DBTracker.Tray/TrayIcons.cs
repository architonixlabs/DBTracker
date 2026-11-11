using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DBTracker.Tray.Core;

namespace DBTracker.Tray
{
    // One drawn icon per light. The tooltip and menu also state the status in words, so colour isn't
    // the only signal; red also gets a bar so it reads as different without colour.
    internal sealed class TrayIcons : IDisposable
    {
        private readonly Dictionary<StatusLight, Icon> _icons = new Dictionary<StatusLight, Icon>();

        public TrayIcons()
        {
            var size = SystemInformation.SmallIconSize.Width;
            _icons[StatusLight.Unknown] = Draw(size, Color.FromArgb(138, 145, 156), false);
            _icons[StatusLight.Green] = Draw(size, Color.FromArgb(46, 158, 91), false);
            _icons[StatusLight.Amber] = Draw(size, Color.FromArgb(224, 161, 0), false);
            _icons[StatusLight.Red] = Draw(size, Color.FromArgb(214, 69, 69), true);
        }

        public Icon For(StatusLight light) => _icons[light];

        private static Icon Draw(int size, Color color, bool bar)
        {
            using (var bitmap = new Bitmap(size, size))
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    var inset = Math.Max(1, size / 8);
                    var rect = new Rectangle(inset, inset, size - 2 * inset - 1, size - 2 * inset - 1);
                    using (var fill = new SolidBrush(color))
                    using (var outline = new Pen(Color.FromArgb(31, 37, 44), Math.Max(1f, size / 16f)))
                    {
                        g.FillEllipse(fill, rect);
                        g.DrawEllipse(outline, rect);
                    }
                    if (bar)
                    {
                        using (var white = new SolidBrush(Color.White))
                        {
                            var h = Math.Max(2, size / 6);
                            g.FillRectangle(white, size / 4, size / 2 - h / 2, size / 2, h);
                        }
                    }
                }

                var handle = bitmap.GetHicon();
                try
                {
                    return (Icon)Icon.FromHandle(handle).Clone();
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        public void Dispose()
        {
            foreach (var icon in _icons.Values)
            {
                icon.Dispose();
            }
        }
    }
}
