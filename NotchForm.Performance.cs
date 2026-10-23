using System.Text.Json;

namespace Yuta;

public sealed partial class NotchForm
{
    // Isolated/offscreen integration exercise: no input injection or system mutations.
    // Wall-clock timing here is diagnostic, not a claim about visible compositor FPS.
    private async Task PerformanceAsync()
    {
        try
        {
            await Task.Delay(1500);
            // Let the initial tab negotiate its final height before measuring warm
            // transitions, which must not reflow the browser on every frame.
            Expand(true); await Task.Delay(500); Expand(false); await Task.Delay(400);
            var sizes = new List<object>();
            var gaps = new List<long>();
            var previousTick = Environment.TickCount64;
            using var heartbeat = new System.Windows.Forms.Timer { Interval = 10 };
            heartbeat.Tick += (_, _) => { var now = Environment.TickCount64; gaps.Add(now - previousTick); previousTick = now; };
            EventHandler resized = (_, _) => sizes.Add(new { at = Environment.TickCount64, width = _web.Width, height = _web.Height });
            _web.SizeChanged += resized; heartbeat.Start();
            var cycles = new List<object>();
            var checks = new List<(string name, bool passed)>();
            var powerBlocked = 0;
            foreach (var action in new[] { "lock", "sleep", "restart", "shutdown" })
            {
                try { await Dispatch("power", new System.Text.Json.Nodes.JsonObject { ["action"] = action }); }
                catch (InvalidOperationException error) when (error.Message == "Power actions are disabled in tests.") { powerBlocked++; }
            }
            checks.Add(("All power actions are blocked in native fixtures without opening a dialog", powerBlocked == 4 && !_dialogOpen));
            checks.Add(("Approved 900 DIP menu uses native 100 percent scale", _web.ZoomFactor == 1 && _web.Size == PanelSize(900, _panelHeight)));
            for (var i = 0; i < 5; i++)
            {
                var before = sizes.Count; Expand(true); await Task.Delay(450);
                var openChanges = sizes.Count - before;
                before = sizes.Count; Expand(false); await Task.Delay(450);
                cycles.Add(new { openViewportChanges = openChanges, closeViewportChanges = sizes.Count - before });
                checks.Add(($"Warm transition {i + 1} retains viewport", openChanges == 0 && sizes.Count == before));
            }
            checks.Add(("Closed compact surface visible while main window and warm browser are hidden", !_expanded && !Visible && !_web.Visible && _pill.Visible && !_userHidden && !_web.CoreWebView2.IsSuspended && _pill.Size == IdleSize()));
            _hover.Start(); await Task.Delay(200);
            checks.Add(("Hover leaving target does not open later", !_expanded));
            Expand(true); await Task.Delay(400);
            foreach (var section in new[] { "Home", "Apps", "Wallpaper", "Today", "Stats", "Home", "Settings", "Today" })
            { Emit("navigate", section); await Task.Delay(100); }
            for (var i = 0; i < 10; i++) { Expand(i % 2 == 0); await Task.Delay(40); }
            Expand(true); await Task.Delay(800);
            checks.Add(("Rapid reversals settle on expanded geometry", !_animation.Enabled && Size == _visualSize && Size == PanelSize(PanelWidth, _panelHeight) && _web.Size == Size && _web.Location == Point.Empty));
            checks.Add(("Rapid reopen restores browser visibility", _expanded && _web.Visible && !_pill.Visible && !_web.CoreWebView2.IsSuspended));
            ResizePanel(PanelWidth, 420);
            var started = _animationStarted;
            ResizePanel(PanelWidth, 420);
            checks.Add(("Duplicate resize does not restart transition", _animationStarted == started));
            await Task.Delay(350); ResizePanel(PanelWidth, _panelHeight, false);
            _reducedMotion = true; Expand(false);
            checks.Add(("Reduced motion closes immediately into a separate compact surface", !_animation.Enabled && !Visible && !_web.Visible && _pill.Visible && _pill.Size == IdleSize()));
            Expand(true);
            checks.Add(("Reduced motion opens immediately", !_animation.Enabled && _web.Visible && Size == PanelSize(PanelWidth, _panelHeight)));
            _reducedMotion = false;
            // This store belongs to the isolated performance profile. Exercise both
            // idle shapes and reversed transitions without touching the user's settings.
            var originalShape = Setting("shape", "pill");
            foreach (var shape in new[] { "pill", "notch" })
            {
                _store.Data["settings"]!["shape"] = shape;
                RepositionPanel();
                Expand(false); await Task.Delay(350);
                checks.Add(($"{shape} idle is compact, independent, and leaves the main window hidden", !_expanded && !Visible && _pill.Visible && !_web.Visible && !_userHidden && _pill.Size == IdleSize() && _pill.Attached == (shape == "notch")));
                for (var i = 0; i < 6; i++) { Expand(i % 2 == 0); await Task.Delay(35); }
                Expand(true); await Task.Delay(500);
                checks.Add(($"{shape} rapid reopen settles on the retained menu", _expanded && Visible && !_animation.Enabled && !_pill.Visible && _web.Visible && !_web.CoreWebView2.IsSuspended && Size == PanelSize(PanelWidth, _panelHeight) && _web.Size == Size));
            }
            _store.Data["settings"]!["shape"] = originalShape;
            RepositionPanel();
            SetMenuHidden(true);
            checks.Add(("Tray hide hides both host and compact surface", _userHidden && !_expanded && !Visible && !_pill.Visible && !_web.Visible));
            SetMenuHidden(false);
            checks.Add(("Tray show restores only compact surface", !_userHidden && !_expanded && !Visible && _pill.Visible && !_web.Visible && _pill.Size == IdleSize()));
            _activeApp = new(new IntPtr(1234), "C:\\Fixture\\Hidden.exe", "Hidden fixture", false);
            _visibility.ExcludeApp(_activeApp.Path, _activeApp.Name); ApplyVisibility();
            checks.Add(("An app exclusion hides both overlay windows", !_pill.Visible && !Visible && _visibilitySuppressed));
            _activeApp = new(new IntPtr(5678), "C:\\Fixture\\Visible.exe", "Visible fixture", false); ApplyVisibility();
            checks.Add(("Switching to an allowed app restores the idle notch", _pill.Visible && !Visible && !_visibilitySuppressed));
            _visibility.Snooze(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); ApplyVisibility();
            checks.Add(("Snooze hides the idle overlay", !_pill.Visible && !Visible));
            Expand(true); await Task.Delay(350); ApplyVisibility();
            checks.Add(("An explicit shortcut can open during snooze without cancelling the pause", _expanded && _web.Visible && _explicitOpen && _visibility.Read().SnoozedUntil > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            Expand(false); await Task.Delay(350);
            checks.Add(("Closing returns to the snoozed state", !_pill.Visible && !Visible && !_explicitOpen));
            _visibility.Resume(); ApplyVisibility();
            checks.Add(("Resume restores the idle notch", _pill.Visible && !_visibilitySuppressed));
            _visibility.RemoveApp("C:\\Fixture\\Hidden.exe"); _activeApp = null;
            var beforeActivity = sizes.Count;
            var music = ActivityTests.Music();
            _pill.SetMedia(music); UpdateIdleActivityGeometry(); await Task.Delay(400);
            checks.Add(("Artwork, short title and bars widen without waking the browser", _pill.Size == IdleSize() && _pill.Width == (int)Math.Round(NotchGeometry.ActivityWidth * DeviceDpi / 96d) && !_web.Visible && !Visible && sizes.Count == beforeActivity));
            var frame = _pill.ActivityFrame; await Task.Delay(140);
            checks.Add(("Playing music animates only the visible native idle surface", !SystemInformation.IsMenuAnimationEnabled || _pill.ActivityAnimating && _pill.ActivityFrame != frame));
            var beforeFlatten = sizes.Count;
            ApplySmartFlatten(true); await Task.Delay(350);
            checks.Add(("Covered controls flatten into a four-DIP surface with no old invisible hitbox or WebView resize", _pill.Height == (int)(NotchGeometry.FlatHeight * DeviceDpi / 96d) && _pill.ClientSize == _pill.Size && !Visible && !_web.Visible && sizes.Count == beforeFlatten));
            frame = _pill.ActivityFrame; await Task.Delay(100);
            checks.Add(("Flattening pauses waveform rendering while keeping the music state", !_pill.ActivityAnimating && _pill.ActivityFrame == frame && _pill.MusicPlaying));
            Expand(true); await Task.Delay(400);
            checks.Add(("Explicit opening from a flat strip restores the complete retained menu", _expanded && _web.Visible && !_pill.Visible && Size == PanelSize(PanelWidth, _panelHeight)));
            Expand(false); await Task.Delay(350);
            checks.Add(("Closing over an obstructed control returns to the flat strip", !_expanded && _pill.Flattened && _pill.Height == (int)(NotchGeometry.FlatHeight * DeviceDpi / 96d) && !_web.Visible));
            ApplySmartFlatten(false); await Task.Delay(350);
            checks.Add(("Clearing the obstruction restores current music and its normal geometry", !_pill.Flattened && _pill.Size == IdleSize() && _pill.Height == (int)(NotchGeometry.IdleHeight * DeviceDpi / 96d) && (SystemInformation.IsMenuAnimationEnabled ? _pill.ActivityAnimating : !_pill.ActivityAnimating)));
            _pill.SetMedia(music with { Playing = false }); UpdateIdleActivityGeometry(); await Task.Delay(350);
            frame = _pill.ActivityFrame; await Task.Delay(100);
            checks.Add(("Pausing contracts to the clock and stops animation work", _pill.Size == IdleSize() && !_pill.HasActivity && !_pill.ActivityAnimating && frame == _pill.ActivityFrame && sizes.Count == beforeActivity));
            _pill.SetMedia(music); UpdateIdleActivityGeometry(); await Task.Delay(50);
            Expand(true); await Task.Delay(400);
            checks.Add(("Opening during music widening settles on the full-size menu and stops hidden animation", _expanded && !_pill.Visible && !_pill.ActivityAnimating && _web.Visible && Size == PanelSize(PanelWidth, _panelHeight)));
            Expand(false); await Task.Delay(400);
            checks.Add(("Closing returns to the wide music notch", _pill.Visible && _pill.Size == IdleSize() && !Visible && !_web.Visible));
            _reducedMotion = true; UpdateIdleActivityGeometry();
            frame = _pill.ActivityFrame; await Task.Delay(100);
            checks.Add(("Reduced motion keeps music static", !_pill.ActivityAnimating && frame == _pill.ActivityFrame));
            _pill.SetMedia(music with { Playing = false }); UpdateIdleActivityGeometry();
            ActivityNotice(new("power", "Charging", 48));
            checks.Add(("A power notice temporarily widens the resting notch", _pill.HasActivity && _pill.Size == IdleSize()));
            _pill.ExpireNotice(Environment.TickCount64 + 5000); UpdateIdleActivityGeometry();
            checks.Add(("An expired notice restores the idle clock geometry", !_pill.HasActivity && _pill.Size == IdleSize() && !_pill.ActivityAnimating));
            _reducedMotion = false; UpdateIdleActivityGeometry();
            Expand(true); await Task.Delay(350);
            var firstControls = _system.ControlsAsync(); var secondControls = _system.ControlsAsync();
            checks.Add(("Hardware reads share pending work", ReferenceEquals(firstControls, secondControls) || ReferenceEquals(await firstControls, await secondControls)));
            checks.Add(("Hardware read cache reuses snapshot", ReferenceEquals(await firstControls, await _system.ControlsAsync())));
            heartbeat.Stop(); _web.SizeChanged -= resized;
            var script = await _web.CoreWebView2.ExecuteScriptAsync("JSON.stringify({panel:document.querySelector('.panel')?.getAttribute('aria-label'),expanded:document.querySelector('#yuta').dataset.expanded==='true',overflow:document.documentElement.scrollWidth>innerWidth,errors:window.__yutaErrors||[]})");
            using var state = JsonDocument.Parse(JsonSerializer.Deserialize<string>(script)!);
            checks.Add(("UI has no errors or horizontal overflow", state.RootElement.GetProperty("errors").GetArrayLength() == 0 && !state.RootElement.GetProperty("overflow").GetBoolean()));
            var sorted = gaps.Order().ToArray();
            var output = new { offscreen = true, animationsEnabled = SystemInformation.IsMenuAnimationEnabled, cycles, checks = checks.Select(x => new { x.name, x.passed }), viewportChanges = sizes.Count, heartbeatP95Ms = sorted.Length > 0 ? sorted[(int)((sorted.Length - 1) * .95)] : 0, heartbeatMaxMs = sorted.LastOrDefault(), expanded = _expanded, mainVisible = Visible, idleVisible = _pill.Visible, webVisible = _web.Visible, suspended = _web.CoreWebView2.IsSuspended, state = state.RootElement, sizes };
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "performance.json"), JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
            if (checks.Any(x => !x.passed)) Environment.ExitCode = 1;
        }
        catch (Exception e) { Program.Log(e); Environment.ExitCode = 1; }
        finally { _allowClose = true; Close(); }
    }
}
