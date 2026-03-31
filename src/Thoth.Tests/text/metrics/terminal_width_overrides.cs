using Shouldly;
using Thoth.Rendering.Text;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_metrics;

public class terminal_width_overrides : IAsyncLifetime
{
    MockWidthProvider _inner = null!;
    TerminalWidthOverrides _provider = null!;

    public Task InitializeAsync()
    {
        _inner = new();
        var exceptions = new Dictionary<string, byte>
                         {
                             ["⚡"] = 1, // Override: lightning bolt treated as width 1
                             ["🚀"] = 1 // Override: rocket treated as width 1
                         };
        _provider = new(exceptions, _inner);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void returns_override_when_present()
    {
        _provider.GetWidth("⚡".AsSpan()).ShouldBe((byte)1);
        _provider.GetWidth("🚀".AsSpan()).ShouldBe((byte)1);
    }

    [Fact]
    public void delegates_to_inner_when_no_override()
    {
        _inner.WidthToReturn = 2;
        _provider.GetWidth("😀".AsSpan()).ShouldBe((byte)2);
        _inner.LastCluster.ShouldBe("😀");
    }

    [Fact]
    public void single_char_delegates_to_inner()
    {
        _inner.WidthToReturn = 1;
        _provider.GetWidth("A".AsSpan()).ShouldBe((byte)1);
        _inner.LastCluster.ShouldBe("A");
    }

    [Fact]
    public void empty_delegates_to_inner()
    {
        _inner.WidthToReturn = 0;
        _provider.GetWidth(ReadOnlySpan<char>.Empty).ShouldBe((byte)0);
    }

    class MockWidthProvider : IWidthProvider
    {
        public byte WidthToReturn { get; set; } = 1;
        public string? LastCluster { get; private set; }

        public byte GetWidth(ReadOnlySpan<char> cluster)
        {
            LastCluster = new(cluster);
            return WidthToReturn;
        }
    }
}