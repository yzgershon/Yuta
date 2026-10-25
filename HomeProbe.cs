using System.Text.Json;
using Yuta.Services;
using NAudio.CoreAudioApi;

namespace Yuta;

// Explicit read-only integration probe. Does not toggle radios, change playback,
// open a microphone stream, paste, or execute a power action.
internal static class HomeProbe
{
    internal static async Task RunAsync()
    {
        var output = Path.Combine(AppContext.BaseDirectory, "home-probe.json");
        try
        {
            var connections = await new ConnectionService().ReadAsync();
            var profile = Path.Combine(Path.GetTempPath(), "Yuta-probe-" + Guid.NewGuid().ToString("N"));
            var launcher = new Launcher(new Store(profile));
            await launcher.ScanAsync();
            var selected = new[] { "Google Chrome", "Microsoft Edge", "Spotify", "File Explorer", "Visual Studio Code", "Windows Terminal" }
                .Select(name => launcher.Apps.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                .OfType<LaunchItem>().ToArray();
            var icons = await new AppIconService().ReadAsync(selected);
            var folder = Path.Combine(AppContext.BaseDirectory, "probe-icons"); Directory.CreateDirectory(folder);
            var iconResults = selected.Select(app =>
            {
                if (icons[app.Id] is not string data) return new { app.Name, available = false, width = 0, height = 0 };
                var bytes = Convert.FromBase64String(data[(data.IndexOf(',') + 1)..]);
                File.WriteAllBytes(Path.Combine(folder, app.Name + ".png"), bytes);
                using var stream = new MemoryStream(bytes); using var bitmap = Image.FromStream(stream);
                return new { app.Name, available = true, width = bitmap.Width, height = bitmap.Height };
            }).ToArray();
            using var system = new SystemServices(); await system.InitializeMediaAsync();
            var media = (MediaSnapshot)await system.MediaAsync();
            var endpoints = await Task.Run(ReadAudioEndpoints);
            using var audio = new AudioLevelService(); audio.SetMedia(media); audio.Start();
            float maximum = 0; var frames = 0; var available = 0; var voice = false;
            var distinct = new HashSet<int>(); var microphoneApps = new HashSet<string>(); var peakReadings = new List<float>(); var barReadings = new List<float>();
            for (var i = 0; i < 120; i++)
            {
                await Task.Delay(50); var level = audio.Latest;
                frames++; if (level.Available) available++;
                maximum = Math.Max(maximum, level.Bars.Max()); distinct.Add((int)(level.Bars[0] * 100)); voice |= level.VoiceActive;
                if (level.VoiceActive) microphoneApps.Add(level.VoiceApp);
                if (level.Available) { peakReadings.Add(level.Peak); barReadings.AddRange(level.Bars); }
            }
            var sortedBars = barReadings.Order().ToArray();
            float Percentile(double fraction) => sortedBars.Length == 0 ? 0 : sortedBars[(int)((sortedBars.Length - 1) * fraction)];
            var barDistribution = new { median = Percentile(.5), p95 = Percentile(.95), above80Percent = barReadings.Count == 0 ? 0 : barReadings.Count(v => v >= .8f) * 100d / barReadings.Count, above90Percent = barReadings.Count == 0 ? 0 : barReadings.Count(v => v >= .9f) * 100d / barReadings.Count };
            File.WriteAllText(output, JsonSerializer.Serialize(new { connections, icons = iconResults, endpoints,
                audio = new { mediaAvailable = media.Available, playing = media.Playing, source = media.SourceName, frames, meteredFrames = available, maximumLevel = maximum, distinctLevels = distinct.Count, barDistribution, microphoneSessionObserved = voice, microphoneApps, peakReadings, recordedAudio = false } }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error) { File.WriteAllText(output, JsonSerializer.Serialize(new { error = error.ToString() })); Environment.ExitCode = 1; }
    }

    private static object[] ReadAudioEndpoints()
    {
        var results = new List<object>();
        using var enumerator = new MMDeviceEnumerator();
        foreach (var flow in new[] { DataFlow.Render, DataFlow.Capture })
        foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        using (device)
        {
            var readings = new Dictionary<string, object?> { ["flow"] = flow.ToString() };
            void Read(string key, Func<object?> read) { try { readings[key] = read(); } catch (Exception error) { readings[key] = error.GetType().Name + ": " + error.Message; } }
            Read("endpointPeak", () => device.AudioMeterInformation.MasterPeakValue);
            Read("muted", () => device.AudioEndpointVolume.Mute);
            Read("volume", () => device.AudioEndpointVolume.MasterVolumeLevelScalar);
            var sessions = new List<object>();
            try
            {
                var list = device.AudioSessionManager.Sessions;
                for (var i = 0; i < list.Count; i++)
                {
                    using var session = list[i];
                    var values = new Dictionary<string, object?>();
                    void Get(string key, Func<object?> read) { try { values[key] = read(); } catch (Exception error) { values[key] = error.GetType().Name + ": " + error.Message; } }
                    Get("pid", () => session.GetProcessID);
                    Get("state", () => session.State.ToString());
                    Get("peak", () => session.AudioMeterInformation.MasterPeakValue);
                    Get("muted", () => session.SimpleAudioVolume.Mute);
                    Get("volume", () => session.SimpleAudioVolume.Volume);
                    Get("process", () => { using var process = System.Diagnostics.Process.GetProcessById((int)session.GetProcessID); return process.ProcessName; });
                    sessions.Add(values);
                }
            }
            catch (Exception error) { readings["error"] = error.ToString(); }
            readings["sessions"] = sessions; results.Add(readings);
        }
        return results.ToArray();
    }
}
