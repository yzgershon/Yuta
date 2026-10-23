namespace Yuta;

// Separate pointer policy from the desktop so boundary timing can be tested
// without moving the user's mouse. Keyboard opening stays open until first entry.
internal sealed class HoverRetraction
{
    private bool _armed;
    private long? _outsideSince;

    public void Reset(bool armed)
    {
        _armed = armed;
        _outsideSince = null;
    }

    public bool Observe(bool inside, bool blocked, long now)
    {
        if (inside) _armed = true;
        if (inside || blocked || !_armed)
        {
            _outsideSince = null;
            return false;
        }
        _outsideSince ??= now;
        return now - _outsideSince.Value >= 180;
    }
}
