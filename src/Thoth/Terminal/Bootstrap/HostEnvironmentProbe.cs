namespace Thoth.Terminal.Bootstrap;

internal static class HostEnvironmentProbe
{
    public static HostEnvironmentInfo Probe(ITerminal terminal)
    {
        var isInputRedirected = Console.IsInputRedirected;
        var isOutputRedirected = Console.IsOutputRedirected;
        var isErrorRedirected = Console.IsErrorRedirected;
        var termProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM");
        var termProgramVersion = Environment.GetEnvironmentVariable("TERM_PROGRAM_VERSION");
        var term = Environment.GetEnvironmentVariable("TERM");
        var colorTerm = Environment.GetEnvironmentVariable("COLORTERM");
        var isInsideTmux = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TMUX"));
        var isInsideScreen = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STY"));

        var isInteractive = terminal.Kind != TerminalKind.Console ||
                            (!isInputRedirected && !isOutputRedirected);
        var wantsTrueColor = WantsTrueColor(colorTerm, term);
        var hasWindowsTerminalSession = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WT_SESSION"));
        var hasKittyWindowId = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KITTY_WINDOW_ID"));
        var terminalHost = DetectTerminalHost(termProgram,
                                              term,
                                              isInsideTmux,
                                              isInsideScreen,
                                              hasWindowsTerminalSession,
                                              hasKittyWindowId);

        return new HostEnvironmentInfo(IsMacOs: OperatingSystem.IsMacOS(),
                                       IsLinux: OperatingSystem.IsLinux(),
                                       IsWindows: OperatingSystem.IsWindows(),
                                       IsInputRedirected: isInputRedirected,
                                       IsOutputRedirected: isOutputRedirected,
                                       IsErrorRedirected: isErrorRedirected,
                                       IsInteractive: isInteractive,
                                       IsInsideTmux: isInsideTmux,
                                       IsInsideScreen: isInsideScreen,
                                       WantsTrueColor: wantsTrueColor,
                                       TermProgram: termProgram,
                                       TermProgramVersion: termProgramVersion,
                                       Term: term,
                                       ColorTerm: colorTerm,
                                       TerminalHost: terminalHost);
    }

    static bool WantsTrueColor(string? colorTerm, string? term)
    {
        if (!string.IsNullOrWhiteSpace(colorTerm) &&
            (colorTerm.Contains("truecolor", StringComparison.OrdinalIgnoreCase) ||
             colorTerm.Contains("24bit", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (string.IsNullOrWhiteSpace(term))
            return false;

        return term.Contains("direct", StringComparison.OrdinalIgnoreCase) ||
               term.Contains("truecolor", StringComparison.OrdinalIgnoreCase);
    }

    internal static TerminalHostKind DetectTerminalHost(string? termProgram,
                                                        string? term,
                                                        bool isInsideTmux,
                                                        bool isInsideScreen,
                                                        bool hasWindowsTerminalSession,
                                                        bool hasKittyWindowId)
    {
        // Deterministic precedence:
        // 1) Multiplexers (tmux, screen)
        // 2) Dedicated host markers (WT_SESSION, KITTY_WINDOW_ID)
        // 3) TERM_PROGRAM
        // 4) TERM fallback
        if (isInsideTmux)
            return TerminalHostKind.Tmux;

        if (isInsideScreen)
            return TerminalHostKind.Screen;

        if (hasWindowsTerminalSession)
            return TerminalHostKind.WindowsTerminal;

        if (hasKittyWindowId)
            return TerminalHostKind.Kitty;

        if (!string.IsNullOrWhiteSpace(termProgram))
        {
            if (termProgram.Equals("ghostty", StringComparison.OrdinalIgnoreCase)) return TerminalHostKind.Ghostty;
            if (termProgram.Equals("WezTerm", StringComparison.OrdinalIgnoreCase)) return TerminalHostKind.WezTerm;
            if (termProgram.Equals("iTerm.app", StringComparison.OrdinalIgnoreCase)) return TerminalHostKind.ITerm2;
            if (termProgram.Equals("Apple_Terminal", StringComparison.OrdinalIgnoreCase)) return TerminalHostKind.AppleTerminal;
            if (termProgram.Equals("vscode", StringComparison.OrdinalIgnoreCase)) return TerminalHostKind.VSCode;
            if (termProgram.Contains("warp", StringComparison.OrdinalIgnoreCase)) return TerminalHostKind.Warp;
        }

        if (string.IsNullOrWhiteSpace(term))
            return TerminalHostKind.Unknown;

        if (term.StartsWith("xterm-kitty", StringComparison.OrdinalIgnoreCase)) return TerminalHostKind.Kitty;
        if (term.Contains("alacritty", StringComparison.OrdinalIgnoreCase)) return TerminalHostKind.Alacritty;
        if (term.StartsWith("xterm", StringComparison.OrdinalIgnoreCase)) return TerminalHostKind.Xterm;

        return TerminalHostKind.Unknown;
    }
}
