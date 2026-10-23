using Yuta.Services;

namespace Yuta;

public sealed partial class NotchForm
{
    private readonly DeviceStatusService _devices = new();

    private void ActivityNotice(IdleNotice notice)
    {
        if (!IsHandleCreated || IsDisposed || Disposing || _allowClose) return;
        if (InvokeRequired) { try { BeginInvoke(() => ActivityNotice(notice)); } catch (InvalidOperationException) { } return; }
        _pill.SetNotice(notice, Environment.TickCount64);
        UpdateIdleActivityGeometry();
    }

    private void UpdateIdleActivityGeometry()
    {
        _pill.ReducedMotion = _reducedMotion;
        if (_expanded) return;
        if (_animation.Enabled) { ResizePanel(IdleWidth, IdleHeight); return; }
        var screens = Screen.AllScreens; var screen = screens[Math.Clamp(Setting("monitor", 0), 0, screens.Length - 1)];
        var attached = Setting("shape", "notch") == "notch";
        var size = IdleSize(); var top = screen.Bounds.Top + (attached ? 0 : (int)Math.Round(2 * DeviceDpi / 96d));
        var target = new Rectangle(_smoke ? -12000 : screen.Bounds.Left + (screen.Bounds.Width - size.Width) / 2, _smoke ? -12000 : top, size.Width, size.Height);
        _pill.TransitionTo(target, !_reducedMotion);
    }
}
