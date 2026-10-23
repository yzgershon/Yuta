using System.Text.RegularExpressions;

namespace Yuta;

internal static partial class ShortTrackTitle
{
    // Keep version/remix labels and the original media metadata. Only featured
    // artist credits are omitted from the small idle title.
    internal static string From(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "Music";
        var concise = FeaturedCredit().Replace(title, "");
        concise = TrailingCredit().Replace(concise, "").Trim();
        return concise.Length == 0 ? title.Trim() : concise;
    }

    [GeneratedRegex(@"\s*[\(\[]\s*(?:feat\.?|ft\.?|featuring)\s+[^\)\]]*[\)\]]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FeaturedCredit();
    [GeneratedRegex(@"\s+(?:[-–—]\s*)?(?:feat\.?|ft\.?|featuring)\s+.+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TrailingCredit();
}
