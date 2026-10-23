using Yuta.Services;
using System.Text.Json;

namespace Yuta;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--self-test")) { SelfTest.Run(); return; }
        if (args.Contains("--network-test")) { SelfTest.NetworkAsync().GetAwaiter().GetResult(); return; }
        using var mutex = new Mutex(true, "Local\\Perch.PersonalMenu", out var first);
        // Retain the original mutex so an older running copy cannot share-write
        // the profile with Yuta. The internal profile folder is intentionally stable.
        if (!first && !args.Contains("--smoke-test") && !args.Contains("--performance-test"))
        {
            var hwnd = Native.FindWindow(null, "Yuta");
            if (hwnd == IntPtr.Zero) hwnd = Native.FindWindow(null, "Perch");
            if (hwnd != IntPtr.Zero) Native.PostMessage(hwnd, 0x802A, IntPtr.Zero, IntPtr.Zero);
            return;
        }
        Application.ThreadException += (_, e) => Log(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log(e.ExceptionObject as Exception ?? new Exception("Unhandled error"));
        Application.Run(new NotchForm(args.Contains("--smoke-test") || args.Contains("--performance-test")));
    }
    internal static void Log(Exception error)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Perch");
        Directory.CreateDirectory(dir); File.AppendAllText(Path.Combine(dir, "errors.log"), DateTimeOffset.Now + " " + error + Environment.NewLine);
    }
}
