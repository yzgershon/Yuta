using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Yuta.Services;

internal readonly record struct PowerReading(bool Online, bool Charging, int? Percent);

internal sealed class ActivityStatusTracker
{
    private PowerReading? _power;
    private string? _output;
    private bool _audioInitialized;
    internal IdleNotice? Power(PowerReading next)
    {
        var previous = _power; _power = next;
        if (previous is null || next.Percent is null || previous.Value.Online == next.Online && previous.Value.Charging == next.Charging) return null;
        return new("power", next.Online ? next.Charging ? "Charging" : next.Percent >= 100 ? "Fully charged" : "Plugged in" : "On battery", next.Percent);
    }
    internal IdleNotice? Audio(string? id, string name)
    {
        var changed = _audioInitialized && _output != id; _output = id; _audioInitialized = true;
        return changed ? new("audio", string.IsNullOrWhiteSpace(id) ? "Audio disconnected" : name) : null;
    }
}

internal sealed class DeviceStatusService : IDisposable
{
    private readonly object _gate = new();
    private readonly ActivityStatusTracker _tracker = new();
    private MMDeviceEnumerator? _enumerator;
    private EndpointNotifications? _notifications;
    private bool _disposed;
    public event Action<IdleNotice>? Notice;

    public Task InitializeAsync() => Task.Run(() =>
    {
        try
        {
            lock (_gate)
            {
                if (_disposed || _enumerator is not null) return;
                _enumerator = new MMDeviceEnumerator();
                ReadOutput();
                _notifications = new EndpointNotifications(() => { _ = Task.Run(RefreshOutput); });
                _enumerator.RegisterEndpointNotificationCallback(_notifications);
            }
        }
        catch (Exception error) { Program.Log(error); }
    });

    public void CheckPower()
    {
        var power = SystemInformation.PowerStatus;
        var next = new PowerReading(power.PowerLineStatus == PowerLineStatus.Online,
            power.BatteryChargeStatus.HasFlag(BatteryChargeStatus.Charging),
            !power.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery) && power.BatteryLifePercent is >= 0 and <= 1 ? (int)Math.Round(power.BatteryLifePercent * 100) : null);
        IdleNotice? notice;
        lock (_gate) { if (_disposed) return; notice = _tracker.Power(next); }
        if (notice is not null) Notice?.Invoke(notice);
    }

    private IdleNotice? ReadOutput()
    {
        try
        {
            using var device = _enumerator!.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return _tracker.Audio(device.ID, device.FriendlyName);
        }
        catch { return _tracker.Audio(null, ""); }
    }
    private void RefreshOutput()
    {
        IdleNotice? notice;
        lock (_gate) { if (_disposed || _enumerator is null) return; notice = ReadOutput(); }
        if (notice is not null) Notice?.Invoke(notice);
    }
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            if (_enumerator is not null && _notifications is not null)
                try { _enumerator.UnregisterEndpointNotificationCallback(_notifications); } catch { }
            _enumerator?.Dispose(); _enumerator = null;
        }
    }
    private sealed class EndpointNotifications(Action changed) : IMMNotificationClient
    {
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string id) { if (flow == DataFlow.Render && role == Role.Multimedia) changed(); }
        public void OnDeviceStateChanged(string id, DeviceState state) { }
        public void OnDeviceAdded(string id) { }
        public void OnDeviceRemoved(string id) { }
        public void OnPropertyValueChanged(string id, PropertyKey key) { }
    }
}
