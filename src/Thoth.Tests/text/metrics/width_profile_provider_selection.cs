using Shouldly;
using Thoth.Rendering.Text;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_metrics;

public class width_profile_provider_selection
{
    [Fact]
    public void unknown_profile_falls_back_to_inner_provider()
    {
        var inner = new UnicodeWidthProvider();

        var provider = WidthProviders.ForProfile("unknown-profile", inner);

        ReferenceEquals(provider, inner).ShouldBeTrue();
    }

    [Fact]
    public void null_profile_falls_back_to_unicode_provider()
    {
        var provider = WidthProviders.ForProfile(null);

        provider.ShouldBeOfType<UnicodeWidthProvider>();
    }

    [Theory]
    [InlineData("iterm2")]
    [InlineData("apple-terminal")]
    [InlineData("vscode")]
    public void known_profiles_apply_terminal_quirk_overrides(string profile)
    {
        var inner = new UnicodeWidthProvider();

        var provider = WidthProviders.ForProfile(profile, inner);

        provider.ShouldBeOfType<TerminalWidthOverrides>();
        provider.GetWidth("❤️".AsSpan()).ShouldBe((byte)2);
        inner.GetWidth("❤️".AsSpan()).ShouldBe((byte)1);
    }
}
