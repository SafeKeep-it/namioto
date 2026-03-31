using System.Text.Json;

namespace Thoth.Themes;

public static class Themes
{
    static readonly List<Theme> _themes = [];
    public static Theme Current { get; private set; } = null!;

    public static IEnumerable<ThemeInfo> Variants =>
        _themes.Select(t => new ThemeInfo(t.Id, t.Name, t.Description, t.Author, t.Variant));

    public static void Load(string themeName, string? variant = null)
    {
        _themes.Clear();
        var assembly = typeof(Themes).Assembly;
        var prefix = $"Comptatata.App.Cli.UI.Themes.{themeName}.";
        var resources = assembly.GetManifestResourceNames()
                                .Where(r => r.StartsWith(prefix) && r.EndsWith(".theme.json"))
                                .OrderBy(r => r);

        foreach (var resource in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream == null) continue;
            var theme = JsonSerializer.Deserialize(stream, ThemeJsonContext.Default.Theme);
            if (theme != null) _themes.Add(theme);
        }

        if (_themes.Count == 0)
            throw new InvalidOperationException($"No themes found starting with {themeName}");

        Current = SelectVariant(variant);
    }

    public static void SwitchToVariant(string variantName)
    {
        var theme = _themes.FirstOrDefault(t => string.Equals(t.Variant, variantName, StringComparison.OrdinalIgnoreCase));
        if (theme != null) Current = theme;
    }

    static Theme SelectVariant(string? requestedVariant)
    {
        if (!string.IsNullOrWhiteSpace(requestedVariant))
            return _themes.FirstOrDefault(t => string.Equals(t.Variant, requestedVariant, StringComparison.OrdinalIgnoreCase))
                   ?? _themes[0];

        return _themes.FirstOrDefault(t => string.Equals(t.Variant, "dark", StringComparison.OrdinalIgnoreCase))
               ?? _themes[0];
    }

    public static void Reset()
    {
        Current = new("System",
                      "system",
                      "Neutral system default colors",
                      "System",
                      "system",
                      1,
                      new(new("#1e1e28", 235, "black", "Default background"),
                          new("#ffffff", 15, "white", "Default text"),
                          new("#808080", 8, "bright_black", "Default dim"),
                          new("#505064", 60, "bright_black", "Default border"),
                          new("#ffa500", 214, "yellow", "Default accent"),
                          new("#ffd166", 221, "bright_yellow", "Default notify"),
                          new("#00ff00", 10, "green", "Default success"),
                          new("#ffa500", 214, "yellow", "Default warning"),
                          new("#ff0000", 9, "red", "Default error")));
    }
}
