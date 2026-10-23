namespace Yuta;

internal static class AccentPalette
{
    public const string DefaultCustom = "#9b80ff";
    public static string Normalize(string? id) => id is "ice" or "rose" or "amber" or "porcelain" or "custom" ? id : "lavender";

    public static string NormalizeCustom(string? color) => color is { Length: 7 } && color[0] == '#' && color.AsSpan(1).ToString().All(Uri.IsHexDigit) ? color.ToLowerInvariant() : DefaultCustom;

    public static Color Resolve(string? id, string? custom = null) => Normalize(id) switch
    {
        "ice" => Color.FromArgb(172, 212, 245),
        "rose" => Color.FromArgb(228, 178, 194),
        "amber" => Color.FromArgb(223, 193, 142),
        "porcelain" => Color.FromArgb(216, 217, 211),
        "custom" => ColorTranslator.FromHtml(NormalizeCustom(custom)),
        _ => Color.FromArgb(194, 182, 236)
    };
}
