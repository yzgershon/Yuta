using Yuta.Services;

namespace Yuta;
internal static class AudioLevelTests
{

    // An anonymous local meter trace, not audio. This exports browser example
    // frames through the very same response curve used by the Windows notch.
    private static float[][] WriteDemoFrames()
    {
        float[] peaks = [
            0.247518f, 0.116349f, 0.091410f, 0.623772f, 0.420680f, 0.288259f, 0.288259f, 0.229325f,
            0.240535f, 0.229300f, 0.229300f, 0.072652f, 0.631821f, 0.512072f, 0.222572f, 0.222572f,
            0.209638f, 0.239122f, 0.301556f, 0.301556f, 0.092439f, 0.403834f, 0.403834f, 0.330585f,
            0.330379f, 0.298529f, 0.224175f, 0.276394f, 0.332446f, 0.332446f, 0.295442f, 0.273524f,
            0.302105f, 0.302105f, 0.285361f, 0.301182f, 0.337357f, 0.337357f, 0.302809f, 0.589082f,
            0.589082f, 0.430326f, 0.521542f, 0.473425f, 0.473425f, 0.471384f, 0.416564f, 0.461113f,
            0.463934f, 0.463934f, 0.462886f, 0.524381f, 0.524381f, 0.439756f, 0.499570f, 0.499570f,
            0.457532f, 0.623299f, 0.652206f, 0.528023f, 0.472795f, 0.472795f, 0.472795f, 0.561454f,
            0.561454f, 0.505302f, 0.623507f, 0.627463f, 0.554094f, 0.510867f, 0.498861f, 0.480603f,
            0.429011f, 0.428242f, 0.586087f, 0.594315f, 0.653054f, 0.653054f, 0.570152f, 0.525710f,
            0.525710f, 0.531211f, 0.531211f, 0.532782f, 0.559714f, 0.632764f, 0.632764f, 0.564010f,
            0.564010f, 0.453767f, 0.489413f, 0.501745f, 0.599921f, 0.599921f, 0.612871f, 0.612871f,
            0.529580f, 0.398648f, 0.402933f, 0.582302f, 0.582302f, 0.478285f, 0.503321f, 0.629991f,
            0.629991f, 0.521324f, 0.548156f, 0.570293f, 0.570293f, 0.535764f, 0.565744f, 0.565744f,
            0.594948f, 0.624988f, 0.613050f, 0.498989f, 0.503807f, 0.602448f, 0.565507f, 0.565507f
        ];
        var envelope = new AudioEnvelope();
        var frames = new List<float[]>();
        foreach (var peak in peaks)
            for (var i = 0; i < 2; i++) frames.Add(envelope.Sample(peak).Select(v => MathF.Round(v, 4)).ToArray());
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "waveform-demo.json"), System.Text.Json.JsonSerializer.Serialize(new { frameMs = 25, frames }));
        return frames.ToArray();
    }

    internal static void Run(Action<string, Func<bool>> check)
    {
        check("Clipboard thumbnails preserve aspect ratio without altering the full paste image", () =>
        {
            using var full = new Bitmap(1000, 500);
            using (var g = Graphics.FromImage(full)) g.Clear(Color.CornflowerBlue);
            using var original = new MemoryStream(); full.Save(original, System.Drawing.Imaging.ImageFormat.Png);
            var data = "data:image/png;base64," + Convert.ToBase64String(original.ToArray());
            var small = ClipboardHistory.Thumbnail(data)!;
            using var bytes = new MemoryStream(Convert.FromBase64String(small[(small.IndexOf(',') + 1)..]));
            using var image = Image.FromStream(bytes);
            return image.Width == 160 && image.Height == 80 && data != small && full.Width == 1000;
        });
        check("Audio levels use dBFS and reject invalid peaks", () => AudioEnvelope.Decibels(1) == 0 && Math.Abs(AudioEnvelope.Decibels(.1f) + 20) < .01 && AudioEnvelope.Decibels(0) == -60 && AudioEnvelope.Decibels(float.NaN) == -60);
        check("Measured transients rise promptly then decay fully to silence", () =>
        {
            var meter = new AudioEnvelope();
            if (meter.Sample(0).Any(v => v != 0)) return false;
            float[] baseline = [];
            for (var i = 0; i < 80; i++) baseline = meter.Sample(.1f);
            var attack = meter.Sample(.8f);
            if (attack[10] - baseline[10] < .25f || attack.Max() >= .75f) return false;
            float[] release = [];
            for (var i = 0; i < 60; i++) release = meter.Sample(0);
            return release.All(v => v == 0);
        });
        check("Music matching does not treat unrelated desktop audio as the track", () =>
            AudioLevelService.MatchesSource("Spotify", "", "Spotify.exe", "Spotify") &&
            AudioLevelService.MatchesSource("Spotify", "", "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", "Spotify") &&
            !AudioLevelService.MatchesSource("Discord", "", "Spotify.exe", "Spotify") &&
            !AudioLevelService.MatchesSource("Chrome", "", "", ""));
        check("Loud music keeps transient contrast and room above every bar", () =>
        {
            var envelope = new AudioEnvelope();
            var trace = new[] { .45f, .8f, .96f, .55f, .7f, .4f, .92f, .6f };
            var frames = Enumerable.Range(0, 96).Select(i => envelope.Sample(trace[i % trace.Length])).Skip(21).ToArray();
            return frames.All(b => b.Length == 21 && b.Max() < .75f) &&
                frames.Max(b => b[10]) - frames.Min(b => b[10]) > .2f;
        });
        check("A steady signal stays steady and unavailable audio settles fully", () =>
        {
            var envelope = new AudioEnvelope(); float[] bars = [];
            for (var i = 0; i < 80; i++) bars = envelope.Sample(.7f);
            var next = envelope.Sample(.7f);
            if (bars.Max() >= .2f || bars.Zip(next, (a, b) => Math.Abs(a - b)).Max() > .001f) return false;
            for (var i = 0; i < 60; i++) bars = envelope.Sample(float.NaN);
            return bars.All(v => v == 0);
        });
        check("Full-scale sustained input never fills the waveform", () =>
        {
            var envelope = new AudioEnvelope();
            for (var i = 0; i < 80; i++) envelope.Sample(.1f);
            var frames = Enumerable.Range(0, 160).Select(_ => envelope.Sample(1)).ToArray();
            return frames.All(b => b.Max() < .75f) && frames[^1].Max() < .2f;
        });
        check("Voice syllables remain distinct and gaps return to silence", () =>
        {
            var envelope = new AudioEnvelope();
            float[] syllable = [0, .02f, .05f, .2f, .65f, .3f, .1f, .05f, 0, 0, 0, 0, 0, 0, 0];
            var frames = Enumerable.Range(0, 90).Select(i => envelope.Sample(syllable[i % syllable.Length])).ToArray();
            float[] silent = [];
            for (var i = 0; i < 60; i++) silent = envelope.Sample(0);
            return frames.Max(b => b[10]) > .3f && frames.All(b => b.Max() < .75f) && silent.All(v => v == 0);
        });
        check("Small input noise does not turn into amplified animation", () =>
        {
            var envelope = new AudioEnvelope();
            for (var i = 0; i < 100; i++) if (envelope.Sample(i % 2 == 0 ? .001f : .0029f).Any(v => v != 0)) return false;
            return true;
        });
        check("Measured music fixture moves distinctly without camping at full height", () =>
        {
            var frames = WriteDemoFrames().Skip(AudioEnvelope.BarCount).ToArray();
            var values = frames.SelectMany(f => f).Order().ToArray();
            var center = frames.Select(f => f[10]).ToArray();
            return values[values.Length / 2] < .25f && values[(int)(values.Length * .95)] < .55f &&
                values[^1] < .75f && center.Max() - center.Min() > .3f &&
                frames.Average(f => (f[0] + f[^1]) / 2) < frames.Average(f => f[10]) / 2;
        });
        var processes = new Dictionary<uint, AudioProcess>
        {
            [1] = new(1, 0, "explorer.exe"),
            [10] = new(10, 1, "WhatsApp.Root.exe"),
            [11] = new(11, 10, "msedgewebview2.exe"),
            [12] = new(12, 11, "msedgewebview2.exe"),
            [13] = new(13, 11, "msedgewebview2.exe"),
            [20] = new(20, 1, "Zoom.exe"),
            [21] = new(21, 20, "CptHost.exe"),
            [22] = new(22, 1, "Zoom.exe"),
            [30] = new(30, 1, "chrome.exe"),
            [31] = new(31, 30, "chrome.exe"),
            [32] = new(32, 30, "chrome.exe"),
            [40] = new(40, 1, "Yuta.exe"),
            [41] = new(41, 40, "msedgewebview2.exe"),
            [50] = new(50, 10, "chrome.exe"),
            [60] = new(60, 60, "Zoom.exe"),
            [70] = new(70, 1, "Discord.exe"),
            [71] = new(71, 70, "Discord.exe"),
            [72] = new(72, 70, "Discord.exe"),
            [73] = new(73, 1, "Discord.exe")
        };
        AudioSessionIdentity Resolve(uint pid) => AudioSessionIdentityResolver.Resolve(pid, processes[pid].Name, Guid.Empty, processes);
        check("WhatsApp WebView audio resolves to the installed host and its helpers", () =>
        {
            var voice = Resolve(12);
            return voice.Name == "WhatsApp" && voice.OwnerId == 10 && voice.Kind == AudioAppKind.WhatsApp &&
                AudioSessionIdentityResolver.ContributesToVoice(voice, Resolve(13), false) &&
                !AudioSessionIdentityResolver.ContributesToVoice(voice, Resolve(41), false);
        });
        check("Zoom helpers contribute without including a separate Zoom instance", () =>
        {
            var voice = Resolve(21);
            return voice.Name == "Zoom" && voice.OwnerId == 20 &&
                AudioSessionIdentityResolver.ContributesToVoice(voice, Resolve(20), false) &&
                !AudioSessionIdentityResolver.ContributesToVoice(voice, Resolve(22), false);
        });
        check("Discord input and output helpers resolve to the same app instance", () =>
        {
            var voice = Resolve(71);
            return voice.Name == "Discord" && voice.Kind == AudioAppKind.Discord && voice.OwnerId == 70 && voice.VoicePriority == 2 &&
                AudioSessionIdentityResolver.ContributesToVoice(voice, Resolve(72), false) &&
                AudioSessionIdentityResolver.ContributesToVoice(voice, Resolve(70), true);
        });
        check("Discord meter excludes separate instances and unrelated calling apps", () =>
        {
            var voice = Resolve(71);
            return !AudioSessionIdentityResolver.ContributesToVoice(voice, Resolve(73), false) &&
                !AudioSessionIdentityResolver.ContributesToVoice(voice, Resolve(21), false) &&
                !AudioSessionIdentityResolver.ContributesToVoice(voice, Resolve(41), false);
        });
        check("Explicit Discord audio grouping remains limited to Discord", () =>
        {
            var group = Guid.NewGuid(); var voice = Resolve(71) with { Group = group };
            return AudioSessionIdentityResolver.ContributesToVoice(voice, Resolve(73) with { Group = group }, false) &&
                !AudioSessionIdentityResolver.ContributesToVoice(voice, Resolve(21) with { Group = group }, false);
        });
        check("Nonempty audio grouping can join split native sessions but not unrelated apps", () =>
        {
            var group = Guid.NewGuid(); var voice = Resolve(21) with { Group = group };
            return AudioSessionIdentityResolver.ContributesToVoice(voice, Resolve(22) with { Group = group }, false) &&
                !AudioSessionIdentityResolver.ContributesToVoice(voice, Resolve(12) with { Group = group }, false);
        });
        check("Meet browser microphone animation excludes all browser render audio", () =>
        {
            var voice = Resolve(31);
            return voice.Name == "Chrome" && voice.OwnerId == 30 && voice.Kind == AudioAppKind.Browser &&
                AudioSessionIdentityResolver.ContributesToVoice(voice, Resolve(31), true) &&
                !AudioSessionIdentityResolver.ContributesToVoice(voice, Resolve(31), false) &&
                !AudioSessionIdentityResolver.ContributesToVoice(voice, Resolve(32), false);
        });
        check("A browser launched by WhatsApp retains its own identity", () => Resolve(50).Kind == AudioAppKind.Browser && Resolve(50).OwnerId == 50);
        check("Missing process snapshots and cycles degrade safely", () =>
        {
            var browser = AudioSessionIdentityResolver.Resolve(99, "msedge.exe", Guid.Empty, new Dictionary<uint, AudioProcess>());
            return browser.Kind == AudioAppKind.Browser && browser.Name == "Edge" &&
                !AudioSessionIdentityResolver.ContributesToVoice(browser, browser, false) && Resolve(60).OwnerId == 60;
        });
        check("Known calling apps outrank background microphone utilities", () => Resolve(10).VoicePriority > Resolve(30).VoicePriority && Resolve(30).VoicePriority > Resolve(41).VoicePriority);
        check("Native process snapshot includes this test process", () => AudioSessionIdentityResolver.ReadProcesses().ContainsKey((uint)Environment.ProcessId));
        check("Notch bars only change in response to measured levels", () =>
        {
            using var idle = new IdleNotchWindow(); idle.SetMedia(ActivityTests.Music());
            var frame = idle.ActivityFrame; idle.SetAudioLevels(AudioLevels.Silent);
            if (frame != idle.ActivityFrame) return false;
            idle.SetAudioLevels(new([.8f,.6f,.4f,.2f,.1f], false, "", true));
            if (SystemInformation.IsMenuAnimationEnabled && frame == idle.ActivityFrame) return false;
            frame = idle.ActivityFrame; idle.SetAudioLevels(new([.8f,.6f,.4f,.2f,.1f], false, "", true));
            return frame == idle.ActivityFrame;
        });
        check("Voice activity widens the notch without requiring music", () =>
        {
            using var idle = new IdleNotchWindow();
            var changed = idle.SetAudioLevels(new([.4f,.2f,.1f,0,0], true, "Discord", true));
            if (!changed || !idle.HasActivity || idle.WidthFor(true) != NotchGeometry.ActivityWidth) return false;
            idle.SetAudioLevels(AudioLevels.Silent); return !idle.HasActivity;
        });
    }
}
