using System.Text;
using System.Windows.Automation;

namespace Yuta.Services;

internal sealed record AppContextInfo(IntPtr Window, string Path, string Name, bool Browser, string? Site = null);
internal readonly record struct SiteObservation(bool Known, string? Host);
internal enum ForegroundKind { Application, Transient, Own, Unknown, Desktop }
internal readonly record struct ForegroundObservation(ForegroundKind Kind, AppContextInfo? App = null);

// Windows flyouts and owned dialogs are temporary focus destinations, not a
// change of workspace. Preserve the confirmed app/site until a real switch.
internal sealed class ForegroundSession
{
    internal AppContextInfo? Active { get; private set; }
    internal bool Transient { get; private set; }
    internal AppContextInfo? Observe(ForegroundObservation observation, bool previousUsable = true)
    {
        if (!previousUsable || observation.Kind == ForegroundKind.Desktop) Active = null;
        Transient = observation.Kind is ForegroundKind.Transient or ForegroundKind.Unknown;
        if (observation.App is { } app)
        {
            if (Active?.Window == app.Window && string.Equals(Active.Path, app.Path, StringComparison.OrdinalIgnoreCase))
                app = app with { Site = Active.Site };
            Active = app;
        }
        return Active;
    }
    internal void SetSite(IntPtr window, string path, string? site)
    {
        if (Active?.Window == window && string.Equals(Active.Path, path, StringComparison.OrdinalIgnoreCase)) Active = Active with { Site = site };
    }
}

internal static class ForegroundContext
{
    internal static bool IsTransientHost(string executable, string windowClass) =>
        windowClass is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "#32768" or "Xaml_WindowedPopupClass" or "Chrome_WidgetWin_2"
        || executable.Equals("explorer", StringComparison.OrdinalIgnoreCase) && windowClass is "Windows.UI.Core.CoreWindow"
            or "XamlExplorerHostIslandWindow" or "ControlCenterWindow" or "TopLevelWindowForOverflowXamlIsland" or "NotifyIconOverflowWindow"
        || executable.ToLowerInvariant() is "shellexperiencehost" or "startmenuexperiencehost" or "searchhost"
            or "searchapp" or "textinputhost" or "credentialuibroker" or "lockapp" or "widgets";

    internal static bool HasDocumentChrome(string path) =>
        System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant() is "gatedspace" or "gatedspace personal" or "superset";

    internal static bool Usable(AppContextInfo? app) => app is not null && Native.IsWindow(app.Window)
        && Native.IsWindowVisible(app.Window) && !Native.IsIconic(app.Window);

    internal static ForegroundObservation Observe(IntPtr window)
    {
        if (window == IntPtr.Zero || !Native.IsWindow(window)) return new(ForegroundKind.Unknown);
        var kind = new StringBuilder(128); Native.GetClassName(window, kind, kind.Capacity);
        if (kind.ToString() is "Progman" or "WorkerW") return new(ForegroundKind.Desktop);
        Native.GetWindowThreadProcessId(window, out var pid);
        if (pid == Environment.ProcessId) return new(ForegroundKind.Own);
        var direct = Capture(window);
        var executable = direct is null ? "" : System.IO.Path.GetFileNameWithoutExtension(direct.Path);
        if (IsTransientHost(executable, kind.ToString())) return new(ForegroundKind.Transient);
        var owner = Native.GetAncestor(window, 3); // GA_ROOTOWNER: Chrome dialogs retain the browser workspace.
        if (owner != IntPtr.Zero && owner != window && Capture(owner) is { } ownerApp && Usable(ownerApp))
            return new(ForegroundKind.Transient, ownerApp);
        if (kind.ToString() is "#32770" || (Native.GetWindowLongPtr(window, -20).ToInt64() & 0x80) != 0)
            return new(ForegroundKind.Transient);
        return direct is null ? new(ForegroundKind.Unknown) : new(ForegroundKind.Application, direct);
    }
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
