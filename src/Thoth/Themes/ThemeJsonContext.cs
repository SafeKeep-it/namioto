using System.Text.Json.Serialization;

namespace Thoth.Themes;

[JsonSerializable(typeof(Theme))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class ThemeJsonContext : JsonSerializerContext { }