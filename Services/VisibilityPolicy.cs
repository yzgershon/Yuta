using System.Text.Json;

namespace Yuta.Services;

internal sealed record HiddenApp(string Path, string Name);
internal sealed record VisibilityPreferences(HiddenApp[] Apps, string[] Sites, long SnoozedUntil)
{
    internal static VisibilityPreferences Empty => new([], [], 0);
}

internal sealed class VisibilityPolicy(Store store)
{
    internal VisibilityPreferences Read()
    {
        try
        {
            var saved = store.Data["visibility"]?.Deserialize<VisibilityPreferences>(Store.JsonOptions);
            return saved is null ? VisibilityPreferences.Empty : new(
                (saved.Apps ?? []).Where(app => app is not null && !string.IsNullOrWhiteSpace(app.Path) && !string.IsNullOrWhiteSpace(app.Name)).ToArray(),
                (saved.Sites ?? []).Where(site => !string.IsNullOrWhiteSpace(site)).ToArray(), saved.SnoozedUntil);
        }
        catch (JsonException) { return VisibilityPreferences.Empty; }
    }

    internal bool Suppressed(string? appPath, string? site, long now)
    {
        var saved = Read();
        return saved.SnoozedUntil > now
            || saved.Apps.Any(app => string.Equals(app.Path, appPath, StringComparison.OrdinalIgnoreCase))
            || site is not null && saved.Sites.Any(rule => MatchesSite(site, rule));
    }

    // Save a host, never a browsing path, query, page title or search term.
    internal static string? SiteHost(string? address)
    {
        if (string.IsNullOrWhiteSpace(address) || address.Length > 8192 || address.Any(char.IsWhiteSpace)) return null;
        address = address.Trim();
        if (System.Net.IPAddress.TryParse(address, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) address = "[" + address.Trim('[', ']') + "]";
        if (!address.Contains("://", StringComparison.Ordinal)) address = "https://" + address;
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length > 0) return null;
        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal)) host = host[4..];
        if (Uri.CheckHostName(host) == UriHostNameType.Unknown || host.Length == 0 || (!host.Contains('.') && host != "localhost" && uri.HostNameType != UriHostNameType.IPv6)) return null;
        return host;
    }

    internal static bool MatchesSite(string host, string rule) => host.Equals(rule, StringComparison.OrdinalIgnoreCase)
        || host.EndsWith("." + rule, StringComparison.OrdinalIgnoreCase);

    internal void ExcludeApp(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.IO.Path.IsPathFullyQualified(path)) throw new ArgumentException("The current app could not be identified.");
        var saved = Read();
        if (saved.Apps.Any(app => app.Path.Equals(path, StringComparison.OrdinalIgnoreCase))) return;
        Save(saved with { Apps = [.. saved.Apps, new(path, name)] });
    }
    internal void ExcludeSite(string host)
    {
        var normalized = SiteHost(host) ?? throw new ArgumentException("The current website could not be identified.");
        var saved = Read();
        if (saved.Sites.Contains(normalized, StringComparer.OrdinalIgnoreCase)) return;
        Save(saved with { Sites = [.. saved.Sites, normalized] });
    }
    internal void RemoveApp(string path) { var saved = Read(); Save(saved with { Apps = saved.Apps.Where(app => !app.Path.Equals(path, StringComparison.OrdinalIgnoreCase)).ToArray() }); }
    internal void RemoveSite(string host) { var saved = Read(); Save(saved with { Sites = saved.Sites.Where(site => !site.Equals(host, StringComparison.OrdinalIgnoreCase)).ToArray() }); }
    internal void Snooze(long now) => Save(Read() with { SnoozedUntil = now + (long)TimeSpan.FromHours(1).TotalMilliseconds });
    internal void Resume() => Save(Read() with { SnoozedUntil = 0 });
    private void Save(VisibilityPreferences value) => store.Update("visibility", JsonSerializer.SerializeToNode(value, Store.JsonOptions));
}
