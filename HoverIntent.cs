namespace Yuta;

// Work in display-independent pixels so the same gesture works on every screen.
// Movement beyond a small tolerance restarts the settling window.
internal sealed class HoverIntent
{
    private long? _enteredAt;
    private long _settledAt;
    private PointF _anchor;

    public void Reset() => _enteredAt = null;

    public bool Observe(PointF point, bool held, long now)
    {
        if (held) { Reset(); return false; }
        if (_enteredAt is null)
        {
            _enteredAt = _settledAt = now; _anchor = point; return false;
        }
        var dx = point.X - _anchor.X; var dy = point.Y - _anchor.Y;
        if (dx * dx + dy * dy > 9)
        {
            _anchor = point; _settledAt = now;
        }
        return now - _enteredAt.Value >= 450 && now - _settledAt >= 250;
    }
}
