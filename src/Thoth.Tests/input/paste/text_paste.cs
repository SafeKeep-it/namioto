using Shouldly;
using Thoth.Terminal.Raw.Ingress;

namespace Comptatata.Tests.App.Cli.input_reader.processing_paste;

public class text_paste : IAsyncLifetime
{
    readonly List<ScreenOp> _ops = new();
    InputReader? _reader;
    MockTerminal? _terminal;

    public async Task InitializeAsync()
    {
        _terminal = new();
        _reader = new(_terminal, op => _ops.Add(op));

        var pasteContent = "hello world"u8.ToArray();
        var start = new byte[] { 0x1B, (byte)'[', (byte)'2', (byte)'0', (byte)'0', (byte)'~' };
        var end = new byte[] { 0x1B, (byte)'[', (byte)'2', (byte)'0', (byte)'1', (byte)'~' };
        var full = start.Concat(pasteContent).Concat(end).ToArray();

        _terminal.QueueInput(full);

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
    public void parses_paste_with_correct_text()
    {
        _ops.ShouldHaveSingleItem();
        _ops[0].Kind.ShouldBe(ScreenOpKind.Paste);
        _ops[0].Text.ShouldBe("hello world");
    }
}