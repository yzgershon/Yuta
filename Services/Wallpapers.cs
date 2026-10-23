using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Yuta.Services;
public sealed record WallpaperItem(string Id, string Name, string Path, string Image);
public sealed class Wallpapers(Store store)
{
    public List<WallpaperItem> Items { get; } = new();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SystemParametersInfo(uint action, uint param, string value, uint flags);
    public Task ScanAsync(string? folder = null) => Task.Run(() =>
    {
        folder ??= store.Data["wallpaperFolder"]?.GetValue<string>() ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Web", "Wallpaper");
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("Choose an existing pictures folder.");
        var list = new List<WallpaperItem>();
        var recent = store.Data["wallpaperRecent"]?.AsArray().Select(x => x!.GetValue<string>()).Where(File.Exists) ?? Enumerable.Empty<string>();
        var files = recent.Concat(Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 4 }).Where(x => new[] { ".png", ".jpg", ".jpeg", ".bmp" }.Contains(Path.GetExtension(x).ToLowerInvariant()))).Distinct().Take(100);
        foreach (var file in files)
        {
            try
            {
                using var image = Image.FromFile(file); using var thumb = new Bitmap(400, 240); using var graphics = Graphics.FromImage(thumb);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                var scale = Math.Max(400f / image.Width, 240f / image.Height); var w = image.Width * scale; var h = image.Height * scale;
                graphics.DrawImage(image, (400 - w) / 2, (240 - h) / 2, w, h);
                using var stream = new MemoryStream(); thumb.Save(stream, ImageFormat.Jpeg);
                list.Add(new(file, Path.GetFileNameWithoutExtension(file), file, "data:image/jpeg;base64," + Convert.ToBase64String(stream.ToArray())));
            } catch { }
        }
        Items.Clear(); Items.AddRange(list);
    });
    public void Apply(string id)
    {
        var item = Items.Find(x => x.Id == id) ?? throw new ArgumentException("Select a wallpaper from this collection.");
        if (!SystemParametersInfo(0x0014, 0, item.Path, 3)) throw new InvalidOperationException("Windows could not apply this wallpaper.");
        store.Data["activeWallpaper"] = id;
        var history = store.Data["wallpaperRecent"]?.AsArray().Select(x => x!.GetValue<string>()).Where(x => x != id).Take(11).ToList() ?? new();
        history.Insert(0, id); store.Data["wallpaperRecent"] = System.Text.Json.JsonSerializer.SerializeToNode(history); store.Save();
    }
}
