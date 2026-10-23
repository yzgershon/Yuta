using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Yuta.Services;

public sealed record LaunchItem(string Id, string Name, string Target, string Kind);
public sealed class Launcher(Store store)
{
    public List<LaunchItem> Apps { get; } = new();
    public async Task ScanAsync()
    {
        var found = await Task.Run(() => {
            var list = new List<LaunchItem>();
            foreach (var folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu) })
                try { foreach (var path in Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories).Take(800)) list.Add(new(path, Path.GetFileNameWithoutExtension(path), path, "Application")); } catch { }
            return list;
        });
        Apps.Clear(); Apps.AddRange(found.DistinctBy(x => x.Name).OrderBy(x => x.Name));
        try
        {
            var p = new Process { StartInfo = new("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
            p.StartInfo.ArgumentList.Add("-NoProfile"); p.StartInfo.ArgumentList.Add("-NonInteractive"); p.StartInfo.ArgumentList.Add("-Command"); p.StartInfo.ArgumentList.Add("Get-StartApps | Select-Object Name,AppID | ConvertTo-Json -Compress");
            p.Start(); var output = p.StandardOutput.ReadToEndAsync(); var errors = p.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try { await p.WaitForExitAsync(timeout.Token); } catch { p.Kill(true); throw; }
            var data = JsonNode.Parse(await output); await errors; p.Dispose();
            var rows = data is JsonArray a ? a : new JsonArray(data);
            foreach (var row in rows)
            {
                var name = row?["Name"]?.GetValue<string>(); var id = row?["AppID"]?.GetValue<string>();
                if (name != null && id != null && id.Contains('!') && !Apps.Any(x => x.Name == name)) Apps.Add(new("uwp:" + id, name, "uwp:" + id, "Application"));
            }
        } catch { /* Start menu shortcuts still work if UWP discovery is unavailable. */ }
    }
    public static bool ValidTarget(string target)
    {
        if (target.Length > 2048 || target.Any(char.IsControl)) return false;
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http") return true;
        if (Directory.Exists(target)) return true;
        return File.Exists(target) && new[] { ".exe", ".lnk", ".pdf", ".txt", ".md", ".docx", ".xlsx", ".pptx", ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(target).ToLowerInvariant());
    }
    public void Open(string target)
    {
        if (target.StartsWith("uwp:") && Apps.Any(x => x.Target == target))
        {
            var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = false }; info.ArgumentList.Add("shell:AppsFolder\\" + target[4..]); Process.Start(info); return;
        }
        if (!ValidTarget(target)) throw new ArgumentException("Choose an existing app, file or folder, or an http/https website. Shell commands are not supported.");
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        var recent = store.Data["recent"]!.AsArray();
        var old = recent.FirstOrDefault(x => x?.GetValue<string>() == target); if (old != null) recent.Remove(old);
        recent.Insert(0, JsonValue.Create(target)); while (recent.Count > 12) recent.RemoveAt(recent.Count - 1); store.Save();
    }
    public static void Settings(string page)
    {
        if (!new[] { "network-wifi", "bluetooth", "display", "sound", "notifications", "powersleep", "personalization-background" }.Contains(page)) throw new ArgumentException("Unknown settings page.");
        Process.Start(new ProcessStartInfo("ms-settings:" + page) { UseShellExecute = true });
    }
}
