using Shouldly;
using Thoth.Terminal.Raw.Ingress;

namespace Comptatata.Tests.App.Cli.input.coalescing;

public class screen_op_coalescing_policies : IAsyncLifetime
{
    readonly ScreenOpBatchCoalescer _sut = new();

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void last_keeps_latest_for_same_target_and_kind()
    {
        var buffer = new List<ScreenOp>
                     {
                         op(ScreenOpCoalesce.Last, a: 1, b: 10),
                         op(ScreenOpCoalesce.Last, a: 2, b: 20)
                     };

        _sut.TryMerge(buffer, op(ScreenOpCoalesce.Last, a: 3, b: 30)).ShouldBeTrue();

        buffer.Count.ShouldBe(2);
        buffer[1].ReservedA.ShouldBe(3);
        buffer[1].ReservedB.ShouldBe(30);
    }

    [Fact]
    public void sum_a_aggregates_reserved_a()
    {
        var buffer = new List<ScreenOp>
                     {
                         op(ScreenOpCoalesce.SumA, a: 5),
                         op(ScreenOpCoalesce.SumA, a: 7)
                     };

        _sut.TryMerge(buffer, op(ScreenOpCoalesce.SumA, a: 11)).ShouldBeTrue();

        buffer[1].ReservedA.ShouldBe(18);
    }

    [Fact]
    public void sum_a_clamps_on_overflow()
    {
        var buffer = new List<ScreenOp>
                     {
                         op(ScreenOpCoalesce.SumA, a: 0),
                         op(ScreenOpCoalesce.SumA, a: int.MaxValue)
                     };

        _sut.TryMerge(buffer, op(ScreenOpCoalesce.SumA, a: 1)).ShouldBeTrue();

        buffer[1].ReservedA.ShouldBe(int.MaxValue);
    }

    [Fact]
    public void sum_b_aggregates_reserved_b()
    {
        var buffer = new List<ScreenOp>
                     {
                         op(ScreenOpCoalesce.SumB, b: -2),
                         op(ScreenOpCoalesce.SumB, b: 3)
                     };

        _sut.TryMerge(buffer, op(ScreenOpCoalesce.SumB, b: -1)).ShouldBeTrue();

        buffer[1].ReservedB.ShouldBe(2);
    }

    [Fact]
    public void sum_b_clamps_on_overflow()
    {
        var buffer = new List<ScreenOp>
                     {
                         op(ScreenOpCoalesce.SumB, b: 0),
                         op(ScreenOpCoalesce.SumB, b: int.MinValue)
                     };

        _sut.TryMerge(buffer, op(ScreenOpCoalesce.SumB, b: -1)).ShouldBeTrue();

        buffer[1].ReservedB.ShouldBe(int.MinValue);
    }

    [Fact]
    public void sum_ab_aggregates_reserved_a_and_reserved_b()
    {
        var buffer = new List<ScreenOp>
                     {
                         op(ScreenOpCoalesce.SumAB, a: 1, b: 2),
                         op(ScreenOpCoalesce.SumAB, a: 3, b: 4)
                     };

        _sut.TryMerge(buffer, op(ScreenOpCoalesce.SumAB, a: 5, b: 6)).ShouldBeTrue();

        buffer[1].ReservedA.ShouldBe(8);
        buffer[1].ReservedB.ShouldBe(10);
    }

    [Fact]
    public void sum_ab_clamps_each_field_independently_on_overflow()
    {
        var buffer = new List<ScreenOp>
                     {
                         op(ScreenOpCoalesce.SumAB, a: 0, b: 0),
                         op(ScreenOpCoalesce.SumAB, a: int.MaxValue, b: int.MinValue)
                     };

        _sut.TryMerge(buffer, op(ScreenOpCoalesce.SumAB, a: 1, b: -1)).ShouldBeTrue();

        buffer[1].ReservedA.ShouldBe(int.MaxValue);
        buffer[1].ReservedB.ShouldBe(int.MinValue);
    }

    [Fact]
    public void none_never_merges()
    {
        var buffer = new List<ScreenOp> { op(ScreenOpCoalesce.None), op(ScreenOpCoalesce.None) };

        _sut.TryMerge(buffer, op(ScreenOpCoalesce.None)).ShouldBeFalse();
        buffer.Count.ShouldBe(2);
    }

    static ScreenOp op(ScreenOpCoalesce coalescence, int a = 0, int b = 0)
    {
        return new(ScreenOpTarget.Editor, ScreenOpKind.Key, coalescence, a, b);
    }
}
