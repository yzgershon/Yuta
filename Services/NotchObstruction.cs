using System.Diagnostics;
using System.Windows.Automation;

namespace Yuta.Services;

internal readonly record struct ObstructionObservation(bool Known, bool Overlaps);

// Geometry only: no screenshots, page content, accessible names, or input values.
internal sealed class NotchObstructionReader
{
    private readonly object _gate = new();
    private readonly Func<IntPtr, Rectangle, ObstructionObservation>? _read;
    private Task<ObstructionObservation>? _pending;
    private IntPtr _window;
    private Rectangle _bounds;
    private bool _documentChrome;
    private bool _retainChrome;
    internal NotchObstructionReader(Func<IntPtr, Rectangle, ObstructionObservation>? read = null) => _read = read;

    internal Task<ObstructionObservation> ReadAsync(IntPtr window, Rectangle bounds, bool documentChrome = false, bool retainChrome = false)
    {
        lock (_gate)
        {
            // A stalled provider never creates an unbounded queue of workers.
            if (_pending is { IsCompleted: false })
                return window == _window && bounds == _bounds && documentChrome == _documentChrome && retainChrome == _retainChrome ? _pending : Task.FromResult(default(ObstructionObservation));
            _window = window; _bounds = bounds; _documentChrome = documentChrome; _retainChrome = retainChrome;
            return _pending = Task.Run(() => _read is null ? Read(window, bounds, documentChrome, retainChrome) : _read(window, bounds));
        }
    }

    internal static bool IntersectsControl(Rectangle notch, RectangleF control, bool interactive, bool offscreen)
    {
        if (!interactive || offscreen || !float.IsFinite(control.X) || !float.IsFinite(control.Y)
            || !float.IsFinite(control.Width) || !float.IsFinite(control.Height) || control.Width <= 0 || control.Height <= 0) return false;
        var overlap = RectangleF.Intersect(notch, control);
        return overlap.Width >= 2 && overlap.Height >= 2 && overlap.Width * overlap.Height >= 8;
    }

    private static bool Interactive(ControlType type) => type == ControlType.TabItem || type == ControlType.Button
        || type == ControlType.SplitButton || type == ControlType.Edit || type == ControlType.ComboBox
        || type == ControlType.MenuItem || type == ControlType.Hyperlink || type == ControlType.CheckBox
        || type == ControlType.RadioButton || type == ControlType.Slider;

    internal static bool CoversAppChrome(Rectangle notch, RectangleF window) =>
        IntersectsControl(notch, new(window.X, window.Y, window.Width, Math.Min(window.Height, notch.Height * 2)), true, false);

    private static ObstructionObservation Read(IntPtr window, Rectangle notch, bool documentChrome, bool retainChrome)
    {
        try
        {
            if (window == IntPtr.Zero || !Native.IsWindow(window)) return default;
            var root = AutomationElement.FromHandle(window);
            if (root is null) return default;
            var windowRect = root.Current.BoundingRectangle;
            if (windowRect.IsEmpty || !windowRect.IntersectsWith(new System.Windows.Rect(notch.X, notch.Y, notch.Width, notch.Height))) return new(true, false);
            // GatedSpace's top strip is custom app chrome, even when Chromium
            // temporarily exposes it as a single document rather than buttons.
            // Once browser tabs caused flattening, an in-window popup must not
            // undo it by temporarily replacing the accessible tab tree. Restore
            // only after the actual window no longer covers the notch strip.
            if ((documentChrome || retainChrome) && CoversAppChrome(notch, new((float)windowRect.X, (float)windowRect.Y, (float)windowRect.Width, (float)windowRect.Height))) return new(true, true);
            var walker = TreeWalker.ControlViewWalker;
            var remaining = 180;
            var watch = Stopwatch.StartNew();
            var found = false; var exhausted = false;
            void Visit(AutomationElement parent, int depth)
            {
                if (found) return;
                if (depth > (documentChrome ? 18 : 10) || remaining <= 0 || watch.ElapsedMilliseconds > 180) { exhausted = true; return; }
                for (var child = walker.GetFirstChild(parent); child is not null; child = walker.GetNextSibling(child))
                {
                    if (--remaining <= 0 || watch.ElapsedMilliseconds > 180) { exhausted = true; return; }
                    var info = child.Current;
                    // GatedSpace renders its app chrome in an Electron document.
                    // Inspect only intersecting geometry/control types, never text,
                    // names, values, or browser page documents in other apps.
                    if ((!documentChrome && info.ControlType == ControlType.Document) || info.IsOffscreen) continue;
                    var rect = info.BoundingRectangle;
                    var bounds = rect.IsEmpty ? RectangleF.Empty : new RectangleF((float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height);
                    if (IntersectsControl(notch, bounds, Interactive(info.ControlType), false)) { found = true; return; }
                    // Empty containers may hold real controls. Nonintersecting
                    // populated containers cannot cover this particular notch.
                    if (rect.IsEmpty || bounds.IntersectsWith(notch)) Visit(child, depth + 1);
                    if (found) return;
                }
            }
            Visit(root, 0);
            return new(found || !exhausted, found);
        }
        catch (Exception error) when (error is ElementNotAvailableException or InvalidOperationException
            or System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        { return default; }
    }
}

internal sealed class ObstructionPolicy
{
    private long? _hitSince, _clearSince;
    internal bool Flattened { get; private set; }
    internal bool Observe(ObstructionObservation value, long now)
    {
        if (!value.Known)
        {
            // Missing data is not proof that the notch has stopped covering a
            // control. Popups and busy providers commonly disappear temporarily.
            _hitSince = null; _clearSince = null;
            return Flattened;
        }
        if (value.Overlaps)
        {
            _clearSince = null; _hitSince ??= now;
            if (now - _hitSince >= 150) Flattened = true;
        }
        else
        {
            _hitSince = null; _clearSince ??= now;
            if (now - _clearSince >= 750) Flattened = false;
        }
        return Flattened;
    }
    internal void Reset(bool flattened = false) { Flattened = flattened; _hitSince = _clearSince = null; }
}
