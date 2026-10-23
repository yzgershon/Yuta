using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Yuta.Services;

namespace Yuta;

internal sealed record IdleNotice(string Kind, string Title, int? Percent = null);

internal sealed partial class IdleNotchWindow
{
    private readonly System.Windows.Forms.Timer _activityTick = new() { Interval = 15 };
    private MediaSnapshot? _displayMedia;
    private Bitmap? _artwork;
    private string? _artworkKey;
    private IdleNotice? _notice;
    private string _shortTitle = "Music";
    private long _noticeUntil, _morphStarted;
    private Rectangle _morphFrom, _morphTo;
    private bool _morphing;
    private bool _reducedMotion;
    private MediaSnapshot? _lastMedia;
    private int _activityFrame, _contentVersion;
    public bool MusicPlaying { get; private set; }
    public bool HasActivity => MusicPlaying || _notice is not null;
    internal int WidthFor(bool attached) => HasActivity || TimerRunning ? NotchGeometry.ActivityWidth : attached ? NotchGeometry.NotchWidth : NotchGeometry.PillWidth;
    internal bool ActivityAnimating => _activityTick.Enabled;
    internal int ActivityFrame => _activityFrame;
    internal bool ReducedMotion { get => _reducedMotion; set { _reducedMotion = value; UpdateActivityTimer(); } }
    private bool MotionEnabled => !_reducedMotion && SystemInformation.IsMenuAnimationEnabled;

    private void InitializeActivity()
    {
        VisibleChanged += (_, _) => UpdateActivityTimer();
        _activityTick.Tick += (_, _) =>
        {
            if (_morphing)
            {
                var t = Math.Clamp((Environment.TickCount64 - _morphStarted) / 260d, 0, 1);
                var ease = 1 - Math.Pow(1 - t, 3);
                var width = (int)Math.Round(_morphFrom.Width + (_morphTo.Width - _morphFrom.Width) * ease);
                var height = (int)Math.Round(_morphFrom.Height + (_morphTo.Height - _morphFrom.Height) * ease);
                _configuring = true;
                try { Bounds = new(_morphTo.X + (_morphTo.Width - width) / 2, _morphTo.Y, width, height); }
                finally { _configuring = false; }
                if (t >= 1) _morphing = false;
            }
            if (!Flattened && MusicPlaying && _notice is null && MotionEnabled) _activityFrame = (_activityFrame + 1) % 10000;
            RefreshSurface(); UpdateActivityTimer();
        };
    }

    private void UpdateActivityTimer() => _activityTick.Enabled = Visible && (_morphing || !Flattened && MusicPlaying && _notice is null && MotionEnabled);

    public void TransitionTo(Rectangle bounds, bool animate)
    {
        if (bounds == Bounds && !_morphing) return;
        if (_morphing && bounds == _morphTo) return;
        _morphFrom = Bounds; _morphTo = bounds; _morphStarted = Environment.TickCount64;
        _morphing = animate && Visible && MotionEnabled;
        if (!_morphing) { _configuring = true; try { Bounds = bounds; } finally { _configuring = false; } RefreshSurface(); }
        UpdateActivityTimer();
    }

    public void SetMedia(MediaSnapshot media)
    {
        var playing = media.Available && media.Playing;
        if (MusicPlaying == playing && _lastMedia?.Available == media.Available && _lastMedia.Title == media.Title && _lastMedia.Artist == media.Artist && _lastMedia.Artwork == media.Artwork) return;
        _lastMedia = media;
        MusicPlaying = playing;
        if (media.Available)
        {
            _displayMedia = media;
            _shortTitle = ShortTrackTitle.From(media.Title);
            if (_artworkKey != media.Artwork)
            {
                _artworkKey = media.Artwork; _artwork?.Dispose(); _artwork = null;
                try
                {
                    if (media.Artwork is { Length: > 0 and < 2_000_000 } data && data.StartsWith("data:image/", StringComparison.Ordinal))
                    {
                        using var stream = new MemoryStream(Convert.FromBase64String(data[(data.IndexOf(',') + 1)..]));
                        using var decoded = Image.FromStream(stream);
                        _artwork = new Bitmap(decoded);
                    }
                }
                catch (Exception error) when (error is ArgumentException or FormatException or OutOfMemoryException) { /* Use the music glyph for unavailable artwork. */ }
            }
        }
        AccessibleDescription = playing ? $"Playing {media.Title} by {media.Artist}" : "Current time";
        _contentVersion++; RefreshSurface(); UpdateActivityTimer();
    }

    public void SetNotice(IdleNotice notice, long now)
    {
        _notice = notice; _noticeUntil = now + 4500;
        _contentVersion++; RefreshSurface(); UpdateActivityTimer();
    }

    public bool ExpireNotice(long now)
    {
        if (_notice is null || now < _noticeUntil) return false;
        _notice = null;
        _contentVersion++; RefreshSurface(); UpdateActivityTimer(); return true;
    }

