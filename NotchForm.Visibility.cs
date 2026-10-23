using Yuta.Services;

namespace Yuta;

public sealed partial class NotchForm
{
    private readonly VisibilityPolicy _visibility;
    private readonly BrowserSiteReader _siteReader = new();
    private ContextMenuStrip? _visibilityMenu;
    private AppContextInfo? _activeApp;
    private bool _visibilitySuppressed, _explicitOpen, _contextMenuOpen, _sitePollBusy;
    private long _lastSitePoll;
    private int _menuGeneration;
    private bool OverlayHidden => _userHidden || _visibilitySuppressed && !_explicitOpen;

    private void InitializeVisibilityMenu()
    {
        _visibilityMenu = new ContextMenuStrip
        {
            ShowImageMargin = false, Font = new Font("Segoe UI", 10), Padding = new Padding(5),
            ForeColor = Color.FromArgb(239, 240, 246), Renderer = new ToolStripProfessionalRenderer(new YutaMenuColors())
        };
        _visibilityMenu.Opening += (_, _) =>
        {
            _contextMenuOpen = true; _hover.Stop(); _hoverIntent.Reset();
            RefreshActiveApp();
            PopulateVisibilityMenu();
        };
        _visibilityMenu.Closed += (_, _) =>
        {
            _contextMenuOpen = false; _menuGeneration++;
            _hoverRequiresExit = _pill.ClientRectangle.Contains(_pill.PointToClient(Cursor.Position));
            ApplyVisibility(); EnsureOverlayOnTop();
        };
        _pill.ContextMenuStrip = _visibilityMenu;
        ContextMenuStrip = _visibilityMenu;
        _tray.ContextMenuStrip = _visibilityMenu;
    }

