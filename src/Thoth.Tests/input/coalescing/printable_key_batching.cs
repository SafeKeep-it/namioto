using Shouldly;
using Thoth.Terminal.Raw.Ingress;

namespace Comptatata.Tests.App.Cli.input.coalescing;

public class printable_key_batching : IAsyncLifetime
{
    readonly ScreenOpBatchCoalescer _sut = new();

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void merges_adjacent_printable_keys_into_single_text_op()
    {
        var buffer = new List<ScreenOp>
                     {
                         key('h', ConsoleKey.H),
                         key('e', ConsoleKey.E)
                     };

        var merged = _sut.TryMerge(buffer, key('y', ConsoleKey.Y));
        _sut.Flush(buffer);

        merged.ShouldBeTrue();
        buffer.Count.ShouldBe(2);
        buffer[1].Text.ShouldBe("ey");
    }

    [Fact]
    public void does_not_merge_ctrl_modified_key()
    {
        var buffer = new List<ScreenOp> { key('a', ConsoleKey.A) };

        var ctrlA = key('a', ConsoleKey.A, ConsoleModifiers.Control);
        var merged = _sut.TryMerge(buffer, ctrlA);

        merged.ShouldBeFalse();
        buffer.Count.ShouldBe(1);
        buffer[0].Text.ShouldBeNull();
    }

    [Fact]
    public void flushes_before_non_merge_key_and_starts_new_append_window()
    {
        var buffer = new List<ScreenOp>
                     {
                         key('h', ConsoleKey.H),
                         key('e', ConsoleKey.E)
                     };

        _sut.TryMerge(buffer, key('l', ConsoleKey.L)).ShouldBeTrue();
        _sut.Flush(buffer);
        buffer.Add(key('\r', ConsoleKey.Enter));
        _sut.TryMerge(buffer, key('o', ConsoleKey.O)).ShouldBeFalse();

        buffer[1].Text.ShouldBe("el");
        buffer[2].Text.ShouldBeNull();
    }

    [Fact]
    public void does_not_merge_escape_key()
    {
        var buffer = new List<ScreenOp> { key('a', ConsoleKey.A) };

        var escape = key('\0', ConsoleKey.Escape);
        var merged = _sut.TryMerge(buffer, escape);

        merged.ShouldBeFalse();
        buffer.Count.ShouldBe(1);
    }

    static ScreenOp key(char ch, ConsoleKey key, ConsoleModifiers modifiers = 0)
    {
        var packed = ((int)key & 0xFF) | (((int)modifiers & 0xFF) << 8);
        return new(Thoth.Terminal.Raw.Ingress.ScreenOpTarget.Editor,
                   ScreenOpKind.Key,
                   ScreenOpCoalesce.AppendText,
                   ch,
                   packed);
    }
}
