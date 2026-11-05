using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using Windows.Storage.Streams;

namespace Yuta.Services;

internal sealed record NotificationPreview(uint Id, string App, string Title, string Body, DateTimeOffset Created, byte[]? Icon = null);
internal sealed record NotificationAccess(string State, bool Enabled);

// No inbox, disk storage or backlog. Startup/reconnect establishes a baseline;
// hidden and rate-limited alerts are consumed rather than replayed later.
internal sealed class NotificationPreviewPolicy
{
    private HashSet<(uint Id, long Created)> _seen = [];
    private bool _initialized;
    private long _nextPreview;
    internal void Reset() { _seen.Clear(); _initialized = false; _nextPreview = 0; }
    internal NotificationPreview? Observe(IReadOnlyList<NotificationPreview> items, DateTimeOffset now, bool canPresent)
    {
        var current = items.Take(512).ToArray();
        var fresh = current.Where(item => !_seen.Contains((item.Id, item.Created.UtcTicks)))
            .Where(item => item.Created <= now && now - item.Created < TimeSpan.FromSeconds(20))
            .OrderByDescending(item => item.Created).FirstOrDefault();
        _seen = current.Select(item => (item.Id, item.Created.UtcTicks)).ToHashSet();
        if (!_initialized) { _initialized = true; return null; }
        if (!canPresent || fresh is null || now.ToUnixTimeMilliseconds() < _nextPreview) return null;
        _nextPreview = now.ToUnixTimeMilliseconds() + 6000;
        return fresh;
    }
    internal static string Clean(string? text, int maximum)
    {
        var clean = string.Concat((text ?? "").Where(c => !char.IsControl(c)
            && c is not '\u202a' and not '\u202b' and not '\u202d' and not '\u202e' and not '\u202c')).Trim();
        return clean[..Math.Min(clean.Length, maximum)];
    }
}

internal sealed class NotificationPreviewService : IDisposable
{
    private readonly NotificationPreviewPolicy _policy = new();
    private bool _busy, _disposed;
    internal bool Enabled { get; private set; }
    internal event Action<NotificationPreview>? Preview;
    internal static bool Packaged
    {
        get { try { return !string.IsNullOrEmpty(Windows.ApplicationModel.Package.Current.Id.Name); } catch { return false; } }
    }
    internal NotificationAccess Status
    {
        get
        {
            if (!Packaged) return new("package-required", Enabled);
            try { return new(UserNotificationListener.Current.GetAccessStatus() switch
                { UserNotificationListenerAccessStatus.Allowed => "allowed", UserNotificationListenerAccessStatus.Denied => "denied", _ => "permission-required" }, Enabled); }
            catch { return new("unavailable", Enabled); }
        }
    }
    internal void SetEnabled(bool enabled) { if (enabled == Enabled) return; Enabled = enabled; _policy.Reset(); }
    internal async Task<NotificationAccess> RequestAsync()
    {
        if (!Packaged || _disposed) return Status;
        // Invoked only by the user's Enable button, on the Windows UI thread.
        try { await UserNotificationListener.Current.RequestAccessAsync(); }
        catch { return new("unavailable", false); }
        return Status;
    }
    internal async Task PollAsync(Func<bool> canPresent)
    {
        if (_disposed || _busy || !Enabled) return;
        if (Status.State != "allowed") { _policy.Reset(); return; }
        _busy = true;
        try
        {
            var notifications = await UserNotificationListener.Current.GetNotificationsAsync(NotificationKinds.Toast);
            if (_disposed || !Enabled || Status.State != "allowed") return;
            var rows = new List<NotificationPreview>();
            foreach (var item in notifications.OrderByDescending(n => n.CreationTime).Take(512))
            {
                try
                {
                    var texts = item.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric)?.GetTextElements();
                    if (texts is null || texts.Count == 0) continue;
                    var app = NotificationPreviewPolicy.Clean(item.AppInfo.DisplayInfo.DisplayName, 60);
                    if (app.Equals("Yuta", StringComparison.OrdinalIgnoreCase)) continue;
                    rows.Add(new(item.Id, app, NotificationPreviewPolicy.Clean(texts[0].Text, 180),
                        NotificationPreviewPolicy.Clean(string.Join(" · ", texts.Skip(1).Select(t => t.Text)), 240), item.CreationTime));
                }
                catch { /* An expired or malformed toast must not block other apps. */ }
            }
            var selected = _policy.Observe(rows, DateTimeOffset.UtcNow, canPresent());
            if (selected is null) return;
            byte[]? icon = null;
            try
            {
                var item = notifications.First(n => n.Id == selected.Id);
                using var stream = await item.AppInfo.DisplayInfo.GetLogo(new Windows.Foundation.Size(48, 48)).OpenReadAsync();
                if (stream.Size is > 0 and <= 262144)
                {
                    using var reader = new DataReader(stream.GetInputStreamAt(0));
                    var count = (uint)stream.Size;
                    if (await reader.LoadAsync(count) == count) { icon = new byte[count]; reader.ReadBytes(icon); }
                }
            }
            catch { /* A clean initial tile is used when an app has no readable logo. */ }
            if (!_disposed && Enabled && Status.State == "allowed" && canPresent()) Preview?.Invoke(selected with { Icon = icon });
        }
        catch { /* Permission/provider changes leave previews quiet; never log notification text. */ }
        finally { _busy = false; }
    }
    public void Dispose() { _disposed = true; _policy.Reset(); }
}
