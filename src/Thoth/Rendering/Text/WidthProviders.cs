namespace Thoth.Rendering.Text;

public static class WidthProviders
{
    public static IWidthProvider Unicode() => new UnicodeWidthProvider();

    public static IWidthProvider ForProfile(string? widthProfile, IWidthProvider? inner = null)
    {
        inner ??= Unicode();
        return widthProfile switch
        {
            "iterm2" => ForTerminal("iTerm.app", inner),
            "apple-terminal" => ForTerminal("Apple_Terminal", inner),
            "vscode" => ForTerminal("vscode", inner),
            _ => inner
        };
    }

    public static IWidthProvider ForTerminal(string? termProgram, IWidthProvider? inner = null)
    {
        inner ??= Unicode();

        var exceptions = TerminalQuirks.For(termProgram);
        return exceptions.Count > 0 ? new TerminalWidthOverrides(exceptions, inner) : inner;
    }

    public static IWidthProvider Default() =>
        ForTerminal(Environment.GetEnvironmentVariable("TERM_PROGRAM"));
}
