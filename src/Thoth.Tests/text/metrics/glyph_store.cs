using Shouldly;
using Thoth.Rendering.Grid;
using Thoth.Rendering.Text;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_metrics;

public class glyph_store : IAsyncLifetime
{
    GlyphStore _store = null!;
    MockWidthProvider _widthProvider = null!;

    public Task InitializeAsync()
    {
        _widthProvider = new();
        _store = new(_widthProvider);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void intern_returns_negated_index()
    {
        (var index, var _) = _store.Intern("👨‍👩‍👧".AsSpan());
        index.ShouldBeLessThan(0); // Negated
    }

    [Fact]
    public void intern_caches_width()
    {
        _widthProvider.WidthToReturn = 2;
        (var _, var width) = _store.Intern("🚀".AsSpan());
        width.ShouldBe((byte)2);
    }

    [Fact]
    public void intern_same_cluster_returns_same_index()
    {
        (var index1, var _) = _store.Intern("👨‍👩‍👧".AsSpan());
        (var index2, var _) = _store.Intern("👨‍👩‍👧".AsSpan());
        index1.ShouldBe(index2);
    }

    [Fact]
    public void intern_different_clusters_returns_different_indices()
    {
        (var index1, var _) = _store.Intern("🚀".AsSpan());
        (var index2, var _) = _store.Intern("😀".AsSpan());
        index1.ShouldNotBe(index2);
    }

    [Fact]
    public void get_retrieves_original_cluster()
    {
        (var index, var _) = _store.Intern("👨‍👩‍👧".AsSpan());
        _store.Get(index).ShouldBe("👨‍👩‍👧");
    }

    [Fact]
    public void get_width_retrieves_cached_width()
    {
        _widthProvider.WidthToReturn = 2;
        (var index, var _) = _store.Intern("🚀".AsSpan());
        _store.GetWidth(index).ShouldBe((byte)2);
    }

    [Fact]
    public void width_only_computed_once_per_cluster()
    {
        _widthProvider.WidthToReturn = 2;
        _store.Intern("🚀".AsSpan());
        _store.Intern("🚀".AsSpan());
        _store.Intern("🚀".AsSpan());
        _widthProvider.CallCount.ShouldBe(1);
    }

    [Fact]
    public void preload_adds_entries()
    {
        _widthProvider.WidthToReturn = 2;
        _store.Preload(["⚡", "🔥"]);

        // Should find preloaded entries
        (var _, var width1) = _store.Intern("⚡".AsSpan());
        (var _, var width2) = _store.Intern("🔥".AsSpan());

        width1.ShouldBe((byte)2);
        width2.ShouldBe((byte)2);
    }

    class MockWidthProvider : IWidthProvider
    {
        public byte WidthToReturn { get; set; } = 1;
        public int CallCount { get; private set; }

        public byte GetWidth(ReadOnlySpan<char> cluster)
        {
            CallCount++;
            return WidthToReturn;
        }
    }
}