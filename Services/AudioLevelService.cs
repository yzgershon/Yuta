using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Yuta.Services;

internal sealed record AudioLevels(float[] Bars, bool VoiceActive, string VoiceApp, bool Available, float Peak = 0)
{
    public static readonly AudioLevels Silent = new(new float[AudioEnvelope.BarCount], false, "", false);
}

// Reads Windows peak meters only: no microphone stream is opened and no audio
// samples are captured, recorded, transmitted, or retained.
internal sealed class AudioLevelService : IDisposable
{
    private readonly ManualResetEventSlim _stop = new(false);
    private Thread? _thread;
    private string _source = "", _sourceName = "";
    private volatile bool _playing;
    private AudioLevels _latest = AudioLevels.Silent;
    public AudioLevels Latest => Volatile.Read(ref _latest);
    public void SetMedia(MediaSnapshot media)
    {
        Volatile.Write(ref _source, media.Source ?? "");
        Volatile.Write(ref _sourceName, media.SourceName ?? "");
        _playing = media.Available && media.Playing;
    }
    public void Start()
    {
        if (_thread is not null) return;
        _thread = new Thread(() => { try { Run(); } catch { Volatile.Write(ref _latest, AudioLevels.Silent); } }) { IsBackground = true, Name = "Yuta audio meters" };
        _thread.SetApartmentState(ApartmentState.MTA); _thread.Start();
    }
    private sealed record Meter(MMDevice Device, AudioSessionControl Session, string Process, string Identifier, string Instance, AudioSessionIdentity Identity, bool Capture);
    private void Run()
    {
        var devices = new List<MMDevice>(); var sessions = new List<Meter>();
        using var enumerator = new MMDeviceEnumerator();
        var envelope = new AudioEnvelope(); long nextScan = 0, previous = Environment.TickCount64;
        string context = "", voiceInstance = "";
        void Clear()
        {
            foreach (var m in sessions) try { m.Session.Dispose(); } catch { }
            sessions.Clear(); foreach (var d in devices) try { d.Dispose(); } catch { }
            devices.Clear();
        }
        try
        {
            while (!_stop.IsSet)
            {
                var now = Environment.TickCount64;
                try
                {
                    if (now >= nextScan)
                    {
                        Clear(); nextScan = now + 1200;
                        var processes = AudioSessionIdentityResolver.ReadProcesses();
                        foreach (var flow in new[] { DataFlow.Render, DataFlow.Capture })
                        foreach (var d in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
                        {
                            devices.Add(d);
                            try
                            {
                                var list = d.AudioSessionManager.Sessions;
                                for (var i = 0; i < list.Count; i++)
                                {
                                    var session = list[i];
                                    try
                                    {
                                        var pid = session.GetProcessID;
                                        if (pid == 0 || pid == Environment.ProcessId) { session.Dispose(); continue; }
                                        using var process = Process.GetProcessById((int)pid);
                                        var group = Guid.Empty;
                                        try { group = session.GetGroupingParam(); } catch { }
                                        var identity = AudioSessionIdentityResolver.Resolve(pid, process.ProcessName + ".exe", group, processes);
                                        sessions.Add(new(d, session, process.ProcessName, session.GetSessionIdentifier ?? "", session.GetSessionInstanceIdentifier ?? "", identity, flow == DataFlow.Capture));
                                    }
                                    catch { session.Dispose(); }
                                }
                            }
                            catch { /* A driver can expose an endpoint but not session meters. */ }
                        }
                    }
                    var voice = sessions.Where(s => s.Capture && IsActive(s))
                        .OrderByDescending(s => s.Identity.VoicePriority)
                        .ThenBy(s => s.Instance == voiceInstance ? 0 : 1)
                        .ThenBy(s => s.Identity.ProcessId).FirstOrDefault();
                    voiceInstance = voice?.Instance ?? "";
                    var source = Volatile.Read(ref _source); var name = Volatile.Read(ref _sourceName);
                    var activeContext = voice is not null ? "voice:" + voice.Identity.OwnerId + ":" + voice.Instance : _playing ? "music:" + source : "";
                    if (activeContext != context) { envelope = new AudioEnvelope(); context = activeContext; }
                    float peak = 0; var available = false;
                    foreach (var session in sessions)
                    {
                        var relevant = voice is not null
                            ? AudioSessionIdentityResolver.ContributesToVoice(voice.Identity, session.Identity, session.Capture)
                            : _playing && !session.Capture && MatchesSource(session.Process, session.Identifier, source, name);
                        if (!relevant || !IsActive(session)) continue;
                        if (TryPeak(session, out var value)) { peak = Math.Max(peak, value); available = true; }
                    }
                    Volatile.Write(ref _latest, new(envelope.Sample(peak, now - previous), voice is not null, voice?.Identity.Name ?? "", available, peak));
                }
                catch
                {
                    nextScan = now + 1200;
                    Volatile.Write(ref _latest, new(envelope.Sample(0, now - previous), false, "", false));
                }
                previous = now;
                _stop.Wait(_playing || Latest.VoiceActive ? 25 : 150);
            }
        }
        finally { Clear(); }
    }
    private static bool IsActive(Meter meter)
    {
        try { return meter.Session.State == AudioSessionState.AudioSessionStateActive; } catch { return false; }
    }
    private static bool TryPeak(Meter meter, out float peak)
    {
        peak = 0;
        try
        {
            if (meter.Device.AudioEndpointVolume.Mute || meter.Device.AudioEndpointVolume.MasterVolumeLevelScalar <= 0 || meter.Session.SimpleAudioVolume.Mute || meter.Session.SimpleAudioVolume.Volume <= 0) return true;
            try { peak = meter.Session.AudioMeterInformation.MasterPeakValue; }
            catch when (meter.Capture) { peak = meter.Device.AudioMeterInformation.MasterPeakValue; }
            return true;
        }
        catch { return false; }
    }
    internal static bool MatchesSource(string process, string identifier, string source, string name)
    {
        if (string.IsNullOrWhiteSpace(source) && string.IsNullOrWhiteSpace(name)) return false;
        static string Normalize(string value) => value.Replace(".exe", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "").ToLowerInvariant();
        var p = Normalize(process); var s = Normalize(source); var n = Normalize(name);
        return p.Length > 2 && (p == s || p == n || s.StartsWith(p + ".", StringComparison.Ordinal) ||
            source.Length > 6 && identifier.Contains(source, StringComparison.OrdinalIgnoreCase));
    }
    public void Dispose() { _stop.Set(); if (_thread is null || _thread.Join(2000)) _stop.Dispose(); }
}
