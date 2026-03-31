namespace Thoth.Terminal.Bootstrap;

internal static class TerminalCapabilityResolver
{
    readonly record struct HostCapabilityRule(string Profile, string WidthProfile, bool ForcesTrueColor);

    public static TerminalCapabilities Resolve(HostEnvironmentInfo environment,
                                               TerminalKind terminalKind)
    {
        if (terminalKind is TerminalKind.Json or TerminalKind.Visual)
            return syntheticTemplate(terminalKind);

        if (!environment.IsInteractive)
            throw new InvalidOperationException("Thoth requires an interactive terminal session.");

        if (!environment.IsMacOs)
            throw new PlatformNotSupportedException("Thoth currently supports macOS terminal sessions only.");

        var hostRule = ResolveHostRule(environment.TerminalHost);
        var trueColor = hostRule.ForcesTrueColor ? true : environment.WantsTrueColor;

        return macTemplate(hostRule.Profile, hostRule.WidthProfile, trueColor);

        static TerminalCapabilities syntheticTemplate(TerminalKind terminalKind)
        {
            var profile = terminalKind == TerminalKind.Json
                ? "json-synthetic-v0"
                : "visual-synthetic-v0";

            return new TerminalCapabilities(Profile: profile,
                                            MaxColors: 16_777_216,
                                            SupportsTrueColor: true,
                                            SupportsAlternateScreen: false,
                                            SupportsMouse: true,
                                            SupportsBracketedPaste: true,
                                            SupportsClipboardOsc52: false,
                                            EnableRawMode: false,
                                            EnableAnsiOptions: false,
                                            WidthProfile: "unicode-default");
        }

        static TerminalCapabilities macTemplate(string profile, string widthProfile, bool trueColor)
        {
            return new TerminalCapabilities(Profile: profile,
                                            MaxColors: trueColor ? 16_777_216 : 256,
                                            SupportsTrueColor: trueColor,
                                            SupportsAlternateScreen: true,
                                            SupportsMouse: true,
                                            SupportsBracketedPaste: true,
                                            SupportsClipboardOsc52: true,
                                            EnableRawMode: true,
                                            EnableAnsiOptions: true,
                                            WidthProfile: widthProfile);
        }

        static HostCapabilityRule ResolveHostRule(TerminalHostKind host)
        {
            // Explicit precedence table driven by resolved host kind from HostEnvironmentProbe.
            // Unknown hosts intentionally preserve unicode-default fallback behavior.
            return host switch
            {
                TerminalHostKind.Ghostty => new("ghostty-macos-truecolor-v0", "ghostty", true),
                TerminalHostKind.WezTerm => new("wezterm-macos-truecolor-v0", "wezterm", true),
                TerminalHostKind.ITerm2 => new("iterm2-macos-truecolor-v0", "iterm2", true),
                TerminalHostKind.Kitty => new("kitty-macos-truecolor-v0", "kitty", true),
                TerminalHostKind.Alacritty => new("alacritty-macos-truecolor-v0", "alacritty", true),
                TerminalHostKind.Warp => new("warp-macos-truecolor-v0", "warp", true),
                TerminalHostKind.VSCode => new("vscode-macos-truecolor-v0", "vscode", true),
                TerminalHostKind.AppleTerminal => new("apple-terminal-macos-v0", "apple-terminal", false),
                TerminalHostKind.Xterm => new("xterm-macos-v0", "xterm", false),
                TerminalHostKind.Tmux => new("tmux-macos-v0", "tmux", false),
                TerminalHostKind.Screen => new("screen-macos-v0", "screen", false),
                _ => new("unknown-macos-v0", "unicode-default", false)
            };
        }
    }
}
