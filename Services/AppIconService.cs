using System.Collections.Concurrent;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Yuta.Services;

// Extract only requested icons, off the UI thread. No web requests or guessed brands.
internal sealed class AppIconService
{
    private readonly ConcurrentDictionary<string, Lazy<Task<string?>>> _cache = new();
    public async Task<Dictionary<string, string?>> ReadAsync(IEnumerable<LaunchItem> items)
    {
        var result = new Dictionary<string, string?>();
        foreach (var item in items.Take(48))
            result[item.Id] = await _cache.GetOrAdd(item.Target, target => new(() => Task.Run(() => Extract(target)))).Value;
        return result;
    }

    internal static string? Extract(string target)
    {
        IShellItemImageFactory? factory = null;
        IntPtr bitmap = IntPtr.Zero;
        try
        {
            if (!target.StartsWith("uwp:") && !File.Exists(target) && !Directory.Exists(target)) return null;
            var path = target.StartsWith("uwp:") ? "shell:AppsFolder\\" + target[4..] : target;
            var iid = typeof(IShellItemImageFactory).GUID;
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory));
            Marshal.ThrowExceptionForHR(factory.GetImage(new(64, 64), 0x5, out bitmap));
            // WPF preserves the alpha channel of the Shell's 32-bit bitmap.
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero,
                System.Windows.Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
            using var stream = new MemoryStream(); encoder.Save(stream);
            return "data:image/png;base64," + Convert.ToBase64String(stream.ToArray());
        }
        catch { return null; }
        finally
        {
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            if (factory is not null) Marshal.ReleaseComObject(factory);
        }
    }
    [StructLayout(LayoutKind.Sequential)] private readonly struct PixelSize(int width, int height) { public readonly int Width = width, Height = height; }
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory { [PreserveSig] int GetImage(PixelSize size, uint flags, out IntPtr bitmap); }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid iid, out IShellItemImageFactory factory);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
}
