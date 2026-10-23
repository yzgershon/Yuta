using System.Drawing.Imaging;
using Yuta.Services;

namespace Yuta;

internal static class ObstructionTests
{
    internal static void Run(Action<string, Func<bool>> check, string root)
    {
        check("Smart flattening detects covered tabs and buttons but ignores adjacent controls and empty title bars", () =>
        {
            var notch = new Rectangle(800, 0, 176, 28);
            return NotchObstructionReader.IntersectsControl(notch, new(780, 8, 180, 34), true, false)
                && NotchObstructionReader.IntersectsControl(notch, new(850, 4, 28, 24), true, false)
                && !NotchObstructionReader.IntersectsControl(notch, new(600, 8, 180, 34), true, false)
                && !NotchObstructionReader.IntersectsControl(notch, new(0, 0, 1920, 40), false, false)
                && !NotchObstructionReader.IntersectsControl(notch, new(780, 8, 180, 34), true, true);
        });
        check("Obstruction geometry follows physical monitor bounds and rejects invalid provider rectangles", () =>
        {
            foreach (var scale in new[] { 1f, 1.25f, 1.5f, 2f })
            {
                var notch = new Rectangle(-1200, 200, (int)(176 * scale), (int)(28 * scale));
                if (!NotchObstructionReader.IntersectsControl(notch, new(-1200, 202, 100 * scale, 30 * scale), true, false)
                    || NotchObstructionReader.IntersectsControl(notch, new(0, 0, 300, 40), true, false)) return false;
            }
            return !NotchObstructionReader.IntersectsControl(new(0, 0, 176, 28), new(float.NaN, 0, 20, 20), true, false);
        });
        check("Flattening requires settled overlap and delayed clearing prevents flicker", () =>
        {
            var policy = new ObstructionPolicy();
            return !policy.Observe(new(true, true), 0) && !policy.Observe(new(true, true), 149)
                && policy.Observe(new(true, true), 300) && policy.Observe(new(true, false), 400)
                && policy.Observe(new(true, true), 600) && policy.Observe(new(true, false), 700)
                && policy.Observe(new(true, false), 1449) && !policy.Observe(new(true, false), 1450);
        });
        check("Unavailable providers expire safely and app changes clear previous overlap", () =>
        {
            var policy = new ObstructionPolicy(); policy.Observe(new(true, true), 0); policy.Observe(new(true, true), 300);
            if (!policy.Observe(default, 1000) || policy.Observe(default, 3301)) return false;
            policy.Observe(new(true, true), 4000); policy.Observe(new(true, true), 4300); policy.Reset();
            return !policy.Flattened && !policy.Observe(default, 4500);
        });
        check("A stalled accessibility read is shared only by the same window and geometry", () =>
        {
            using var release = new ManualResetEventSlim(); var count = 0;
            var reader = new NotchObstructionReader((_, _) => { Interlocked.Increment(ref count); release.Wait(3000); return new(true, true); });
            var first = reader.ReadAsync(new(1), new(0, 0, 176, 28));
            try
            {
                var same = reader.ReadAsync(new(1), new(0, 0, 176, 28));
                var otherWindow = reader.ReadAsync(new(2), new(0, 0, 176, 28));
                var otherBounds = reader.ReadAsync(new(1), new(0, 0, 224, 28));
                release.Set();
                return first == same && !otherWindow.Result.Known && !otherBounds.Result.Known && first.Result.Overlaps && count == 1;
            }
            finally { release.Set(); first.Wait(3500); }
        });
        check("Smart flattening can be disabled without losing unrelated preferences", () =>
        {
            var store = new Store(Path.Combine(root, "smart-flatten"));
            var settings = store.Data["settings"]!.DeepClone(); settings["smartFlatten"] = false;
            store.Update("settings", settings);
            return new Store(store.Root).Data["settings"]?["smartFlatten"]?.GetValue<bool>() == false;
        });
        check("Flat notch contains only black alpha pixels and hides artwork timer and notices at every DPI", () =>
        {
            var output = Path.Combine(AppContext.BaseDirectory, "obstruction-renders"); Directory.CreateDirectory(output);
            foreach (var dpi in new[] { 96, 120, 144, 192 })
            {
                var scale = dpi / 96f;
                using var idle = new IdleNotchWindow { Flattened = true };
                idle.Configure(new(-12000, -12000, (int)(176 * scale), (int)(NotchGeometry.FlatHeight * scale)), scale, true, "03:12", true);
                idle.SetMedia(ActivityTests.Music()); idle.SetNotice(new("power", "Charging", 65), 0);
                using var bitmap = idle.RenderBitmap(); bitmap.Save(Path.Combine(output, $"flat-{dpi}.png"), ImageFormat.Png);
                if (bitmap.Height != (int)(4 * scale) || bitmap.GetPixel(bitmap.Width / 2, 0).A != 255) return false;
                for (var y = 0; y < bitmap.Height; y++) for (var x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (pixel.A > 0 && (pixel.R != 0 || pixel.G != 0 || pixel.B != 0)) return false;
                }
            }
            return true;
        });
    }
}
