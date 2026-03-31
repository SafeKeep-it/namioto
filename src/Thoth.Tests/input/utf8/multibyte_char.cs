using Shouldly;
using Thoth.Terminal.Raw.Ingress;

namespace Comptatata.Tests.App.Cli.input_reader.processing_utf8_char;

public class multibyte_char : IAsyncLifetime
{
    readonly List<ScreenOp> _ops = new();
    InputReader? _reader;
    MockTerminal? _terminal;

    public async Task InitializeAsync()
    {
        _terminal = new();
        _reader = new(_terminal, op => _ops.Add(op));

        _terminal.QueueInput([0xC3, 0xA9]); // é

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
    public void parses_utf8_char()
    {
        _ops.ShouldHaveSingleItem();
        _ops[0].Kind.ShouldBe(ScreenOpKind.Key);
        _ops[0].ReservedA.ShouldBe('é');
    }
}