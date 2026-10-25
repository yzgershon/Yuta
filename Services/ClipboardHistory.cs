using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;

namespace Yuta.Services;

public sealed record ClipItem(string Id, string Type, string Text, string? Image, string Label, DateTimeOffset Copied, int Width = 0, int Height = 0, bool Pinned = false);

public sealed class ClipboardHistory
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string?> _thumbnails = new();
    private readonly string _path;
    private uint _sequence;
    public bool Enabled { get; set; }
    public List<ClipItem> Items { get; private set; } = new();
    public ClipboardHistory(string root)
    {
        _path = Path.Combine(root, "clipboard.bin");
        if (File.Exists(_path))
        {
            try { Items = JsonSerializer.Deserialize<List<ClipItem>>(ProtectedData.Unprotect(File.ReadAllBytes(_path), null, DataProtectionScope.CurrentUser), Store.JsonOptions) ?? new(); }
            catch { /* Unreadable encrypted data stays untouched until a successful capture. */ }
        }
        _sequence = Native.GetClipboardSequenceNumber();
    }
    // Keep original images for paste, but send small, cached previews to Home.
    // Decoding happens off the UI thread; repeated media updates never resend
    // full-resolution clipboard images to the WebView.
    public Task<List<ClipItem>> ReadUiItemsAsync()
    {
        var snapshot = Items.ToArray();
        var ids = snapshot.Select(x => x.Id).ToHashSet();
        foreach (var key in _thumbnails.Keys) if (!ids.Contains(key)) _thumbnails.TryRemove(key, out _);
        return Task.Run(() => snapshot.Select(item => item.Type != "image" ? item : item with
        { Image = _thumbnails.GetOrAdd(item.Id, _ => Thumbnail(item.Image)) }).ToList());
    }
    internal static string? Thumbnail(string? data)
    {
        try
        {
            if (data is null) return null;
            using var input = new MemoryStream(Convert.FromBase64String(data[(data.IndexOf(',') + 1)..]));
            using var original = Image.FromStream(input);
            var factor = Math.Min(1d, Math.Min(160d / original.Width, 128d / original.Height));
            using var preview = new Bitmap(Math.Max(1, (int)Math.Round(original.Width * factor)), Math.Max(1, (int)Math.Round(original.Height * factor)));
            using (var g = Graphics.FromImage(preview))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(original, new Rectangle(0, 0, preview.Width, preview.Height));
            }
            using var output = new MemoryStream(); preview.Save(output, ImageFormat.Png);
            return "data:image/png;base64," + Convert.ToBase64String(output.ToArray());
        }
        catch { return null; }
    }
    public bool Capture()
    {
        if (!Enabled || _sequence == Native.GetClipboardSequenceNumber()) return false;
        try
        {
            var data = Clipboard.GetDataObject();
            if (data is null || data.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing")) return false;
            if (data.GetData("CanIncludeInClipboardHistory") is MemoryStream allowed && allowed.ToArray().All(x => x == 0)) return false;
            ClipItem? item = null;
            if (Clipboard.ContainsImage())
            {
                using var image = Clipboard.GetImage();
                if (image is null || (long)image.Width * image.Height > 20_000_000) return false;
                using var memory = new MemoryStream(); image.Save(memory, ImageFormat.Png);
                if (memory.Length > 3_000_000) return false;
                item = new(Guid.NewGuid().ToString("N"), "image", "", "data:image/png;base64," + Convert.ToBase64String(memory.ToArray()), $"Image · {image.Width} × {image.Height}", DateTimeOffset.Now, image.Width, image.Height);
            }
            else if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text) || text.Length > 100_000) return false;
                var type = Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" ? "link" : "text";
                item = new(Guid.NewGuid().ToString("N"), type, text, null, text.Length > 100 ? text[..100] : text, DateTimeOffset.Now);
            }
            _sequence = Native.GetClipboardSequenceNumber();
            if (item is null) return false;
            var prior = Items.Find(x => x.Type == item.Type && x.Text == item.Text && x.Image == item.Image);
            if (prior is not null) { Items.Remove(prior); item = item with { Pinned = prior.Pinned }; }
            Items.Insert(0, item);
            Trim(); Save(); return true;
        }
        catch (ExternalException) { return false; }
    }
    private void Trim()
    {
        while (Items.Count > 200 || Items.Sum(x => (long)(x.Image?.Length ?? x.Text.Length)) > 48_000_000)
        {
            var old = Items.FindLastIndex(x => !x.Pinned);
            if (old < 0) old = Items.Count - 1;
            Items.RemoveAt(old);
        }
    }
    private void Save()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(Items, Store.JsonOptions);
        File.WriteAllBytes(_path + ".tmp", ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
        File.Move(_path + ".tmp", _path, true);
    }
    public void Copy(string id)
    {
        var item = Items.Find(x => x.Id == id) ?? throw new ArgumentException("That clipboard entry no longer exists.");
        if (item.Type == "image")
        {
            using var stream = new MemoryStream(Convert.FromBase64String(item.Image!.Split(',')[1]));
            using var image = Image.FromStream(stream); Clipboard.SetImage(image);
        }
        else Clipboard.SetText(item.Text);
        _sequence = Native.GetClipboardSequenceNumber();
    }
    public void Pin(string id)
    {
        var i = Items.FindIndex(x => x.Id == id);
        if (i >= 0) { Items[i] = Items[i] with { Pinned = !Items[i].Pinned }; Save(); }
    }
    public void Remove(IEnumerable<string> ids) { var set = ids.ToHashSet(); Items.RemoveAll(x => set.Contains(x.Id)); Save(); }
}
