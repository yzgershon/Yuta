using Yuta.Services;
using System.Drawing.Imaging;

namespace Yuta;

internal static class NotificationTests
{
    internal static void Run(Action<string, Func<bool>> check)
    {
        var now = DateTimeOffset.Parse("2026-11-04T12:00:00Z");
        NotificationPreview Item(uint id, int seconds = 0) => new(id, "Messages", "Study group", "See you at four.", now.AddSeconds(seconds));
        check("Notification previews ignore startup history and consume hidden alerts without replay", () =>
        {
            var policy = new NotificationPreviewPolicy();
            if (policy.Observe([Item(1)], now, true) is not null) return false;
            if (policy.Observe([Item(1), Item(2, 1)], now.AddSeconds(1), false) is not null) return false;
            if (policy.Observe([Item(1), Item(2, 1)], now.AddSeconds(2), true) is not null) return false;
            return policy.Observe([Item(3, 3)], now.AddSeconds(3), true)?.Id == 3;
        });
        check("Burst notifications select the newest, enforce a quiet gap and never replay duplicates", () =>
        {
            var policy = new NotificationPreviewPolicy(); policy.Observe([], now, true);
            if (policy.Observe([Item(1, 1), Item(2, 2)], now.AddSeconds(2), true)?.Id != 2) return false;
            if (policy.Observe([Item(3, 3)], now.AddSeconds(3), true) is not null) return false;
            if (policy.Observe([Item(3, 3)], now.AddSeconds(10), true) is not null) return false;
            return policy.Observe([Item(4, 11)], now.AddSeconds(11), true)?.Id == 4;
        });
        check("Permission reconnect re-baselines notifications and stale content is not shown", () =>
        {
            var policy = new NotificationPreviewPolicy(); policy.Observe([], now, true); policy.Reset();
            if (policy.Observe([Item(1)], now, true) is not null) return false;
            return policy.Observe([Item(2, -50)], now, true) is null
                && NotificationPreviewPolicy.Clean("\u202ehello\nworld", 6) == "hellow";
        });
        check("Native notification previews fit every DPI, dismiss, expire and remain black while flattened", () =>
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "notification-renders"); Directory.CreateDirectory(directory);
            foreach (var dpi in new[] {96,120,144,192})
            {
                var scale = dpi / 96f;
                using var pill = new IdleNotchWindow();
                pill.SetNotification(Item(1), 1000);
                pill.Configure(new(-12000,-12000,(int)(320*scale),(int)(64*scale)), scale, true, "12:00", false);
                using (var image = pill.RenderBitmap()) image.Save(Path.Combine(directory,$"preview-{dpi}.png"),ImageFormat.Png);
                if (pill.WidthFor(true) != 320 || pill.HeightFor() != 64 || pill.ExpireNotification(6499)) return false;
                pill.Flattened = true;
                pill.Configure(new(-12000,-12000,(int)(176*scale),(int)(4*scale)),scale,true,"12:00",false);
                using var flat = pill.RenderBitmap();
                for (var y=0;y<flat.Height;y++) for(var x=0;x<flat.Width;x++)
                {var pixel=flat.GetPixel(x,y);if(pixel.A>0&&(pixel.R!=0||pixel.G!=0||pixel.B!=0))return false;}
                if (!pill.ExpireNotification(6500) || pill.NotificationVisible || pill.HeightFor()!=NotchGeometry.IdleHeight) return false;
            }
            return true;
        });
    }
}