    private float DrawActivity(Graphics g, int width, int height, float scale)
    {
        if (!HasActivity) return 1;
        var normalWidth = Attached ? NotchGeometry.NotchWidth : NotchGeometry.PillWidth;
        var notice = _notice;
        var contentWidth = NotchGeometry.ActivityWidth - normalWidth;
        var reveal = Math.Clamp((width / scale - normalWidth) / contentWidth, 0, 1);
        if (reveal <= 0) return 1;
        // A timer remains centered between the music visuals. Temporary notices
        // take priority, then return to the timer or current track.
        var clockOpacity = TimerRunning && notice is null ? 1 : Math.Clamp(1 - reveal * 2, 0, 1);
        var contentOpacity = TimerRunning && notice is null ? reveal : Math.Clamp(reveal * 2 - 1, 0, 1);
        if (notice is not null)
        {
            DrawNotice(g, notice, width, height, scale, contentOpacity);
            return clockOpacity;
        }
        if (_displayMedia is null) return 1;
        var alpha = (int)(255 * contentOpacity);
        var itemScale = scale * NotchGeometry.IdleItemScale;
        var artBounds = new RectangleF(24 * scale, (height - 16 * itemScale) / 2, 16 * itemScale, 16 * itemScale);
        using var artPath = RoundedRect(artBounds, 4 * itemScale);
        var state = g.Save(); g.SetClip(artPath, CombineMode.Intersect);
        if (_artwork is not null)
        {
            using var attributes = new ImageAttributes();
            var matrix = new ColorMatrix { Matrix33 = contentOpacity }; attributes.SetColorMatrix(matrix);
            var edge = Math.Min(_artwork.Width, _artwork.Height);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(_artwork, Rectangle.Round(artBounds), (_artwork.Width - edge) / 2, (_artwork.Height - edge) / 2, edge, edge, GraphicsUnit.Pixel, attributes);
        }
        else
        {
            using var fill = new SolidBrush(Color.FromArgb(alpha, 55, 49, 72)); g.FillPath(fill, artPath);
            using var note = new Pen(Color.FromArgb(alpha, AccentColor), 1.5f * itemScale);
            var x = artBounds.Left; var y = artBounds.Top;
            g.DrawLine(note, x + 8 * itemScale, y + 3 * itemScale, x + 8 * itemScale, y + 11 * itemScale);
            g.DrawLine(note, x + 8 * itemScale, y + 3 * itemScale, x + 12 * itemScale, y + 2 * itemScale);
            using var dot = new SolidBrush(Color.FromArgb(alpha, AccentColor));
            g.FillEllipse(dot, x + 4 * itemScale, y + 9 * itemScale, 5 * itemScale, 3 * itemScale);
        }
        g.Restore(state);
        var waveRight = width - 24 * scale;
        var waveLeft = waveRight - 14.4f * itemScale;
        using var wave = new Pen(Color.FromArgb(alpha, AccentColor), 1.8f * itemScale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        for (var i = 0; i < 5; i++)
        {
            var phase = (Environment.TickCount64 % 600000) * .0065;
            var bar = 3 + (Math.Sin(phase + i * 1.7) + 1) * 3 + (Math.Sin(phase * .63 + i) + 1) * 1.1;
            var x = waveLeft + i * 3.6f * itemScale;
            g.DrawLine(wave, x, (float)(height - bar * itemScale) / 2, x, (float)(height + bar * itemScale) / 2);
        }
        if (!TimerRunning)
        {
            var left = artBounds.Right + 10 * scale;
            DrawActivityText(g, _shortTitle, new(left, 0, Math.Max(1, waveLeft - 10 * scale - left), height), scale, contentOpacity);
        }
        return clockOpacity;
    }

    private void DrawNotice(Graphics g, IdleNotice notice, int width, int height, float scale, float opacity)
    {
        var alpha = (int)(255 * opacity);
        var saved = g.Save();
        // Reuse the 22 DIP glyph coordinates at the approved proportional size.
        g.TranslateTransform(24 * scale, (height - 16 * scale * NotchGeometry.IdleItemScale) / 2);
        g.ScaleTransform(NotchGeometry.IdleItemScale, NotchGeometry.IdleItemScale);
        g.TranslateTransform(-21 * scale, -3 * scale);
        using var pen = new Pen(Color.FromArgb(alpha, notice.Kind == "power" ? Color.FromArgb(159, 220, 172) : AccentColor), 1.6f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        if (notice.Kind == "power")
        {
            g.DrawRectangle(pen, 21 * scale, 7 * scale, 15 * scale, 8 * scale);
            g.DrawLine(pen, 38 * scale, 9 * scale, 38 * scale, 13 * scale);
            using var fill = new SolidBrush(pen.Color);
            if (notice.Percent is int percent) g.FillRectangle(fill, 23 * scale, 9 * scale, 11 * scale * Math.Clamp(percent, 0, 100) / 100f, 4 * scale);
        }
        else
        {
            g.DrawArc(pen, 22 * scale, 4 * scale, 14 * scale, 14 * scale, 180, 180);
            g.DrawLine(pen, 22 * scale, 11 * scale, 22 * scale, 17 * scale); g.DrawLine(pen, 36 * scale, 11 * scale, 36 * scale, 17 * scale);
            g.DrawLine(pen, 25 * scale, 12 * scale, 25 * scale, 17 * scale); g.DrawLine(pen, 33 * scale, 12 * scale, 33 * scale, 17 * scale);
        }
        g.Restore(saved);
        DrawActivityText(g, notice.Title, new(55 * scale, 0, Math.Max(1, width - (notice.Percent.HasValue ? 112 : 79) * scale), height), scale, opacity);
        if (notice.Percent is int value) DrawActivityText(g, value + "%", new(width - 57 * scale, 0, 40 * scale, height), scale, opacity, StringAlignment.Far);
    }

    private static void DrawActivityText(Graphics g, string text, RectangleF bounds, float scale, float opacity, StringAlignment alignment = StringAlignment.Near)
    {
        using var font = new Font("Segoe UI Semibold", NotchGeometry.IdleFontSize * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.FromArgb((int)(255 * opacity), 244, 246, 249));
        using var format = new StringFormat { Alignment = alignment, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(text, font, brush, bounds, format);
    }

    private static GraphicsPath RoundedRect(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath(); var d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90); path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90); path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
    }
}
