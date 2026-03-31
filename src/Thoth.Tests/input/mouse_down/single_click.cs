using Shouldly;
using Thoth.Terminal.Raw.Ingress;

namespace Comptatata.Tests.App.Cli.input.mouse_down;

public class single_click : IAsyncLifetime
{
    readonly List<ScreenOp> _ops = new();
    InputReader? _reader;
    MockTerminal? _terminal;

    public async Task InitializeAsync()
    {
        _terminal = new();
        _reader = new(_terminal, op => _ops.Add(op));

        // CSI < 0 ; 10 ; 20 M
        _terminal.QueueInput([
            0x1B,
            (byte)'[',
            (byte)'<',
            (byte)'0',
            (byte)';',
            (byte)'1',
            (byte)'0',
            (byte)';',
            (byte)'2',
            (byte)'0',
            (byte)'M'
        ]);

        using var cts = new CancellationTokenSource(100);
        var readerTask = Task.Run(() =>
        {
            try
            {
                _reader.RunReader(cts.Token);
            }
            catch { }
        });
        await Task.Delay(50);
        await cts.CancelAsync();
        await readerTask.WaitAsync(TimeSpan.FromMilliseconds(200)).ContinueWith(_ => { });

        using var parseCts = new CancellationTokenSource(100);
        try
        {
            _reader.RunParser(parseCts.Token);
        }
        catch (OperationCanceledException) { }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void parses_as_single_mouse_down_event()
    {
        _ops.ShouldHaveSingleItem();
        _ops[0].Kind.ShouldBe(ScreenOpKind.MouseDown);
    }

    [Fact]
    public void extracts_correct_coordinates()
    {
        _ops[0].ReservedA.ShouldBe(10);
        (var y, var btn, var _) = InputReader.UnpackMouseB(_ops[0].ReservedB);
        y.ShouldBe(20);
        btn.ShouldBe(0);
    }
}