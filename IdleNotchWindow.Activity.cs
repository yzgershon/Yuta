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
    private long _noticeUntil, _noticeStarted, _morphStarted;
    private Rectangle _morphFrom, _morphTo;
    private bool _morphing;
    private bool _reducedMotion;
    private MediaSnapshot? _lastMedia;
    private int _activityFrame, _contentVersion;
    public bool MusicPlaying { get; private set; }
    private AudioLevels _levels = AudioLevels.Silent;
    public bool VoiceActive => _levels.VoiceActive;
    public bool HasActivity => MusicPlaying || VoiceActive || _notice is not null;
    public bool SetAudioLevels(AudioLevels levels)
    {
        var contextChanged = _levels.VoiceActive != levels.VoiceActive || _levels.VoiceApp != levels.VoiceApp;
        var changed = contextChanged || _levels.Bars.Length != levels.Bars.Length || _levels.Bars.Where((v, i) => (int)(v * 64) != (int)(levels.Bars[i] * 64)).Any();
        _levels = levels;
        if (changed && MotionEnabled && (MusicPlaying || VoiceActive))
        {
            _contentVersion++; _activityFrame = (_activityFrame + 1) % 10000;
            if (Visible && !Flattened && _notice is null) RefreshSurface();
        }
        if (contextChanged) { _contentVersion++; if (Visible) RefreshSurface(); }
        return contextChanged;
    }
    internal int WidthFor(bool attached) => HasActivity || TimerRunning ? NotchGeometry.ActivityWidth : attached ? NotchGeometry.NotchWidth : NotchGeometry.PillWidth;
    internal bool ActivityAnimating => _activityTick.Enabled;
    internal int ActivityFrame => _activityFrame;
    internal bool ReducedMotion { get => _reducedMotion; set { _reducedMotion = value; _contentVersion++; RefreshSurface(); UpdateActivityTimer(); } }
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
            _activityFrame = (_activityFrame + 1) % 10000;
            RefreshSurface(); UpdateActivityTimer();
        };
    }

    private bool PowerEntranceActive => _notice?.Kind == "power" && MotionEnabled && Environment.TickCount64 - _noticeStarted < 620;
    private void UpdateActivityTimer() => _activityTick.Enabled = Visible && (_morphing || !Flattened && PowerEntranceActive);

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
        _notice = notice; _noticeStarted = now; _noticeUntil = now + 4500;
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
        if (_displayMedia is null && !VoiceActive) return 1;
        var alpha = (int)(255 * contentOpacity);
        var itemScale = scale * NotchGeometry.IdleItemScale;
        var artBounds = new RectangleF(24 * scale, (height - 16 * itemScale) / 2, 16 * itemScale, 16 * itemScale);
        using var artPath = RoundedRect(artBounds, 4 * itemScale);
        var state = g.Save(); g.SetClip(artPath, CombineMode.Intersect);
        if (VoiceActive)
        {
            using var mic = new Pen(Color.FromArgb(alpha, 139, 213, 187), 1.5f * itemScale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            var x = artBounds.Left; var y = artBounds.Top;
            g.DrawArc(mic, x + 5 * itemScale, y + 2 * itemScale, 6 * itemScale, 9 * itemScale, 0, 360);
            g.DrawArc(mic, x + 2 * itemScale, y + 4 * itemScale, 12 * itemScale, 10 * itemScale, 0, 180);
            g.DrawLine(mic, x + 8 * itemScale, y + 14 * itemScale, x + 8 * itemScale, y + 16 * itemScale);
        }
        else if (_artwork is not null)
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
        var waveLeft = waveRight - (AudioEnvelope.BarCount - 1) * 1.25f * itemScale;
        using var waveColor = new LinearGradientBrush(new PointF(waveLeft, 0), new PointF(waveRight, 0), Color.White, Color.White)
        {
            WrapMode = WrapMode.TileFlipX,
            InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(alpha, 179, 148, 255), Color.FromArgb(alpha, 86, 182, 255)],
                Positions = [0, 1]
            }
        };
        using var wave = new Pen(waveColor, .65f * itemScale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        for (var i = 0; i < AudioEnvelope.BarCount; i++)
        {
            var level = i < _levels.Bars.Length && float.IsFinite(_levels.Bars[i]) ? Math.Clamp(_levels.Bars[i], 0, 1) : 0;
            var bar = 1.2f + (MotionEnabled ? level * 14.5f : 0);
            var x = waveLeft + i * 1.25f * itemScale;
            g.DrawLine(wave, x, (float)(height - bar * itemScale) / 2, x, (float)(height + bar * itemScale) / 2);
        }
        if (!TimerRunning)
        {
            var left = artBounds.Right + 10 * scale;
            DrawActivityText(g, VoiceActive ? "Mic · " + _levels.VoiceApp : _shortTitle, new(left, 0, Math.Max(1, waveLeft - 10 * scale - left), height), scale, contentOpacity);
        }
        return clockOpacity;
    }

    private void DrawNotice(Graphics g, IdleNotice notice, int width, int height, float scale, float opacity)
    {
        if (notice.Kind == "power")
        {
            DrawPowerNotice(g, notice, width, height, scale, opacity);
            return;
        }
        var alpha = (int)(255 * opacity);
        var item = scale * NotchGeometry.IdleItemScale;
        var saved = g.Save();
        g.TranslateTransform(24 * scale, (height - 16 * item) / 2);
        using var pen = new Pen(Color.FromArgb(alpha, 139, 213, 187), 1.6f * item) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawArc(pen, item, item, 14 * item, 14 * item, 180, 180);
        g.DrawLine(pen, item, 8 * item, item, 14 * item); g.DrawLine(pen, 15 * item, 8 * item, 15 * item, 14 * item);
        g.DrawLine(pen, 4 * item, 9 * item, 4 * item, 14 * item); g.DrawLine(pen, 12 * item, 9 * item, 12 * item, 14 * item);
        g.Restore(saved);
        DrawActivityText(g, notice.Title, new(55 * scale, 0, Math.Max(1, width - 79 * scale), height), scale, opacity);
    }

    private void DrawPowerNotice(Graphics g, IdleNotice notice, int width, int height, float scale, float opacity)
    {
        var elapsed = Math.Max(0, Environment.TickCount64 - _noticeStarted);
        var reveal = MotionEnabled ? 1 - Math.Pow(1 - Math.Clamp(elapsed / 220d, 0, 1), 3) : 1;
        opacity *= (float)(.35 + .65 * reveal);
        var alpha = (int)(255 * opacity);
        DrawActivityText(g, notice.Title, new(24 * scale, 0, Math.Max(1, width - 119 * scale), height), scale, opacity);
        if (notice.Percent is int value)
            DrawActivityText(g, Math.Clamp(value, 0, 100) + "%", new(width - 95 * scale, 0, 36 * scale, height), scale, opacity, StringAlignment.Far);
        var body = new RectangleF(width - 51 * scale, (height - 12 * scale) / 2, 25 * scale, 12 * scale);
        using var outline = RoundedRect(body, 3 * scale);
        using var shell = new SolidBrush(Color.FromArgb((int)(65 * opacity), 181, 190, 201));
        g.FillPath(shell, outline);
        using var rim = new Pen(Color.FromArgb((int)(120 * opacity), 181, 190, 201), .65f * scale);
        g.DrawPath(rim, outline);
        using var tip = RoundedRect(new(body.Right + 1.2f * scale, body.Top + 4 * scale, 1.8f * scale, 4 * scale), .7f * scale);
        g.FillPath(shell, tip);
        var inner = RectangleF.Inflate(body, -1.6f * scale, -1.6f * scale);
        using var innerPath = RoundedRect(inner, 1.7f * scale);
        var state = g.Save(); g.SetClip(innerPath, CombineMode.Intersect);
        var fillWidth = inner.Width * Math.Clamp(notice.Percent ?? 0, 0, 100) / 100f;
        var fillBounds = new RectangleF(inner.X, inner.Y, fillWidth, inner.Height);
        using var fill = new SolidBrush(Color.FromArgb(alpha, notice.Title == "On battery" ? Color.FromArgb(168, 178, 192) : Color.FromArgb(150, 224, 169)));
        g.FillRectangle(fill, fillBounds);
        // One short sheen on arrival; the filled percentage remains truthful.
        if (MotionEnabled && elapsed < 620 && fillWidth > 0)
        {
            g.SetClip(fillBounds, CombineMode.Intersect);
            var x = inner.Left - 7 * scale + (float)(elapsed / 620d) * (inner.Width + 14 * scale);
            using var sheen = new LinearGradientBrush(new RectangleF(x, inner.Top, 7 * scale, inner.Height), Color.Transparent, Color.FromArgb((int)(85 * opacity), Color.White), 0f);
            g.FillRectangle(sheen, x, inner.Top, 7 * scale, inner.Height);
        }
        g.Restore(state);
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
