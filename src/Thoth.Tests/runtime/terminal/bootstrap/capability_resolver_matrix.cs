using Shouldly;
using Thoth.Terminal;
using Thoth.Terminal.Bootstrap;

namespace Comptatata.Tests.App.Cli.UI.Thoth.terminal.bootstrap;

public sealed class capability_resolver_matrix
{
    public static TheoryData<TerminalHostKind, string, string, bool> host_matrix => new()
    {
        { TerminalHostKind.Ghostty, "ghostty-macos-truecolor-v0", "ghostty", true },
        { TerminalHostKind.WezTerm, "wezterm-macos-truecolor-v0", "wezterm", true },
        { TerminalHostKind.ITerm2, "iterm2-macos-truecolor-v0", "iterm2", true },
        { TerminalHostKind.Kitty, "kitty-macos-truecolor-v0", "kitty", true },
        { TerminalHostKind.Alacritty, "alacritty-macos-truecolor-v0", "alacritty", true },
        { TerminalHostKind.Warp, "warp-macos-truecolor-v0", "warp", true },
        { TerminalHostKind.VSCode, "vscode-macos-truecolor-v0", "vscode", true },
        { TerminalHostKind.AppleTerminal, "apple-terminal-macos-v0", "apple-terminal", false },
        { TerminalHostKind.Xterm, "xterm-macos-v0", "xterm", false },
        { TerminalHostKind.Tmux, "tmux-macos-v0", "tmux", false },
        { TerminalHostKind.Screen, "screen-macos-v0", "screen", false },
        { TerminalHostKind.WindowsTerminal, "unknown-macos-v0", "unicode-default", false },
        { TerminalHostKind.Unknown, "unknown-macos-v0", "unicode-default", false },
    };

    public static TheoryData<TerminalHostKind, bool, bool, int> true_color_matrix => new()
    {
        { TerminalHostKind.ITerm2, false, true, 16_777_216 },
        { TerminalHostKind.ITerm2, true, true, 16_777_216 },
        { TerminalHostKind.AppleTerminal, false, false, 256 },
        { TerminalHostKind.AppleTerminal, true, true, 16_777_216 },
        { TerminalHostKind.Tmux, false, false, 256 },
        { TerminalHostKind.Tmux, true, true, 16_777_216 },
        { TerminalHostKind.Unknown, false, false, 256 },
        { TerminalHostKind.Unknown, true, true, 16_777_216 },
    };

    public static TheoryData<string?, string?, bool, bool, bool, bool, TerminalHostKind> precedence_matrix => new()
    {
        { "iTerm.app", "xterm-kitty", true, true, true, true, TerminalHostKind.Tmux },
        { "iTerm.app", "xterm-kitty", false, true, true, true, TerminalHostKind.Screen },
        { "Apple_Terminal", "xterm-kitty", false, false, false, true, TerminalHostKind.Kitty },
        { "iTerm.app", "xterm-256color", false, false, true, false, TerminalHostKind.WindowsTerminal },
        { "iTerm.app", "xterm-256color", false, false, false, false, TerminalHostKind.ITerm2 },
        { null, null, false, false, false, false, TerminalHostKind.Unknown },
    };

    [Theory]
    [MemberData(nameof(host_matrix))]
    public void host_kind_maps_to_expected_profile_and_width_profile(TerminalHostKind host,
                                                                      string expectedProfile,
                                                                      string expectedWidthProfile,
                                                                      bool forcedTrueColor)
    {
        var capabilities = TerminalCapabilityResolver.Resolve(host_environment(host, wantsTrueColor: false),
                                                              TerminalKind.Console);

        capabilities.Profile.ShouldBe(expectedProfile);
        capabilities.WidthProfile.ShouldBe(expectedWidthProfile);
        capabilities.SupportsTrueColor.ShouldBe(forcedTrueColor);
    }

    [Theory]
    [MemberData(nameof(true_color_matrix))]
    public void host_kind_and_wants_true_color_produce_expected_true_color(TerminalHostKind host,
                                                                            bool wantsTrueColor,
                                                                            bool expectedTrueColor,
                                                                            int expectedMaxColors)
    {
        var capabilities = TerminalCapabilityResolver.Resolve(host_environment(host, wantsTrueColor),
                                                              TerminalKind.Console);

        capabilities.SupportsTrueColor.ShouldBe(expectedTrueColor);
        capabilities.MaxColors.ShouldBe(expectedMaxColors);
    }

    [Theory]
    [MemberData(nameof(precedence_matrix))]
    public void host_detection_precedence_is_deterministic(string? termProgram,
                                                           string? term,
                                                           bool isInsideTmux,
                                                           bool isInsideScreen,
                                                           bool hasWindowsTerminalSession,
                                                           bool hasKittyWindowId,
                                                           TerminalHostKind expectedHost)
    {
        var resolvedHost = HostEnvironmentProbe.DetectTerminalHost(termProgram,
                                                                   term,
                                                                   isInsideTmux,
                                                                   isInsideScreen,
                                                                   hasWindowsTerminalSession,
                                                                   hasKittyWindowId);

        resolvedHost.ShouldBe(expectedHost);
    }

    static HostEnvironmentInfo host_environment(TerminalHostKind host, bool wantsTrueColor)
    {
        return new HostEnvironmentInfo(IsMacOs: true,
                                       IsLinux: false,
                                       IsWindows: false,
                                       IsInputRedirected: false,
                                       IsOutputRedirected: false,
                                       IsErrorRedirected: false,
                                       IsInteractive: true,
                                       IsInsideTmux: false,
                                       IsInsideScreen: false,
                                       WantsTrueColor: wantsTrueColor,
                                       TermProgram: "iTerm.app",
                                       TermProgramVersion: "1",
                                       Term: "xterm-256color",
                                       ColorTerm: wantsTrueColor ? "truecolor" : null,
                                       TerminalHost: host);
    }
}
