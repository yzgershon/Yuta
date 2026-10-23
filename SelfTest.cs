using System.Text.Json;
using System.Text.Json.Nodes;
using Yuta.Services;

namespace Yuta;
internal static class SelfTest
{
    internal static async Task NetworkAsync()
    {
        var results = new List<object>(); var service = new SystemServices();
        try { results.Add(new { name = "Weather sample query", passed = true, result = await service.WeatherAsync("New York", true) }); } catch (Exception e) { results.Add(new { name = "Weather sample query", passed = false, error = e.Message }); }
        try { results.Add(new { name = "Speed endpoint", passed = true, result = await service.SpeedAsync() }); } catch (Exception e) { results.Add(new { name = "Speed endpoint", passed = false, error = e.Message }); }
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "network-test.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        Environment.ExitCode = results.Any(x => JsonSerializer.Serialize(x).Contains("\"passed\":false")) ? 1 : 0;
    }
    internal static void Run()
    {
        var checks = new List<object>();
        void Check(string name, Func<bool> check) { try { checks.Add(new { name, passed = check() }); } catch (Exception e) { checks.Add(new { name, passed = false, error = e.Message }); } }
        MediaSessionTests.Run(Check);
        var root = Path.Combine(Path.GetTempPath(), "Yuta-test-" + Guid.NewGuid().ToString("N"));
        HotkeyTests.Run(Check, root);
        VisibilityTests.Run(Check, root);
        ActivityTests.Run(Check);
        ObstructionTests.Run(Check, root);
        var store = new Store(root);
        Check("Local state survives reopening", () => { store.Update("notes", JsonValue.Create("A durable note")); return new Store(root).Data["notes"]!.GetValue<string>() == "A durable note"; });
        var legacyRoot = Path.Combine(root, "legacy-profile");
        Check("Yuta defaults migrate without moving or losing the existing profile", () =>
        {
            Directory.CreateDirectory(legacyRoot);
            var legacy = new JsonObject
            {
                ["notes"] = "Keep my existing note",
                ["todos"] = new JsonArray(new JsonObject { ["id"] = "keep", ["text"] = "Keep my task", ["done"] = false }),
                ["routines"] = new JsonArray(new JsonObject { ["id"] = "routine", ["name"] = "Keep my routine", ["targets"] = new JsonArray("https://example.com") }),
                ["settings"] = new JsonObject { ["shape"] = "pill", ["openOnHover"] = false, ["city"] = "Boston", ["fahrenheit"] = false, ["clipboardEnabled"] = false }
            };
            File.WriteAllText(Path.Combine(legacyRoot, "state.json"), legacy.ToJsonString());
            var migrated = new Store(legacyRoot);
            return migrated.Root == legacyRoot && migrated.Data["notes"]!.GetValue<string>() == "Keep my existing note"
                && migrated.Data["todos"]![0]!["text"]!.GetValue<string>() == "Keep my task"
                && migrated.Data["routines"]![0]!["name"]!.GetValue<string>() == "Keep my routine"
                && migrated.Data["settings"]!["city"]!.GetValue<string>() == "Boston"
                && !migrated.Data["settings"]!["fahrenheit"]!.GetValue<bool>()
                && migrated.Data["settings"]!["shape"]!.GetValue<string>() == "notch"
                && migrated.Data["settings"]!["openOnHover"]!.GetValue<bool>()
                && migrated.Data["yutaInterfaceVersion"]!.GetValue<int>() == 1;
        });
        Check("A later explicit pill and click-only preference survives reopening", () =>
        {
            var migrated = new Store(legacyRoot);
            var settings = migrated.Data["settings"]!.DeepClone();
            settings["shape"] = "pill"; settings["openOnHover"] = false;
            settings["accent"] = "custom"; settings["customAccent"] = "#83BBCC"; migrated.Update("settings", settings);
            var reopened = new Store(legacyRoot);
            return reopened.Data["settings"]!["shape"]!.GetValue<string>() == "pill"
                && reopened.Data["settings"]!["accent"]!.GetValue<string>() == "custom"
                && reopened.Data["settings"]!["customAccent"]!.GetValue<string>() == "#83bbcc"
                && AccentPalette.Resolve("custom", "#83bbcc") == ColorTranslator.FromHtml("#83bbcc")
                && AccentPalette.NormalizeCustom("invalid") == "#9b80ff"
                && !reopened.Data["settings"]!["openOnHover"]!.GetValue<bool>()
                && reopened.Data["notes"]!.GetValue<string>() == "Keep my existing note";
        });
        Check("Unknown state keys rejected", () => { try { store.Update("arbitrary", JsonValue.Create(1)); return false; } catch (ArgumentException) { return true; } });
        Check("Malformed list rejected", () => { try { store.Update("todos", JsonValue.Create("bad")); return false; } catch (ArgumentException) { return true; } });
        Check("Shell execution blocked", () => !Launcher.ValidTarget("powershell -Command whoami") && !Launcher.ValidTarget("javascript:alert(1)") && !Launcher.ValidTarget("file:///C:/Windows/System32/cmd.exe"));
        Check("Website accepted", () => Launcher.ValidTarget("https://example.com/path?q=hello"));
        Check("Clipboard capture starts disabled", () => !new ClipboardHistory(root).Enabled);
        Check("Encrypted clipboard and pin state survive reopening", () =>
        {
            var fixture = new List<ClipItem> { new("fixture", "text", "fixture clipboard text", null, "Fixture", DateTimeOffset.UtcNow) };
            var payload = JsonSerializer.SerializeToUtf8Bytes(fixture, Store.JsonOptions);
            var path = Path.Combine(root, "clipboard.bin");
            File.WriteAllBytes(path, System.Security.Cryptography.ProtectedData.Protect(payload, null, System.Security.Cryptography.DataProtectionScope.CurrentUser));
            var history = new ClipboardHistory(root); history.Pin("fixture");
            return new ClipboardHistory(root).Items[0].Pinned && !System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains("fixture clipboard text");
        });
        Check("Hardware metadata readable", () => new SystemServices().Controls() != null);
        Check("A new local profile defaults to the approved violet", () => store.Data["settings"]!["accent"]!.GetValue<string>() == "custom" && store.Data["settings"]!["customAccent"]!.GetValue<string>() == "#9b80ff");
        Check("All six accents persist and unknown stored accents fall back safely", () =>
        {
            foreach (var accent in new[] { "lavender", "ice", "rose", "amber", "porcelain", "custom" })
            {
                var settings = store.Data["settings"]!.DeepClone(); settings["accent"] = accent; store.Update("settings", settings);
                if (new Store(root).Data["settings"]!["accent"]!.GetValue<string>() != accent) return false;
            }
            var invalid = store.Data["settings"]!.DeepClone(); invalid["accent"] = "missing-palette"; store.Update("settings", invalid);
            var reopened = new Store(root);
            return reopened.Data["settings"]!["accent"]!.GetValue<string>() == "lavender"
                && reopened.Data["notes"]!.GetValue<string>() == "A durable note";
        });
        Check("Idle clock and focus label redraw changes and reuse an unchanged cached bitmap", () =>
        {
            // Observe the actual bitmap sent by RefreshSurface, not a fresh render
            // that could conceal a stale cached native surface. This form stays hidden.
            var surfaceField = typeof(IdleNotchWindow).GetField("_surface", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Native surface cache is unavailable.");
            using var idle = new IdleNotchWindow { Location = new(-12000, -12000), Size = new(NotchGeometry.NotchWidth, NotchGeometry.IdleHeight), Attached = true, RenderScale = 1, TimerRunning = true, ClockText = "03:12" };
            bool ContainsAccentColor(Bitmap bitmap, Color expected)
            {
                for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var actual = bitmap.GetPixel(x, y);
                    if (actual.A == 255 && Math.Abs(actual.R - expected.R) <= 1 && Math.Abs(actual.G - expected.G) <= 1 && Math.Abs(actual.B - expected.B) <= 1) return true;
                }
                return false;
            }
            Bitmap? previous = null;
            foreach (var (id, hex) in new[] { ("lavender", "#c2b6ec"), ("ice", "#acd4f5"), ("rose", "#e4b2c2"), ("amber", "#dfc18e"), ("porcelain", "#d8d9d3"), ("custom", "#9b80ff") })
            {
                var expected = ColorTranslator.FromHtml(hex);
                idle.AccentColor = AccentPalette.Resolve(id); idle.RefreshSurface();
                var current = (Bitmap?)surfaceField.GetValue(idle) ?? throw new InvalidOperationException("Native surface was not rendered.");
                if (idle.AccentColor.ToArgb() != expected.ToArgb() || ReferenceEquals(previous, current) || !ContainsAccentColor(current, expected)) return false;
                idle.RefreshSurface();
                if (!ReferenceEquals(current, surfaceField.GetValue(idle))) return false;
                previous = current;
            }
            idle.TimerRunning = false; idle.ClockText = "3:12 PM"; idle.RefreshSurface();
            var clock = surfaceField.GetValue(idle);
            if (ReferenceEquals(previous, clock)) return false;
            idle.ClockText = "3:13 PM"; idle.RefreshSurface();
            var nextMinute = surfaceField.GetValue(idle);
            if (ReferenceEquals(clock, nextMinute)) return false;
            idle.RefreshSurface();
            return ReferenceEquals(nextMinute, surfaceField.GetValue(idle)) && !idle.Visible;
        });
        Check("Deliberate hover requires dwell and a settled pointer", () =>
        {
            var intent = new HoverIntent();
            return !intent.Observe(new(10, 10), false, 0)
                && !intent.Observe(new(10, 10), false, 449)
                && intent.Observe(new(10, 10), false, 450);
        });
        Check("Crossing the notch slowly does not open it", () =>
        {
            var intent = new HoverIntent();
            for (var t = 0; t <= 1800; t += 30)
                if (intent.Observe(new(t / 20f, 10), false, t)) return false;
            return !intent.Observe(new(90, 10), false, 2049)
                && intent.Observe(new(90, 10), false, 2050);
        });
        Check("A hover exit or drag requires a fresh deliberate gesture", () =>
        {
            var intent = new HoverIntent(); intent.Observe(new(10, 10), false, 0);
            if (intent.Observe(new(10, 10), true, 1000)) return false;
            if (intent.Observe(new(10, 10), false, 2000) || intent.Observe(new(10, 10), false, 2449)) return false;
            if (!intent.Observe(new(10, 10), false, 2450)) return false;
            intent.Reset();
            return !intent.Observe(new(10, 10), false, 5000) && !intent.Observe(new(10, 10), false, 5449)
                && intent.Observe(new(10, 10), false, 5450);
        });
        Check("Hover retraction starts only after entering the menu", () =>
        {
            var hover = new HoverRetraction(); hover.Reset(false);
            return !hover.Observe(false, false, 0) && !hover.Observe(false, false, 10000)
                && !hover.Observe(true, false, 10001) && !hover.Observe(false, false, 10002)
                && !hover.Observe(false, false, 10181) && hover.Observe(false, false, 10182);
        });
        Check("Hover reentry cancels the pending close", () =>
        {
            var hover = new HoverRetraction(); hover.Reset(true);
            return !hover.Observe(false, false, 100) && !hover.Observe(true, false, 279)
                && !hover.Observe(false, false, 280) && !hover.Observe(false, false, 459)
                && hover.Observe(false, false, 460);
        });
        Check("Dialogs and pointer drags defer retraction until a fresh grace period", () =>
        {
            var hover = new HoverRetraction(); hover.Reset(true);
            return !hover.Observe(false, false, 100) && !hover.Observe(false, true, 500)
                && !hover.Observe(false, true, 5000) && !hover.Observe(false, false, 5001)
                && !hover.Observe(false, false, 5180) && hover.Observe(false, false, 5181);
        });
        Check("Reset cancels a previous hover session", () =>
        {
            var hover = new HoverRetraction(); hover.Reset(true); hover.Observe(false, false, 100);
            hover.Reset(false);
            return !hover.Observe(false, false, 1000) && !hover.Observe(true, true, 1001)
                && !hover.Observe(false, false, 1002) && hover.Observe(false, false, 1182);
        });
        var shapes = new List<object>();
        var shapeDirectory = Path.Combine(AppContext.BaseDirectory, "shapes");
        Directory.CreateDirectory(shapeDirectory);
        foreach (var dpi in new[] { 96, 120, 144, 192 })
        foreach (var attached in new[] { false, true })
        {
            var shape = attached ? "notch" : "pill";
            Check($"Compact {shape} has transparent antialiased edges at {dpi} DPI", () =>
            {
                var scale = dpi / 96f;
                var expectedSize = new Size((int)((attached ? NotchGeometry.NotchWidth : NotchGeometry.PillWidth) * scale), (int)(NotchGeometry.IdleHeight * scale));
                using var idle = new IdleNotchWindow { Attached = attached, RenderScale = scale, Size = expectedSize };
                using var bitmap = idle.RenderBitmap();
                var centerAlpha = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2).A;
                var cornerAlpha = new[] { (int)bitmap.GetPixel(0, 0).A, bitmap.GetPixel(bitmap.Width - 1, 0).A, bitmap.GetPixel(0, bitmap.Height - 1).A, bitmap.GetPixel(bitmap.Width - 1, bitmap.Height - 1).A };
                var topCenterAlpha = bitmap.GetPixel(bitmap.Width / 2, 0).A;
                var leftMiddleAlpha = bitmap.GetPixel(0, bitmap.Height / 2).A;
                var rightMiddleAlpha = bitmap.GetPixel(bitmap.Width - 1, bitmap.Height / 2).A;
                var fractionalEdgePixels = 0;
                for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++)
                {
                    // Only the outside quarters: exclude the status mark from this edge test.
                    if (x >= bitmap.Width / 4 && x < bitmap.Width * 3 / 4) continue;
                    var alpha = bitmap.GetPixel(x, y).A;
                    if (alpha is > 0 and < 255) fractionalEdgePixels++;
                }
                bitmap.Save(Path.Combine(shapeDirectory, $"{shape}-{dpi}.png"), System.Drawing.Imaging.ImageFormat.Png);
                shapes.Add(new { shape, dpi, bitmap.Width, bitmap.Height, centerAlpha, cornerAlpha, topCenterAlpha, leftMiddleAlpha, rightMiddleAlpha, fractionalEdgePixels, visible = idle.Visible });
                // An attached notch meets the top edge at its shoulder endpoints;
                // those corner pixels may be feathered. Its lower corners and the
                // space beside the inward shoulders must be fully transparent.
                var contourTransparent = attached
                    ? cornerAlpha.Skip(2).All(alpha => alpha == 0) && leftMiddleAlpha == 0 && rightMiddleAlpha == 0 && topCenterAlpha == 255
                    : cornerAlpha.All(alpha => alpha == 0);
                return bitmap.Size == expectedSize && centerAlpha == 255 && contourTransparent && fractionalEdgePixels > 0 && !idle.Visible;
            });
            Check($"Idle {shape} clock and timer stay crisp at {dpi} DPI", () =>
            {
                var scale = dpi / 96f;
                using var idle = new IdleNotchWindow
                {
                    Attached = attached, RenderScale = scale, TimerRunning = true,
                    Size = new((int)((attached ? NotchGeometry.NotchWidth : NotchGeometry.PillWidth) * scale), (int)(NotchGeometry.IdleHeight * scale))
                };
                foreach (var countdown in new[] { "03:12", "11:11", "88:88", "1440:00", "3:12 PM", "11:11 AM", "12:59 PM" })
                {
                    idle.TimerRunning = !countdown.Contains(' ');
                    idle.ClockText = countdown;
                    using var bitmap = idle.RenderBitmap();
                    var ink = new List<Point>(); var solid = 0;
                    for (var y = 0; y < bitmap.Height; y++)
                    for (var x = 0; x < bitmap.Width; x++)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        if (pixel.R > 200 && pixel.G > 200 && pixel.B > 200)
                        {
                            if (pixel.A != 255) return false;
                            ink.Add(new(x, y));
                            if (pixel.R >= 240) solid++;
                        }
                    }
                    bitmap.Save(Path.Combine(shapeDirectory, $"{shape}-timer-{dpi}-{countdown.Replace(":", "-")}.png"));
                    if (ink.Count == 0 || solid < 15 * scale * scale) return false;
                    var inkHeight = ink.Max(p => p.Y) - ink.Min(p => p.Y) + 1;
                    if (inkHeight < 8 * scale || ink.Min(p => p.Y) < scale || ink.Max(p => p.Y) >= bitmap.Height - scale
                        || ink.Min(p => p.X) < 12 * scale || ink.Max(p => p.X) >= bitmap.Width - 12 * scale) return false;
                }
                return !idle.Visible;
            });
        }
        File.WriteAllText(Path.Combine(shapeDirectory, "alpha.json"), JsonSerializer.Serialize(shapes, new JsonSerializerOptions { WriteIndented = true }));
        var path = Path.Combine(AppContext.BaseDirectory, "self-test.json"); File.WriteAllText(path, JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
        Environment.ExitCode = checks.Any(x => JsonSerializer.Serialize(x).Contains("\"passed\":false")) ? 1 : 0;
    }
}
