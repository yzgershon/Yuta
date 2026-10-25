using System.Drawing.Drawing2D;

namespace Yuta;

internal static class NotchGeometry
{
    internal const int IdleHeight = 28;
    internal const int FlatHeight = 4;
    internal const int PillWidth = 168;
    internal const int NotchWidth = 176;
    // The approved workbench reserves 224 DIP for titles, timers and notices,
    // including pill mode. Its 152 DIP music minimum applies only without text.
    internal const int ActivityWidth = 224;
    internal const float IdleFontSize = 12.8f;
    internal const float IdleItemScale = IdleHeight / 22f * 1.05f;
    private const float CircleBezier = .55228475f;

    internal static GraphicsPath Create(RectangleF bounds, float scale, bool attached)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0) return path;
        scale = float.IsFinite(scale) && scale > 0 ? scale : 1;
        var x = bounds.X; var y = bounds.Y; var w = bounds.Width; var h = bounds.Height;
        if (!attached)
        {
            var radius = Math.Min(26 * scale, Math.Min(h / 2, w / 2));
            var diameter = radius * 2;
            path.AddArc(x, y, diameter, diameter, 180, 90);
            path.AddArc(x + w - diameter, y, diameter, diameter, 270, 90);
            path.AddArc(x + w - diameter, y + h - diameter, diameter, diameter, 0, 90);
            path.AddArc(x, y + h - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        // Concave shoulders meet the screen edge; the bottom corners stay convex.
        // The idle shoulders and bottom radii follow its height proportionally.
        var shoulder = Math.Min(22 * scale, Math.Min(h * .57f, w / 4));
        var shoulderDepth = Math.Min(34 * scale, h * .52f);
        var radiusBottom = Math.Min(26 * scale, Math.Min(h - shoulderDepth, (w - 2 * shoulder) / 2));
        var left = x + shoulder; var right = x + w - shoulder;
        var bottom = y + h; var cornerY = bottom - radiusBottom;
        var control = radiusBottom * CircleBezier;
        path.StartFigure();
        path.AddLine(x, y, x + w, y);
        path.AddBezier(x + w, y, x + w - shoulder * .62f, y,
            right, y + shoulderDepth * .38f, right, y + shoulderDepth);
        path.AddLine(right, y + shoulderDepth, right, cornerY);
        path.AddBezier(right, cornerY, right, cornerY + control,
            right - radiusBottom + control, bottom, right - radiusBottom, bottom);
        path.AddLine(right - radiusBottom, bottom, left + radiusBottom, bottom);
        path.AddBezier(left + radiusBottom, bottom, left + radiusBottom - control, bottom,
            left, cornerY + control, left, cornerY);
        path.AddLine(left, cornerY, left, y + shoulderDepth);
        path.AddBezier(left, y + shoulderDepth, left, y + shoulderDepth * .38f,
            x + shoulder * .62f, y, x, y);
        path.CloseFigure();
        return path;
    }
}
