using System.Text.Json;
using Yuta.Services;

namespace Yuta;

internal static class VisibilityTests
{
    internal static void Run(Action<string, Func<bool>> check, string root)
    {
        check("Website rules keep hosts only and reject searches or non-web addresses", () =>
            VisibilityPolicy.SiteHost("https://www.Example.com/private?q=secret#section") == "example.com"
            && VisibilityPolicy.SiteHost("music.example.com/album") == "music.example.com"
            && VisibilityPolicy.SiteHost("look up a song") is null
            && VisibilityPolicy.SiteHost("about:blank") is null
            && VisibilityPolicy.SiteHost("chrome://settings") is null
            && VisibilityPolicy.SiteHost("https://person:password@example.com") is null);
        check("IPv6 site rules round trip and malformed saved entries are ignored", () =>
        {
            var store = new Store(Path.Combine(root, "visibility-malformed"));
            store.Update("visibility", System.Text.Json.Nodes.JsonNode.Parse("{\"apps\":[null],\"sites\":[null],\"snoozedUntil\":0}"));
            var policy = new VisibilityPolicy(store);
            if (policy.Suppressed("C:\\Example.exe", "example.com", 0)) return false;
            var host = VisibilityPolicy.SiteHost("http://[::1]:8080/page");
            if (host is null) return false;
            policy.ExcludeSite(host); return policy.Suppressed(null, host, 0);
        });
        check("Website exclusions match domain boundaries without hiding unrelated sites", () =>
            VisibilityPolicy.MatchesSite("example.com", "example.com")
            && VisibilityPolicy.MatchesSite("music.example.com", "example.com")
            && !VisibilityPolicy.MatchesSite("notexample.com", "example.com")
            && !VisibilityPolicy.MatchesSite("example.com.evil.test", "example.com")
            && !VisibilityPolicy.MatchesSite("example.com", "music.example.com"));
        var profile = Path.Combine(root, "visibility"); var store = new Store(profile); var rules = new VisibilityPolicy(store);
        check("App and website exclusions survive reopening and can be removed individually", () =>
        {
            rules.ExcludeApp("C:\\Apps\\Example.exe", "Example"); rules.ExcludeSite("example.com");
            var saved = new VisibilityPolicy(new Store(profile));
            if (!saved.Suppressed("c:\\apps\\example.exe", null, 1000) || !saved.Suppressed(null, "video.example.com", 1000)
                || saved.Suppressed("C:\\Other\\Example.exe", "other.test", 1000)) return false;
            saved.RemoveApp("C:\\Apps\\Example.exe"); saved.RemoveSite("example.com");
            return !new VisibilityPolicy(new Store(profile)).Suppressed("C:\\Apps\\Example.exe", "example.com", 1000);
        });
        check("One-hour pause persists, expires on time, and can resume early", () =>
        {
            const long now = 1000000; rules.Snooze(now);
            var saved = new VisibilityPolicy(new Store(profile));
            if (!saved.Suppressed(null, null, now + 3599999) || saved.Suppressed(null, null, now + 3600000)) return false;
            saved.Resume(); return !new VisibilityPolicy(new Store(profile)).Suppressed(null, null, now + 1);
        });
        check("Website capture identifies the address field without using window titles", () =>
        {
            using var window = new ProbeWindow { Location = new(-12000, -12000), Size = new(420, 160), Text = "Misleading example.org title" };
            using var address = new TextBox { AccessibleName = "Address and search bar", Text = "https://actual.example.test/path?private=value", Dock = DockStyle.Top };
            window.Controls.Add(address); window.Show();
            var handle = window.Handle;
            var read = new BrowserSiteReader().ReadAsync(handle);
            var deadline = Environment.TickCount64 + 5000;
            while (!read.IsCompleted && Environment.TickCount64 < deadline) { Application.DoEvents(); Thread.Sleep(5); }
            if (!read.IsCompletedSuccessfully) return false;
            return read.Result.Known && read.Result.Host == "actual.example.test";
        });
        check("Restoring topmost does not activate the overlay", () =>
        {
            using var window = new ProbeWindow { Location = new(-12000, -12000), Size = new(100, 30) };
            window.Show();
            var before = Native.GetForegroundWindow();
            var raised = Native.SetWindowPos(window.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0010 | 0x0001 | 0x0002 | 0x0200);
            return raised && (Native.GetWindowLongPtr(window.Handle, -20).ToInt64() & 8) != 0 && Native.GetForegroundWindow() == before;
        });
    }
    private sealed class ProbeWindow : Form
    {
        protected override bool ShowWithoutActivation => true;
        public ProbeWindow() { ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; }
    }
}
