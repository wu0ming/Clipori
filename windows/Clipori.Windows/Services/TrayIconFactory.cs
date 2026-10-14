using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Clipori.Windows.Services;

/// <summary>Produces a real violet cat icon instead of the default generic Windows application icon.</summary>
internal static class TrayIconFactory
{
    [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyIcon(IntPtr handle);
    public static Icon Make()
    {
        using var bitmap = new Bitmap(64, 64);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var bg = new SolidBrush(Color.FromArgb(145, 113, 209));
            using var white = new SolidBrush(Color.White);
            g.FillEllipse(bg, 2, 2, 60, 60);
            var left = new[] { new Point(14, 26), new Point(16, 10), new Point(30, 23) };
            var right = new[] { new Point(34, 23), new Point(49, 10), new Point(51, 26) };
            g.FillPolygon(white, left);
            g.FillPolygon(white, right);
            g.FillEllipse(white, 14, 20, 37, 30);
            using var eye = new SolidBrush(Color.FromArgb(98, 61, 169));
            g.FillEllipse(eye, 24, 35, 4, 5);
            g.FillEllipse(eye, 38, 35, 4, 5);
            g.FillEllipse(eye, 31, 40, 5, 3);
        }
        var handle = bitmap.GetHicon();
        try { using var icon = Icon.FromHandle(handle); return (Icon)icon.Clone(); }
        finally { DestroyIcon(handle); }
    }
}
