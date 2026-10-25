using System.Drawing.Imaging;
using Yuta.Services;

namespace Yuta;

internal static class ActivityTests
{
    internal static MediaSnapshot Music()
    {
        // A generated fixture cover; no listening history or external artwork.
        using var cover = new Bitmap(64, 64);
        using (var g = Graphics.FromImage(cover))
        {
            g.Clear(Color.FromArgb(79, 61, 116));
            using var brush = new SolidBrush(Color.FromArgb(221, 186, 221));
            g.FillEllipse(brush, 10, 7, 45, 45);
            using var stripe = new SolidBrush(Color.FromArgb(111, 130, 167));
            g.FillRectangle(stripe, 0, 39, 64, 25);
        }
        using var data = new MemoryStream(); cover.Save(data, ImageFormat.Png);
        return new() { Available = true, Playing = true, Title = "Rover (feat. Lil Tecca)", Artist = "Preview artist", SourceName = "Music", Artwork = "data:image/png;base64," + Convert.ToBase64String(data.ToArray()) };
    }

    internal static void Run(Action<string, Func<bool>> check)
    {
        check("Idle titles omit featured credits without altering original metadata or remix labels", () =>
        {
            var original = Music();
            return ShortTrackTitle.From(original.Title) == "Rover" && original.Title == "Rover (feat. Lil Tecca)"
                && ShortTrackTitle.From("Song [FEAT. Guest]") == "Song"
                && ShortTrackTitle.From("Song - ft. Guest") == "Song"
                && ShortTrackTitle.From("Song (Live)") == "Song (Live)"
                && ShortTrackTitle.From("Song (Remix)") == "Song (Remix)"
                && ShortTrackTitle.From(null) == "Music";
        });
        check("Power notices describe real transitions, never startup or percentage ticks", () =>
        {
            var status = new ActivityStatusTracker();
            return status.Power(new(false, false, 47)) is null
                && status.Power(new(false, false, 46)) is null
                && status.Power(new(true, true, 46)) is { Kind: "power", Title: "Charging", Percent: 46 }
                && status.Power(new(true, true, 47)) is null
                && status.Power(new(true, false, 100)) is { Title: "Fully charged", Percent: 100 }
                && status.Power(new(false, false, 100)) is { Title: "On battery" }
                && status.Power(new(true, false, null)) is null;
        });
        check("Audio notices require an output change and never invent device battery", () =>
        {
            var status = new ActivityStatusTracker();
            return status.Audio("speaker", "Speakers") is null && status.Audio("speaker", "Speakers") is null
                && status.Audio("headset", "My headphones") is { Kind: "audio", Title: "My headphones", Percent: null }
                && status.Audio(null, "") is { Title: "Audio disconnected" } && status.Audio(null, "") is null;
        });
        check("Notices expire back to active music or the idle clock", () =>
        {
            using var idle = new IdleNotchWindow();
            idle.SetNotice(new("power", "Charging", 48), 1000);
            if (!idle.HasActivity || idle.ExpireNotice(5499) || !idle.ExpireNotice(5500) || idle.HasActivity) return false;
            idle.SetMedia(Music()); idle.SetNotice(new("audio", "My headphones"), 6000);
            return idle.ExpireNotice(10500) && idle.MusicPlaying && idle.HasActivity && !idle.ActivityAnimating;
        });
        var folder = Path.Combine(AppContext.BaseDirectory, "activity-renders"); Directory.CreateDirectory(folder);
        foreach (var dpi in new[] { 96, 120, 144, 192 })
        foreach (var attached in new[] { true, false })
        {
            var shape = attached ? "notch" : "pill";
            check($"{shape} activity stays crisp and within its silhouette at {dpi} DPI", () =>
            {
                var scale = dpi / 96f;
                using var idle = new IdleNotchWindow();
                idle.Configure(new(-12000, -12000, (int)Math.Round(NotchGeometry.ActivityWidth * scale), (int)Math.Round(NotchGeometry.IdleHeight * scale)), scale, attached, "03:12", false);
                idle.SetMedia(Music());
                foreach (var view in new[] { "music", "timer-music", "voice", "charging", "headphones" })
                {
                    var envelope = new AudioEnvelope();
                    float[] levels = [], peaks = [.1f, .12f, .11f, .5f, .8f, .4f, .15f, .08f, .3f, .6f, .25f];
                    for (var i = 0; i < 80; i++) levels = envelope.Sample(peaks[i % peaks.Length]);
                    idle.SetAudioLevels(new(levels, view == "voice", view == "voice" ? "Discord" : "", true));
                    idle.TimerRunning = view == "timer-music";
                    if (view == "charging") idle.SetNotice(new("power", "Charging", 48), 0);
                    if (view == "headphones") idle.SetNotice(new("audio", "My headphones"), 0);
                    idle.Configure(new(-12000, -12000, (int)Math.Round(NotchGeometry.ActivityWidth * scale), (int)Math.Round(NotchGeometry.IdleHeight * scale)), scale, attached, "03:12", view == "timer-music");
                    using var bitmap = idle.RenderBitmap();
                    var ink = new List<Point>();
                    for (var y = 0; y < bitmap.Height; y++)
                    for (var x = 0; x < bitmap.Width; x++)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        if (view == "music" ? pixel.B > 90 : pixel.R > 220 && pixel.G > 220 && pixel.B > 220)
                        {
                            if (pixel.A != 255) return false;
                            ink.Add(new(x, y));
                        }
                    }
                    if (view == "music" && !ink.Any(p => p.X > 60 * scale && p.X < bitmap.Width - 60 * scale)) return false;
                    if (ink.Count < 20 * scale * scale || ink.Min(p => p.Y) < scale || ink.Max(p => p.Y) >= bitmap.Height - scale
                        || ink.Min(p => p.X) < 18 * scale || ink.Max(p => p.X) > bitmap.Width - 15 * scale
                        || ink.Max(p => p.Y) - ink.Min(p => p.Y) + 1 < 7 * scale) return false;
                    bitmap.Save(Path.Combine(folder, $"{shape}-{view}-{dpi}.png"));
                }
                return !idle.Visible && !idle.ActivityAnimating;
            });
        }
    }
}
