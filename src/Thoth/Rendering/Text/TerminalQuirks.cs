namespace Thoth.Rendering.Text;

public static class TerminalQuirks
{
    static readonly Dictionary<string, byte> EmojiPresentationOverrides =
        new(StringComparer.Ordinal)
        {
            ["❤️"] = 2
        };

    public static Dictionary<string, byte> For(string? termProgram) =>
        (termProgram ?? string.Empty).ToLowerInvariant() switch
        {
            "iterm.app" => Clone(EmojiPresentationOverrides),
            "apple_terminal" => Clone(EmojiPresentationOverrides),
            "vscode" => Clone(EmojiPresentationOverrides),
            var _ => []
        };

    static Dictionary<string, byte> Clone(Dictionary<string, byte> source) =>
        new(source, StringComparer.Ordinal);
}
