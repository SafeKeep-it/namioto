using System.Text;
using Shouldly;
using Thoth.Terminal.Raw.Ingress;

namespace Comptatata.Tests.App.Cli.input.coalescing;

public class mouse_scroll_bursts : IAsyncLifetime
{
    readonly List<ScreenOp> _ops = [];
    InputReader? _reader;
    MockTerminal? _terminal;

    public Task InitializeAsync()
    {
        _terminal = new();
        _reader = new(_terminal, op => _ops.Add(op));
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task aggregates_scroll_burst_into_one_delta_event()
    {
        await ProcessInput(scroll(64) + scroll(64) + scroll(64));

        _ops.ShouldHaveSingleItem();
        _ops[0].Kind.ShouldBe(ScreenOpKind.MouseScroll);
        _ops[0].ReservedA.ShouldBe(10);
        (var y, var delta, var _) = InputReader.UnpackMouseB(_ops[0].ReservedB);
        y.ShouldBe(20);
        delta.ShouldBe(3);
    }

    [Fact]
    public async Task decodes_down_scroll_as_negative_delta()
    {
        await ProcessInput(scroll(65));

        _ops.ShouldHaveSingleItem();
        _ops[0].Kind.ShouldBe(ScreenOpKind.MouseScroll);
        (var _, var delta, var _) = InputReader.UnpackMouseB(_ops[0].ReservedB);
        delta.ShouldBe(-1);
    }

    async Task ProcessInput(string sgrSequence)
    {
        _ops.Clear();
        _terminal!.QueueInput(Encoding.ASCII.GetBytes(sgrSequence));

        using var cts = new CancellationTokenSource(100);
        var readerTask = Task.Run(() =>
        {
            try
            {
                _reader!.RunReader(cts.Token);
            }
            catch
            {
            }
        });

        await Task.Delay(50);
        await cts.CancelAsync();
        await readerTask.WaitAsync(TimeSpan.FromMilliseconds(200)).ContinueWith(_ => { });

        using var parseCts = new CancellationTokenSource(100);
        try
        {
            _reader!.RunParser(parseCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    static string scroll(int b) => $"\x1b[<{b};10;20M";
}
