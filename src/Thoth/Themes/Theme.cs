using System.Text.Json.Serialization;

namespace Thoth.Themes;

public record Theme([property: JsonPropertyName("name")] string Name,
                    [property: JsonPropertyName("id")] string Id,
                    [property: JsonPropertyName("description")]
                    string Description,
                    [property: JsonPropertyName("author")] string Author,
                    [property: JsonPropertyName("variant")]
                    string Variant,
                    [property: JsonPropertyName("version")]
                    int Version,
                    [property: JsonPropertyName("colors")] ThemeColors Colors)
{
    public ThemePalette BuildPalette(ThemePaletteOverrides? overrides = null) => ThemePalette.From(this, overrides);
}
