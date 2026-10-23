using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using NAudio.CoreAudioApi;

namespace Yuta.Services;

public sealed record ProcessUsage(string Name, long Memory, double Cpu);

public sealed class SystemServices : IDisposable
{
    private readonly MediaSessionService _media = new();
    public event Action<MediaSnapshot> MediaChanged { add => _media.Changed += value; remove => _media.Changed -= value; }
    public Task InitializeMediaAsync() => _media.InitializeAsync();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(25) };
    private long _idle, _kernel, _user, _rx, _tx;
    private DateTime _sampleAt = DateTime.UtcNow;
    private readonly Dictionary<int, double> _processCpu = new();
    private string _cpuName = "Processor", _gpuName = "Graphics";
    private readonly object _controlsGate = new();
    private Task _controlsWork = Task.CompletedTask;
    private Task<object>? _controlsRead;
    private object? _controlsCache;
    private long _controlsCachedAt, _controlsVersion;
    private Task? _initialization;
    public SystemServices()
    {
        Native.GetSystemTimes(out _idle, out _kernel, out _user);
        (_rx, _tx) = NetworkBytes();
    }
    public Task InitializeAsync()
    {
        lock (_controlsGate)
            return _initialization ??= Task.Run(() => {
                _cpuName = ReadWmi("SELECT Name FROM Win32_Processor", "Name") ?? "Processor";
                _gpuName = ReadWmi("SELECT Name FROM Win32_VideoController", "Name") ?? "Graphics";
            });
    }
    private static string? ReadWmi(string query, string field)
    {
        try { using var searcher = new ManagementObjectSearcher(query); using var results = searcher.Get(); return results.Cast<ManagementObject>().FirstOrDefault()?[field]?.ToString(); }
        catch { return null; }
    }
    private static (long rx, long tx) NetworkBytes()
    {
        long rx = 0, tx = 0;
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(x => x.OperationalStatus == OperationalStatus.Up && x.NetworkInterfaceType != NetworkInterfaceType.Loopback))
        {
            try { var s = adapter.GetIPv4Statistics(); rx += s.BytesReceived; tx += s.BytesSent; } catch { }
        }
        return (rx, tx);
    }
    public Task<object> StatsAsync() => Task.Run<object>(() =>
    {
        Native.GetSystemTimes(out var idle, out var kernel, out var user);
        var total = kernel - _kernel + user - _user;
        double? cpu = total > 0 ? Math.Clamp(100d * (total - (idle - _idle)) / total, 0, 100) : null;
        _idle = idle; _kernel = kernel; _user = user;
        var memory = new Native.MemoryStatus { Length = (uint)Marshal.SizeOf<Native.MemoryStatus>() };
        Native.GlobalMemoryStatusEx(ref memory);
        var now = DateTime.UtcNow; var seconds = Math.Max(.1, (now - _sampleAt).TotalSeconds);
        var (rx, tx) = NetworkBytes();
        var download = Math.Max(0, rx - _rx) / seconds; var upload = Math.Max(0, tx - _tx) / seconds;
        _rx = rx; _tx = tx; _sampleAt = now;
        var processes = new List<ProcessUsage>(); var alive = new HashSet<int>();
        foreach (var p in Process.GetProcesses())
        {
            using (p) try
            {
                if (p.Id == 0) continue;
                var current = p.TotalProcessorTime.TotalMilliseconds; alive.Add(p.Id);
                var percent = _processCpu.TryGetValue(p.Id, out var last) ? Math.Clamp((current - last) / (seconds * 10 * Environment.ProcessorCount), 0, 100) : 0;
                _processCpu[p.Id] = current;
                processes.Add(new(p.ProcessName, p.WorkingSet64, Math.Round(percent, 1)));
            } catch { }
        }
        foreach (var id in _processCpu.Keys.Where(x => !alive.Contains(x)).ToArray()) _processCpu.Remove(id);
        double? gpu = null;
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine");
            using var rows = searcher.Get();
            // Sum processes for each engine, then show the busiest engine (not a sum across engines).
            var engines = rows.Cast<ManagementObject>().Select(x => new { name = x["Name"]?.ToString() ?? "", value = Convert.ToDouble(x["UtilizationPercentage"] ?? 0) }).ToArray();
            if (engines.Length > 0) gpu = Math.Clamp(engines.GroupBy(x => x.name.Contains("luid_") ? x.name[x.name.IndexOf("luid_")..] : x.name).Max(g => g.Sum(x => x.value)), 0, 100);
        } catch { }
        var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
        var groupedProcesses = processes.GroupBy(x => x.Name).Select(g => new ProcessUsage(g.Key, g.Sum(x => x.Memory), Math.Min(100, Math.Round(g.Sum(x => x.Cpu), 1)))).ToArray();
        return new { cpu, cpuName = _cpuName, gpu, gpuName = _gpuName, ram = memory.Load, totalRam = memory.TotalPhysical, usedRam = memory.TotalPhysical - memory.AvailablePhysical, freeRam = memory.AvailablePhysical, diskTotal = drive.TotalSize, diskFree = drive.AvailableFreeSpace, download, upload, uptime = Environment.TickCount64 / 1000, processes = groupedProcesses };
    });
    public object Controls()
    {
        float? volume = null; bool muted = false; string output = "No audio output"; int? brightness = null;
        try { using var e = new MMDeviceEnumerator(); using var d = e.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia); volume = d.AudioEndpointVolume.MasterVolumeLevelScalar * 100; muted = d.AudioEndpointVolume.Mute; output = d.FriendlyName; } catch { }
        try { using var s = new ManagementObjectSearcher("root\\wmi", "SELECT CurrentBrightness FROM WmiMonitorBrightness"); using var r = s.Get(); var row = r.Cast<ManagementObject>().FirstOrDefault(); if (row != null) brightness = Convert.ToInt32(row["CurrentBrightness"]); } catch { }
        var power = SystemInformation.PowerStatus;
        return new { volume, muted, output, brightness, battery = power.BatteryLifePercent is >= 0 and <= 1 ? (int?)(power.BatteryLifePercent * 100) : null, charging = power.PowerLineStatus == PowerLineStatus.Online };
    }
    public Task<object> ControlsAsync()
    {
        lock (_controlsGate)
        {
            if (_controlsCache is not null && Environment.TickCount64 - _controlsCachedAt < 2_000)
                return Task.FromResult(_controlsCache);
            if (_controlsRead is { IsCompleted: false }) return _controlsRead;
            var version = _controlsVersion;
            return _controlsRead = QueueControlsWork(() =>
            {
                var controls = Controls();
                lock (_controlsGate)
                {
                    // A write requested after this read must not revive an older snapshot.
                    if (version == _controlsVersion)
                    {
                        _controlsCache = controls;
                        _controlsCachedAt = Environment.TickCount64;
                    }
                }
                return controls;
            });
        }
    }
    // Schedule while holding _controlsGate. The continuation always uses a worker,
    // including the first request; chaining preserves the order of slider changes.
    private Task<T> QueueControlsWork<T>(Func<T> work)
    {
        var scheduled = _controlsWork.ContinueWith(_ => work(), CancellationToken.None,
            TaskContinuationOptions.None, TaskScheduler.Default);
        _controlsWork = scheduled;
        return scheduled;
    }
    private void InvalidateControls()
    {
        lock (_controlsGate)
        {
            _controlsVersion++;
            _controlsCache = null;
            _controlsRead = null;
        }
    }
    private Task ChangeControlsAsync(Action change)
    {
        lock (_controlsGate)
        {
            InvalidateControls();
            return QueueControlsWork(() => { change(); return true; });
        }
    }
    private void ChangeControls(Action change)
    {
        InvalidateControls();
        try { change(); }
        finally { InvalidateControls(); }
    }
    public Task VolumeAsync(int value) => ChangeControlsAsync(() => SetVolume(value));
    public Task MuteAsync() => ChangeControlsAsync(ToggleMute);
    public Task BrightnessAsync(int value) => ChangeControlsAsync(() => SetBrightness(value));
    public void Volume(int value) => ChangeControls(() => SetVolume(value));
    public void Mute() => ChangeControls(ToggleMute);
    public void Brightness(int value) => ChangeControls(() => SetBrightness(value));
    private static void SetVolume(int value)
    {
        using var e = new MMDeviceEnumerator(); using var d = e.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        d.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0, 100) / 100f;
    }
    private static void ToggleMute()
    {
        using var e = new MMDeviceEnumerator(); using var d = e.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia); d.AudioEndpointVolume.Mute = !d.AudioEndpointVolume.Mute;
    }
    private static void SetBrightness(int value)
    {
        using var s = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM WmiMonitorBrightnessMethods"); using var rows = s.Get();
        if (rows.Count == 0) throw new InvalidOperationException("This display does not expose brightness controls. Use Windows display settings.");
        foreach (ManagementObject row in rows) row.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)Math.Clamp(value, 0, 100) });
    }
    public async Task<object> MediaAsync() => await _media.ReadAsync();
    public Task<bool> MediaAction(string action, double? position = null, string? source = null, string? trackTitle = null) => _media.ActionAsync(action, position, source, trackTitle);
    public void Dispose() { _media.Dispose(); _http.Dispose(); }
    public async Task<object> WeatherAsync(string city, bool fahrenheit)
    {
        if (city.Length < 2 || city.Length > 120) throw new ArgumentException("Enter a city name.");
        var geo = JsonNode.Parse(await _http.GetStringAsync("https://geocoding-api.open-meteo.com/v1/search?count=1&language=en&name=" + Uri.EscapeDataString(city)));
        var place = geo?["results"]?[0] ?? throw new InvalidOperationException("No matching city. Try a nearby city or postal code.");
        var lat = place["latitude"]!.ToJsonString(); var lon = place["longitude"]!.ToJsonString();
        var forecast = JsonNode.Parse(await _http.GetStringAsync($"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code&temperature_unit={(fahrenheit ? "fahrenheit" : "celsius")}"));
        return new { city = place["name"]!.GetValue<string>(), temperature = forecast!["current"]!["temperature_2m"]!.GetValue<double>(), code = forecast["current"]!["weather_code"]!.GetValue<int>(), unit = fahrenheit ? "°F" : "°C" };
    }
    public async Task<object> SpeedAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var watch = Stopwatch.StartNew();
        using var response = await _http.GetAsync("https://speed.cloudflare.com/__down?bytes=10000000", HttpCompletionOption.ResponseHeadersRead, timeout.Token); response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(); var buffer = new byte[64 * 1024]; long count = 0; int read;
        while ((read = await stream.ReadAsync(buffer, timeout.Token)) > 0) { count += read; if (count > 12_000_000) throw new InvalidOperationException("Unexpected speed test response."); }
        if (count != 10_000_000) throw new InvalidOperationException("The test server returned an incomplete sample. Try again later.");
        var down = count * 8 / watch.Elapsed.TotalSeconds / 1_000_000;
        var bytes = new byte[1_000_000]; System.Security.Cryptography.RandomNumberGenerator.Fill(bytes); watch.Restart();
        using var content = new ByteArrayContent(bytes); using var uploaded = await _http.PostAsync("https://speed.cloudflare.com/__up", content, timeout.Token); uploaded.EnsureSuccessStatusCode();
        return new { download = Math.Round(down, 1), upload = Math.Round(bytes.Length * 8 / watch.Elapsed.TotalSeconds / 1_000_000, 1), measuredAt = DateTimeOffset.Now };
    }
}
