using System.Text;
using Shouldly;
using Thoth.Terminal.Raw.Ingress;

namespace Comptatata.Tests.App.Cli.input.regular_key;

public class character_input : IAsyncLifetime
{
    readonly List<ScreenOp> _ops = new();
    InputReader? _reader;
    MockTerminal? _terminal;

    public Task InitializeAsync()
    {
        _terminal = new();
        _reader = new(_terminal, op => _ops.Add(op));
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task ProcessInput(byte[] data)
    {
        _ops.Clear();
        _terminal!.QueueInput(data);

        using var cts = new CancellationTokenSource(100);
        var readerTask = Task.Run(() =>
        {
            try
            {
                _reader!.RunReader(cts.Token);
            }
            catch { }
        });
        await Task.Delay(50);
        await cts.CancelAsync();
        await readerTask.WaitAsync(TimeSpan.FromMilliseconds(200)).ContinueWith(_ => { });

        using var parseCts = new CancellationTokenSource(100);
        try
        {
            _reader!.RunParser(parseCts.Token);
        }
        catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task parses_regular_key_a()
    {
        await ProcessInput([(byte)'a']);
        _ops.ShouldHaveSingleItem();
        _ops[0].Kind.ShouldBe(ScreenOpKind.Key);
        _ops[0].ReservedA.ShouldBe('a');
        ((ConsoleKey)(_ops[0].ReservedB & 0xFF)).ShouldBe(ConsoleKey.A);
    }

    [Fact]
    public async Task parses_csi_u_shift_enter()
    {
        await ProcessInput(Encoding.ASCII.GetBytes("\x1b[13;2u"));

        _ops.ShouldHaveSingleItem();
        _ops[0].Kind.ShouldBe(ScreenOpKind.Key);
        _ops[0].ReservedA.ShouldBe('\r');
        var key = (ConsoleKey)(_ops[0].ReservedB & 0xFF);
        var mods = (ConsoleModifiers)((_ops[0].ReservedB >> 8) & 0xFF);

        key.ShouldBe(ConsoleKey.Enter);
        mods.HasFlag(ConsoleModifiers.Shift).ShouldBeTrue();
    }

    [Fact]
    public async Task parses_newline_as_standard_enter()
    {
        await ProcessInput([(byte)'\n']);

        _ops.ShouldHaveSingleItem();
        _ops[0].Kind.ShouldBe(ScreenOpKind.Key);
        _ops[0].ReservedA.ShouldBe('\r');
        var key = (ConsoleKey)(_ops[0].ReservedB & 0xFF);
        var mods = (ConsoleModifiers)((_ops[0].ReservedB >> 8) & 0xFF);

        key.ShouldBe(ConsoleKey.Enter);
        ((int)mods).ShouldBe(0);
    }
}