using System.Text.Json.Serialization;

namespace Thoth.Themes;

public record ThemeColors([property: JsonPropertyName("background")]
                          ThemeColor Background,
                          [property: JsonPropertyName("text")] ThemeColor Text,
                          [property: JsonPropertyName("dim")] ThemeColor Dim,
                          [property: JsonPropertyName("border")] ThemeColor Border,
                          [property: JsonPropertyName("accent")] ThemeColor Accent,
                          [property: JsonPropertyName("notify")] ThemeColor Notify,
                          [property: JsonPropertyName("success")]
                          ThemeColor Success,
                          [property: JsonPropertyName("warning")]
                          ThemeColor Warning,
                          [property: JsonPropertyName("error")] ThemeColor Error)
{
    public ThemeColor Foreground => Text;
}
