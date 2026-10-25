using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace Yuta;

internal sealed partial class IdleNotchWindow : Form
{
    private bool _hover;
    private bool _configuring;
    private bool _flattened;
    private Bitmap? _surface;
    private (Size size, float scale, bool attached, bool running, string text, bool hover, int accent, int content, int frame)? _surfaceKey;

    public bool Attached { get; set; }
    public bool Flattened
    {
        get => _flattened;
        set { if (_flattened == value) return; _flattened = value; _contentVersion++; RefreshSurface(); UpdateActivityTimer(); }
    }
    public bool TimerRunning { get; set; }
    public string ClockText { get; set; } = DateTime.Now.ToString("h:mm tt");
    public float RenderScale { get; set; } = 1;
    public Color AccentColor { get; set; } = AccentPalette.Resolve("custom");
    protected override bool ShowWithoutActivation => true;

    public IdleNotchWindow()
    {
        Text = "Yuta idle";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        Size = new(NotchGeometry.PillWidth, NotchGeometry.IdleHeight);
        Cursor = Cursors.Hand;
        AccessibleName = "Open Yuta";
        AccessibleRole = AccessibleRole.PushButton;
        InitializeActivity();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00080000 | 0x08000000 | 0x00000080; // layered, no activate, tool window
            return cp;
        }
    }

    public void Configure(Rectangle bounds, float scale, bool attached, string clock, bool running)
    {
        _morphing = false;
        _configuring = true;
        try
        {
            RenderScale = float.IsFinite(scale) && scale > 0 ? scale : 1;
            Attached = attached; ClockText = clock; TimerRunning = running;
            Bounds = bounds;
        }
        finally { _configuring = false; }
        RefreshSurface();
        UpdateActivityTimer();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RefreshSurface();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (IsHandleCreated && !_configuring) RefreshSurface();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true; RefreshSurface();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false; RefreshSurface();
        base.OnMouseLeave(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0021) { m.Result = new IntPtr(3); return; } // MA_NOACTIVATE
        base.WndProc(ref m);
    }

    public Bitmap RenderBitmap()
    {
        var width = Math.Max(1, Width); var height = Math.Max(1, Height);
        var scale = float.IsFinite(RenderScale) && RenderScale > 0 ? RenderScale : 1;
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        if (Flattened)
        {
            using var flat = RoundedRect(new RectangleF(0, -2 * scale, width, height + 2 * scale), Math.Min(2 * scale, height / 2f));
            g.FillPath(Brushes.Black, flat);
            return bitmap;
        }
        using var outline = NotchGeometry.Create(new RectangleF(0, 0, width, height), scale, Attached);
        using var body = new LinearGradientBrush(new Rectangle(0, 0, width, height),
            _hover ? Color.FromArgb(23, 26, 33) : Color.FromArgb(14, 16, 22),
            _hover ? Color.FromArgb(13, 16, 22) : Color.FromArgb(7, 9, 14), 90f);
        g.FillPath(body, outline);
        g.SetClip(outline);
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        var clockOpacity = DrawActivity(g, width, height, scale);
        if (clockOpacity > 0) DrawClock(g, width, height, scale, clockOpacity);
        return bitmap;
    }
    private void DrawClock(Graphics g, int width, int height, float scale, float opacity)
    {
        // Lay out every idle widget in the same content units, then rasterize at
        // the monitor's physical pixel size. Never stretch a small text bitmap.
        var contentScale = scale;
        var parts = ClockText.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var digits = parts.FirstOrDefault() ?? "";
        var label = TimerRunning ? "FOCUS" : parts.ElementAtOrDefault(1) ?? "";
        using var font = new Font("Segoe UI Semibold", NotchGeometry.IdleFontSize * contentScale, FontStyle.Regular, GraphicsUnit.Pixel);
        using var labelFont = new Font("Segoe UI Semibold", 8.2f * contentScale, FontStyle.Regular, GraphicsUnit.Pixel);
        var alpha = (int)Math.Round(255 * opacity);
        using var text = new SolidBrush(Color.FromArgb(alpha, 244, 246, 249));
        using var labelBrush = new SolidBrush(Color.FromArgb(alpha, TimerRunning ? Color.FromArgb(239, 189, 133) : Color.FromArgb(153, 160, 174)));
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.Alignment = StringAlignment.Center; format.LineAlignment = StringAlignment.Center;
        format.FormatFlags |= StringFormatFlags.NoWrap;
        // Grayscale hinting preserves the layered surface's alpha. Equal,
        // pixel-aligned digit cells keep both clock and countdown steady.
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        var digitWidth = (int)Math.Ceiling("0123456789".Max(c => g.MeasureString(c.ToString(), font, PointF.Empty, format).Width));
        var colonWidth = (int)Math.Ceiling(g.MeasureString(":", font, PointF.Empty, format).Width);
        var textWidth = digits.Sum(c => c == ':' ? colonWidth : digitWidth);
        var labelWidth = label.Length == 0 ? 0 : (int)Math.Ceiling(g.MeasureString(label, labelFont, PointF.Empty, format).Width);
        var gap = labelWidth == 0 ? 0 : (int)Math.Round(5 * contentScale);
        var x = (int)Math.Round((width - textWidth - gap - labelWidth) / 2f);
        foreach (var character in digits)
        {
            var advance = character == ':' ? colonWidth : digitWidth;
            g.DrawString(character.ToString(), font, text, new RectangleF(x, 0, advance, height), format);
            x += advance;
        }
        if (labelWidth > 0) g.DrawString(label, labelFont, labelBrush, new RectangleF(x + gap, 1 * scale, labelWidth, height - scale), format);
    }

    public void RefreshSurface()
    {
        if (_configuring || IsDisposed || Disposing || Width <= 0 || Height <= 0) return;
        var key = (Size, RenderScale, Attached, TimerRunning, ClockText, _hover, AccentColor.ToArgb(), _contentVersion, _activityFrame);
        if (_surfaceKey != key || _surface is null)
        {
            _surface?.Dispose(); _surface = RenderBitmap(); _surfaceKey = key;
        }
        // Do not use Form.Opacity or TransparencyKey: they switch layered-window
        // modes and conflict with this per-pixel-alpha surface.
        var screenDc = LayeredNative.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        IntPtr memoryDc = IntPtr.Zero, dib = IntPtr.Zero, previous = IntPtr.Zero;
        try
        {
            memoryDc = LayeredNative.CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            var info = new LayeredNative.BitmapInfo
            {
                Header = new LayeredNative.BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<LayeredNative.BitmapInfoHeader>(),
                    Width = _surface.Width, Height = -_surface.Height, Planes = 1, BitCount = 32
                }
            };
            dib = LayeredNative.CreateDIBSection(screenDc, ref info, 0, out var pixels, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero || pixels == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            var data = _surface.LockBits(new Rectangle(Point.Empty, _surface.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                var row = new byte[_surface.Width * 4];
                for (var y = 0; y < _surface.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                    Marshal.Copy(row, 0, IntPtr.Add(pixels, y * row.Length), row.Length);
                }
            }
            finally { _surface.UnlockBits(data); }
            previous = LayeredNative.SelectObject(memoryDc, dib);
            if (previous == IntPtr.Zero || previous == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
            var destination = new LayeredNative.Point(Left, Top);
            var source = new LayeredNative.Point(0, 0);
            var size = new LayeredNative.Size(_surface.Width, _surface.Height);
            var blend = new LayeredNative.BlendFunction { SourceConstantAlpha = 255, AlphaFormat = 1 };
            if (!LayeredNative.UpdateLayeredWindow(Handle, screenDc, ref destination, ref size, memoryDc, ref source, 0, ref blend, 2))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            if (previous != IntPtr.Zero && previous != new IntPtr(-1) && memoryDc != IntPtr.Zero) LayeredNative.SelectObject(memoryDc, previous);
            if (dib != IntPtr.Zero) LayeredNative.DeleteObject(dib);
            if (memoryDc != IntPtr.Zero) LayeredNative.DeleteDC(memoryDc);
            LayeredNative.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _activityTick.Stop(); _activityTick.Dispose(); _artwork?.Dispose(); _surface?.Dispose(); _surface = null; }
        base.Dispose(disposing);
    }

    private static class LayeredNative
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Point(int x, int y) { internal int X = x, Y = y; }
        [StructLayout(LayoutKind.Sequential)] internal struct Size(int width, int height) { internal int Width = width, Height = height; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] internal struct BlendFunction { internal byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)] internal struct BitmapInfoHeader
        {
            internal uint Size; internal int Width, Height; internal ushort Planes, BitCount;
            internal uint Compression, SizeImage; internal int XPelsPerMeter, YPelsPerMeter; internal uint ClrUsed, ClrImportant;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct BitmapInfo { internal BitmapInfoHeader Header; internal uint Colors; }
        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("gdi32.dll", SetLastError = true)] internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll", SetLastError = true)] internal static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll", SetLastError = true)] internal static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr value);
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UpdateLayeredWindow(IntPtr window, IntPtr destinationDc, ref Point destination, ref Size size,
            IntPtr sourceDc, ref Point source, uint colorKey, ref BlendFunction blend, uint flags);
    }
}