    private void PopulateVisibilityMenu()
    {
        if (_visibilityMenu is null) return;
        var menu = _visibilityMenu; var generation = ++_menuGeneration;
        foreach (ToolStripItem item in menu.Items.Cast<ToolStripItem>().ToArray()) { menu.Items.Remove(item); item.Dispose(); }
        void Add(string title, Action action) => menu.Items.Add(title, null, (_, _) => RunVisibilityAction(action));
        Add("Open Yuta", () => Expand(true));
        Add("Settings", () => { Expand(true); Emit("navigate", "Settings"); });
        menu.Items.Add(new ToolStripSeparator());
        var target = _activeApp;
        if (target is { Browser: true })
        {
            var siteItem = new ToolStripMenuItem("Checking current website…") { Enabled = false };
            menu.Items.Add(siteItem);
            _ = ResolveMenuSiteAsync(target, siteItem, generation);
        }
        if (target is not null)
            Add("Never show in " + MenuLabel(target.Name), () => { _explicitOpen = false; _visibility.ExcludeApp(target.Path, target.Name); ApplyVisibility(); });
        else menu.Items.Add(new ToolStripMenuItem("Current app unavailable") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        Add("Turn off for 1 hour", () => { _explicitOpen = false; _visibility.Snooze(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); ApplyVisibility(); });
        var preferences = _visibility.Read();
        if (preferences.SnoozedUntil > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            var until = DateTimeOffset.FromUnixTimeMilliseconds(preferences.SnoozedUntil).LocalDateTime.ToString("h:mm tt");
            Add("Resume now (paused until " + until + ")", () => { _visibility.Resume(); _userHidden = false; ApplyVisibility(); });
        }
        Add(_userHidden ? "Show Yuta" : "Hide until reopened", () => SetMenuHidden(!_userHidden));
        var exclusions = new ToolStripMenuItem("Hidden websites and apps");
        foreach (var site in preferences.Sites)
            exclusions.DropDownItems.Add("Show on " + MenuLabel(site), null, (_, _) => RunVisibilityAction(() => { _visibility.RemoveSite(site); ApplyVisibility(); }));
        foreach (var app in preferences.Apps)
            exclusions.DropDownItems.Add("Show in " + MenuLabel(app.Name), null, (_, _) => RunVisibilityAction(() => { _visibility.RemoveApp(app.Path); ApplyVisibility(); }));
        if (exclusions.DropDownItems.Count == 0) exclusions.DropDownItems.Add(new ToolStripMenuItem("No exclusions") { Enabled = false });
        menu.Items.Add(exclusions);
        menu.Items.Add(new ToolStripSeparator());
        Add("Exit Yuta", () => { _allowClose = true; Close(); });
    }

    private static string MenuLabel(string value) => value.Replace("&", "&&").Replace("\r", " ").Replace("\n", " ");

    private async Task ResolveMenuSiteAsync(AppContextInfo target, ToolStripMenuItem item, int generation)
    {
        try
        {
            var read = await _siteReader.ReadAsync(target.Window).WaitAsync(TimeSpan.FromSeconds(2));
            if (IsDisposed || generation != _menuGeneration || item.IsDisposed) return;
            if (read is { Known: true, Host: not null })
            {
                var site = read.Host;
                if (_activeApp?.Window == target.Window && _activeApp.Path == target.Path) _activeApp = target with { Site = site };
                item.Text = "Never show on " + MenuLabel(site); item.Enabled = true;
                item.Click += (_, _) => RunVisibilityAction(() => { _explicitOpen = false; _visibility.ExcludeSite(site); ApplyVisibility(); });
            }
            else item.Text = "Current website unavailable";
        }
        catch (TimeoutException) { if (!item.IsDisposed && generation == _menuGeneration) item.Text = "Current website unavailable"; }
        catch (Exception error) { Program.Log(error); if (!item.IsDisposed && generation == _menuGeneration) item.Text = "Current website unavailable"; }
    }

    private void RunVisibilityAction(Action action)
    {
        try { action(); }
        catch (Exception error)
        {
            Program.Log(error);
            _tray.ShowBalloonTip(4000, "Yuta", "Could not save this visibility change. Please try again.", ToolTipIcon.Warning);
        }
    }

    private void RefreshActiveApp()
    {
        var foreground = Native.GetForegroundWindow();
        var context = ForegroundContext.Capture(foreground);
        if (context is not null) _previousWindow = foreground;
        else
        {
            Native.GetWindowThreadProcessId(foreground, out var pid);
            var kind = new System.Text.StringBuilder(128); Native.GetClassName(foreground, kind, kind.Capacity);
            if (pid == Environment.ProcessId || kind.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
                context = ForegroundContext.Capture(_previousWindow);
        }
        if (context is null) { _activeApp = null; return; }
        // A temporarily inaccessible address bar retains the last confirmed host
        // for this window. Never reuse it across windows or executable identities.
        if (_activeApp?.Window == context.Window && _activeApp.Path == context.Path)
            context = context with { Site = _activeApp.Site };
        _activeApp = context;
    }

    private async void ForegroundTick()
    {
        RefreshActiveApp(); ApplyVisibility(); EnsureOverlayOnTop();
        _ = ObserveObstructionAsync();
        if (_sitePollBusy || _contextMenuOpen || _activeApp is not { Browser: true } target
            || _visibility.Read().Sites.Length == 0 || Environment.TickCount64 - _lastSitePoll < 600) return;
        _lastSitePoll = Environment.TickCount64; _sitePollBusy = true;
        try
        {
            var site = await _siteReader.ReadAsync(target.Window).WaitAsync(TimeSpan.FromSeconds(2));
            if (IsDisposed || Disposing || _allowClose || _activeApp?.Window != target.Window || _activeApp.Path != target.Path) return;
            if (site.Known) { _activeApp = target with { Site = site.Host }; ApplyVisibility(); }
        }
        catch (TimeoutException) { /* A slow accessibility provider never blocks the notch. */ }
        catch (Exception error) { Program.Log(error); }
        finally { _sitePollBusy = false; }
    }

    private void ApplyVisibility()
    {
        _visibilitySuppressed = _visibility.Suppressed(_activeApp?.Path, _activeApp?.Site, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        if (_contextMenuOpen) return;
        if (OverlayHidden)
        {
            if (_expanded) Expand(false);
            _hover.Stop(); _hoverIntent.Reset();
            if (!_animation.Enabled) { Hide(); _pill.Hide(); }
        }
        else if (!_expanded && !_animation.Enabled && !_pill.Visible)
        {
            PositionWindow();
            _hoverRequiresExit = _pill.ClientRectangle.Contains(_pill.PointToClient(Cursor.Position));
            _pill.Show(); _pill.RefreshSurface();
        }
    }

    private void EnsureOverlayOnTop()
    {
        if (_smoke || _contextMenuOpen || _dialogOpen || _webDialogOpen || OverlayHidden) return;
        Form window = _expanded || _animation.Enabled ? this : _pill;
        if (!window.Visible || !window.IsHandleCreated) return;
        // Restore z-order without activating, moving, resizing, or raising owners.
        Native.SetWindowPos(window.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0010 | 0x0001 | 0x0002 | 0x0200);
    }

    private sealed class YutaMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(22, 24, 31);
        public override Color MenuBorder => Color.FromArgb(55, 57, 69);
        public override Color MenuItemSelected => Color.FromArgb(48, 44, 62);
        public override Color MenuItemBorder => Color.FromArgb(64, 58, 82);
        public override Color MenuItemSelectedGradientBegin => MenuItemSelected;
        public override Color MenuItemSelectedGradientEnd => MenuItemSelected;
        public override Color MenuItemPressedGradientBegin => MenuItemSelected;
        public override Color MenuItemPressedGradientEnd => MenuItemSelected;
        public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
        public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
        public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;
        public override Color SeparatorDark => MenuBorder;
        public override Color SeparatorLight => ToolStripDropDownBackground;
    }
}
