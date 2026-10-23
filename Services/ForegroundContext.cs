using System.Text;
using System.Windows.Automation;

namespace Yuta.Services;

internal sealed record AppContextInfo(IntPtr Window, string Path, string Name, bool Browser, string? Site = null);
internal readonly record struct SiteObservation(bool Known, string? Host);

internal static class ForegroundContext
{
    private static readonly Dictionary<string, string> Browsers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chrome"] = "Google Chrome", ["msedge"] = "Microsoft Edge", ["firefox"] = "Firefox",
        ["brave"] = "Brave", ["opera"] = "Opera", ["vivaldi"] = "Vivaldi", ["arc"] = "Arc"
    };

    internal static AppContextInfo? Capture(IntPtr window)
    {
        if (window == IntPtr.Zero || !Native.IsWindow(window)) return null;
        Native.GetWindowThreadProcessId(window, out var pid);
        if (pid == Environment.ProcessId || pid == 0) return null;
        var className = new StringBuilder(128); Native.GetClassName(window, className, className.Capacity);
        if (className.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW") return null;
        var process = Native.OpenProcess(0x1000, false, pid); // query limited information
        if (process == IntPtr.Zero) return null;
        try
        {
            var path = new StringBuilder(2048); var size = path.Capacity;
            if (!Native.QueryFullProcessImageName(process, 0, path, ref size)) return null;
            var executable = System.IO.Path.GetFileNameWithoutExtension(path.ToString());
            // These shared hosts cannot safely identify a single app exclusion.
            if (executable.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase)) return null;
            var browser = Browsers.TryGetValue(executable, out var name);
            return new(window, path.ToString(), name ?? executable, browser);
        }
        finally { Native.CloseHandle(process); }
    }

    internal static SiteObservation ReadSite(IntPtr window)
    {
        // Only browser chrome is traversed. Never descend into page documents or
        // read page inputs. All provider calls run on a worker, never the UI thread.
        try
        {
            var root = AutomationElement.FromHandle(window);
            if (root is null) return default;
            var walker = TreeWalker.ControlViewWalker;
            var remaining = 140;
            SiteObservation? result = null;
            void Visit(AutomationElement parent, int depth)
            {
                if (depth > 8 || remaining <= 0 || result is not null) return;
                for (var element = walker.GetFirstChild(parent); element is not null && remaining-- > 0; element = walker.GetNextSibling(element))
                {
                    var current = element.Current;
                    if (current.ControlType == ControlType.Document) continue;
                    if (current.ControlType == ControlType.Edit && IsAddressField(current.AutomationId, current.Name))
                    {
                        // Text being edited in the address bar is not yet the page.
                        if (current.HasKeyboardFocus) { result = default(SiteObservation); return; }
                        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern))
                        {
                            var address = ((ValuePattern)pattern).Current.Value;
                            result = new SiteObservation(true, VisibilityPolicy.SiteHost(address)); return;
                        }
                    }
                    Visit(element, depth + 1);
                    if (result is not null) return;
                }
            }
            Visit(root, 0);
            return result ?? default;
        }
        catch (Exception error) when (error is ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        { return default; }
    }

    internal static bool IsAddressField(string id, string name) => id is "addressEditBox" or "urlbar-input" or "urlbar" or "omnibox"
        || name is "Address and search bar" or "Search or enter address" or "Address field" or "Search with Google or enter address";
}

internal sealed class BrowserSiteReader
{
    private readonly object _gate = new();
    private Task<SiteObservation>? _pending;
    private IntPtr _window;
    internal Task<SiteObservation> ReadAsync(IntPtr window)
    {
        lock (_gate)
        {
            if (_pending is { IsCompleted: false }) return window == _window ? _pending : Task.FromResult(default(SiteObservation));
            _window = window;
            return _pending = Task.Run(() => ForegroundContext.ReadSite(window));
        }
    }
}
