using Yuta.Services;

namespace Yuta;

public sealed partial class NotchForm
{
    private readonly NotificationPreviewService _notifications = new();
    private bool CanPreviewNotification() => !OverlayHidden && !_pill.Flattened && !_expanded && !_animation.Enabled
        && !_transientForeground && !_contextMenuOpen && !_dialogOpen && !_webDialogOpen && !_hotkeyRecording;
    private void ShowNotification(NotificationPreview preview)
    {
        if (IsDisposed || Disposing || !CanPreviewNotification()) return;
        _hover.Stop(); _hoverIntent.Reset();
        _pill.SetNotification(preview, Environment.TickCount64);
        UpdateIdleActivityGeometry();
    }
    private async Task<object> EnableNotificationsAsync()
    {
        if (_smoke) return new NotificationAccess("permission-required", false);
        NotificationAccess access;
        _dialogOpen = true;
        try { access = await _notifications.RequestAsync(); }
        finally { _dialogOpen = false; }
        if (access.State == "allowed")
        {
            var settings = _store.Data["settings"]!.DeepClone(); settings["notificationPreviews"] = true;
            _store.Update("settings", settings); _notifications.SetEnabled(true);
        }
        return _notifications.Status;
    }
}
