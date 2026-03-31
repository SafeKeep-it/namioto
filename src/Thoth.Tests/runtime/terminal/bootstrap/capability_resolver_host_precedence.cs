using Shouldly;
using Thoth.Terminal;
using Thoth.Terminal.Bootstrap;

namespace Comptatata.Tests.App.Cli.UI.Thoth.terminal.bootstrap;

public sealed class capability_resolver_host_precedence
{
    [Theory]
    [InlineData(TerminalHostKind.ITerm2, "iterm2-macos-truecolor-v0", "iterm2")]
    [InlineData(TerminalHostKind.Kitty, "kitty-macos-truecolor-v0", "kitty")]
    [InlineData(TerminalHostKind.VSCode, "vscode-macos-truecolor-v0", "vscode")]
    public void forced_truecolor_hosts_select_expected_profile_and_width_profile(TerminalHostKind host,
                                                                                 string expectedProfile,
                                                                                 string expectedWidthProfile)
    {
        var env = host_environment(host, wantsTrueColor: false);

        var capabilities = TerminalCapabilityResolver.Resolve(env, TerminalKind.Console);

        capabilities.Profile.ShouldBe(expectedProfile);
        capabilities.WidthProfile.ShouldBe(expectedWidthProfile);
        capabilities.SupportsTrueColor.ShouldBeTrue();
        capabilities.MaxColors.ShouldBe(16_777_216);
    }

    [Theory]
    [InlineData(TerminalHostKind.AppleTerminal, true, true, 16_777_216)]
    [InlineData(TerminalHostKind.AppleTerminal, false, false, 256)]
    [InlineData(TerminalHostKind.Tmux, true, true, 16_777_216)]
    [InlineData(TerminalHostKind.Tmux, false, false, 256)]
    public void passthrough_hosts_follow_wants_truecolor(TerminalHostKind host,
                                                          bool wantsTrueColor,
                                                          bool expectedTrueColor,
                                                          int expectedMaxColors)
    {
        var env = host_environment(host, wantsTrueColor: wantsTrueColor);

        var capabilities = TerminalCapabilityResolver.Resolve(env, TerminalKind.Console);

        capabilities.SupportsTrueColor.ShouldBe(expectedTrueColor);
        capabilities.MaxColors.ShouldBe(expectedMaxColors);
    }

    [Fact]
    public void unknown_host_preserves_fallback_profile_and_width_profile()
    {
        var env = host_environment(TerminalHostKind.Unknown, wantsTrueColor: false);

        var capabilities = TerminalCapabilityResolver.Resolve(env, TerminalKind.Console);

        capabilities.Profile.ShouldBe("unknown-macos-v0");
        capabilities.WidthProfile.ShouldBe("unicode-default");
        capabilities.SupportsTrueColor.ShouldBeFalse();
    }

    [Fact]
    public void conflicting_markers_prioritize_tmux_then_screen_then_dedicated_markers()
    {
        HostEnvironmentProbe.DetectTerminalHost(termProgram: "iTerm.app",
                                                term: "xterm-kitty",
                                                isInsideTmux: true,
                                                isInsideScreen: true,
                                                hasWindowsTerminalSession: true,
                                                hasKittyWindowId: true)
          .ShouldBe(TerminalHostKind.Tmux);

        HostEnvironmentProbe.DetectTerminalHost(termProgram: "iTerm.app",
                                                term: "xterm-kitty",
                                                isInsideTmux: false,
                                                isInsideScreen: true,
                                                hasWindowsTerminalSession: true,
                                                hasKittyWindowId: true)
          .ShouldBe(TerminalHostKind.Screen);

        HostEnvironmentProbe.DetectTerminalHost(termProgram: "Apple_Terminal",
                                                term: "xterm-kitty",
                                                isInsideTmux: false,
                                                isInsideScreen: false,
                                                hasWindowsTerminalSession: false,
                                                hasKittyWindowId: true)
          .ShouldBe(TerminalHostKind.Kitty);
    }

    [Fact]
    public void null_markers_resolve_to_unknown_host_fallback()
    {
        var host = HostEnvironmentProbe.DetectTerminalHost(termProgram: null,
                                                           term: null,
                                                           isInsideTmux: false,
                                                           isInsideScreen: false,
                                                           hasWindowsTerminalSession: false,
                                                           hasKittyWindowId: false);

        host.ShouldBe(TerminalHostKind.Unknown);

        var env = host_environment(host, wantsTrueColor: false, termProgram: null, term: null);
        var capabilities = TerminalCapabilityResolver.Resolve(env, TerminalKind.Console);
        capabilities.Profile.ShouldBe("unknown-macos-v0");
        capabilities.WidthProfile.ShouldBe("unicode-default");
    }

    static HostEnvironmentInfo host_environment(TerminalHostKind host,
                                                bool wantsTrueColor,
                                                string? termProgram = "iTerm.app",
                                                string? term = "xterm-256color")
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
                                       TermProgram: termProgram,
                                       TermProgramVersion: "1",
                                       Term: term,
                                       ColorTerm: wantsTrueColor ? "truecolor" : null,
                                       TerminalHost: host);
    }
}
