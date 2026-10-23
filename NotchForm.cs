using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Yuta.Services;

namespace Yuta;

public sealed partial class NotchForm : Form
{
    private const string Origin = "https://yuta.local";
    // Preserve the original control and type sizes. Short displays scroll the
    // content area instead of shrinking the entire interface.
    private const int PanelWidth = 900;
    private readonly bool _smoke;
    private readonly bool _performance = Environment.GetCommandLineArgs().Contains("--performance-test");
    private readonly Store _store;
    private readonly SystemServices _system = new();
    private readonly Launcher _launcher;
    private readonly Wallpapers _wallpapers;
    private readonly ClipboardHistory _clipboard;
    private readonly WebView2 _web = new() { DefaultBackgroundColor = Color.FromArgb(13, 15, 22) };
    private readonly IdleNotchWindow _pill = new();
    private readonly System.Windows.Forms.Timer _hover = new() { Interval = 30 };
    private readonly HoverIntent _hoverIntent = new();
    private bool _hoverRequiresExit;
    private readonly System.Windows.Forms.Timer _pointerWatch = new() { Interval = 30 };
    private readonly HoverRetraction _retraction = new();
    private bool _webDialogOpen;
    private readonly System.Windows.Forms.Timer _idleSuspend = new() { Interval = 15000 };
    private bool _suspendPending;
    private readonly NotifyIcon _tray;
    // WinForms timers are quantized by Windows. A 16 ms request can fall to ~32 ms;
    // use the next available message tick and interpolate from elapsed wall time.
    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 1 };
    private readonly System.Windows.Forms.Timer _foreground = new() { Interval = 300 };
    private readonly System.Windows.Forms.Timer _clipboardRetry = new() { Interval = 120 };
    private readonly System.Windows.Forms.Timer _timerAlarm = new() { Interval = 1000 };
    private bool _expanded, _ready, _allowClose, _statsBusy, _speedBusy, _dialogOpen, _hotkey, _userHidden;
    private HotkeyBinding? _hotkeyBinding;
    private bool _hotkeyRecording;
    private uint _recordedKey;
    private IntPtr _previousWindow;
    private Size _from, _to;
    private Size _visualSize;
    private int _panelHeight = 452;
    private bool _reducedMotion;
    private long _animationStarted;
    private int _animationDuration = 280;
    private int _clipRetries;
    protected override bool ShowWithoutActivation => true;
    public NotchForm(bool smoke)
    {
        _smoke = smoke;
        _store = new Store(smoke ? Path.Combine(Path.GetTempPath(), "Yuta-smoke-" + Environment.ProcessId) : null);
        _visibility = new(_store);
        _launcher = new(_store); _wallpapers = new(_store); _clipboard = new(_store.Root) { Enabled = !smoke && Setting("clipboardEnabled", false) };
        Text = "Yuta"; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
        TopMost = !smoke; BackColor = Color.FromArgb(13, 15, 22); AutoScaleMode = AutoScaleMode.Dpi;
        _pill.TopMost = !smoke;
        Controls.Add(_web); Size = IdleSize(); _visualSize = Size;
        _web.Size = PanelSize(PanelWidth, _panelHeight); PositionWindow();
        _pill.MouseUp += (_, e) => { if (e.Button == MouseButtons.Left && !_contextMenuOpen) Expand(true); };
        _pill.MouseEnter += (_, _) =>
        {
            if (_expanded || _hoverRequiresExit || _contextMenuOpen || !Setting("openOnHover", true)) return;
            _hoverIntent.Reset();
            var point = _pill.PointToClient(Cursor.Position);
            _hoverIntent.Observe(new PointF(point.X / _pill.RenderScale, point.Y / _pill.RenderScale), Control.MouseButtons != MouseButtons.None, Environment.TickCount64);
            _hover.Start();
        };
        _pill.MouseLeave += (_, _) => { _hover.Stop(); _hoverIntent.Reset(); _hoverRequiresExit = false; };
        _hover.Tick += (_, _) => ObserveHoverIntent();
        _pointerWatch.Tick += (_, _) =>
        {
            if (!_expanded) { _pointerWatch.Stop(); return; }
            if (_retraction.Observe(CursorInsideMenu(), _dialogOpen || _webDialogOpen || _contextMenuOpen || _hotkeyRecording || _animation.Enabled || Control.MouseButtons != MouseButtons.None, Environment.TickCount64)) Expand(false);
        };
        _idleSuspend.Tick += async (_, _) => { _idleSuspend.Stop(); await SuspendWebAsync(); };
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        _tray = new NotifyIcon { Text = "Yuta · " + Hotkey.Parse(Setting("hotkey", Hotkey.Default)).Label, Icon = Icon ?? SystemIcons.Application, Visible = !smoke };
        InitializeVisibilityMenu();
        _tray.DoubleClick += (_, _) => Expand(true);
        _animation.Tick += (_, _) => Animate();
        _foreground.Tick += (_, _) => ForegroundTick();
        _clipboardRetry.Tick += (_, _) => { if (_clipboard.Capture()) { _clipboardRetry.Stop(); Emit("clipboard", _clipboard.Items); } else if (++_clipRetries >= 4) _clipboardRetry.Stop(); };
        _timerAlarm.Tick += (_, _) => { CheckTimer(); UpdatePill(); };
        _system.MediaChanged += MediaChanged;
        _devices.Notice += ActivityNotice;
        UpdatePill();
        Load += async (_, _) => await InitializeAsync();
        Deactivate += (_, _) => { if (_expanded && !_dialogOpen && !_webDialogOpen && !_contextMenuOpen && !_hotkeyRecording && !_smoke) Expand(false); };
        DpiChanged += (_, _) => RepositionPanel();
        _pill.DpiChanged += (_, _) => { if (IsHandleCreated && !IsDisposed && !Disposing) BeginInvoke(RepositionPanel); };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
    }
    private T Setting<T>(string key, T fallback) { try { var value = _store.Data["settings"]?[key]; return value is null ? fallback : value.GetValue<T>(); } catch { return fallback; } }
    private async Task InitializeAsync()
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(_store.Root, "WebView2"));
            await _web.EnsureCoreWebView2Async(env);
            _web.ZoomFactor = 1;
            var core = _web.CoreWebView2;
            core.SetVirtualHostNameToFolderMapping("yuta.local", Path.Combine(AppContext.BaseDirectory, "ui"), CoreWebView2HostResourceAccessKind.DenyCors);
            core.Settings.AreDevToolsEnabled = _smoke; core.Settings.AreDefaultContextMenusEnabled = false; core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false; core.Settings.IsZoomControlEnabled = false;
            _web.KeyDown += RecordShortcutAccelerator;
            _web.KeyUp += (_, e) => { if (_hotkeyRecording) { e.Handled = true; e.SuppressKeyPress = true; } _recordedKey = 0; };
            core.NavigationStarting += (_, e) => { if (!e.Uri.StartsWith(Origin + "/", StringComparison.Ordinal)) e.Cancel = true; };
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.WebMessageReceived += Receive;
            core.NavigationCompleted += async (_, e) => { if (!e.IsSuccess) return; _ready = true; if (_performance) await PerformanceAsync(); else if (_smoke) await SmokeAsync(); else { await Task.Delay(200); Emit("expanded", _expanded); if (!_expanded) await SuspendWebAsync(); } };
            core.Navigate(Origin + "/index.html?native=1");
            ResizePanel(IdleWidth, IdleHeight, false);
            if (!_smoke)
            {
                _hotkeyBinding = new HotkeyBinding(Handle);
                _hotkey = _hotkeyBinding.TrySet(Setting("hotkey", Hotkey.Default));
                UpdateShortcutLabel();
                Native.AddClipboardFormatListener(Handle); _foreground.Start(); _timerAlarm.Start();
                _devices.CheckPower(); _ = _devices.InitializeAsync();
            }
            _ = _system.InitializeAsync();
            if (!_performance) _ = _system.InitializeMediaAsync();
            await _launcher.ScanAsync(); Emit("apps", _launcher.Apps);
        }
        catch (Exception e)
        {
            Program.Log(e);
            if (!_smoke) MessageBox.Show("Yuta could not start its interface. Make sure Microsoft Edge WebView2 Runtime is installed.\n\n" + e.Message, "Yuta", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _allowClose = true; Close();
        }
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x802A) Expand(true);
        if (m.Msg == 0x0312 && _hotkeyBinding is { Registered: true } binding && m.WParam.ToInt32() == binding.Id)
        {
            if (_hotkeyRecording) Emit("hotkeyCaptured", binding.Current.Shortcut);
            else { _previousWindow = Native.GetForegroundWindow(); Expand(!_expanded); }
            return;
        }
        if (m.Msg == 0x031D && _clipboard.Enabled) { _clipRetries = 0; _clipboardRetry.Start(); }
        base.WndProc(ref m);
    }
    private void DisplayChanged(object? sender, EventArgs e) { if (IsHandleCreated) BeginInvoke(RepositionPanel); }
    private void UpdateShortcutLabel() => _tray.Text = "Yuta · " + Hotkey.Parse(Setting("hotkey", Hotkey.Default)).Label;
    private void RecordShortcutAccelerator(object? sender, KeyEventArgs e)
    {
        if (!_hotkeyRecording) return;
        e.Handled = true; e.SuppressKeyPress = true;
        var key = (uint)e.KeyValue;
        if (key is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C || key == _recordedKey) return;
        _recordedKey = key;
        var eventName = "hotkeyCaptured"; string? value = null;
        if (key == 0x1B) eventName = "hotkeyCancel";
        else
        {
            try { value = Hotkey.FromVirtualKey(key, Hotkey.ReadModifiers()).Shortcut; }
            catch (ArgumentException error) { eventName = "hotkeyError"; value = error.Message; }
        }
        // Return from WebView's synchronous keyboard callback before sending IPC.
        BeginInvoke((Action)(() => { if (_hotkeyRecording) Emit(eventName, value); }));
    }
    private object SetShortcut(string shortcut)
    {
        var next = Hotkey.Parse(shortcut);
        void Persist(Hotkey value)
        {
            var settings = _store.Data["settings"]!.DeepClone();
            settings["hotkey"] = value.Shortcut;
            _store.Update("settings", settings);
        }
        if (_smoke) Persist(next);
        else if (_hotkeyBinding is null || !_hotkeyBinding.TrySet(next.Shortcut, Persist))
            throw new InvalidOperationException(next.Label + " is already in use or reserved by Windows. Choose another shortcut; your previous shortcut is unchanged.");
        _hotkey = _smoke || _hotkeyBinding?.Registered == true;
        _hotkeyRecording = false;
        UpdateShortcutLabel();
        return new { shortcut = next.Shortcut, label = next.Label, registered = _hotkey };
    }
    private int IdleHeight => _pill.Flattened ? NotchGeometry.FlatHeight : NotchGeometry.IdleHeight;
    private int IdleWidth => _pill.Flattened ? (Setting("shape", "notch") == "notch" ? NotchGeometry.NotchWidth : NotchGeometry.PillWidth) : _pill.WidthFor(Setting("shape", "notch") == "notch");
    private void ObserveHoverIntent()
    {
        var point = _pill.PointToClient(Cursor.Position);
        using var outline = NotchGeometry.Create(_pill.ClientRectangle, _pill.RenderScale, _pill.Attached);
        if (_expanded || _hoverRequiresExit || _contextMenuOpen || OverlayHidden || !_pill.Visible || !Setting("openOnHover", true) || !outline.IsVisible(point))
        {
            _hover.Stop(); _hoverIntent.Reset(); return;
        }
        var scale = _pill.RenderScale;
        if (_hoverIntent.Observe(new PointF(point.X / scale, point.Y / scale), Control.MouseButtons != MouseButtons.None, Environment.TickCount64)) Expand(true);
    }
    private Size IdleSize() => PanelSize(IdleWidth, IdleHeight);
    private void RepositionPanel() => ResizePanel(_expanded ? PanelWidth : IdleWidth, _expanded ? _panelHeight : IdleHeight, false);
    private bool CursorInsideMenu()
    {
        var point = PointToClient(Cursor.Position);
        if (!ClientRectangle.Contains(point)) return false;
        var visible = _visualSize.IsEmpty ? Size : _visualSize;
        using var path = NotchGeometry.Create(new RectangleF((Width - visible.Width) / 2, 0, visible.Width, visible.Height), DeviceDpi / 96f, Setting("shape", "notch") == "notch");
        return path.IsVisible(point);
    }
    private void SetMenuHidden(bool hidden)
    {
        _userHidden = hidden;
        if (hidden) { Expand(false); _animation.Stop(); FinishAnimation(); Hide(); _pill.Hide(); }
        else ApplyVisibility();
        EnsureOverlayOnTop();
    }
    private void PositionWindow()
    {
        var screens = Screen.AllScreens; var screen = screens[Math.Clamp(Setting("monitor", 0), 0, screens.Length - 1)];
        var attached = Setting("shape", "notch") == "notch";
        var top = screen.Bounds.Top + (attached ? 0 : (int)Math.Round(2 * DeviceDpi / 96d));
        Location = _smoke ? new(-12000, -12000) : new(screen.Bounds.Left + (screen.Bounds.Width - Width) / 2, top);
        _web.Location = new((Width - _web.Width) / 2, 0);
        var pillSize = IdleSize();
        _pill.Configure(new Rectangle(_smoke ? -12000 : screen.Bounds.Left + (screen.Bounds.Width - pillSize.Width) / 2, _smoke ? -12000 : top, pillSize.Width, pillSize.Height),
            DeviceDpi / 96f, attached, _pill.ClockText, _pill.TimerRunning);
        ApplyRegion(_visualSize.IsEmpty ? Size : _visualSize);
    }
    private void ApplyRegion(Size visible)
    {
        if (IsHandleCreated)
        {
            var left = (Width - visible.Width) / 2;
            using var path = NotchGeometry.Create(new RectangleF(left, 0, visible.Width, visible.Height), DeviceDpi / 96f, Setting("shape", "notch") == "notch");
            using var region = new Region(path);
            using var graphics = CreateGraphics();
            var rgn = region.GetHrgn(graphics);
            if (Native.SetWindowRgn(Handle, rgn, true) == 0) Native.DeleteObject(rgn);
        }
    }
    private Size PanelSize(int width, int height)
    {
        var screens = Screen.AllScreens;
        var screen = screens[Math.Clamp(Setting("monitor", 0), 0, screens.Length - 1)]; var scale = DeviceDpi / 96d;
        return new(Math.Min((int)(width * scale), screen.WorkingArea.Width - 16), Math.Min((int)(height * scale), screen.WorkingArea.Height - 24));
    }
    private void ResizePanel(int width, int height, bool animate = true)
    {
        var target = PanelSize(width, height);
        if (target == _to && _animation.Enabled && animate) return;
        _animation.Stop();
        _from = _visualSize.IsEmpty ? Size : _visualSize; _to = target;
        // Keep a stable browser viewport while only the native clipping region moves.
        // In particular, opening no longer repeatedly crosses CSS responsive breakpoints.
        if (_expanded) _web.Size = target;
        Size = new(Math.Max(_from.Width, target.Width), Math.Max(_from.Height, target.Height));
        PositionWindow();
        _animationStarted = Environment.TickCount64;
        _animationDuration = _expanded ? 280 : 210;
        if (_from != _to && animate && !_reducedMotion && SystemInformation.IsMenuAnimationEnabled && (!_smoke || _performance)) _animation.Start(); else FinishAnimation();
    }
    private void Animate()
    {
        var t = Math.Min(1, (Environment.TickCount64 - _animationStarted) / (double)_animationDuration);
        // The shoulders spread ahead of the body. No viewport resizing or layout
        // animation is needed, and rapid reversal continues from the visible shape.
        var widthEase = _expanded ? 1 - Math.Pow(1 - t, 4) : t * t * (3 - 2 * t);
        var heightEase = _expanded ? 1 - Math.Pow(1 - t, 3) : widthEase;
        _visualSize = new((int)Math.Round(_from.Width + (_to.Width - _from.Width) * widthEase), (int)Math.Round(_from.Height + (_to.Height - _from.Height) * heightEase)); ApplyRegion(_visualSize);
        if (t == 1) FinishAnimation();
    }
    private void FinishAnimation()
    {
        _animation.Stop(); _visualSize = _to; Size = _to; PositionWindow();
        if (!_expanded)
        {
            _web.Visible = false; Hide(); UpdatePill();
            // Closing beneath a stationary pointer is not a fresh opening gesture.
            _hoverRequiresExit = _pill.ClientRectangle.Contains(_pill.PointToClient(Cursor.Position));
            if (!OverlayHidden) { _pill.Show(); _pill.RefreshSurface(); } else _pill.Hide();
        }
        EnsureOverlayOnTop();
    }
    private void Expand(bool value)
    {
        _hover.Stop(); _hoverIntent.Reset();
        if (value) _userHidden = false;
        if (_expanded == value) { if (value && !Visible) { Show(); PositionWindow(); } return; }
        _idleSuspend.Stop();
        if (value && !_expanded) { var foreground = Native.GetForegroundWindow(); if (foreground != Handle && foreground != _pill.Handle && foreground != IntPtr.Zero) _previousWindow = foreground; }
        if (value && _pill.Visible) { _visualSize = _pill.Size; Size = _pill.Size; }
        _expanded = value;
        _explicitOpen = value && _visibilitySuppressed;
        _retraction.Reset(value && !_smoke && CursorInsideMenu());
        if (value)
        {
            ResizePanel(PanelWidth, _panelHeight);
            _web.CoreWebView2?.Resume(); _web.Visible = true; _web.BringToFront();
            if (!Visible) Show(); _pill.Hide();
            EnsureOverlayOnTop();
            if (!_smoke) _pointerWatch.Start();
        }
        else { _pointerWatch.Stop(); ResizePanel(IdleWidth, IdleHeight); _idleSuspend.Start(); }
        Emit("expanded", value);
        if (value && !_smoke) { Activate(); _web.Focus(); }
    }
    private async Task SuspendWebAsync()
    {
        if (!_ready || _expanded || _suspendPending || IsDisposed || _animation.Enabled) return;
        _suspendPending = true;
        try
        {
            _web.Visible = false;
            await _web.CoreWebView2.TrySuspendAsync();
            if (IsDisposed || Disposing || _allowClose) return;
            if (_expanded) { _web.CoreWebView2.Resume(); _web.Visible = true; _web.BringToFront(); }
        }
        catch (Exception e) { Program.Log(e); }
        finally { _suspendPending = false; }
    }
    private void UpdatePill()
    {
        if (!_smoke) _devices.CheckPower();
        _pill.ExpireNotice(Environment.TickCount64);
        var timer = _store.Data["timer"]; var running = timer?["running"]?.GetValue<bool>() == true;
        var seconds = running ? Math.Max(0, (long)Math.Ceiling(((timer?["endsAt"]?.GetValue<long>() ?? 0) - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 1000d)) : 0;
        var text = running ? $"{seconds / 60:00}:{seconds % 60:00}" : DateTime.Now.ToString("h:mm tt");
        var accent = AccentPalette.Resolve(Setting("accent", "custom"), Setting("customAccent", AccentPalette.DefaultCustom));
        if (_pill.ClockText != text || _pill.TimerRunning != running || _pill.AccentColor != accent)
        {
            _pill.ClockText = text; _pill.TimerRunning = running; _pill.AccentColor = accent; _pill.RefreshSurface();
        }
        UpdateIdleActivityGeometry();
    }
    private void MediaChanged(MediaSnapshot snapshot)
    {
        if (!IsHandleCreated || IsDisposed || Disposing || _allowClose) return;
        try { BeginInvoke(() => { if (IsDisposed || Disposing) return; _pill.SetMedia(snapshot); UpdateIdleActivityGeometry(); Emit("media", snapshot); }); }
        catch (InvalidOperationException) { /* The window closed while a session event arrived. */ }
    }
    private void Emit(string name, object? data) { if (_ready && !IsDisposed && !Disposing && !_allowClose) _web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { @event = name, data }, Store.JsonOptions)); }
    private void Reply(object response)
    {
        if (!IsDisposed && !Disposing && !_allowClose && _web.CoreWebView2 is { } core)
            core.PostWebMessageAsJson(JsonSerializer.Serialize(response, Store.JsonOptions));
    }
    private async void Receive(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (e.Source != Origin + "/index.html?native=1") return;
        string? id = null;
        try
        {
            if (e.WebMessageAsJson.Length > 1_100_000) throw new ArgumentException("Message too large.");
            var message = JsonNode.Parse(e.WebMessageAsJson)!; id = message["id"]?.GetValue<string>();
            var method = message["method"]!.GetValue<string>(); var args = message["args"] ?? new JsonObject();
            var result = await Dispatch(method, args);
            if (id != null) Reply(new { id, result });
        }
        catch (Exception error)
        {
            Program.Log(error);
            if (id != null) Reply(new { id, error = error.Message });
        }
    }
    private async Task<object?> Dispatch(string method, JsonNode args)
    {
        string Text(string key) => args[key]?.GetValue<string>() ?? "";
        switch (method)
        {
            case "bootstrap": return new { state = _store.Data, apps = _launcher.Apps, clips = _clipboard.Items, hotkey = _hotkey, monitors = Screen.AllScreens.Select((s, i) => new { id = i, name = $"Display {i + 1}{(s.Primary ? " · primary" : "")}" }) };
            case "save":
                if (Text("key") == "settings" && args["value"]?["hotkey"] is JsonValue requestedHotkey && requestedHotkey.GetValue<string>() != Setting("hotkey", Hotkey.Default))
                    throw new ArgumentException("Use the keyboard shortcut recorder to change this setting.");
                var oldShape = Setting("shape", "notch"); var oldMonitor = Setting("monitor", 0);
                var result = _store.Update(Text("key"), args["value"]);
                if (Text("key") == "settings")
                {
                    _clipboard.Enabled = !_smoke && Setting("clipboardEnabled", false);
                    if (!Setting("smartFlatten", true)) { _obstructionPolicy.Reset(); ApplySmartFlatten(false); }
                    if (oldShape != Setting("shape", "notch") || oldMonitor != Setting("monitor", 0)) RepositionPanel();
                    UpdatePill();
                }
                if (Text("key") == "timer") UpdatePill(); return result;
            case "expand": Expand(args["value"]?.GetValue<bool>() ?? true); return true;
            case "hotkeyRecord": _hotkeyRecording = args["active"]?.GetValue<bool>() ?? false; _recordedKey = 0; return true;
            case "hotkeySet": return SetShortcut(Text("shortcut"));
            case "dialogState": _webDialogOpen = args["open"]?.GetValue<bool>() ?? false; return true;
            case "visibilityMenu": if (!_smoke) _visibilityMenu?.Show(Cursor.Position); return true;
            case "resize":
                var panelHeight = Math.Clamp(args["height"]!.GetValue<int>(), 220, 800);
                _reducedMotion = args["reducedMotion"]?.GetValue<bool>() ?? false;
                if (_expanded && (panelHeight != _panelHeight || _web.Size != PanelSize(PanelWidth, panelHeight))) ResizePanel(PanelWidth, panelHeight, !_reducedMotion);
                _panelHeight = panelHeight; return true;
            case "controls": return await _system.ControlsAsync();
            case "volume": if (!_smoke) await _system.VolumeAsync(args["value"]!.GetValue<int>()); return true;
            case "mute": if (!_smoke) await _system.MuteAsync(); return true;
            case "brightness": if (!_smoke) await _system.BrightnessAsync(args["value"]!.GetValue<int>()); return true;
            case "settings": if (!_smoke) Launcher.Settings(Text("page")); return true;
            // Performance fixtures drive the idle player directly. Reading a
            // real session here would subscribe to live events and overwrite
            // their paused/playing state while measuring a transition.
            case "media": return _performance ? new MediaSnapshot() : await _system.MediaAsync();
            case "mediaAction": return !_smoke && await _system.MediaAction(Text("action"), args["position"]?.GetValue<double>(), Text("source"), Text("trackTitle"));
            case "stats": if (_statsBusy) return null; _statsBusy = true; try { return await _system.StatsAsync(); } finally { _statsBusy = false; }
            case "weather": return await _system.WeatherAsync(Text("city"), args["fahrenheit"]?.GetValue<bool>() ?? true);
            case "speed": if (_speedBusy) throw new InvalidOperationException("A speed test is already running."); _speedBusy = true; try { return await _system.SpeedAsync(); } finally { _speedBusy = false; }
            case "apps": return _launcher.Apps;
            case "launch": if (!_smoke) _launcher.Open(Text("target")); return true;
            case "routine":
                var routine = _store.Data["routines"]!.AsArray().FirstOrDefault(x => x?["id"]?.GetValue<string>() == Text("id")) ?? throw new ArgumentException("Routine not found.");
                var targets = routine["targets"]!.AsArray().Select(x => x!.GetValue<string>()).Take(12).ToArray();
                if (targets.Any(x => !Launcher.ValidTarget(x) && !_launcher.Apps.Any(a => a.Target == x))) throw new ArgumentException("One of this routine's destinations is no longer available. Edit the routine first.");
                var errors = new List<string>();
                foreach (var target in targets) { try { if (!_smoke) _launcher.Open(target); } catch (Exception ex) { errors.Add(ex.Message); } await Task.Delay(150); }
                return new { opened = targets.Length - errors.Count, errors };
            case "pick":
                _dialogOpen = true;
                try
                {
                    if (Text("kind") == "folder") { using var picker = new FolderBrowserDialog { Description = "Choose a folder" }; return picker.ShowDialog(this) == DialogResult.OK ? picker.SelectedPath : null; }
                    using var file = new OpenFileDialog { Title = "Choose an app or file", Filter = "Supported files|*.exe;*.lnk;*.pdf;*.txt;*.md;*.docx;*.xlsx;*.pptx;*.png;*.jpg;*.jpeg" }; return file.ShowDialog(this) == DialogResult.OK ? file.FileName : null;
                }
                finally { _dialogOpen = false; }
            case "clipboard": return _clipboard.Items;
            case "clipCopy": _clipboard.Copy(Text("id")); return true;
            case "clipPin": _clipboard.Pin(Text("id")); return _clipboard.Items;
            case "clipRemove": _clipboard.Remove(args["ids"]!.AsArray().Select(x => x!.GetValue<string>())); return _clipboard.Items;
            case "copyText": if (Text("text").Length > 100_000) throw new ArgumentException("Text is too long."); if (!_smoke) Clipboard.SetText(Text("text")); return true;
            case "clipPaste":
                _clipboard.Copy(Text("id"));
                if (_previousWindow == IntPtr.Zero || !Native.IsWindow(_previousWindow)) throw new InvalidOperationException("Copied. Return to your app and press Ctrl+V.");
                var destination = _previousWindow; Expand(false);
                if (!_smoke)
                {
                    Native.SetForegroundWindow(destination); await Task.Delay(150);
                    if (Native.GetForegroundWindow() != destination) throw new InvalidOperationException("Copied. Return to your app and press Ctrl+V.");
                    SendKeys.SendWait("^v");
                }
                return true;
            case "wallpapers": if (_wallpapers.Items.Count == 0) await _wallpapers.ScanAsync(); return _wallpapers.Items;
            case "wallpaperFolder":
                _dialogOpen = true; string? folder = null;
                try { using var picker = new FolderBrowserDialog { Description = "Choose your wallpaper folder" }; if (picker.ShowDialog(this) == DialogResult.OK) folder = picker.SelectedPath; } finally { _dialogOpen = false; }
                if (folder != null) { await _wallpapers.ScanAsync(folder); _store.Data["wallpaperFolder"] = folder; _store.Save(); } return _wallpapers.Items;
            case "wallpaperApply": if (!_smoke) _wallpapers.Apply(Text("id")); return true;
            case "power":
                if (_smoke) throw new InvalidOperationException("Power actions are disabled in tests.");
                var action = Text("action"); if (!new[] { "lock", "sleep", "restart", "shutdown" }.Contains(action)) throw new ArgumentException("Unknown power action.");
                Expand(false);
                if (action == "lock") { if (!Native.LockWorkStation()) throw new InvalidOperationException("Windows could not lock."); }
                else if (action == "sleep") { if (!Native.SetSuspendState(false, false, false)) throw new InvalidOperationException("Sleep is not available."); }
                else { var start = new ProcessStartInfo("shutdown.exe") { CreateNoWindow = true, UseShellExecute = false }; start.ArgumentList.Add(action == "restart" ? "/r" : "/s"); start.ArgumentList.Add("/t"); start.ArgumentList.Add("0"); Process.Start(start); }
                return true;
            case "quit": _allowClose = true; BeginInvoke(Close); return true;
            default: throw new ArgumentException("Unknown action.");
        }
    }
    private void CheckTimer()
    {
        var timer = _store.Data["timer"];
        if (timer?["running"]?.GetValue<bool>() != true) return;
        var due = timer["endsAt"]?.GetValue<long>() ?? 0;
        if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < due) return;
        timer["running"] = false; timer["remaining"] = 0; _store.Save();
        _tray.ShowBalloonTip(5000, "Yuta", "Your focus timer is complete. Time for a breather.", ToolTipIcon.Info);
        System.Media.SystemSounds.Asterisk.Play(); Emit("timerDone", true);
    }
    private async Task SmokeAsync()
    {
        try
        {
            Expand(true); await Task.Delay(1600);
            var dir = Path.Combine(AppContext.BaseDirectory, "smoke"); Directory.CreateDirectory(dir);
            var result = await _web.CoreWebView2.ExecuteScriptAsync("JSON.stringify({title:document.title,native:!!window.chrome.webview,tabs:[...document.querySelectorAll('[role=tab]')].map(x=>x.textContent),width:innerWidth,height:innerHeight,errors:window.__yutaErrors||[]})");
            File.WriteAllText(Path.Combine(dir, "ui.json"), result);
            await using (var shot = File.Create(Path.Combine(dir, "native.png"))) await _web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, shot);
            await _system.InitializeAsync();
            var hardware = await _system.StatsAsync();
            var controlState = await _system.ControlsAsync();
            await _wallpapers.ScanAsync();
            var music = await _system.MediaAsync();
            File.WriteAllText(Path.Combine(dir, "integrations.json"), JsonSerializer.Serialize(new { hardware, controls = controlState, wallpapers = _wallpapers.Items.Count, apps = _launcher.Apps.Count, media = music }, Store.JsonOptions));
            foreach (var section in new[] { "Home", "Today", "Stats", "Clipboard", "Apps", "Wallpaper", "Settings" })
            {
                Emit("navigate", section); await Task.Delay(section == "Stats" ? 2000 : 600);
                await using var capture = File.Create(Path.Combine(dir, section.ToLowerInvariant() + ".png")); await _web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, capture);
                var layout = await _web.CoreWebView2.ExecuteScriptAsync("JSON.stringify({panel:document.querySelector('.panel')?.getAttribute('aria-label'),overflow:document.documentElement.scrollWidth>innerWidth,scrollNeeded:document.querySelector('.panel').scrollHeight>document.querySelector('.panel').clientHeight+1,errors:window.__yutaErrors||[]})");
                File.WriteAllText(Path.Combine(dir, section.ToLowerInvariant() + ".json"), layout);
            }
            Expand(false); await Task.Delay(500);
            File.WriteAllText(Path.Combine(dir, "warm-idle.json"), JsonSerializer.Serialize(new { suspended = _web.CoreWebView2.IsSuspended, pillVisible = _pill.Visible, mainVisible = Visible, width = _pill.Width, height = _pill.Height, webVisible = _web.Visible }, Store.JsonOptions));
            using var pillBitmap = _pill.RenderBitmap(); pillBitmap.Save(Path.Combine(dir, "pill.png"));
            Expand(true); await Task.Delay(500);
            File.WriteAllText(Path.Combine(dir, "resume.json"), JsonSerializer.Serialize(new { suspended = _web.CoreWebView2.IsSuspended, expanded = _expanded, webVisible = _web.Visible, state = await _web.CoreWebView2.ExecuteScriptAsync("JSON.stringify({expanded:document.querySelector('#yuta').dataset.expanded==='true',errors:window.__yutaErrors||[]})") }, Store.JsonOptions));
            Expand(false); await Task.Delay(16000);
            var suspended = _web.CoreWebView2.IsSuspended;
            File.WriteAllText(Path.Combine(dir, "idle.json"), JsonSerializer.Serialize(new { suspended, pillVisible = _pill.Visible, webVisible = _web.Visible }, Store.JsonOptions));
            Expand(true); await Task.Delay(700);
            File.WriteAllText(Path.Combine(dir, "cold-resume.json"), JsonSerializer.Serialize(new { suspended = _web.CoreWebView2.IsSuspended, expanded = _expanded, webVisible = _web.Visible, state = await _web.CoreWebView2.ExecuteScriptAsync("JSON.stringify({panel:document.querySelector('.panel')?.getAttribute('aria-label'),expanded:document.querySelector('#yuta').dataset.expanded,errors:window.__yutaErrors||[]})") }, Store.JsonOptions));
            if (!suspended || _web.CoreWebView2.IsSuspended || !_web.Visible || !_expanded) throw new InvalidOperationException("Renderer idle/resume lifecycle failed.");
        }
        catch (Exception e) { Program.Log(e); Environment.ExitCode = 1; }
        finally { _allowClose = true; Close(); }
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Expand(false); return; }
        _foreground.Stop(); _timerAlarm.Stop(); _animation.Stop(); _clipboardRetry.Stop(); _hover.Stop(); _idleSuspend.Stop(); _pointerWatch.Stop(); _pointerWatch.Dispose();
        _hotkeyBinding?.Dispose(); Native.RemoveClipboardFormatListener(Handle); _tray.Visible = false; _tray.Dispose();
        _visibilityMenu?.Dispose();
        _pill.Close(); _pill.Dispose();
        _system.MediaChanged -= MediaChanged; _system.Dispose();
        _devices.Notice -= ActivityNotice; _devices.Dispose();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        base.OnFormClosing(e);
    }
}
