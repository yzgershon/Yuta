using Yuta.Services;

namespace Yuta;

public sealed partial class NotchForm
{
    private readonly NotchObstructionReader _obstructionReader = new();
    private readonly ObstructionPolicy _obstructionPolicy = new();
    private bool _obstructionBusy;
    private IntPtr _obstructionWindow;
    private long _lastObstructionPoll;
    private Rectangle _obstructionBounds;

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
        _pill.Flattened = flatten;
        UpdateIdleActivityGeometry();
        _hoverRequiresExit = _pill.ClientRectangle.Contains(_pill.PointToClient(Cursor.Position));
    }

    private async Task ObserveObstructionAsync()
    {
        if (_smoke || _expanded || _animation.Enabled || _contextMenuOpen || _dialogOpen || _webDialogOpen || OverlayHidden) return;
        if (!Setting("smartFlatten", true) || _activeApp is null)
        {
            _obstructionWindow = IntPtr.Zero; _obstructionPolicy.Reset(); ApplySmartFlatten(false); return;
        }
        var target = _activeApp; var bounds = NormalIdleBounds();
        if (_obstructionWindow != target.Window || _obstructionBounds != bounds)
        {
            var sameWindow = _obstructionWindow == target.Window;
            _obstructionWindow = target.Window; _obstructionBounds = bounds;
            _obstructionPolicy.Reset(sameWindow && _pill.Flattened);
            if (!sameWindow) ApplySmartFlatten(false);
        }
        if (_obstructionBusy || Environment.TickCount64 - _lastObstructionPoll < 300) return;
        _obstructionBusy = true; _lastObstructionPoll = Environment.TickCount64;
        try
        {
            var observation = await _obstructionReader.ReadAsync(target.Window, bounds).WaitAsync(TimeSpan.FromMilliseconds(900));
            if (IsDisposed || Disposing || _allowClose || _expanded || _animation.Enabled || _contextMenuOpen
                || OverlayHidden || !Setting("smartFlatten", true) || _activeApp?.Window != target.Window
                || _activeApp.Path != target.Path || NormalIdleBounds() != bounds) return;
            ApplySmartFlatten(_obstructionPolicy.Observe(observation, Environment.TickCount64));
        }
        catch (TimeoutException)
        {
            if (!IsDisposed && !Disposing && !_allowClose && !_expanded && !_animation.Enabled && !_contextMenuOpen
                && !OverlayHidden && Setting("smartFlatten", true) && _activeApp?.Window == target.Window && NormalIdleBounds() == bounds)
                ApplySmartFlatten(_obstructionPolicy.Observe(default, Environment.TickCount64));
        }
        catch (Exception error) { Program.Log(error); }
        finally { _obstructionBusy = false; }
    }
}
