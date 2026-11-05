using System.Drawing.Drawing2D;
using Yuta.Services;

namespace Yuta;

internal sealed partial class IdleNotchWindow
{
    private NotificationPreview? _notification;
    private Bitmap? _notificationIcon;
    private long _notificationUntil;
    internal bool NotificationVisible => _notification is not null;
    internal int HeightFor() => NotificationVisible ? 64 : NotchGeometry.IdleHeight;
    internal void SetNotification(NotificationPreview notification, long now)
    {
        ClearNotification();
        _notification = notification with { Icon = null };
        _notificationUntil = now + 5500;
        try
        {
            if (notification.Icon is { Length: > 0 and <= 262144 })
            {
                using var bytes = new MemoryStream(notification.Icon);
                using var decoded = Image.FromStream(bytes);
                _notificationIcon = new Bitmap(decoded, new Size(48, 48));
            }
        }
        catch { /* Use the app's initial if its logo cannot be decoded. */ }
        AccessibleName = "Dismiss notification preview";
        AccessibleDescription = $"{notification.App}: {notification.Title}. {notification.Body}";
        _contentVersion++; RefreshSurface();
    }
    internal bool ClearNotification()
    {
        if (_notification is null) return false;
        _notification = null; _notificationIcon?.Dispose(); _notificationIcon = null;
        AccessibleName = "Open Yuta"; AccessibleDescription = "Current activity";
        _contentVersion++; RefreshSurface(); return true;
    }
    internal bool ExpireNotification(long now) => _notification is not null && now >= _notificationUntil && ClearNotification();
    private void DrawNotification(Graphics g, int width, int height, float scale)
    {
        if (_notification is not { } item) return;
        var iconBounds = new RectangleF(32 * scale, 16 * scale, 30 * scale, 30 * scale);
        using var iconPath = RoundedRect(iconBounds, 8 * scale);
        var state = g.Save(); g.SetClip(iconPath, CombineMode.Intersect);
        if (_notificationIcon is not null)
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(_notificationIcon, iconBounds);
        }
        else
        {
            using var tile = new SolidBrush(Color.FromArgb(44, 48, 66)); g.FillPath(tile, iconPath);
            DrawActivityText(g, item.App.Length > 0 ? item.App[..1].ToUpperInvariant() : "Y", iconBounds, scale, 1, StringAlignment.Center);
        }
        g.Restore(state);
        void Line(string value, float y, float size, Color color, bool bold)
        {
            using var font = new Font(bold ? "Segoe UI Semibold" : "Segoe UI", size * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            using var ink = new SolidBrush(color);
            using var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            g.DrawString(value, font, ink, new RectangleF(72 * scale, y * scale, Math.Max(1, width - 112 * scale), 17 * scale), format);
        }
        Line(item.App, 7, 9.5f, Color.FromArgb(163, 171, 190), true);
        Line(item.Title, 21, 12, Color.FromArgb(246, 247, 250), true);
        Line(item.Body, 39, 10.5f, Color.FromArgb(193, 199, 213), false);
        using var close = new Pen(Color.FromArgb(142, 151, 172), 1.2f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var x = width - 36 * scale; var y = 30 * scale;
        g.DrawLine(close, x - 3 * scale, y - 3 * scale, x + 3 * scale, y + 3 * scale);
        g.DrawLine(close, x + 3 * scale, y - 3 * scale, x - 3 * scale, y + 3 * scale);
    }
}
