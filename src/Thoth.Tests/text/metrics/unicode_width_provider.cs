using Shouldly;
using Thoth.Rendering.Text;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_metrics;

public class unicode_width_provider : IAsyncLifetime
{
    UnicodeWidthProvider _provider = null!;

    public Task InitializeAsync()
    {
        _provider = new();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void empty_cluster_returns_0()
    {
        _provider.GetWidth(ReadOnlySpan<char>.Empty).ShouldBe((byte)0);
    }

    [Fact]
    public void ascii_char_returns_1()
    {
        _provider.GetWidth("A".AsSpan()).ShouldBe((byte)1);
    }

    [Fact]
    public void cjk_char_returns_2()
    {
        _provider.GetWidth("中".AsSpan()).ShouldBe((byte)2);
    }

    [Fact]
    public void emoji_returns_2()
    {
        _provider.GetWidth("🚀".AsSpan()).ShouldBe((byte)2);
    }

    [Fact]
    public void combining_sequence_uses_base_width()
    {
        // e + combining acute - width is based on 'e' = 1
        _provider.GetWidth("e\u0301".AsSpan()).ShouldBe((byte)1);
    }

    [Fact]
    public void invalid_sequence_returns_1()
    {
        // Single high surrogate (invalid)
        _provider.GetWidth("\uD83D".AsSpan()).ShouldBe((byte)1);
    }
}