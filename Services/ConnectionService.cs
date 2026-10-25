using NAudio.CoreAudioApi;
using Windows.Devices.Radios;

namespace Yuta.Services;

internal sealed record ConnectionState(bool Available, bool Enabled, string Label, string Detail);
internal sealed class ConnectionService
{
    private RadioAccessStatus? _access;
    private readonly SemaphoreSlim _gate = new(1);
    public async Task<Dictionary<string, ConnectionState>> ReadAsync()
    {
        var result = new Dictionary<string, ConnectionState>
        {
            ["wifi"] = new(false, false, "Wi-Fi unavailable", "Open Wi-Fi settings"),
            ["bluetooth"] = new(false, false, "Bluetooth unavailable", "Open Bluetooth settings")
        };
        try
        {
            var radios = await Radio.GetRadiosAsync();
            foreach (var (key, kind, label) in new[] { ("wifi", RadioKind.WiFi, "Wi-Fi"), ("bluetooth", RadioKind.Bluetooth, "Bluetooth") })
            {
                var radio = radios.FirstOrDefault(r => r.Kind == kind);
                if (radio is not null) result[key] = new(radio.State is RadioState.On or RadioState.Off, radio.State == RadioState.On,
                    label + (radio.State == RadioState.On ? " on" : " off"), radio.Name);
            }
        }
        catch { /* Settings arrows remain usable when a driver exposes no radio. */ }
        result["audio"] = await Task.Run(() =>
        {
            try
            {
                using var e = new MMDeviceEnumerator(); using var d = e.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return new ConnectionState(true, !d.AudioEndpointVolume.Mute, d.AudioEndpointVolume.Mute ? "Muted" : "Audio on", d.FriendlyName);
            }
            catch { return new ConnectionState(false, false, "No audio device", "Open sound settings"); }
        });
        return result;
    }
    public async Task<Dictionary<string, ConnectionState>> SetAsync(string kind, bool enabled)
    {
        if (kind is not ("wifi" or "bluetooth" or "audio")) throw new ArgumentException("Unknown connection.");
        await _gate.WaitAsync();
        try
        {
            if (kind == "audio") await Task.Run(() =>
            {
                using var e = new MMDeviceEnumerator(); using var d = e.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                d.AudioEndpointVolume.Mute = !enabled;
            });
            else
            {
                // Request consent only after the user presses a toggle, never on startup.
                _access ??= await Radio.RequestAccessAsync();
                if (_access != RadioAccessStatus.Allowed) { _access = null; throw new InvalidOperationException("Windows has not allowed radio control. Use the settings arrow to manage this connection."); }
                var radios = await Radio.GetRadiosAsync();
                var radio = radios.FirstOrDefault(r => r.Kind == (kind == "wifi" ? RadioKind.WiFi : RadioKind.Bluetooth))
                    ?? throw new InvalidOperationException("This device does not expose a controllable radio.");
                var target = enabled ? RadioState.On : RadioState.Off;
                if (await radio.SetStateAsync(target) != RadioAccessStatus.Allowed) throw new InvalidOperationException("Windows could not change this connection.");
                for (var i = 0; i < 20 && radio.State != target; i++) await Task.Delay(100);
                if (radio.State != target) throw new InvalidOperationException("The connection did not change. Check Windows settings or the device's hardware switch.");
            }
            return await ReadAsync();
        }
        finally { _gate.Release(); }
    }
}
