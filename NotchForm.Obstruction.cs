using Yuta.Services;

namespace Yuta;

public sealed partial class NotchForm
{
    private readonly NotchObstructionReader _obstructionReader = new();
    private readonly ObstructionPolicy _obstructionPolicy = new();
    private bool _obstructionBusy;
    private IntPtr _obstructionWindow;
    private string? _obstructionPath;
    private long _lastObstructionPoll;
    private Rectangle _obstructionBounds;
    private readonly Dictionary<(IntPtr Window, string Path), bool> _knownObstructions = new();

    private Rectangle NormalIdleBounds()
    {
        var screens = Screen.AllScreens;
        var screen = screens[Math.Clamp(Setting("monitor", 0), 0, screens.Length - 1)];
        var attached = Setting("shape", "notch") == "notch";
        // Always inspect the full normal notch, even while the visible strip is
        // four DIP high. Otherwise it would oscillate as soon as it made room.
        var size = PanelSize(_pill.WidthFor(attached), NotchGeometry.IdleHeight);
        var top = screen.Bounds.Top + (attached ? 0 : (int)Math.Round(2 * DeviceDpi / 96d));
        return new(screen.Bounds.Left + (screen.Bounds.Width - size.Width) / 2, top, size.Width, size.Height);
    }

    private void ApplySmartFlatten(bool flatten)
    {
        if (_pill.Flattened == flatten) return;
        _hover.Stop(); _hoverIntent.Reset();
        if (flatten) _pill.ClearNotification();
        _pill.Flattened = flatten;
        UpdateIdleActivityGeometry();
        _hoverRequiresExit = _pill.ClientRectangle.Contains(_pill.PointToClient(Cursor.Position));
    }

    private async Task ObserveObstructionAsync()
    {
        if (_smoke || _expanded || _animation.Enabled || _contextMenuOpen || _dialogOpen || _webDialogOpen || OverlayHidden || _transientForeground) return;
        if (!Setting("smartFlatten", true) || _activeApp is null)
        {
            _obstructionWindow = IntPtr.Zero; _obstructionPolicy.Reset(); ApplySmartFlatten(false); return;
        }
        var target = _activeApp; var bounds = NormalIdleBounds();
        if (_obstructionWindow != target.Window || _obstructionPath != target.Path || _obstructionBounds != bounds)
        {
            var sameWindow = _obstructionWindow == target.Window && _obstructionPath == target.Path;
            _obstructionWindow = target.Window; _obstructionPath = target.Path; _obstructionBounds = bounds;
            var retained = sameWindow ? _pill.Flattened : _knownObstructions.GetValueOrDefault((target.Window, target.Path));
            _obstructionPolicy.Reset(retained);
            ApplySmartFlatten(retained);
        }
        if (_obstructionBusy || Environment.TickCount64 - _lastObstructionPoll < 300) return;
        _obstructionBusy = true; _lastObstructionPoll = Environment.TickCount64;
        try
        {
            var observation = await _obstructionReader.ReadAsync(target.Window, bounds, ForegroundContext.HasDocumentChrome(target.Path), target.Browser && _pill.Flattened).WaitAsync(TimeSpan.FromMilliseconds(900));
            if (IsDisposed || Disposing || _allowClose || _expanded || _animation.Enabled || _contextMenuOpen
                || OverlayHidden || _transientForeground || !Setting("smartFlatten", true) || _activeApp?.Window != target.Window
                || _activeApp.Path != target.Path || NormalIdleBounds() != bounds) return;
            ApplySmartFlatten(_obstructionPolicy.Observe(observation, Environment.TickCount64));
            if (_knownObstructions.Count >= 32 && !_knownObstructions.ContainsKey((target.Window, target.Path))) _knownObstructions.Clear();
            _knownObstructions[(target.Window, target.Path)] = _pill.Flattened;
        }
        catch (TimeoutException)
        {
            if (!IsDisposed && !Disposing && !_allowClose && !_expanded && !_animation.Enabled && !_contextMenuOpen
                && !OverlayHidden && !_transientForeground && Setting("smartFlatten", true)
                && _activeApp?.Window == target.Window && _activeApp.Path == target.Path && NormalIdleBounds() == bounds)
                ApplySmartFlatten(_obstructionPolicy.Observe(default, Environment.TickCount64));
        }
        catch (Exception error) { Program.Log(error); }
        finally { _obstructionBusy = false; }
    }
}
